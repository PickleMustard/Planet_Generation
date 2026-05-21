using System;
using Constructables;
using Godot;
using Logistics.Resources;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// 3D orbital placement state for stations. Shows a ghost model that snaps
/// to orbital bands (band-based bodies) or floats at an adjustable radius
/// (continuous bodies). Replaces BandSelectionPopup and RadiusInputPopup.
/// </summary>
public partial class OrbitalPlacementState : LimboState
{
    private const float DEFAULT_FREE_DISTANCE = 100f;
    private const float RADIUS_SCROLL_STEP = 10f;
    private const float MIN_RADIUS_MULTIPLIER = 1.2f;

    private Camera3D? _camera;
    private IOrbitalBody? _targetBody;
    private Node3D? _targetBodyNode;
    private StationDefinition? _stationDef;
    private Label? _statusLabel;

    private Node3D? _ghostContainer;
    private Node3D? _ghostNode;

    private bool _usesBands;
    private int _selectedBandIndex;
    private float _selectedRadius;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "OrbitalPlacementState");

        _camera = GetViewport().GetCamera3D();
        _statusLabel = GetNodeOrNull<Label>("StatusLabel");

        // Read target body from blackboard using OrbitalBodyConverter
        if (Blackboard == null)
        {
            GameLogger.Error("OrbitalPlacementState: Blackboard is null");
            Dispatch("placement_cancelled");
            return;
        }

        try
        {
            OrbitalBodyConverter.GetFromBlackboard(
                Blackboard.Top(),
                "ConstructionTargetBody",
                onCelestialBody: cb =>
                {
                    _targetBody = cb;
                    _targetBodyNode = cb;
                },
                onSatelliteBody: sb =>
                {
                    _targetBody = sb;
                    _targetBodyNode = sb;
                }
            );
        }
        catch (Exception e)
        {
            GameLogger.Error(
                $"OrbitalPlacementState: Failed to get target body from blackboard - {e.Message}"
            );
            Dispatch("placement_cancelled");
            return;
        }

        if (_targetBody == null || _targetBodyNode == null)
        {
            GameLogger.Error("OrbitalPlacementState: No target body in blackboard");
            Dispatch("placement_cancelled");
            return;
        }

        // Look up station definition
        var defName = Blackboard?.Top().GetVar("ConstructionDefinitionName").AsString() ?? "";
        StationDatabase.Instance.TryGetStation(defName, out _stationDef);
        if (_stationDef == null)
        {
            GameLogger.Error($"OrbitalPlacementState: Station definition '{defName}' not found");
            Dispatch("placement_cancelled");
            return;
        }

        _usesBands = _targetBody.UsesBandPlacement;
        _selectedBandIndex = 0;
        _selectedRadius = _usesBands
            ? (
                _targetBody.GetBandCount() > 0
                    ? _targetBody.GetOrbitBandRadius(0)
                    : _targetBody.Radius * 2f
            )
            : DEFAULT_FREE_DISTANCE;

        CreateGhostModel();
        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        UpdateStatusLabel();
        GameLogger.Debug(
            $"OrbitalPlacementState: Placing '{_stationDef.Name}' around '{_targetBody.BodyName}'"
        );
    }

    public override void _Exit()
    {
        base._Exit();

        DestroyGhost();

        if (_statusLabel != null)
            _statusLabel.Visible = false;

        _targetBody = null;
        _targetBodyNode = null;
        _stationDef = null;
        _camera = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        if (
            _camera == null
            || _targetBody == null
            || _targetBodyNode == null
            || _ghostContainer == null
        )
            return;

        if (_usesBands)
            UpdateBandSnapping();
        else
            UpdateFreeFloating();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            GD.Print("OrbitalPlacementState: Mouse button pressed");
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
            if (keyEvent.Keycode == Key.Escape)
            {
                Dispatch("placement_cancelled");
                GetViewport().SetInputAsHandled();
            }
            else if (keyEvent.Keycode == Key.Bracketright)
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

    private void UpdateBandSnapping()
    {
        int bandCount = _targetBody!.GetBandCount();
        if (bandCount == 0)
            return;

        // Cast ray from screen center to find distance from body
        var viewport = GetViewport();
        var screenCenter = viewport.GetVisibleRect().Size / 2.0f;
        Vector3 rayOrigin = _camera!.ProjectRayOrigin(screenCenter);
        Vector3 rayDir = _camera.ProjectRayNormal(screenCenter);

        // Find closest point on ray to body center
        Vector3 bodyPos = _targetBodyNode!.GlobalPosition;
        Vector3 toBody = bodyPos - rayOrigin;
        float t = toBody.Dot(rayDir);
        Vector3 closestPoint = rayOrigin + rayDir * Mathf.Max(t, 0f);
        float distanceFromBody = (closestPoint - bodyPos).Length();

        // Find nearest band
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

        // Position ghost on the line from body center toward closest point, at band radius
        Vector3 direction = (closestPoint - bodyPos).Normalized();
        if (direction.LengthSquared() < 0.001f)
            direction = Vector3.Up;

        _ghostContainer!.GlobalPosition = bodyPos + direction * _selectedRadius;
        _ghostContainer.Visible = true;

        UpdateStatusLabel();
    }

    private void UpdateFreeFloating()
    {
        // Position ghost at _selectedRadius from camera along forward direction
        Vector3 camPos = _camera!.GlobalPosition;
        Vector3 camForward = -_camera.GlobalBasis.Z;

        _ghostContainer!.GlobalPosition = camPos + camForward * _selectedRadius;
        _ghostContainer.Visible = true;

        UpdateStatusLabel();
    }

    private void AdjustRadius(float delta)
    {
        if (_usesBands)
        {
            // Cycle through bands
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

        UpdateStatusLabel();
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
                    _stationDef
                );
            }
            else
            {
                ConstructionManager.Instance.CreateStationAtRadius(
                    _targetBody,
                    _selectedRadius,
                    null,
                    _stationDef
                );
            }

            ToastSystem.Instance?.Show($"Construction started: {_stationDef.Name}");
            Dispatch("placement_confirmed");
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"OrbitalPlacementState: Failed to create station — {e.Message}");
            ToastSystem.Instance?.Show($"Error: {e.Message}");
        }
    }

    private void UpdateStatusLabel()
    {
        if (_statusLabel == null)
            return;

        if (_usesBands)
        {
            int bandCount = _targetBody!.GetBandCount();
            int currentCount = _targetBody.GetBandSatelliteCount(_selectedBandIndex);
            bool canAdd = _targetBody.CanAddToBand(_selectedBandIndex);
            string status = canAdd ? "available" : "FULL";
            _statusLabel.Text =
                $"Band {_selectedBandIndex + 1}/{bandCount} — Radius: {_selectedRadius:F0} — {status} ({currentCount} occupants)\nScroll/[/] to change band — Click to place — ESC to cancel";
        }
        else
        {
            _statusLabel.Text =
                $"Radius: {_selectedRadius:F0}\nScroll/[/] to adjust — Click to place — ESC to cancel";
        }

        _statusLabel.Visible = true;
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
                Mesh = new CylinderMesh { Height = fallbackHeight, TopRadius = fallbackRadius, BottomRadius = fallbackRadius },
                Name = "GhostFallbackMesh",
            };
            GameLogger.Warning(
                $"OrbitalPlacementState: Using fallback ghost model for '{_stationDef?.Name}'"
            );
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

    private void DestroyGhost()
    {
        if (_ghostContainer != null)
        {
            _ghostContainer.QueueFree();
            _ghostContainer = null;
            _ghostNode = null;
        }
    }
}
