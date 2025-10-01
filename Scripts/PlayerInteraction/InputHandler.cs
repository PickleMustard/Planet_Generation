using Godot;

public partial class InputHandler : Node
{
    private bool _isMouseButtonPressed = false;
    private Vector3 _moveDirection = Vector3.Zero;
    private Vector3 _verticalMovement = Vector3.Zero;
    private float _rotation = 0.0f;

    [Signal]
    public delegate void MoveEventHandler(Vector3 direction);

    [Signal]
    public delegate void LookEventHandler(Vector2 mouseDelta);

    [Signal]
    public delegate void AccelerateEventHandler(bool accelerate);

    [Signal]
    public delegate void VerticalMoveEventHandler(float vertical);

    [Signal]
    public delegate void RotateAxisEventHandler(float rotation);

    [Signal]
    public delegate void CameraLookEventHandler(Vector2 mouseDelta);

    [Signal]
    public delegate void IndependentRotatationEventHandler(bool IsMouseButtonPressed);

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Right)
        {
            if (mouseEvent.Pressed)
            {
                _isMouseButtonPressed = true;
                EmitSignal(SignalName.IndependentRotatation, true);
            }
            else if (!mouseEvent.Pressed)
            {
                _isMouseButtonPressed = false;
                EmitSignal(SignalName.IndependentRotatation, false);
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            EmitSignal(SignalName.CameraLook, mouseMotion.Relative);
        }
        //Button Press
        else if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.W || keyEvent.Keycode == Key.S || keyEvent.Keycode == Key.A || keyEvent.Keycode == Key.D)
            {
                if (keyEvent.Keycode == Key.W) _moveDirection.Z -= 1;
                else if (keyEvent.Keycode == Key.S) _moveDirection.Z += 1;
                if (keyEvent.Keycode == Key.A) _moveDirection.X -= 1;
                else if (keyEvent.Keycode == Key.D) _moveDirection.X += 1;
                _moveDirection = _moveDirection.Normalized();
                EmitSignal(SignalName.Move, _moveDirection);
            }
            if (keyEvent.Keycode == Key.Space || keyEvent.Keycode == Key.Ctrl)
            {
                if (keyEvent.Keycode == Key.Space) _verticalMovement.Y += 1;
                else if (keyEvent.Keycode == Key.Ctrl) _verticalMovement.Y -= 1;
                _verticalMovement = _verticalMovement.Normalized();
                EmitSignal(SignalName.VerticalMove, _verticalMovement);
            }
            if (keyEvent.Keycode == Key.Q || keyEvent.Keycode == Key.E)
            {
                if (keyEvent.Keycode == Key.Q) _rotation -= 1;
                else if (keyEvent.Keycode == Key.E) _rotation += 1;
                EmitSignal(SignalName.RotateAxis, _rotation);
            }
            if (keyEvent.Keycode == Key.Shift)
            {
                EmitSignal(SignalName.Accelerate, true);
            }
        }
        //Button release
        else if (@event is InputEventKey keyUpEvent && !keyUpEvent.IsPressed())
        {
            if (keyUpEvent.Keycode == Key.W || keyUpEvent.Keycode == Key.S || keyUpEvent.Keycode == Key.A || keyUpEvent.Keycode == Key.D)
            {
                if (keyUpEvent.Keycode == Key.W) _moveDirection.Z += 1;
                else if (keyUpEvent.Keycode == Key.S) _moveDirection.Z -= 1;
                if (keyUpEvent.Keycode == Key.A) _moveDirection.X += 1;
                else if (keyUpEvent.Keycode == Key.D) _moveDirection.X -= 1;
                _moveDirection = _moveDirection.Normalized();
                EmitSignal(SignalName.Move, _moveDirection);
            }
            if (keyUpEvent.Keycode == Key.Space || keyUpEvent.Keycode == Key.Ctrl)
            {
                if (keyUpEvent.Keycode == Key.Space) _verticalMovement.Y -= 1;
                else if (keyUpEvent.Keycode == Key.Ctrl) _verticalMovement.Y += 1;
                _verticalMovement = _verticalMovement.Normalized();
                EmitSignal(SignalName.VerticalMove, _verticalMovement);
            }
            if (keyUpEvent.Keycode == Key.Q || keyUpEvent.Keycode == Key.E)
            {
                if (keyUpEvent.Keycode == Key.Q) _rotation += 1;
                else if (keyUpEvent.Keycode == Key.E) _rotation -= 1;
                EmitSignal(SignalName.RotateAxis, _rotation);
            }
            if (keyUpEvent.Keycode == Key.Shift)
            {
                EmitSignal(SignalName.Accelerate, false);
            }

        }
    }
}
