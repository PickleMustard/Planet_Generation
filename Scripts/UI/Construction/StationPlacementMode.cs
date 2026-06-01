using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using UtilityLibrary;

namespace UI.Construction;

/// <summary>
/// Standalone orbital placement engine for stations, extracted from the old
/// OrbitalPlacementState so it can run as an overlay (no HSM transition). Shows a
/// ghost that snaps to orbital bands (band-based bodies) or floats at an
/// adjustable radius (continuous bodies); scroll / [ ] cycle the radius; left
/// click confirms. Mirrors <see cref="BuildingPlacementMode"/>'s lifecycle:
/// Initialize → PlacementConfirmed/PlacementCancelled → Cleanup.
/// </summary>
public partial class StationPlacementMode : Node
{
    private const float DEFAULT_FREE_DISTANCE = 100f;
    private const float RADIUS_SCROLL_STEP = 10f;
    private const float MIN_RADIUS_MULTIPLIER = 1.2f;

    [Signal]
    public delegate void PlacementConfirmedEventHandler();

    [Signal]
    public delegate void PlacementCancelledEventHandler();

    /// <summary>See <see cref="BuildingPlacementMode.UseMousePosition"/>.</summary>
    public bool UseMousePosition { get; set; }

    private Camera3D? _camera;
    private IOrbitalBody? _targetBody;
    private Node3D? _targetBodyNode;
    private StationDefinition? _stationDef;

    private Node3D? _ghostContainer;
    private Node3D? _ghostNode;

    private bool _usesBands;
    private int _selectedBandIndex;
    private float _selectedRadius;
    private bool _isActive;

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera3D();
    }

    public void Initialize(StationDefinition definition, IOrbitalBody targetBody)
    {
        _stationDef = definition;
        _targetBody = targetBody;
        _targetBodyNode = targetBody as Node3D;

        _usesBands = _targetBody.UsesBandPlacement;
        _selectedBandIndex = 0;
        _selectedRadius = _usesBands
            ? (_targetBody.GetBandCount() > 0
                ? _targetBody.GetOrbitBandRadius(0)
                : _targetBody.Radius * 2f)
            : DEFAULT_FREE_DISTANCE;

        CreateGhostModel();
        _isActive = true;

        GameLogger.Debug(
            $"StationPlacementMode: Placing '{_stationDef.Name}' around '{_targetBody.BodyName}'");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_isActive
            || _camera == null
            || _targetBody == null
            || _targetBodyNode == null
            || _ghostContainer == null)
            return;

        if (_usesBands)
            UpdateBandSnapping();
        else
            UpdateFreeFloating();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isActive)
            return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                ConfirmPlacement();
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                AdjustRadius(RADIUS_SCROLL_STEP);
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                AdjustRadius(-RADIUS_SCROLL_STEP);
                GetViewport().SetInputAsHandled();
            }
        }

        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Bracketright)
            {
                AdjustRadius(RADIUS_SCROLL_STEP);
                GetViewport().SetInputAsHandled();
            }
            else if (keyEvent.Keycode == Key.Bracketleft)
            {
                AdjustRadius(-RADIUS_SCROLL_STEP);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private Vector2 AimPoint()
    {
        var viewport = GetViewport();
        return UseMousePosition
            ? viewport.GetMousePosition()
            : viewport.GetVisibleRect().Size / 2.0f;
    }

    private void UpdateBandSnapping()
    {
        int bandCount = _targetBody!.GetBandCount();
        if (bandCount == 0)
            return;

        var aim = AimPoint();
        Vector3 rayOrigin = _camera!.ProjectRayOrigin(aim);
        Vector3 rayDir = _camera.ProjectRayNormal(aim);

        Vector3 bodyPos = _targetBodyNode!.GlobalPosition;
        Vector3 toBody = bodyPos - rayOrigin;
        float t = toBody.Dot(rayDir);
        Vector3 closestPoint = rayOrigin + rayDir * Mathf.Max(t, 0f);
        float distanceFromBody = (closestPoint - bodyPos).Length();

        float bestDelta = float.MaxValue;
        int bestBand = 0;
        for (int i = 0; i < bandCount; i++)
        {
            float bandRadius = _targetBody.GetOrbitBandRadius(i);
            float delta = Mathf.Abs(distanceFromBody - bandRadius);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestBand = i;
            }
        }

        _selectedBandIndex = bestBand;
        _selectedRadius = _targetBody.GetOrbitBandRadius(bestBand);

        Vector3 direction = (closestPoint - bodyPos).Normalized();
        if (direction.LengthSquared() < 0.001f)
            direction = Vector3.Up;

        _ghostContainer!.GlobalPosition = bodyPos + direction * _selectedRadius;
        _ghostContainer.Visible = true;
    }

    private void UpdateFreeFloating()
    {
        Vector3 camPos = _camera!.GlobalPosition;
        Vector3 camForward = -_camera.GlobalBasis.Z;

        _ghostContainer!.GlobalPosition = camPos + camForward * _selectedRadius;
        _ghostContainer.Visible = true;
    }

    private void AdjustRadius(float delta)
    {
        if (_usesBands)
        {
            int bandCount = _targetBody!.GetBandCount();
            if (bandCount == 0)
                return;

            if (delta > 0)
                _selectedBandIndex = (_selectedBandIndex + 1) % bandCount;
            else
                _selectedBandIndex = (_selectedBandIndex - 1 + bandCount) % bandCount;

            _selectedRadius = _targetBody.GetOrbitBandRadius(_selectedBandIndex);
        }
        else
        {
            float minRadius = _targetBody!.Radius * MIN_RADIUS_MULTIPLIER;
            _selectedRadius = Mathf.Max(minRadius, _selectedRadius + delta);
        }
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
                    _targetBody,
                    _selectedBandIndex,
                    null,
                    _stationDef);
            }
            else
            {
                ConstructionManager.Instance.CreateStationAtRadius(
                    _targetBody,
                    _selectedRadius,
                    null,
                    _stationDef);
            }

            ToastSystem.Instance?.Show($"Construction started: {_stationDef.Name}");
            EmitSignal(SignalName.PlacementConfirmed);
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"StationPlacementMode: Failed to create station — {e.Message}");
            ToastSystem.Instance?.Show($"Error: {e.Message}");
        }
    }

    private void CreateGhostModel()
    {
        _ghostNode = _stationDef?.Visual?.CreateModelInstance();

        if (_ghostNode == null)
        {
            float fallbackHeight = 2f;
            float fallbackRadius = fallbackHeight * 0.15f;
            _ghostNode = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    Height = fallbackHeight,
                    TopRadius = fallbackRadius,
                    BottomRadius = fallbackRadius,
                },
                Name = "GhostFallbackMesh",
            };
            GameLogger.Warning(
                $"StationPlacementMode: Using fallback ghost model for '{_stationDef?.Name}'");
        }

        ApplyGhostMaterial(_ghostNode);

        _ghostContainer = new Node3D { Name = "OrbitalGhostContainer", Visible = false };
        _ghostContainer.AddChild(_ghostNode);
        AddChild(_ghostContainer);
    }

    private static void ApplyGhostMaterial(Node node)
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

        if (_ghostContainer != null)
        {
            _ghostContainer.QueueFree();
            _ghostContainer = null;
            _ghostNode = null;
        }
    }
}
