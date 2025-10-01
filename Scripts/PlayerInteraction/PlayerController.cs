using Godot;

public partial class PlayerController : Node3D
{
    [Export] public float MaxSpeed { get; set; } = 10.0f;
    [Export] public float Acceleration { get; set; } = 5.0f;
    [Export] public float DecelerationTime { get; set; } = 2.0f;
    [Export] public float CameraSensitivity { get; set; } = .1f;
    [Export] public float ShipRotationSpeed { get; set; } = 2.0f;
    [Export] public float CameraSnapSpeed { get; set; } = 5.0f;

    private InputHandler _inputHandler;
    private ShipMovement _shipMovement;
    private Vector3 _currentVelocity = Vector3.Zero;
    private float _currentRotation = 0.0f;
    private Node3D _parent;
    private Camera3D _camera;
    private Quaternion _defaultCameraRotation;
    private Vector3 _defaultCameraOffset;
    private float _totalPitch = 0.0f;
    private Vector2 _mousePosition = Vector2.Zero;
    private Vector3 _currentCameraRotation;
    private bool _isRightMousePressed = false;

    public override void _Ready()
    {
        _inputHandler = GetNode<InputHandler>("../InputHandler"); // Assuming InputHandler is a sibling
        _parent = GetParent<Node3D>();
        _camera = GetNode<Camera3D>("../Camera3D"); // Assuming camera is a child
        _shipMovement = GetParent() as ShipMovement;
        _defaultCameraOffset = _camera.Position;
        _currentCameraRotation = Vector3.Zero;

        if (_inputHandler != null)
        {
            _inputHandler.Move += OnMove;
            _inputHandler.Accelerate += OnAccelerate;
            _inputHandler.VerticalMove += OnVerticalMove;
            _inputHandler.RotateAxis += OnRotateAxis;
            _inputHandler.CameraLook += OnCameraLook;
            _inputHandler.IndependentRotatation += OnMakeCameraIndependent;
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
        //_parent.GlobalPosition = _parent.GlobalPosition + _currentVelocity * deltaTime;

        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (_mousePosition.LengthSquared() < 0.1f) return;
        _mousePosition *= CameraSensitivity;
        var yaw = _mousePosition.X;
        var pitch = _mousePosition.Y;
        _mousePosition = Vector2.Zero;

        _totalPitch += pitch;

        if (_isRightMousePressed)
        {
            // Camera rotation: pitch around local right, yaw around world up
            Quaternion cameraRotation = _camera.Basis.GetRotationQuaternion();
            Quaternion pitchRotation = new Quaternion(GlobalBasis.X.Normalized(), Mathf.DegToRad(-pitch));
            Quaternion yawRotation = new Quaternion(Vector3.Up, Mathf.DegToRad(-yaw));
            cameraRotation = yawRotation * cameraRotation * pitchRotation;
            _camera.Basis = new Basis(cameraRotation);
        }
        else
        {
            // Ship rotation: pitch around local right, yaw around local up
            Quaternion cameraRotation = _camera.Basis.GetRotationQuaternion();
            Quaternion pitchRotation = new Quaternion(GlobalBasis.X.Normalized(), Mathf.DegToRad(-pitch));
            Quaternion yawRotation = new Quaternion(Vector3.Up, Mathf.DegToRad(-yaw));
            cameraRotation = yawRotation * cameraRotation * pitchRotation;
            _camera.Basis = new Basis(cameraRotation);
            _shipMovement.SetDesiredRotation(yaw, pitch);
        }
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

    private void OnMakeCameraIndependent(bool isMouseButtonPressed)
    {
        if (isMouseButtonPressed)
        {
            _defaultCameraRotation = _camera.Quaternion;
        }
        else
        {
            GD.Print($"{_defaultCameraRotation}");
            _camera.Quaternion = _defaultCameraRotation;
        }
        _isRightMousePressed = isMouseButtonPressed;
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

    private void OnCameraLook(Vector2 mouseDelta)
    {
        _mousePosition = mouseDelta;
    }
}
