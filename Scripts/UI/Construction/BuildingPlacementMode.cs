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

    private BuildingDefinition _definition = null!;
    private Camera3D? _camera;
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
    private bool _previewWanted;
    private bool _previewWarningEmitted;
    private int _gridRadius;

    public void Initialize(BuildingDefinition definition)
    {
        _definition = definition;
        _isActive = true;
        _lastCameraTransform = default;

        // Scan behavior entries for power contributor info
        var powerEntry = _definition.BehaviorEntries
            .FirstOrDefault(e => e.BehaviorId is "PowerProducerBehavior"
                or "BatteryBehavior");
        _gridRadius = powerEntry != null
            ? BehaviorConfigHelper.ReadInt(powerEntry.Config, "grid_radius", -1)
            : -1;
        _previewWanted = _gridRadius >= 0;
        _previewWarningEmitted = false;
        // Ghost model will be created on first valid hover to get proper body scaling
    }

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera3D();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isActive || _camera == null)
            return;

        CastRayFromScreenCenter();

        // Recover from the first-frame race where PowerGridMgr was null (deferred-add on
        // CelestialBody._Ready) when the user first hovered a cell. Without this retry the
        // preview would stay blank until the user moved to a different cell.
        if (_previewWanted
            && _previewCoverage == null
            && _hoveredBody != null
            && _selectedCells.Count > 0)
        {
            UpdateGridPreview(_hoveredBody);
            if (_previewCoverage != null)
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
                //GetViewport().SetInputAsHandled();
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
        var screenCenter = viewport.GetVisibleRect().Size / 2.0f;

        Vector3 origin = _camera!.ProjectRayOrigin(screenCenter);
        Vector3 direction = origin + _camera.ProjectRayNormal(screenCenter) * 1000f;

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

        if (_gridRadius < 0)
            return;
        if (body is not CelestialBody cb || cb.PowerGridMgr == null)
        {
            if (_previewWanted && !_previewWarningEmitted)
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

        var preview = cb.PowerGridMgr.PreviewPlacement(_selectedCells, _gridRadius);
        _previewCoverage = preview.Coverage as HashSet<VoronoiCell> ?? new HashSet<VoronoiCell>(preview.Coverage);
        _previewDominant = preview.Dominant;
        _previewAbsorbed = preview.Absorbed;
    }

    private void UpdateHighlight(ISelectableBody body)
    {
        if (body.Mesh == null)
            return;

        byte[] buf = body.Mesh.AcquirePlacementBuffer(out int width, out int height);
        if (buf.Length == 0)
            return;

        if (_previewCoverage != null && _previewCoverage.Count > 0)
        {
            foreach (var cell in _previewCoverage)
            {
                int idx = cell.Index;
                if (idx < 0 || idx >= buf.Length)
                    continue;
                bool inDominant = _previewDominant != null && _previewDominant.CoveredCells.Contains(cell);
                bool inAbsorbed = false;
                if (!inDominant)
                {
                    foreach (var g in _previewAbsorbed)
                    {
                        if (g.CoveredCells.Contains(cell))
                        {
                            inAbsorbed = true;
                            break;
                        }
                    }
                }
                buf[idx] = inDominant ? (byte)4 : inAbsorbed ? (byte)5 : (byte)3;
            }
        }

        for (int i = 0; i < _selectedCells.Count; i++)
        {
            int idx = _selectedCells[i].Index;
            if (idx < 0 || idx >= buf.Length)
                continue;
            buf[idx] = _cellValidity[i] ? (byte)1 : (byte)2;
        }

        body.Mesh.SetPlacementHighlightData(buf, width, height);
    }

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
        GD.Print("OnPlacementClick");
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

            GD.Print("Construction started");
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
            GD.Print("Construction blocked: placement requirements not met");
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

        if (_ghostContainer != null)
            _ghostContainer.Visible = false;
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
