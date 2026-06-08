using System.Collections.Generic;
using Constructables;
using Godot;
using PlayerInteraction.Camera;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using UI.OrbitalScheduling;
using UtilityLibrary;

namespace UI.Construction;

/// <summary>
/// Top-down board placement engine for stations. Swoops the player camera to an
/// overhead view of the target body, draws its orbit bands as 3D rings, and lets
/// the player place a station by pointing: the ghost snaps to the nearest band
/// (band bodies) or sits at the pointed radius (continuous bodies), with a ghost
/// ring showing the resulting orbit. Left-click confirms. Mirrors
/// <see cref="BuildingPlacementMode"/>'s lifecycle so
/// <see cref="PlacementOverlayController"/> wiring is unchanged:
/// Initialize → PlacementConfirmed/PlacementCancelled → Cleanup. The camera
/// swoop-back is driven by the overlay on teardown.
/// </summary>
public partial class StationOrbitPlacementMode : Node
{
    private const float MIN_RADIUS_MULTIPLIER = 1.2f;

    [Signal]
    public delegate void PlacementConfirmedEventHandler();

    [Signal]
    public delegate void PlacementCancelledEventHandler();

    private Camera3D? _camera;
    private PlayerCameraController? _controller;
    private IOrbitalBody? _targetBody;
    private Node3D? _targetBodyNode;
    private StationDefinition? _stationDef;

    private Node3D? _ghostContainer;
    private PlacementOrbitRings? _rings;

    private bool _usesBands;
    private int _selectedBandIndex;
    private float _selectedRadius;
    private bool _isActive;
    private bool _panning;
    private readonly List<float> _bandRadii = new();

    public void Initialize(StationDefinition definition, IOrbitalBody targetBody)
    {
        _stationDef = definition;
        _targetBody = targetBody;
        _targetBodyNode = targetBody as Node3D;
        _camera = GetViewport().GetCamera3D();
        _controller = _camera as PlayerCameraController;

        _usesBands = targetBody.UsesBandPlacement;
        _selectedBandIndex = 0;
        _bandRadii.Clear();
        if (_usesBands)
        {
            int count = targetBody.GetBandCount();
            for (int i = 0; i < count; i++)
                _bandRadii.Add(targetBody.GetOrbitBandRadius(i));
        }
        _selectedRadius = _usesBands
            ? (_bandRadii.Count > 0 ? _bandRadii[0] : targetBody.Radius * 2f)
            : targetBody.Radius * 2f;

        // Run after the camera controller's _Process so the ghost reads the
        // freshest camera pose each render frame (avoids cursor/ghost lag).
        ProcessPriority = 1000;

        (_ghostContainer, _) = StationGhostFactory.Create(_stationDef);
        AddChild(_ghostContainer);

        if (_targetBodyNode != null)
        {
            _rings = new PlacementOrbitRings { Name = "PlacementOrbitRings" };
            _targetBodyNode.AddChild(_rings);
            _rings.Initialize(targetBody);
        }

        // Swoop the camera overhead.
        if (_controller != null && _targetBodyNode != null)
        {
            Node3D anchor = (targetBody as ISelectableBody)?.GetOrCreateCameraAnchor() ?? _targetBodyNode;
            var strategy = new TopDownPlacementFramingStrategy(targetBody);
            _controller.EnterTopDownPlacement(anchor, _targetBodyNode, strategy, ComputeSystemPanExtent());
        }
        else
        {
            GameLogger.Warning("StationOrbitPlacementMode: no PlayerCameraController; placement runs without swoop");
        }

        _isActive = true;
        GameLogger.Debug(
            $"StationOrbitPlacementMode: Placing '{_stationDef.Name}' around '{_targetBody.BodyName}'");
    }

    public override void _Process(double delta)
    {
        if (!_isActive || _targetBody == null
            || _targetBodyNode == null || _ghostContainer == null)
            return;

        // Re-acquire the active camera each frame so we always project from the
        // camera that is actually rendering the view.
        _camera = GetViewport().GetCamera3D() ?? _camera;
        if (_camera == null)
            return;

        Vector2 m = _camera.GetViewport().GetMousePosition();
        Vector3 rayOrigin = _camera.ProjectRayOrigin(m);
        Vector3 rayDir = _camera.ProjectRayNormal(m);
        Vector3 center = _targetBodyNode.GlobalPosition;

        if (!PlacementMath.ProjectToPlaneY(rayOrigin, rayDir, center.Y, out Vector3 hit))
            return;

        Vector3 offset = hit - center;
        offset.Y = 0f;
        float pointedRadius = offset.Length();
        Vector3 dir = pointedRadius > 1e-3f ? offset / pointedRadius : Vector3.Right;

        bool valid = true;
        if (_usesBands && _bandRadii.Count > 0)
        {
            _selectedBandIndex = PlacementMath.NearestBand(pointedRadius, _bandRadii, out _selectedRadius);
            valid = _targetBody.CanAddToBand(_selectedBandIndex);
        }
        else if (!_usesBands)
        {
            _selectedRadius = Mathf.Max(_targetBody.Radius * MIN_RADIUS_MULTIPLIER, pointedRadius);
        }

        _ghostContainer.GlobalPosition = center + dir * _selectedRadius;
        _ghostContainer.Visible = true;
        _rings?.UpdateGhostRing(_selectedRadius, valid);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isActive)
            return;

        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                ConfirmPlacement();
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
                _panning = mb.Pressed;
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion mm && _panning)
        {
            _controller?.HandlePlacementPan(mm.Relative);
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey key && key.Pressed && _usesBands && _bandRadii.Count > 0)
        {
            // [ ] still cycle bands as a keyboard fallback to pointing.
            if (key.Keycode == Key.Bracketright)
                CycleBand(1);
            else if (key.Keycode == Key.Bracketleft)
                CycleBand(-1);
        }
    }

    /// <summary>
    /// Largest horizontal distance from the target body to any other body in the
    /// system, scaled by 1.2 so panning reaches just past the system's edge.
    /// </summary>
    private float ComputeSystemPanExtent()
    {
        if (_targetBodyNode == null)
            return 1f;

        Vector3 center = _targetBodyNode.GlobalPosition;
        var container = OrbitalScheduleUiHelpers.FindSystemContainer(_targetBodyNode);
        var bodies = OrbitalScheduleUiHelpers.GetAllBodies(container);

        float max = 0f;
        foreach (var body in bodies)
        {
            if (body is not Node3D node || !IsInstanceValid(node))
                continue;
            Vector3 offset = node.GlobalPosition - center;
            offset.Y = 0f;
            max = Mathf.Max(max, offset.Length());
        }

        return Mathf.Max(max, 1f) * 1.2f;
    }

    private void CycleBand(int dir)
    {
        int count = _bandRadii.Count;
        _selectedBandIndex = (_selectedBandIndex + dir + count) % count;
        _selectedRadius = _bandRadii[_selectedBandIndex];
        GetViewport().SetInputAsHandled();
    }

    private void ConfirmPlacement()
    {
        if (_targetBody == null || _stationDef == null)
            return;

        try
        {
            if (_usesBands)
            {
                if (!_targetBody.CanAddToBand(_selectedBandIndex))
                {
                    ToastSystem.Instance?.Show("Band is at capacity");
                    return;
                }

                ConstructionManager.Instance.CreateStation(
                    _targetBody, _selectedBandIndex, null, _stationDef);
            }
            else
            {
                ConstructionManager.Instance.CreateStationAtRadius(
                    _targetBody, _selectedRadius, null, _stationDef);
            }

            ToastSystem.Instance?.Show($"Construction started: {_stationDef.Name}");
            EmitSignal(SignalName.PlacementConfirmed);
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"StationOrbitPlacementMode: Failed to create station — {e.Message}");
            ToastSystem.Instance?.Show($"Error: {e.Message}");
        }
    }

    public void Cleanup()
    {
        _isActive = false;

        if (_ghostContainer != null)
        {
            _ghostContainer.QueueFree();
            _ghostContainer = null;
        }

        if (_rings != null)
        {
            _rings.QueueFree();
            _rings = null;
        }
    }
}
