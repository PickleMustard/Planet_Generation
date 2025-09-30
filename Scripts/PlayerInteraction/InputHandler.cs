using Godot;

public partial class InputHandler : Node
{
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

    private Vector2 _lastMousePosition;

    public override void _Ready()
    {
        _lastMousePosition = GetViewport().GetMousePosition();
    }

    public override void _Process(double delta)
    {
        // Movement input (WASD)
        Vector3 moveDirection = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) moveDirection.Z -= 1;
        if (Input.IsKeyPressed(Key.S)) moveDirection.Z += 1;
        if (Input.IsKeyPressed(Key.A)) moveDirection.X -= 1;
        if (Input.IsKeyPressed(Key.D)) moveDirection.X += 1;
        moveDirection = moveDirection.Normalized();
        if (moveDirection != Vector3.Zero)
        {
            EmitSignal(SignalName.Move, moveDirection);
        }

        // Mouse look
        Vector2 currentMousePosition = GetViewport().GetMousePosition();
        Vector2 mouseDelta = currentMousePosition - _lastMousePosition;
        _lastMousePosition = currentMousePosition;
        if (mouseDelta != Vector2.Zero)
        {
            EmitSignal(SignalName.Look, mouseDelta);
        }

        // Acceleration (Shift)
        bool accelerate = Input.IsKeyPressed(Key.Shift);
        EmitSignal(SignalName.Accelerate, accelerate);

        // Vertical movement (Space/Ctrl)
        float vertical = 0.0f;
        if (Input.IsKeyPressed(Key.Space)) vertical += 1;
        if (Input.IsKeyPressed(Key.Ctrl)) vertical -= 1;
        if (vertical != 0.0f)
        {
            EmitSignal(SignalName.VerticalMove, vertical);
        }

        // Rotation around camera axis (Q/E)
        float rotation = 0.0f;
        if (Input.IsKeyPressed(Key.Q)) rotation -= 1;
        if (Input.IsKeyPressed(Key.E)) rotation += 1;
        if (rotation != 0.0f)
        {
            EmitSignal(SignalName.RotateAxis, rotation);
        }
    }
}
