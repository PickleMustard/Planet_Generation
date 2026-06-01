using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using UtilityLibrary;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Logistics-unit equivalent of <see cref="UI.StationWindow.StationOrbitCamera"/>.
/// Repurposes the player camera to keep the inspected ship framed in the
/// window's negative space. Because ships move (including mid-transfer), the
/// framing transform is recomputed every frame from the unit's live
/// <see cref="Node3D.GlobalPosition"/>, so the camera tracks the ship as it
/// travels. No drag or zoom; smooth lerp in/out.
/// </summary>
public partial class LogisticsUnitOrbitCamera : Node
{
    [Export]
    public float FramingDistance { get; set; } = 10.0f;

    private Camera3D? _camera;
    private LogisticsUnit? _unit;
    private Node3D? _parentBodyNode;

    private Transform3D _savedCameraTransform;
    private bool _hasSavedState;

    private float _transitionProgress;
    private bool _isTransitioningIn;
    private bool _isTransitioningOut;
    private bool _isOrbiting;
    private Transform3D _transitionStartTransform;
    private const float TRANSITION_SPEED = 3.0f;

    /// <summary>
    /// Screen-space offset (pixels) used to keep the ship centered in the
    /// unobstructed area of the window. Set by <see cref="LogisticsUnitWindow"/>
    /// once it knows the panel geometry.
    /// </summary>
    public Vector2 ScreenOffset { get; set; }

    public override void _Ready()
    {
        SetProcess(false);
    }

    public void BeginOrbit(Camera3D camera, LogisticsUnit unit)
    {
        if (camera == null || unit == null)
        {
            GameLogger.Warning("[LogisticsUnitOrbitCamera] Null camera or unit; cannot begin orbit.");
            return;
        }

        _camera = camera;
        _unit = unit;
        _parentBodyNode = FindParentBody(unit);

        _savedCameraTransform = camera.GlobalTransform;
        _transitionStartTransform = camera.GlobalTransform;
        _hasSavedState = true;

        _transitionProgress = 0f;
        _isTransitioningIn = true;
        _isTransitioningOut = false;
        _isOrbiting = true;

        SetProcess(true);
    }

    public void EndOrbit()
    {
        if (!_hasSavedState || _camera == null)
        {
            SetProcess(false);
            return;
        }

        _transitionStartTransform = _camera.GlobalTransform;
        _isTransitioningIn = false;
        _isTransitioningOut = true;
        _transitionProgress = 0f;
    }

    public override void _Process(double delta)
    {
        if (_camera == null || _unit == null || !IsInstanceValid(_unit))
            return;

        float dt = (float)delta;

        if (_isTransitioningIn)
        {
            _transitionProgress = Mathf.Min(_transitionProgress + dt * TRANSITION_SPEED, 1.0f);
            float t = EaseOut(_transitionProgress);
            ApplyBlendedTransform(_transitionStartTransform, ComputeOrbitTransform(), t);

            if (_transitionProgress >= 1.0f)
                _isTransitioningIn = false;
        }
        else if (_isTransitioningOut)
        {
            _transitionProgress = Mathf.Min(_transitionProgress + dt * TRANSITION_SPEED, 1.0f);
            float t = EaseOut(_transitionProgress);
            ApplyBlendedTransform(_transitionStartTransform, _savedCameraTransform, t);

            if (_transitionProgress >= 1.0f)
            {
                _isTransitioningOut = false;
                _camera.GlobalTransform = _savedCameraTransform;
                _hasSavedState = false;
                _unit = null;
                _parentBodyNode = null;
                _isOrbiting = false;
                SetProcess(false);
                return;
            }
        }
        else if (_isOrbiting)
        {
            _camera.GlobalTransform = ComputeOrbitTransform();
        }
    }

    private void ApplyBlendedTransform(Transform3D start, Transform3D end, float t)
    {
        if (_camera == null)
            return;
        var pos = start.Origin.Lerp(end.Origin, t);
        var rot = start.Basis.GetRotationQuaternion().Slerp(end.Basis.GetRotationQuaternion(), t);
        _camera.GlobalTransform = new Transform3D(new Basis(rot), pos);
    }

    private Transform3D ComputeOrbitTransform()
    {
        Vector3 shipPos = _unit!.GlobalPosition;

        // Look at the ship from the direction "away" from its parent body so
        // the body sits behind it; fall back to a fixed direction when the ship
        // has no celestial parent (e.g. mid heliocentric transfer).
        Vector3 away = _parentBodyNode != null && IsInstanceValid(_parentBodyNode)
            ? shipPos - _parentBodyNode.GlobalPosition
            : Vector3.Back;
        if (away.LengthSquared() < 1e-4f)
            away = Vector3.Back;
        away = away.Normalized();

        Vector3 camPos = shipPos + away * FramingDistance;
        Vector3 up = Mathf.Abs(away.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
        Basis basis = Basis.LookingAt(shipPos - camPos, up);

        // Slide the camera laterally so the ship projects to the negative-space
        // center instead of the geometric viewport center. Convert pixels →
        // world units using the camera's vertical frustum extent at framing
        // distance. (Mirrors StationOrbitCamera.)
        if (ScreenOffset.X != 0f || ScreenOffset.Y != 0f)
        {
            float vpHeight = _camera!.GetViewport().GetVisibleRect().Size.Y;
            if (vpHeight > 0f)
            {
                float fovRad = Mathf.DegToRad(_camera.Fov);
                float worldPerPixel = (2f * FramingDistance * Mathf.Tan(fovRad * 0.5f)) / vpHeight;
                Vector3 right = basis.X;
                Vector3 upAxis = basis.Y;
                camPos +=
                    -right * (ScreenOffset.X * worldPerPixel)
                    + upAxis * (ScreenOffset.Y * worldPerPixel);
                basis = Basis.LookingAt(shipPos - camPos, up);
            }
        }

        return new Transform3D(basis, camPos);
    }

    private static Node3D? FindParentBody(Node node)
    {
        Node? current = node.GetParent();
        while (current != null)
        {
            if (current is CelestialBody body)
                return body;
            current = current.GetParent();
        }
        return null;
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
