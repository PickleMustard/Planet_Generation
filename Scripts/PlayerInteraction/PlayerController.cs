using Godot;

public partial class PlayerController : Node3D
{
    [Export] public float MaxSpeed { get; set; } = 10.0f;
    [Export] public float Acceleration { get; set; } = 5.0f;
    [Export] public float DecelerationTime { get; set; } = 2.0f;

    private InputHandler _inputHandler;
    private Vector3 _currentVelocity = Vector3.Zero;
    private float _currentRotation = 0.0f;
    private Node3D _parent;

    public override void _Ready()
    {
        _inputHandler = GetNode<InputHandler>("../InputHandler"); // Assuming InputHandler is a sibling
        _parent = GetParent<Node3D>();

        if (_inputHandler != null)
        {
            _inputHandler.Move += OnMove;
            _inputHandler.Look += OnLook;
            _inputHandler.Accelerate += OnAccelerate;
            _inputHandler.VerticalMove += OnVerticalMove;
            _inputHandler.RotateAxis += OnRotateAxis;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaTime = (float)delta;

        // Apply deceleration if no input
        if (_currentVelocity.Length() > 0)
        {
            float deceleration = MaxSpeed / DecelerationTime;
            _currentVelocity = _currentVelocity.MoveToward(Vector3.Zero, deceleration * deltaTime);
        }

        // Set velocity for movement
        _parent.GlobalPosition = _parent.GlobalPosition + _currentVelocity * (float)delta;

        // Apply rotation
        RotateY(_currentRotation * deltaTime);
    }

    private void OnMove(Vector3 direction)
    {
        // Strafe in any direction
        Vector3 accelerationVector = direction * Acceleration;
        _currentVelocity += accelerationVector;

        // Cap at max speed
        if (_currentVelocity.Length() > MaxSpeed)
        {
            _currentVelocity = _currentVelocity.Normalized() * MaxSpeed;
        }
    }

    private void OnLook(Vector2 mouseDelta)
    {
        // Rotate based on mouse movement (adjust sensitivity as needed)
        float sensitivity = 0.1f;
        RotateY(-mouseDelta.X * sensitivity);
        RotateX(-mouseDelta.Y * sensitivity);
    }

    private void OnAccelerate(bool accelerate)
    {
        // Modify acceleration if needed (e.g., boost)
        if (accelerate)
        {
            Acceleration *= 2.0f; // Example boost
        }
        else
        {
            Acceleration = MaxSpeed / 2.0f; // Reset
        }
    }

    private void OnVerticalMove(float vertical)
    {
        // Add vertical component to velocity
        _currentVelocity.Y += vertical * Acceleration;
        if (_currentVelocity.Y > MaxSpeed) _currentVelocity.Y = MaxSpeed;
        if (_currentVelocity.Y < -MaxSpeed) _currentVelocity.Y = -MaxSpeed;
    }

    private void OnRotateAxis(float rotation)
    {
        // Rotate around Y-axis
        _currentRotation = rotation * Mathf.Pi; // Adjust speed
    }
}
