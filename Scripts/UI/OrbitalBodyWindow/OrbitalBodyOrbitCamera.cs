using Godot;

namespace UI;

/// <summary>
/// Controls the player camera during orbital body inspection.
/// Smoothly transitions from the ship view to an orbit view centered on the body,
/// and supports click-drag rotation around the body (clamped at poles).
/// </summary>
public partial class OrbitalBodyOrbitCamera : Node
{
    [Export]
    public float OrbitRadiusMultiplier { get; set; } = 4.0f;

    private Camera3D? _camera;
    private IOrbitalBody? _targetBody;
    private Node3D? _cameraAnchor;

    // Saved player camera state for restoration
    private Transform3D _savedCameraTransform;
    private bool _hasSavedState;

    // Orbit parameters
    private float _orbitYaw;
    private float _orbitPitch;
    private float _orbitDistance;
    private bool _isOrbiting;

    // Smooth transition
    private float _transitionProgress;
    private bool _isTransitioningIn;
    private bool _isTransitioningOut;
    private Transform3D _transitionStartTransform;
    private const float TRANSITION_SPEED = 3.0f;

    // Drag detection (click vs drag)
    private bool _isLeftPressed;
    private bool _isDragging;
    private Vector2 _pressPosition;
    private const float DRAG_THRESHOLD_PX = 5.0f;
    private const float ORBIT_SENSITIVITY = 0.005f;
    private const float PITCH_CLAMP = Mathf.Pi * 85.0f / 180.0f; // ~85 degrees

    public bool IsDragging => _isDragging;

    /// <summary>
    /// Screen-space offset (pixels) used to keep the planet centered in the
    /// unobstructed area of the window — set by the surrounding UI once it
    /// knows the tab strip and bottom-pane geometry.
    /// </summary>
    public Vector2 ScreenOffset { get; set; }

    /// <summary>
    /// Returns the on-screen angle (radians) of the planet's north pole,
    /// measured clockwise from screen-up. Used to spin the compass rose so
    /// its needle tracks the planet's actual N. Returns 0 if the camera or
    /// target isn't available.
    /// </summary>
    public float GetPlanetNorthScreenAngle()
    {
        if (_camera == null || _targetBody == null)
            return 0f;

        Vector3 center = _cameraAnchor != null
            ? _cameraAnchor.GlobalPosition
            : _targetBody.BodyPosition;
        Vector3 north = center + Vector3.Up * Mathf.Max(_targetBody.Radius, 1f);

        Vector2 centerScreen = _camera.UnprojectPosition(center);
        Vector2 northScreen = _camera.UnprojectPosition(north);
        Vector2 delta = northScreen - centerScreen;
        if (delta.LengthSquared() < 1e-6f)
            return 0f;
        // Atan2(x, -y): 0 = pointing up on screen, positive = clockwise.
        return Mathf.Atan2(delta.X, -delta.Y);
    }

    public void BeginOrbit(Camera3D camera, IOrbitalBody body)
    {
        _camera = camera;
        _targetBody = body;

        // Get the body's CameraAnchor for position reference
        // Both CelestialBody and SatelliteBody implement ISelectableBody
        if (body is ProceduralGeneration.PlanetGeneration.ISelectableBody selectableBody)
        {
            _cameraAnchor = selectableBody.GetOrCreateCameraAnchor();
        }

        // Save current camera state
        _savedCameraTransform = camera.GlobalTransform;
        _transitionStartTransform = camera.GlobalTransform;
        _hasSavedState = true;

        // Compute initial orbit angles from camera's current direction relative to anchor.
        // The target position lies on the great circle through the player, at a constant
        // radius = body.Radius * OrbitRadiusMultiplier.
        var anchorPos =
            _cameraAnchor != null ? _cameraAnchor.GlobalPosition : ((Node3D)body).GlobalPosition;
        var offset = camera.GlobalPosition - anchorPos;

        _orbitDistance = body.Radius * OrbitRadiusMultiplier;

        const float EPS = 1e-4f;
        if (offset.LengthSquared() > EPS)
        {
            _orbitYaw = Mathf.Atan2(offset.X, offset.Z);
            _orbitPitch = Mathf.Asin(Mathf.Clamp(offset.Y / offset.Length(), -1f, 1f));
            _orbitPitch = Mathf.Clamp(_orbitPitch, -PITCH_CLAMP, PITCH_CLAMP);
        }
        else
        {
            _orbitYaw = 0f;
            _orbitPitch = 0f;
        }

        // Begin transition
        _transitionProgress = 0f;
        _isTransitioningIn = true;
        _isTransitioningOut = false;
        _isOrbiting = true;

        _isLeftPressed = false;
        _isDragging = false;

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

    public override void _Ready()
    {
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (_camera == null || _targetBody == null)
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
                _targetBody = null;
                _cameraAnchor = null;
                _isOrbiting = false;
                SetProcess(false);
                return;
            }
        }
        else if (_isOrbiting)
        {
            // Continue updating while orbiting to follow the body's movement
            ComputeTransformAndPosition();
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
        var center =
            _cameraAnchor != null ? _cameraAnchor.GlobalPosition : _targetBody!.BodyPosition;

        float x = Mathf.Cos(_orbitPitch) * Mathf.Sin(_orbitYaw);
        float y = Mathf.Sin(_orbitPitch);
        float z = Mathf.Cos(_orbitPitch) * Mathf.Cos(_orbitYaw);

        var dir = new Vector3(x, y, z).Normalized();
        var camPos = center - dir * _orbitDistance;

        var up = Mathf.Abs(dir.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
        var basis = Basis.LookingAt(center - camPos, up);

        // Slide the camera laterally so the body projects to the negative-space
        // center instead of the geometric viewport center. ScreenOffset is the
        // desired pixel offset of the body's screen position from the actual
        // viewport center. We convert pixels → world units using the camera's
        // vertical frustum extent at the orbit distance.
        if (_camera != null && (ScreenOffset.X != 0f || ScreenOffset.Y != 0f))
        {
            float vpHeight = _camera.GetViewport().GetVisibleRect().Size.Y;
            if (vpHeight > 0f)
            {
                float fovRad = Mathf.DegToRad(_camera.Fov);
                float worldPerPixel = (2f * _orbitDistance * Mathf.Tan(fovRad * 0.5f)) / vpHeight;
                Vector3 right = basis.X;
                Vector3 upAxis = basis.Y;
                // Negative X offset because shifting the camera right moves the
                // body left on screen; matching signs for Y because screen Y is
                // inverted relative to world Y.
                camPos += -right * (ScreenOffset.X * worldPerPixel)
                       +  upAxis * (ScreenOffset.Y * worldPerPixel);
                basis = Basis.LookingAt(center - camPos, up);
            }
        }

        return new Transform3D(basis, camPos);
    }

    public void HandleDragInput(InputEvent @event)
    {
        if (_camera == null || _targetBody == null)
            return;

        if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            if (mouseBtn.Pressed)
            {
                _isLeftPressed = true;
                _isDragging = false;
                _pressPosition = mouseBtn.Position;
            }
            else
            {
                if (_isDragging)
                {
                    // Consume the release so it doesn't trigger cell selection
                    _camera.GetViewport().SetInputAsHandled();
                }
                _isLeftPressed = false;
                _isDragging = false;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isLeftPressed)
        {
            var currentPos = mouseMotion.Position;
            if (!_isDragging)
            {
                float dist = _pressPosition.DistanceTo(currentPos);
                if (dist >= DRAG_THRESHOLD_PX)
                    _isDragging = true;
                else
                    return;
            }

            // Apply rotation
            _orbitYaw -= mouseMotion.Relative.X * ORBIT_SENSITIVITY;
            _orbitPitch += mouseMotion.Relative.Y * ORBIT_SENSITIVITY;
            _orbitPitch = Mathf.Clamp(_orbitPitch, -PITCH_CLAMP, PITCH_CLAMP);

            _camera.GetViewport().SetInputAsHandled();
        }
    }

    private void ComputeTransformAndPosition()
    {
        _camera!.GlobalTransform = ComputeOrbitTransform();
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
