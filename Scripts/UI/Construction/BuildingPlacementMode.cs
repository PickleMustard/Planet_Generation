using System.Collections.Generic;
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
    private bool _isActive;
    private Transform3D _lastCameraTransform;
    private int _rotationOffset;
    private List<VoronoiCell> _sortedNeighbors = new();

    public void Initialize(BuildingDefinition definition)
    {
        _definition = definition;
        _isActive = true;
        _lastCameraTransform = default;
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
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isActive)
            return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            GD.Print("Placment Click");
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
        UpdateHighlight(body);
    }

    private void SelectAndValidateCells()
    {
        _selectedCells.Clear();
        _cellValidity.Clear();

        if (_hoveredCell == null)
            return;

        _selectedCells.Add(_hoveredCell);
        bool primaryValid = BuildingDatabase.Instance.ValidatePlacement(
            _definition.IdName!,
            _hoveredCell
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
                    neighborCell
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

    private void UpdateHighlight(ISelectableBody body)
    {
        if (body.Mesh == null)
            return;

        int[] cellIds = new int[_selectedCells.Count];
        bool[] valid = new bool[_selectedCells.Count];
        for (int i = 0; i < _selectedCells.Count; i++)
        {
            cellIds[i] = _selectedCells[i].Index;
            valid[i] = _cellValidity[i];
        }

        body.Mesh.SetPlacementHighlight(cellIds, valid);
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
            ToastSystem.Instance?.Show($"{_definition.DisplayName} has already been built");
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
            float fallbackSize = 0.3f * bodyRadius * 0.5f;
            _ghostNode = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(fallbackSize, fallbackSize, fallbackSize) },
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
