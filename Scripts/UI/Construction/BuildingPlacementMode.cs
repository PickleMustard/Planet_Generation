using System.Collections.Generic;
using System.Linq;
using Constructables.Buildings;
using Constructables.Power;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace UI.Construction;

public partial class BuildingPlacementMode : Node
{
    [Signal]
    public delegate void PlacementConfirmedEventHandler(
        VoronoiCell primaryCell,
        Node3D body,
        Godot.Collections.Array<VoronoiCell> additionalCells
    );

    [Signal]
    public delegate void PlacementCancelledEventHandler();

    /// <summary>
    /// When true, the placement reticle follows the mouse cursor instead of the
    /// screen center. Used when placing over the Orbital Body Window (cursor
    /// visible, body off-center). Defaults false → classic captured-mouse,
    /// center-reticle behavior.
    /// </summary>
    public bool UseMousePosition { get; set; }

    /// <summary>
    /// Explicit camera to raycast through, injected by the placement overlay. Set
    /// when launching over the Orbital Body Window, whose orbit camera repositions
    /// the player camera without marking it <c>Current</c> — so the one-shot
    /// <see cref="_Ready"/> <c>GetCamera3D()</c> would otherwise return null.
    /// </summary>
    public Camera3D? OverrideCamera { get; set; }

    private BuildingDefinition _definition = null!;
    private Camera3D? _camera;
    private Node3D? _targetBodyNode;
    private Node3D? _ghostContainer;
    private Node3D? _ghostNode;
    private VoronoiCell? _hoveredCell;
    private ISelectableBody? _hoveredBody;
    private Node3D? _hoveredBodyNode;
    private List<VoronoiCell> _selectedCells = new();
    private List<bool> _cellValidity = new();
    private bool _allCellsValid;
    private bool _globalLimitReached;
    private bool _isActive;
    private Transform3D _lastCameraTransform;
    private int _rotationOffset;
    private List<VoronoiCell> _sortedNeighbors = new();
    private HashSet<VoronoiCell>? _previewCoverage;
    private PowerGrid? _previewDominant;
    private IReadOnlyList<PowerGrid> _previewAbsorbed = System.Array.Empty<PowerGrid>();
    private IReadOnlyList<PowerGrid> _allGrids = System.Array.Empty<PowerGrid>();
    private readonly HashSet<PowerGrid> _touchedGrids = new();
    private bool _previewWanted;
    private bool _previewResolved;
    private bool _previewWarningEmitted;
    private bool _isPowerConsumer;
    private int _gridRadius;
    private PlacementCursorLabel? _cursorLabel;

    /// <summary>
    /// <paramref name="targetBody"/> locks placement to one body (the inspected
    /// body when launched from the Orbital Body Window, or the aimed body from
    /// the HUD). When set, raycast hits on any other body are ignored so the
    /// reticle can't wander onto a background planet. Mirrors
    /// <see cref="StationOrbitPlacementMode"/>'s body lock.
    /// </summary>
    public void Initialize(BuildingDefinition definition, IOrbitalBody? targetBody = null)
    {
        _definition = definition;
        _targetBodyNode = targetBody as Node3D;
        _isActive = true;
        _lastCameraTransform = default;

        // Scan behavior entries for power contributor info
        var powerEntry = _definition.BehaviorEntries
            .FirstOrDefault(e => e.BehaviorId is "PowerProducerBehavior"
                or "BatteryBehavior");
        _gridRadius = powerEntry != null
            ? BehaviorConfigHelper.ReadInt(powerEntry.Config, "grid_radius", -1)
            : -1;
        // Consumers have no coverage radius but still connect to a grid — show the
        // existing-grid overlay for them too so the player sees what they'll join.
        _isPowerConsumer = _definition.BehaviorEntries
            .Any(e => e.BehaviorId == "PowerConsumerBehavior");
        _previewWanted = _gridRadius >= 0 || _isPowerConsumer;
        _previewWarningEmitted = false;
        // Ghost model will be created on first valid hover to get proper body scaling
    }

    public override void _Ready()
    {
        _camera = OverrideCamera ?? GetViewport().GetCamera3D();

        // Screen-space cursor label, on its own CanvasLayer so it draws above the HUD.
        var layer = new CanvasLayer { Layer = 50 };
        AddChild(layer);
        _cursorLabel = new PlacementCursorLabel();
        layer.AddChild(_cursorLabel);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Re-acquire lazily: the camera may have been unavailable on the launch
        // frame (orbit camera not marked Current). This self-heals once a usable
        // camera exists.
        _camera ??= OverrideCamera ?? GetViewport().GetCamera3D();
        if (!_isActive || _camera == null)
            return;

        CastRayFromScreenCenter();

        // Keep the cursor label beside the aim point (mouse, or screen center in HUD mode).
        if (_cursorLabel != null)
        {
            var anchor = UseMousePosition
                ? GetViewport().GetMousePosition()
                : GetViewport().GetVisibleRect().Size * 0.5f;
            _cursorLabel.UpdateAnchor(anchor);
        }

        // Recover from the first-frame race where PowerGridMgr was null (deferred-add on
        // CelestialBody._Ready) when the user first hovered a cell. Without this retry the
        // preview would stay blank until the user moved to a different cell.
        if (_previewWanted
            && !_previewResolved
            && _hoveredBody != null
            && _selectedCells.Count > 0)
        {
            UpdateGridPreview(_hoveredBody);
            if (_previewResolved)
                UpdateHighlight(_hoveredBody);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isActive)
            return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                OnPlacementClick();
                // Consume the click so neither HudState (HUD launch) nor the
                // Orbital Body Window (in-window launch) also reacts to it.
                GetViewport().SetInputAsHandled();
            }
        }

        if (
            @event is InputEventKey keyEvent
            && keyEvent.Pressed
            && keyEvent.Keycode == Key.R
        )
        {
            if (
                _hoveredCell != null
                && _hoveredBody != null
                && _definition.Placement.CellCount > 1
                && _sortedNeighbors.Count > 0
            )
            {
                _rotationOffset = (_rotationOffset + 1) % _sortedNeighbors.Count;
                SelectAndValidateCells();
                UpdateGridPreview(_hoveredBody);
                UpdateHighlight(_hoveredBody);
                if (_hoveredBodyNode != null)
                    UpdateGhostPosition(_selectedCells, _hoveredBodyNode);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void CastRayFromScreenCenter()
    {
        var viewport = GetViewport();
        var aimPoint = UseMousePosition
            ? viewport.GetMousePosition()
            : viewport.GetVisibleRect().Size / 2.0f;

        Vector3 origin = _camera!.ProjectRayOrigin(aimPoint);
        Vector3 direction = origin + _camera.ProjectRayNormal(aimPoint) * 1000f;

        var query = PhysicsRayQueryParameters3D.Create(origin, direction);
        query.CollideWithAreas = true;
        var spaceState = _camera!.GetWorld3D().DirectSpaceState;
        var result = spaceState.IntersectRay(query);

        if (result == null || result.Count == 0)
        {
            ClearHover();
            return;
        }

        var collider = (Node3D)result["collider"];
        var position = (Vector3)result["position"];

        var selectableBody = FindSelectableBody(collider);

        if (selectableBody == null)
        {
            ClearHover();
            return;
        }

        // Lock to the chosen body: ignore hits on anything else so placement
        // stays on the body the player selected/inspected.
        if (_targetBodyNode != null && (Node3D)selectableBody != _targetBodyNode)
        {
            ClearHover();
            return;
        }

        // Try to use face_index first (faster, more accurate)
        CellSelectionResult? selectionResult = null;
        if (result.ContainsKey("face_index"))
        {
            var faceIndex = (int)result["face_index"];
            selectionResult = selectableBody.GetFaceFromIndex(faceIndex);
        }

        if (selectionResult?.Cell == null)
        {
            ClearHover();
            return;
        }

        var cell = selectionResult.Cell;
        var bodyNode = (Node3D)selectableBody;

        // Only recalculate if cell changed
        if (_hoveredCell != cell || _hoveredBody != selectableBody)
        {
            _hoveredCell = cell;
            _hoveredBody = selectableBody;
            _hoveredBodyNode = bodyNode;
            _rotationOffset = 0;
            UpdatePlacementValidation(cell, selectableBody, position);

            // Create ghost model on first valid hover to ensure proper body scaling
            if (_ghostContainer == null)
            {
                CreateGhostModel();
            }

            // Refresh the cursor label content for the newly hovered cell.
            _cursorLabel?.SetRows(PlacementLabelResolver.Resolve(_definition, cell));
        }

        UpdateGhostPosition(_selectedCells, bodyNode);
    }

    private void UpdatePlacementValidation(
        VoronoiCell primaryCell,
        ISelectableBody body,
        Vector3 hitPosition
    )
    {
        // Rebuild angularly sorted neighbor cache for multi-cell buildings
        int cellCount = _definition.Placement.CellCount;
        if (cellCount > 1)
        {
            var neighbors = body.GetRuntimeCellNeighbors(primaryCell);
            _sortedNeighbors = SortNeighborsByAngle(primaryCell, neighbors);

            // Set initial rotation to the neighbor closest to the hit point
            if (_sortedNeighbors.Count > 0)
            {
                var bodyNode = (Node3D)body;
                var localHit = hitPosition - bodyNode.GlobalPosition;
                float bestDist = float.MaxValue;
                int bestIndex = 0;
                for (int i = 0; i < _sortedNeighbors.Count; i++)
                {
                    float dist = _sortedNeighbors[i].Center.DistanceSquaredTo(localHit);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIndex = i;
                    }
                }
                _rotationOffset = bestIndex;
            }
        }
        else
        {
            _sortedNeighbors.Clear();
        }

        SelectAndValidateCells();
        UpdateGridPreview(body);
        UpdateHighlight(body);
    }

    private void SelectAndValidateCells()
    {
        _selectedCells.Clear();
        _cellValidity.Clear();

        if (_hoveredCell == null)
            return;

        _globalLimitReached = !BuildingDatabase.Instance.ValidateGlobalPlacement(_definition.IdName!);

        _selectedCells.Add(_hoveredCell);
        bool primaryValid = BuildingDatabase.Instance.ValidatePlacement(
            _definition.IdName!,
            _hoveredCell,
            _hoveredBody as IOrbitalBody
        );
        _cellValidity.Add(primaryValid);

        int cellCount = _definition.Placement.CellCount;
        if (cellCount > 1 && _sortedNeighbors.Count > 0)
        {
            int additionalNeeded = cellCount - 1;
            int neighborCount = _sortedNeighbors.Count;
            for (int i = 0; i < Mathf.Min(additionalNeeded, neighborCount); i++)
            {
                int index = (_rotationOffset + i) % neighborCount;
                var neighborCell = _sortedNeighbors[index];
                _selectedCells.Add(neighborCell);
                bool valid = BuildingDatabase.Instance.ValidatePlacement(
                    _definition.IdName!,
                    neighborCell,
                    _hoveredBody as IOrbitalBody
                );
                _cellValidity.Add(valid);
            }

            // If we couldn't find enough neighbor cells, mark as invalid
            if (_selectedCells.Count < cellCount)
            {
                for (int i = _cellValidity.Count - 1; i >= 0; i--)
                    _cellValidity[i] = false;
            }
        }

        // Adjacency: every non-primary cell must share an edge with at least one
        // other selected cell. The angular-sorted neighbor ring usually satisfies
        // this; the check rejects degenerate selections (e.g. coastline gaps).
        if (_definition.Placement.RequiresAdjacent && _selectedCells.Count > 1 && _hoveredBody != null)
        {
            for (int i = 1; i < _selectedCells.Count; i++)
            {
                var nbrs = _hoveredBody.GetRuntimeCellNeighbors(_selectedCells[i]);
                bool hasAdjacentInSelection = false;
                for (int j = 0; j < _selectedCells.Count && !hasAdjacentInSelection; j++)
                {
                    if (j == i) continue;
                    int otherIndex = _selectedCells[j].Index;
                    for (int k = 0; k < nbrs.Length; k++)
                    {
                        if (nbrs[k].Index == otherIndex) { hasAdjacentInSelection = true; break; }
                    }
                }
                if (!hasAdjacentInSelection)
                {
                    for (int k = 0; k < _cellValidity.Count; k++)
                        _cellValidity[k] = false;
                    break;
                }
            }
        }

        // Hitting the global build cap forces every cell to invalid so the highlight
        // matches the same "cannot construct" red the player sees for unmet placement
        // requirements. Single source of visual truth = single source of confusion.
        if (_globalLimitReached)
        {
            for (int i = 0; i < _cellValidity.Count; i++)
                _cellValidity[i] = false;
        }

        _allCellsValid = !_cellValidity.Contains(false);
    }

    private List<VoronoiCell> SortNeighborsByAngle(
        VoronoiCell primaryCell,
        VoronoiCell[] rawNeighbors
    )
    {
        var normal = primaryCell.Center.Normalized();

        // Build tangent-plane basis
        var right = Vector3.Right.Cross(normal).Normalized();
        if (right.LengthSquared() < 0.001f)
            right = Vector3.Forward.Cross(normal).Normalized();
        var forward = normal.Cross(right).Normalized();

        var result = new List<(VoronoiCell cell, float angle)>();
        foreach (var neighbor in rawNeighbors)
        {
            if (neighbor.Index == primaryCell.Index)
                continue;

            var offset = neighbor.Center - primaryCell.Center;
            // Project onto tangent plane
            offset -= normal * offset.Dot(normal);
            float angle = Mathf.Atan2(offset.Dot(forward), offset.Dot(right));
            result.Add((neighbor, angle));
        }

        result.Sort((a, b) => a.angle.CompareTo(b.angle));
        return result.ConvertAll(item => item.cell);
    }

    private void UpdateGridPreview(ISelectableBody body)
    {
        _previewCoverage = null;
        _previewDominant = null;
        _previewAbsorbed = System.Array.Empty<PowerGrid>();
        _allGrids = System.Array.Empty<PowerGrid>();
        _touchedGrids.Clear();
        _previewResolved = false;

        if (!_previewWanted)
            return;
        if (body is not CelestialBody cb || cb.PowerGridMgr == null)
        {
            if (!_previewWarningEmitted)
            {
                _previewWarningEmitted = true;
                GameLogger.Warning(
                    $"BuildingPlacementMode: grid preview suppressed for '{_definition.IdName}' " +
                    $"— hovered body is not a CelestialBody with a PowerGridMgr (body={body?.GetType().Name})"
                );
            }
            return;
        }
        if (_selectedCells.Count == 0)
            return;

        var mgr = cb.PowerGridMgr;
        _previewResolved = true;
        // Snapshot every grid on the body so all of them render (each its own shade),
        // not just the ones this placement connects to.
        _allGrids = mgr.Grids;

        if (_gridRadius >= 0)
        {
            // Producer/battery: real coverage range, may extend or merge grids.
            var preview = mgr.PreviewPlacement(_selectedCells, _gridRadius);
            _previewCoverage = preview.Coverage as HashSet<VoronoiCell>
                ?? new HashSet<VoronoiCell>(preview.Coverage);
            _previewDominant = preview.Dominant;
            _previewAbsorbed = preview.Absorbed;
            if (_previewDominant != null)
                _touchedGrids.Add(_previewDominant);
            foreach (var g in _previewAbsorbed)
                _touchedGrids.Add(g);
        }
        else
        {
            // Consumer: no coverage range; it simply joins whatever grid(s) cover its
            // footprint cells. Consumers never merge grids.
            foreach (var c in _selectedCells)
            {
                var g = mgr.GetGridForCell(c);
                if (g != null)
                    _touchedGrids.Add(g);
            }
        }
    }

    private void UpdateHighlight(ISelectableBody body)
    {
        if (body.Mesh == null)
            return;

        byte[] buf = body.Mesh.AcquirePlacementBuffer(out int width, out int height);
        if (buf.Length == 0)
            return;

        // Pass 1: building coverage range (yellow). Lowest priority.
        if (_previewCoverage != null && _previewCoverage.Count > 0)
        {
            foreach (var cell in _previewCoverage)
            {
                int idx = cell.Index;
                if (idx >= 0 && idx < buf.Length)
                    buf[idx] = CODE_COVERAGE;
            }
        }

        // Pass 2: every existing grid (each its own orange shade). Overrides yellow
        // coverage where they overlap, so existing grids win. A touched grid also gets
        // a line pattern: single diagonal if the placement joins exactly one grid, an
        // X if it would merge two or more.
        int touchedCount = _touchedGrids.Count;
        int linePattern = touchedCount >= 2 ? PATTERN_X : PATTERN_SINGLE;
        for (int slot = 0; slot < _allGrids.Count; slot++)
        {
            var grid = _allGrids[slot];
            int pattern = _touchedGrids.Contains(grid) ? linePattern : PATTERN_NONE;
            byte code = GridCode(slot, pattern);
            foreach (var cell in grid.CoveredCells)
            {
                int idx = cell.Index;
                if (idx >= 0 && idx < buf.Length)
                    buf[idx] = code;
            }
        }

        // Pass 3: building footprint validity (green/red). Highest priority.
        for (int i = 0; i < _selectedCells.Count; i++)
        {
            int idx = _selectedCells[i].Index;
            if (idx < 0 || idx >= buf.Length)
                continue;
            buf[idx] = _cellValidity[i] ? (byte)1 : (byte)2;
        }

        body.Mesh.SetPlacementHighlightData(buf, width, height);
    }

    // Per-cell highlight codes shared with cell_selection_highlight.gdshader.
    private const byte CODE_COVERAGE = 3;
    private const int GRID_CODE_BASE = 16;
    private const int GRID_PALETTE = 16;
    private const int PATTERN_NONE = 0;
    private const int PATTERN_SINGLE = 1;
    private const int PATTERN_X = 2;

    /// <summary>
    /// Encodes an existing-grid cell as <c>GRID_CODE_BASE + (slot % GRID_PALETTE) * 3 +
    /// pattern</c>. The slot selects the orange shade; pattern (0/1/2) selects the
    /// diagonal-line overlay. Max value 16 + 15*3 + 2 = 63, well within R8.
    /// </summary>
    private static byte GridCode(int slot, int pattern) =>
        (byte)(GRID_CODE_BASE + (slot % GRID_PALETTE) * 3 + pattern);

    private void UpdateGhostPosition(List<VoronoiCell> cells, Node3D bodyNode)
    {
        if (_ghostContainer == null || cells.Count == 0)
            return;

        // Calculate centroid of all selected cells for multi-cell buildings
        Vector3 centroidLocal;
        if (cells.Count > 1)
        {
            centroidLocal = Vector3.Zero;
            foreach (var cell in cells)
                centroidLocal += cell.Center;
            centroidLocal /= cells.Count;
        }
        else
        {
            centroidLocal = cells[0].Center;
        }

        // Transform centroid to world space
        var centroidWorldPos = bodyNode.GlobalTransform * centroidLocal;

        // Calculate orientation vectors for surface alignment
        var up = (centroidWorldPos - bodyNode.GlobalPosition).Normalized();
        var forward = up.Cross(Vector3.Right).Normalized();
        if (forward.LengthSquared() < 0.001f)
            forward = up.Cross(Vector3.Forward).Normalized();
        var right = forward.Cross(up).Normalized();
        forward = up.Cross(right).Normalized();

        // Container handles only position and orientation — model child retains its own scale
        _ghostContainer.GlobalTransform = new Transform3D(
            new Basis(right, up, forward),
            centroidWorldPos
        );

        _ghostContainer.Visible = true;
    }

    private void OnPlacementClick()
    {
        if (_hoveredCell == null || _hoveredBodyNode == null)
            return;

        // Check global placement limits
        string buildingId = _definition.IdName!;
        if (!BuildingDatabase.Instance.ValidateGlobalPlacement(buildingId))
        {
            int placed = BuildingDatabase.Instance.GetGlobalPlacementCount(buildingId);
            int limit = _definition.BuildingLimit;
            ToastSystem.Instance?.Show(
                $"Building construction limit reached: {placed} / {limit}"
            );
            return;
        }

        if (_allCellsValid)
        {
            var additionalCells = new Godot.Collections.Array<VoronoiCell>();
            for (int i = 1; i < _selectedCells.Count; i++)
                additionalCells.Add(_selectedCells[i]);

            ToastSystem.Instance?.Show("Construction started");
            EmitSignal(
                SignalName.PlacementConfirmed,
                _hoveredCell,
                _hoveredBodyNode,
                additionalCells
            );
        }
        else
        {
            ToastSystem.Instance?.Show("Construction blocked: placement requirements not met");
        }
    }

    private static ISelectableBody? FindSelectableBody(Node node)
    {
        Node? current = node;
        while (current != null)
        {
            if (current is ISelectableBody body)
                return body;
            current = current.GetParentOrNull<Node>();
        }
        return null;
    }

    private void ClearHover()
    {
        if (_hoveredBody?.Mesh != null)
            _hoveredBody.Mesh.ClearPlacementHighlight();

        _hoveredCell = null;
        _hoveredBody = null;
        _hoveredBodyNode = null;
        _selectedCells.Clear();
        _cellValidity.Clear();
        _rotationOffset = 0;
        _sortedNeighbors.Clear();
        _previewCoverage = null;
        _previewDominant = null;
        _previewAbsorbed = System.Array.Empty<PowerGrid>();
        _allGrids = System.Array.Empty<PowerGrid>();
        _touchedGrids.Clear();
        _previewResolved = false;

        if (_ghostContainer != null)
            _ghostContainer.Visible = false;

        _cursorLabel?.HideLabel();
    }

    private void CreateGhostModel()
    {
        // Get body radius from hovered body for scaling (default to 1.0 if not available)
        float bodyRadius = 1.0f;
        if (_hoveredBody is IOrbitalBody orbitalBody)
        {
            bodyRadius = orbitalBody.Radius;
        }
        else if (_hoveredBody is ProceduralGeneration.PlanetGeneration.CelestialBody celestialBody)
        {
            bodyRadius = celestialBody.Radius;
        }

        // Create ghost from pre-loaded prototype with body-relative scaling
        _ghostNode = _definition.Visual?.CreateModelInstance(bodyRadius);

        // Fallback: create a simple box placeholder (scaled by body radius)
        if (_ghostNode == null)
        {
            float fallbackHeight = 0.3f * bodyRadius * 0.5f;
            float fallbackRadius = fallbackHeight * 0.15f;
            _ghostNode = new MeshInstance3D
            {
                Mesh = new CylinderMesh { Height = fallbackHeight, TopRadius = fallbackRadius, BottomRadius = fallbackRadius },
                Name = "GhostFallbackMesh",
            };
            GameLogger.Warning(
                $"BuildingPlacementMode: Using fallback ghost model for '{_definition.IdName}' with body scale"
            );
        }
        else
        {
            GameLogger.Info(
                $"BuildingPlacementMode: Created ghost model from prototype for '{_definition.IdName}' with body scale {bodyRadius}"
            );
        }

        // Apply semi-transparent material to all mesh instances
        ApplyGhostMaterial(_ghostNode);

        // Container Node3D handles position and orientation only;
        // the model child retains its own scale independently
        _ghostContainer = new Node3D { Name = "GhostContainer", Visible = false };
        _ghostContainer.AddChild(_ghostNode);
        AddChild(_ghostContainer);
    }

    private void ApplyGhostMaterial(Node node)
    {
        var ghostMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 1f, 1f, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        if (node is MeshInstance3D meshInstance)
            meshInstance.MaterialOverride = ghostMat;

        foreach (var child in node.GetChildren())
            ApplyGhostMaterial(child);
    }

    public void Cleanup()
    {
        _isActive = false;
        _lastCameraTransform = default;
        _rotationOffset = 0;
        _sortedNeighbors.Clear();
        ClearHover();

        if (_ghostContainer != null)
        {
            _ghostContainer.QueueFree();
            _ghostContainer = null;
            _ghostNode = null;
        }
    }
}
