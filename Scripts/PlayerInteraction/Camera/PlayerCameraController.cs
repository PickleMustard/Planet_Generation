using Godot;
using UI;

namespace PlayerInteraction.Camera;

/// <summary>
/// The single player camera with a dedicated finite state machine. In FreeFly it
/// is a child of PlayerShip driven by <see cref="PlayerController"/> (WASD +
/// mouse-look). In Focus it is reparented under a focused object's CameraAnchor
/// and driven by an <see cref="IFocusFramingStrategy"/> with orbit-rotate + zoom
/// only. The camera stays <see cref="Camera3D.Current"/> in every state so
/// <c>GetViewport().GetCamera3D()</c> always resolves to it.
///
/// State + saved free-fly pose live on the <see cref="CameraState"/> subnode.
/// </summary>
public partial class PlayerCameraController : Camera3D
{
    [Export]
    public float TransitionSpeed { get; set; } = 3.0f;

    // Top-down placement swoop tuning (used only by EnterTopDownPlacement and its
    // matching ExitFocus return).
    [Export]
    public float SwoopTravelTime { get; set; } = 0.9f;

    [Export]
    public float SwoopAccelExp { get; set; } = 2.0f;

    [Export]
    public float SwoopDecelExp { get; set; } = 2.0f;

    // Drag tuning (orbital bodies only), ported from OrbitalBodyOrbitCamera.
    private const float DRAG_THRESHOLD_PX = 5.0f;
    private const float ORBIT_SENSITIVITY = 0.005f;
    private const float PITCH_CLAMP = Mathf.Pi * 85.0f / 180.0f; // ~85 degrees

    private CameraState _state = null!;

    // Orbit parameters (live while focused).
    private float _orbitYaw;
    private float _orbitPitch;
    private float _orbitDistance;

    // Transition blend. Duration <= 0 is the legacy sentinel: drive progress by
    // TransitionSpeed and ease with the quadratic EaseOut (window focus). A
    // positive duration + _useSwoopEasing drives the configurable placement swoop.
    private float _transitionProgress;
    private Transform3D _transitionStart;
    private float _activeDuration;
    private bool _useSwoopEasing;

    // Top-down placement state.
    private bool _placementMode;
    private Vector3 _panOffset;
    private float _placementHeight;
    private float _maxPan;
    private TopDownPlacementFramingStrategy? _placementStrategy;

    // Drag detection.
    private bool _isLeftPressed;
    private bool _isDragging;
    private Vector2 _pressPosition;

    // PushDisable/PopDisable balance across (possibly retargeted) focus sessions.
    private bool _inputDisabled;

    // ───────── Public API ─────────

    /// <summary>The focused object, or null when in free-fly.</summary>
    public Node3D? FocusTarget =>
        _state.CurrentState != CameraFsmState.FreeFly ? _state.FocusTarget : null;

    /// <summary>True while fully focused (not transitioning).</summary>
    public bool IsFocused => _state.CurrentState == CameraFsmState.Focus;

    /// <summary>True in HUD/free-fly (PlayerController owns the camera).</summary>
    public bool IsFreeFly => _state.CurrentState == CameraFsmState.FreeFly;

    /// <summary>Whether the last left-press has crossed the drag threshold.</summary>
    public bool IsDragging => _isDragging;

    public override void _Ready()
    {
        _state = GetNode<CameraState>("CameraState");
        SetProcess(false); // FreeFly: PlayerController drives the camera.
    }

    /// <summary>
    /// Begins (or retargets) focus on <paramref name="anchor"/>. If already
    /// focused or transitioning out, this cancels and redirects to the new
    /// target without bouncing through free-fly (cross-window navigation).
    /// </summary>
    public void EnterFocus(
        Node3D anchor,
        Node3D target,
        IFocusFramingStrategy strategy,
        Vector2 screenOffset)
    {
        if (anchor == null || strategy == null)
            return;

        bool wasFreeFly = _state.CurrentState == CameraFsmState.FreeFly;
        if (wasFreeFly)
            _state.SaveFreeFly(this);

        // Reparent under the new anchor, preserving the current world pose so the
        // transition starts from where the camera actually is. Clearing TopLevel
        // reinterprets the transform relative to the parent, so re-apply the world
        // pose across the flip before reparenting.
        Transform3D worldPose = GlobalTransform;
        TopLevel = false;
        GlobalTransform = worldPose;
        Reparent(anchor, keepGlobalTransform: true);

        _state.FocusAnchor = anchor;
        _state.FocusTarget = target;
        _state.Strategy = strategy;
        _state.ScreenOffset = screenOffset;

        SeedOrbitParams(anchor);

        _transitionStart = GlobalTransform;
        _transitionProgress = 0f;
        _activeDuration = 0f; // legacy: TransitionSpeed-driven, EaseOut
        _useSwoopEasing = false;
        _placementMode = false;
        _placementStrategy = null;
        _isLeftPressed = false;
        _isDragging = false;
        _state.SetState(CameraFsmState.TransitionIn);

        if (!_inputDisabled)
        {
            WorldInputController.Instance?.PushDisable();
            _inputDisabled = true;
        }

        SetProcess(true);
    }

    /// <summary>
    /// Swoops the camera to a top-down view above <paramref name="targetBody"/>
    /// for orbital station placement. Reuses the focus reparent + input-disable
    /// machinery but drives a configurable swoop curve (<see cref="SwoopTravelTime"/>,
    /// <see cref="SwoopAccelExp"/>, <see cref="SwoopDecelExp"/>) and maps the
    /// scroll-wheel to camera height + middle-drag to a planar pan instead of
    /// orbit-rotate. <see cref="ExitFocus"/> swoops back to the saved pose.
    /// </summary>
    public void EnterTopDownPlacement(
        Node3D anchor,
        Node3D targetBody,
        TopDownPlacementFramingStrategy strategy,
        float maxPanRadius)
    {
        if (anchor == null || strategy == null)
            return;

        bool wasFreeFly = _state.CurrentState == CameraFsmState.FreeFly;
        if (wasFreeFly)
            _state.SaveFreeFly(this);

        Transform3D worldPose = GlobalTransform;
        TopLevel = false;
        GlobalTransform = worldPose;
        Reparent(anchor, keepGlobalTransform: true);

        _state.FocusAnchor = anchor;
        _state.FocusTarget = targetBody;
        _state.Strategy = strategy;
        _state.ScreenOffset = Vector2.Zero;

        _placementMode = true;
        _placementStrategy = strategy;
        _panOffset = Vector3.Zero;
        _placementHeight = strategy.ComputeOrbitDistance(anchor);
        _maxPan = Mathf.Max(maxPanRadius, strategy.OuterExtent);

        _transitionStart = GlobalTransform;
        _transitionProgress = 0f;
        _activeDuration = Mathf.Max(SwoopTravelTime, 0.01f);
        _useSwoopEasing = true;
        _isLeftPressed = false;
        _isDragging = false;
        _state.SetState(CameraFsmState.TransitionIn);

        if (!_inputDisabled)
        {
            WorldInputController.Instance?.PushDisable();
            _inputDisabled = true;
        }

        SetProcess(true);
    }

    /// <summary>
    /// Planar XZ pan for the top-down placement board. Converts a pixel drag to
    /// world units at the current camera height (same frustum math as
    /// <see cref="FramingMath.ApplyScreenOffset"/>) and accumulates a clamped
    /// offset. No-op outside placement mode.
    /// </summary>
    public void HandlePlacementPan(Vector2 screenDelta)
    {
        if (!_placementMode || _placementStrategy == null)
            return;

        float vpHeight = GetViewport().GetVisibleRect().Size.Y;
        if (vpHeight <= 0f)
            return;

        float fovRad = Mathf.DegToRad(Fov);
        float worldPerPixel = (2f * _placementHeight * Mathf.Tan(fovRad * 0.5f)) / vpHeight;

        Vector3 right = GlobalBasis.X;
        right.Y = 0f;
        right = right.LengthSquared() > 1e-6f ? right.Normalized() : Vector3.Right;
        Vector3 forward = GlobalBasis.Y; // camera "up" lies on the plane when looking straight down
        forward.Y = 0f;
        forward = forward.LengthSquared() > 1e-6f ? forward.Normalized() : Vector3.Forward;

        _panOffset += -right * (screenDelta.X * worldPerPixel)
                    + forward * (screenDelta.Y * worldPerPixel);

        float maxPan = Mathf.Max(_maxPan, 1f);
        if (_panOffset.Length() > maxPan)
            _panOffset = _panOffset.Normalized() * maxPan;
    }

    /// <summary>
    /// Begins a smooth return to the saved free-fly pose. The camera is reparented
    /// back to PlayerShip immediately (windows call this from the focused object's
    /// TreeExiting, before it frees — so the camera, currently its child, isn't
    /// freed with it); the blend then runs in world space.
    /// </summary>
    public void ExitFocus()
    {
        if (_state.CurrentState == CameraFsmState.FreeFly)
            return;

        RestoreFreeFlyParent();
        _transitionStart = GlobalTransform;
        _transitionProgress = 0f;
        _state.SetState(CameraFsmState.TransitionOut);
        SetProcess(true);
    }

    /// <summary>Compass angle of the focused object's north pole; 0 when n/a.</summary>
    public float GetFocusNorthScreenAngle()
    {
        if (_state.Strategy == null || _state.FocusAnchor == null
            || !IsInstanceValid(_state.FocusAnchor))
            return 0f;
        return _state.Strategy.GetNorthScreenAngle(_state.FocusAnchor, this);
    }

    /// <summary>
    /// Forwarded by the focusing window for orbital drag-rotate. No-op when the
    /// active strategy disallows drag.
    /// </summary>
    public void HandleDragInput(InputEvent @event)
    {
        if (_state.Strategy is not { AllowsDrag: true } || !IsFocused)
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
                    GetViewport().SetInputAsHandled();
                _isLeftPressed = false;
                _isDragging = false;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isLeftPressed)
        {
            if (!_isDragging)
            {
                if (_pressPosition.DistanceTo(mouseMotion.Position) >= DRAG_THRESHOLD_PX)
                    _isDragging = true;
                else
                    return;
            }

            _orbitYaw -= mouseMotion.Relative.X * ORBIT_SENSITIVITY;
            _orbitPitch -= mouseMotion.Relative.Y * ORBIT_SENSITIVITY;
            _orbitPitch = Mathf.Clamp(_orbitPitch, -PITCH_CLAMP, PITCH_CLAMP);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Scroll-wheel zoom (mouse-mode-agnostic; windows run MouseMode.Visible).
        if (!IsFocused || _state.Strategy == null)
            return;

        if (@event is InputEventMouseButton wheel && wheel.Pressed)
        {
            float step = 0f;
            if (wheel.ButtonIndex == MouseButton.WheelUp)
                step = -0.1f;
            else if (wheel.ButtonIndex == MouseButton.WheelDown)
                step = 0.1f;

            if (step != 0f)
            {
                var (min, max) = _state.Strategy.ZoomClamps;
                if (_placementMode)
                    _placementHeight = Mathf.Clamp(_placementHeight * (1f + step), min, max);
                else
                    _orbitDistance = Mathf.Clamp(_orbitDistance * (1f + step), min, max);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        switch (_state.CurrentState)
        {
            case CameraFsmState.TransitionIn:
            {
                if (!FocusValid())
                {
                    ForceReturnToFreeFly();
                    return;
                }
                ApplyBlendedTransform(_transitionStart, ComputeWorldPose(), AdvanceTransition(dt));
                if (_transitionProgress >= 1.0f)
                    _state.SetState(CameraFsmState.Focus);
                break;
            }

            case CameraFsmState.Focus:
            {
                if (!FocusValid())
                {
                    ForceReturnToFreeFly();
                    return;
                }
                GlobalTransform = ComputeWorldPose();
                break;
            }

            case CameraFsmState.TransitionOut:
            {
                ApplyBlendedTransform(_transitionStart, _state.SavedFreeFlyTransform, AdvanceTransition(dt));
                if (_transitionProgress >= 1.0f)
                    FinalizeReturn();
                break;
            }
        }
    }

    // ───────── Internals ─────────

    private bool FocusValid() =>
        _state.FocusAnchor != null
        && IsInstanceValid(_state.FocusAnchor)
        && _state.Strategy != null;

    private void SeedOrbitParams(Node3D anchor)
    {
        _orbitDistance = _state.Strategy!.ComputeOrbitDistance(anchor);

        var offset = GlobalPosition - anchor.GlobalPosition;
        if (offset.LengthSquared() > 1e-4f)
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
    }

    /// <summary>
    /// Advances the active transition and returns its eased [0,1] value. Legacy
    /// (duration &lt;= 0) uses TransitionSpeed + quadratic EaseOut; placement
    /// swoops use the configurable S-curve.
    /// </summary>
    private float AdvanceTransition(float dt)
    {
        float rate = _activeDuration > 0f ? dt / _activeDuration : dt * TransitionSpeed;
        _transitionProgress = Mathf.Min(_transitionProgress + rate, 1.0f);
        return _useSwoopEasing
            ? CameraEasing.SCurve(_transitionProgress, SwoopAccelExp, SwoopDecelExp)
            : EaseOut(_transitionProgress);
    }

    private Transform3D ComputeWorldPose()
    {
        if (_placementMode && _placementStrategy != null && _state.FocusAnchor != null)
            return _placementStrategy.ComputeTopDownPose(
                _state.FocusAnchor, _panOffset, _placementHeight, this);

        return _state.Strategy!.ComputeWorldPose(
            _state.FocusAnchor!, _orbitYaw, _orbitPitch, _orbitDistance, _state.ScreenOffset, this);
    }

    private void ApplyBlendedTransform(Transform3D start, Transform3D end, float t)
    {
        var pos = start.Origin.Lerp(end.Origin, t);
        var rot = start.Basis.GetRotationQuaternion().Slerp(end.Basis.GetRotationQuaternion(), t);
        GlobalTransform = new Transform3D(new Basis(rot), pos);
    }

    /// <summary>Completes a normal smooth return: reparent back, restore pose.</summary>
    private void FinalizeReturn()
    {
        RestoreFreeFlyParent();
        GlobalTransform = _state.SavedFreeFlyTransform;
        EndFocusBookkeeping();
    }

    /// <summary>Hard return used when the focused object vanished mid-flow.</summary>
    private void ForceReturnToFreeFly()
    {
        RestoreFreeFlyParent();
        GlobalTransform = _state.SavedFreeFlyTransform;
        EndFocusBookkeeping();
    }

    private void RestoreFreeFlyParent()
    {
        // Preserve the world pose across both the reparent and the TopLevel flip.
        Transform3D worldPose = GlobalTransform;
        var parent = _state.SavedFreeFlyParent;
        if (parent != null && IsInstanceValid(parent) && GetParentOrNull<Node>() != parent)
            Reparent(parent, keepGlobalTransform: true);
        TopLevel = true;
        GlobalTransform = worldPose;
    }

    private void EndFocusBookkeeping()
    {
        if (_inputDisabled)
        {
            WorldInputController.Instance?.PopDisable();
            _inputDisabled = false;
        }
        _state.ClearFocus();
        _state.SetState(CameraFsmState.FreeFly);
        _placementMode = false;
        _placementStrategy = null;
        _panOffset = Vector3.Zero;
        _isLeftPressed = false;
        _isDragging = false;
        SetProcess(false);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
