using Godot;

public partial class PlayerController : Node3D
{
    [Export] public float MaxSpeed { get; set; } = 10.0f;
    [Export] public float Acceleration { get; set; } = 9.0f;
    [Export] public float DecelerationTime { get; set; } = .8f;
    [Export] public float CameraSensitivity { get; set; } = .1f;
    [Export] public float ShipRotationSpeed { get; set; } = 2.0f;
    [Export] public float CameraSnapSpeed { get; set; } = 5.0f;

    //Scene Objects
    private Node3D _parent;
    private Node3D _pointerNode;
    private Camera3D _camera;
    private InputHandler _inputHandler;
    private ShipMovement _shipMovement;

    //Local Variables
    private Quaternion _defaultCameraRotation;
    private Vector2 _mousePosition = Vector2.Zero;
    private Vector3 _movementDirection = Vector3.Zero;
    private Vector3 _verticalMovement = Vector3.Zero;

    [Export]
    public Vector3 currentVelocity = Vector3.Zero;

    private bool _isRightMousePressed = false;
    private float _decelerateFactor;

    public override void _Ready()
    {
        _inputHandler = GetNode<InputHandler>("../InputHandler"); // Assuming InputHandler is a sibling
        _parent = GetParent() as Node3D;
        _camera = GetNode<Camera3D>("../Camera3D"); // Assuming camera is a child
        _pointerNode = GetNode<Node3D>("../Camera3D/Pointer");
        _shipMovement = GetParent() as ShipMovement;
        _decelerateFactor = Mathf.Log(DecelerationTime);

        if (_inputHandler != null)
        {
            _inputHandler.Move += OnMove;
            _inputHandler.Accelerate += OnAccelerate;
            _inputHandler.VerticalMove += OnVerticalMove;
            _inputHandler.CameraLook += OnCameraLook;
            _inputHandler.IndependentRotatation += OnMakeCameraIndependent;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaTime = (float)delta;
        Vector3 worldDirection = _parent.Basis * _movementDirection;
        Vector3 worldVertical = _parent.Basis * _verticalMovement;
        // Strafe in any direction
        Vector3 accelerationVector = worldDirection * Acceleration;

        // Cap at max speed

        // Add vertical component to velocity
        if (_movementDirection.Length() <= 0)
            currentVelocity = currentVelocity.MoveToward(Vector3.Zero, _decelerateFactor);
        else
        {
            currentVelocity += accelerationVector;
            currentVelocity += worldVertical * Acceleration;
            var magnitude = currentVelocity.Length();
            currentVelocity = currentVelocity.Normalized() * Mathf.Min(MaxSpeed, magnitude);
        }

        // Set velocity for movement
        _parent.GlobalPosition = _parent.GlobalPosition + currentVelocity * deltaTime;
        _camera.GlobalPosition = _camera.GlobalPosition + currentVelocity * deltaTime;

        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (_mousePosition.LengthSquared() < 0.1f) return;
        _mousePosition *= CameraSensitivity;
        var yaw = _mousePosition.X;
        var pitch = _mousePosition.Y;
        _mousePosition = Vector2.Zero;

        if (_isRightMousePressed)
        {
            // Camera rotation: pitch around local right, yaw around world up
            Quaternion cameraRotation = _camera.Basis.GetRotationQuaternion();
            Quaternion pitchRotation = new Quaternion(Basis.X.Normalized(), Mathf.DegToRad(pitch));
            Quaternion yawRotation = new Quaternion(Basis.Z.Normalized(), Mathf.DegToRad(-yaw));
            cameraRotation = yawRotation * cameraRotation * pitchRotation;
            _camera.Basis = new Basis(cameraRotation);
        }
        else
        {
            // Ship rotation: pitch around local right, yaw around local up
            Quaternion cameraRotation = _camera.Basis.GetRotationQuaternion();
            Quaternion pitchRotation = new Quaternion(Basis.X.Normalized(), Mathf.DegToRad(pitch));
            Quaternion yawRotation = new Quaternion(Basis.Z.Normalized(), Mathf.DegToRad(-yaw));
            cameraRotation = yawRotation * cameraRotation * pitchRotation;
            _camera.Basis = new Basis(cameraRotation);
            _shipMovement.SetDesiredRotation(yaw, pitch);
        }
    }

    private void OnMove(Vector3 direction)
    {
        _movementDirection = direction;
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
            Acceleration /= 2.0f; // Reset
        }
    }

    private void OnMakeCameraIndependent(bool isMouseButtonPressed)
    {
        if (isMouseButtonPressed)
        {
            _defaultCameraRotation = _camera.Quaternion;
            _pointerNode.TopLevel = true;
        }
        else
        {
            _camera.Quaternion = _defaultCameraRotation;
            _pointerNode.TopLevel = false;
        }
        _isRightMousePressed = isMouseButtonPressed;
    }

    private void OnVerticalMove(float vertical)
    {
        _verticalMovement = new Vector3(0, vertical, 0);
    }

    private void OnCameraLook(Vector2 mouseDelta)
    {
        _mousePosition = mouseDelta;
    }
}
