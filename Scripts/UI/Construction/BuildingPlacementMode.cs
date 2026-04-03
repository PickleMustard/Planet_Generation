using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Resources;
using UI;

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
    private Node3D? _ghostNode;
    private VoronoiCell? _hoveredCell;
    private ISelectableBody? _hoveredBody;
    private Node3D? _hoveredBodyNode;
    private List<VoronoiCell> _selectedCells = new();
    private List<bool> _cellValidity = new();
    private bool _allCellsValid;
    private bool _isActive;
    private Transform3D _lastCameraTransform;

    public void Initialize(BuildingDefinition definition)
    {
        _definition = definition;
        _isActive = true;
        _lastCameraTransform = default;
        CreateGhostModel();
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

        // Find the parent selectable body (same pattern as PlayerController)
        string parentName = ((string)collider.GetName()).Split("_")[0];
        parentName = Regex.Replace(parentName, "[0-9]", "");
        ISelectableBody? selectableBody = collider.FindParent(parentName) as CelestialBody;
        selectableBody ??= collider.FindParent(parentName) as SatelliteBody;

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
            UpdatePlacementValidation(cell, selectableBody, position);
        }

        UpdateGhostPosition(cell, bodyNode);
    }

    private void UpdatePlacementValidation(
        VoronoiCell primaryCell,
        ISelectableBody body,
        Vector3 hitPosition
    )
    {
        _selectedCells.Clear();
        _cellValidity.Clear();
        _selectedCells.Add(primaryCell);

        bool primaryValid = BuildingDatabase.Instance.ValidatePlacement(
            _definition.IdName!,
            primaryCell
        );
        _cellValidity.Add(primaryValid);

        // Handle multi-cell buildings
        int cellCount = _definition.Placement.CellCount;
        if (cellCount > 1)
        {
            var neighbors = body.GetRuntimeCellNeighbors(primaryCell);

            // Sort neighbors by distance to hit point
            var bodyNode = (Node3D)body;
            var localHit = hitPosition - bodyNode.GlobalPosition;
            var sortedNeighbors = new List<VoronoiCell>(neighbors);
            sortedNeighbors.Remove(primaryCell);
            sortedNeighbors.Sort(
                (a, b) =>
                    a
                        .Center.DistanceSquaredTo(localHit)
                        .CompareTo(b.Center.DistanceSquaredTo(localHit))
            );

            int additionalNeeded = cellCount - 1;
            for (int i = 0; i < Mathf.Min(additionalNeeded, sortedNeighbors.Count); i++)
            {
                var neighborCell = sortedNeighbors[i];
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
                // Not enough cells available
                for (int i = _cellValidity.Count - 1; i >= 0; i--)
                    _cellValidity[i] = false;
            }
        }

        _allCellsValid = !_cellValidity.Contains(false);

        // Update shader highlight
        UpdateHighlight(body);
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

    private void UpdateGhostPosition(VoronoiCell cell, Node3D bodyNode)
    {
        if (_ghostNode == null)
            return;

        // Position ghost at cell center in world space
        var cellWorldPos = bodyNode.GlobalTransform * cell.Center;
        _ghostNode.GlobalPosition = cellWorldPos;

        // Orient along surface normal
        var up = (cellWorldPos - bodyNode.GlobalPosition).Normalized();
        var forward = up.Cross(Vector3.Right).Normalized();
        if (forward.LengthSquared() < 0.001f)
            forward = up.Cross(Vector3.Forward).Normalized();
        var right = forward.Cross(up).Normalized();
        forward = up.Cross(right).Normalized();

        _ghostNode.GlobalTransform = new Transform3D(new Basis(right, up, forward), cellWorldPos);

        _ghostNode.Visible = true;
    }

    private void OnPlacementClick()
    {
        GD.Print("OnPlacementClick");
        if (_hoveredCell == null || _hoveredBodyNode == null)
            return;

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

    private void ClearHover()
    {
        if (_hoveredBody?.Mesh != null)
            _hoveredBody.Mesh.ClearPlacementHighlight();

        _hoveredCell = null;
        _hoveredBody = null;
        _hoveredBodyNode = null;
        _selectedCells.Clear();
        _cellValidity.Clear();

        if (_ghostNode != null)
            _ghostNode.Visible = false;
    }

    private void CreateGhostModel()
    {
        if (!string.IsNullOrEmpty(_definition.Visual?.ModelPath))
        {
            var scene = GD.Load<PackedScene>(_definition.Visual.ModelPath);
            if (scene != null)
            {
                _ghostNode = scene.Instantiate<Node3D>();
                _ghostNode.Scale = Vector3.One * _definition.Visual.Scale;
                _ghostNode.RotationDegrees = _definition.Visual.RotationOffset;
            }
        }

        // Fallback: create a simple box placeholder
        if (_ghostNode == null)
        {
            var meshInstance = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.3f, 0.3f) },
            };
            _ghostNode = meshInstance;
        }

        // Apply semi-transparent material to all mesh instances
        ApplyGhostMaterial(_ghostNode);

        _ghostNode.Visible = false;
        AddChild(_ghostNode);
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
        ClearHover();

        if (_ghostNode != null)
        {
            _ghostNode.QueueFree();
            _ghostNode = null;
        }
    }
}
