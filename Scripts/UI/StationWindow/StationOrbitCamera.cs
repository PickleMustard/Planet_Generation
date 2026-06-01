using Constructables;
using Godot;
using UtilityLibrary;

namespace UI.StationWindow;

/// <summary>
/// Controls the player camera during station inspection. Locks the camera
/// along the parent-body→station ray so the parent body is always behind the
/// station from the viewer's POV. No drag or zoom; smooth lerp in/out.
/// </summary>
public partial class StationOrbitCamera : Node
{
    [Export]
    public float FramingDistanceMultiplier { get; set; } = 1.5f;

    [Export]
    public float MinFramingDistance { get; set; } = 6.0f;

    private Camera3D? _camera;
    private StationSatellite? _station;
    private Node3D? _cameraAnchor;
    private Node3D? _parentBodyNode;

    private Transform3D _savedCameraTransform;
    private bool _hasSavedState;

    private float _framingDistance;

    private float _transitionProgress;
    private bool _isTransitioningIn;
    private bool _isTransitioningOut;
    private bool _isOrbiting;
    private Transform3D _transitionStartTransform;
    private const float TRANSITION_SPEED = 3.0f;

    /// <summary>
    /// Screen-space offset (pixels) used to keep the station centered in the
    /// unobstructed area of the window. Set by <see cref="StationWindow"/>
    /// once it knows the right-panel geometry.
    /// </summary>
    public Vector2 ScreenOffset { get; set; }

    public override void _Ready()
    {
        SetProcess(false);
    }

    public void BeginOrbit(Camera3D camera, StationSatellite station)
    {
        if (station.ParentBody is not Node3D parentNode)
        {
            GameLogger.Warning(
                $"[StationOrbitCamera] Station '{station.Name}' has no Node3D ParentBody; "
                + "cannot begin orbit."
            );
            return;
        }

        _camera = camera;
        _station = station;
        _parentBodyNode = parentNode;
        _cameraAnchor = station.GetOrCreateCameraAnchor();

        _framingDistance = Mathf.Max(
            _cameraAnchor.Position.Length() * FramingDistanceMultiplier,
            MinFramingDistance
        );

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
        if (_camera == null || _station == null || _parentBodyNode == null)
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
                _station = null;
                _cameraAnchor = null;
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
        Vector3 stationPos = _cameraAnchor?.GlobalPosition ?? _station!.GlobalPosition;
        Vector3 parentPos = _parentBodyNode!.GlobalPosition;

        Vector3 away = stationPos - parentPos;
        if (away.LengthSquared() < 1e-4f)
            away = Vector3.Back;
        away = away.Normalized();

        Vector3 camPos = stationPos + away * _framingDistance;
        Vector3 up = Mathf.Abs(away.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
        Basis basis = Basis.LookingAt(stationPos - camPos, up);

        // Slide the camera laterally so the station projects to the
        // negative-space center (left third of the viewport) instead of the
        // geometric viewport center. Convert pixels → world units using the
        // camera's vertical frustum extent at framing distance.
        if (ScreenOffset.X != 0f || ScreenOffset.Y != 0f)
        {
            float vpHeight = _camera!.GetViewport().GetVisibleRect().Size.Y;
            if (vpHeight > 0f)
            {
                float fovRad = Mathf.DegToRad(_camera.Fov);
                float worldPerPixel = (2f * _framingDistance * Mathf.Tan(fovRad * 0.5f)) / vpHeight;
                Vector3 right = basis.X;
                Vector3 upAxis = basis.Y;
                // Negative X offset because shifting the camera right moves
                // the station left on screen; matching Y sign because screen
                // Y is inverted relative to world Y.
                camPos +=
                    -right * (ScreenOffset.X * worldPerPixel)
                    + upAxis * (ScreenOffset.Y * worldPerPixel);
                basis = Basis.LookingAt(stationPos - camPos, up);
            }
        }

        return new Transform3D(basis, camPos);
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
