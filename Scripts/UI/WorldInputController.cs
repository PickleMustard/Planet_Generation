using System.Collections.Generic;
using Godot;

namespace UI;

/// <summary>
/// Central node that owns 3D-world input (movement, camera, mode toggle).
/// States that need to take over input call PushDisable on _Enter and
/// PopDisable on _Exit; reference-counted so nested overrides are safe.
/// </summary>
public partial class WorldInputController : Node
{
    public const string GroupName = "world_input_controller";

    public static WorldInputController? Instance { get; private set; }

    [Signal]
    public delegate void MoveEventHandler(Vector3 direction);

    [Signal]
    public delegate void VerticalMoveEventHandler(float vertical);

    [Signal]
    public delegate void RotateAxisEventHandler(float rotation);

    [Signal]
    public delegate void AccelerateEventHandler(bool accelerate);

    [Signal]
    public delegate void CameraLookEventHandler(Vector2 mouseDelta);

    [Signal]
    public delegate void IndependentRotatationEventHandler(bool isMouseButtonPressed);

    private bool _isMiddleMousePressed = false;
    private Vector3 _moveDirection = Vector3.Zero;
    private Vector3 _verticalMovement = Vector3.Zero;
    private float _rotation = 0.0f;

    private int _disableRefCount = 0;

    public bool IsActive => _disableRefCount == 0;

    // Gameplay cursor mode when no panel holds the stack. Owned by the middle-mouse
    // toggle below; the stack restores to this when emptied.
    private Input.MouseModeEnum _baseMouseMode = Input.MouseModeEnum.Captured;

    // LIFO of cursor-mode requests from GUI panels. A panel pushes its desired mode
    // on entry and pops on exit; the effective mode is the top of the stack, or the
    // base mode when empty. This mirrors the GUI navigation return stack.
    private readonly List<Input.MouseModeEnum> _cursorStack = new();

    /// <summary>
    /// Request a cursor mode while a GUI panel is open. Pair every call with
    /// <see cref="PopCursorMode"/> in the panel's exit/hide.
    /// </summary>
    public void PushCursorMode(Input.MouseModeEnum mode)
    {
        _cursorStack.Add(mode);
        ApplyEffectiveMouseMode();
    }

    /// <summary>
    /// Release the cursor mode pushed by a GUI panel, restoring whatever was
    /// effective before it (the panel underneath, or the gameplay base mode).
    /// </summary>
    public void PopCursorMode()
    {
        if (_cursorStack.Count > 0)
            _cursorStack.RemoveAt(_cursorStack.Count - 1);
        ApplyEffectiveMouseMode();
    }

    /// <summary>
    /// Backstop for the no-panel HUD/idle state: drop any leaked cursor requests and
    /// fall back to the gameplay base mode. Guards against a panel that failed to pop.
    /// </summary>
    public void ResetCursorMode()
    {
        _cursorStack.Clear();
        ApplyEffectiveMouseMode();
    }

    private void ApplyEffectiveMouseMode()
    {
        var mode = _cursorStack.Count > 0 ? _cursorStack[^1] : _baseMouseMode;
        Input.SetMouseMode(mode);
    }

    public override void _EnterTree()
    {
        Instance = this;
        AddToGroup(GroupName);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        _baseMouseMode = Input.MouseModeEnum.Captured;
        ApplyEffectiveMouseMode();
    }

    public void PushDisable()
    {
        _disableRefCount++;
        UpdateProcessing();
    }

    public void PopDisable()
    {
        if (_disableRefCount > 0)
            _disableRefCount--;
        UpdateProcessing();
    }

    private void UpdateProcessing()
    {
        var active = IsActive;
        SetProcessInput(active);
        SetProcessUnhandledInput(active);
    }

    public override void _Input(InputEvent @event)
    {
#if DEBUG
        if (Debug.DebugMenu.Instance?.IsVisible == true)
            return;
#endif

        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Middle && mouseEvent.Pressed)
            {
                _isMiddleMousePressed = !_isMiddleMousePressed;
                _baseMouseMode = _isMiddleMousePressed
                    ? Input.MouseModeEnum.Confined
                    : Input.MouseModeEnum.Captured;
                ApplyEffectiveMouseMode();
            }
        }
        else if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.IsEcho())
        {
            if (
                keyEvent.Keycode == Key.W
                || keyEvent.Keycode == Key.S
                || keyEvent.Keycode == Key.A
                || keyEvent.Keycode == Key.D
            )
            {
                if (keyEvent.Keycode == Key.W)
                    _moveDirection.Z -= 1;
                else if (keyEvent.Keycode == Key.S)
                    _moveDirection.Z += 1;
                if (keyEvent.Keycode == Key.A)
                    _moveDirection.X -= 1;
                else if (keyEvent.Keycode == Key.D)
                    _moveDirection.X += 1;
                _moveDirection = _moveDirection.Normalized();
                EmitSignal(SignalName.Move, _moveDirection);
                ConsumeInput();
            }
            if (keyEvent.Keycode == Key.Space || keyEvent.Keycode == Key.Ctrl)
            {
                if (keyEvent.Keycode == Key.Space)
                    _verticalMovement.Y += 1;
                else if (keyEvent.Keycode == Key.Ctrl)
                    _verticalMovement.Y -= 1;
                _verticalMovement = _verticalMovement.Normalized();
                EmitSignal(SignalName.VerticalMove, _verticalMovement);
                ConsumeInput();
            }
            if (keyEvent.Keycode == Key.Q || keyEvent.Keycode == Key.E)
            {
                if (keyEvent.Keycode == Key.Q)
                    _rotation -= 1;
                else if (keyEvent.Keycode == Key.E)
                    _rotation += 1;
                EmitSignal(SignalName.RotateAxis, _rotation);
                ConsumeInput();
            }
            if (keyEvent.Keycode == Key.Shift)
            {
                EmitSignal(SignalName.Accelerate, true);
                ConsumeInput();
            }
            if (keyEvent.Keycode == Key.Bracketright)
            {
                Godot.Engine.TimeScale += .5;
                GD.Print($"TimeScale: {Godot.Engine.TimeScale}");
                Godot.Engine.PhysicsTicksPerSecond += 5;
                ConsumeInput();
            }
            if (keyEvent.Keycode == Key.Bracketleft)
            {
                Godot.Engine.TimeScale -= .5;
                GD.Print($"TimeScale: {Godot.Engine.TimeScale}");
                Godot.Engine.PhysicsTicksPerSecond -= 5;
                ConsumeInput();
            }
        }
        else if (@event is InputEventKey keyUpEvent && !keyUpEvent.Pressed)
        {
            if (
                keyUpEvent.Keycode == Key.W
                || keyUpEvent.Keycode == Key.S
                || keyUpEvent.Keycode == Key.A
                || keyUpEvent.Keycode == Key.D
            )
            {
                if (keyUpEvent.Keycode == Key.W)
                    _moveDirection.Z += -_moveDirection.Z;
                else if (keyUpEvent.Keycode == Key.S)
                    _moveDirection.Z += -_moveDirection.Z;
                if (keyUpEvent.Keycode == Key.A)
                    _moveDirection.X += -_moveDirection.X;
                else if (keyUpEvent.Keycode == Key.D)
                    _moveDirection.X += -_moveDirection.X;
                _moveDirection = _moveDirection.Normalized();
                EmitSignal(SignalName.Move, _moveDirection);
                ConsumeInput();
            }
            if (keyUpEvent.Keycode == Key.Space || keyUpEvent.Keycode == Key.Ctrl)
            {
                if (keyUpEvent.Keycode == Key.Space)
                    _verticalMovement.Y -= 1;
                else if (keyUpEvent.Keycode == Key.Ctrl)
                    _verticalMovement.Y += 1;
                _verticalMovement = _verticalMovement.Normalized();
                EmitSignal(SignalName.VerticalMove, _verticalMovement);
                ConsumeInput();
            }
            if (keyUpEvent.Keycode == Key.Q || keyUpEvent.Keycode == Key.E)
            {
                if (keyUpEvent.Keycode == Key.Q)
                    _rotation += 1;
                else if (keyUpEvent.Keycode == Key.E)
                    _rotation -= 1;
                EmitSignal(SignalName.RotateAxis, _rotation);
                ConsumeInput();
            }
            if (keyUpEvent.Keycode == Key.Shift)
            {
                EmitSignal(SignalName.Accelerate, false);
                ConsumeInput();
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
#if DEBUG
        if (Debug.DebugMenu.Instance?.IsVisible == true)
            return;
#endif

        if (
            @event is InputEventMouseMotion mouseMotion
            && Input.GetMouseMode() != Input.MouseModeEnum.Visible
        )
        {
            EmitSignal(SignalName.CameraLook, mouseMotion.Relative);
            GetViewport().SetInputAsHandled();
        }
    }

    private void ConsumeInput()
    {
        GetViewport().SetInputAsHandled();
    }
}
