using Constructables;
using Godot;
using Godot.Collections;
using PlayerInteraction.Camera;
using ProceduralGeneration.PlanetGeneration;
using Registries;
using UI;
using UtilityLibrary;

public partial class PlayerController : Node3D
{
    [Export]
    public float MaxSpeed { get; set; } = 10.0f;

    [Export]
    public float Acceleration { get; set; } = 9.0f;

    [Export]
    public float DecelerationTime { get; set; } = .8f;

    [Export]
    public float CameraSensitivity { get; set; } = .1f;

    [Export]
    public float ShipRotationSpeed { get; set; } = 2.0f;

    [Export]
    public float CameraSnapSpeed { get; set; } = 5.0f;

    // Configured-in-editor sound effects (point each Clip at a res://Audio clip).
    [Export]
    public SoundEffect? BoostSfx { get; set; }

    [Export]
    public SoundEffect? CaptureSfx { get; set; }

    [Export]
    public SoundEffect? EngineSfx { get; set; }

    //Scene Objects
    private Node3D? _parent;
    private Node3D? _pointerNode;
    private Camera3D? _camera;
    private PlayerCameraController? _cameraController;
    private WorldInputController? _worldInput;
    private ShipMovement? _shipMovement;
    private ShipCaptureController? _captureController;

    //Local Variables
    private Quaternion _defaultCameraRotation;
    private Vector2 _mousePosition = Vector2.Zero;
    private Vector3 _movementDirection = Vector3.Zero;
    private Vector3 _verticalMovement = Vector3.Zero;

    [Export]
    public Vector3 currentVelocity = Vector3.Zero;

    private bool _isRightMousePressed = false;
    private float _decelerateFactor;
    private bool _engineActive;

    public override void _Ready()
    {
        _parent = GetParent() as Node3D;
        _camera = GetNode<Camera3D>("../Camera3D"); // Assuming camera is a child
        _cameraController = _camera as PlayerCameraController;
        _pointerNode = GetNode<Node3D>("../Camera3D/Pointer");
        _shipMovement = GetParent() as ShipMovement;
        _decelerateFactor = Mathf.Log(DecelerationTime);
        _captureController = new ShipCaptureController(GetTree());

        // Integrate movement AFTER all body positions update this frame (NBodyCoordinator runs at
        // -100; analytical bodies run at default 0). Otherwise the captured-frame sync delta would
        // read stale body positions.
        ProcessPriority = 50;

        Callable rayCastRequest = new Callable(this, "OnCastRay");
        SignalBus.Instance!.ConnectToSignal("RequestRayCast", rayCastRequest);

        CallDeferred(MethodName.WireWorldInputSignals);
    }

    private void WireWorldInputSignals()
    {
        _worldInput =
            GetTree().GetFirstNodeInGroup(WorldInputController.GroupName)
            as WorldInputController;
        if (_worldInput == null)
        {
            GD.PushWarning(
                "PlayerController: WorldInputController not found in group; input wiring skipped."
            );
            return;
        }

        _worldInput.Move += OnMove;
        _worldInput.Accelerate += OnAccelerate;
        _worldInput.VerticalMove += OnVerticalMove;
        _worldInput.CameraLook += OnCameraLook;
        _worldInput.IndependentRotatation += OnMakeCameraIndependent;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_parent == null || _camera == null)
            return;

        float deltaTime = (float)delta;

        // The ship accepts movement input only while it owns the camera (free-fly) AND no GUI
        // panel holds the input stack. While focused/transitioning the camera FSM owns the camera
        // transform; while a menu is open WorldInput is disabled. In BOTH cases we drop input —
        // but the passive physics below (velocity bleed-off + captured-body frame sync) MUST keep
        // running so the ship still decelerates and rides along with its body while a menu is open.
        bool freeFly = _cameraController == null || _cameraController.IsFreeFly;
        bool acceptInput = freeFly && (_worldInput?.IsActive ?? true);

        Vector3 movementDirection = acceptInput ? _movementDirection : Vector3.Zero;
        Vector3 verticalMovement = acceptInput ? _verticalMovement : Vector3.Zero;

        // Engine loop: start when the ship has thrust input, stop when it goes idle.
        bool wantEngine = movementDirection.Length() > 0 || verticalMovement.Length() > 0;
        if (wantEngine && !_engineActive)
        {
            _engineActive = true;
            AudioBus.Instance?.StartLoop(EngineSfx);
        }
        else if (!wantEngine && _engineActive)
        {
            _engineActive = false;
            AudioBus.Instance?.StopLoop(EngineSfx);
        }

        Vector3 worldDirection = _parent.Basis * movementDirection;
        Vector3 worldVertical = _parent.Basis * verticalMovement;
        // Strafe in any direction
        Vector3 accelerationVector = worldDirection * Acceleration;

        // Cap at max speed

        // Add vertical component to velocity
        if (movementDirection.Length() <= 0)
            currentVelocity = currentVelocity.MoveToward(Vector3.Zero, _decelerateFactor);
        else
        {
            currentVelocity += accelerationVector;
            currentVelocity += worldVertical * Acceleration;
            var magnitude = currentVelocity.Length();
            currentVelocity = currentVelocity.Normalized() * Mathf.Min(MaxSpeed, magnitude);
        }

        // Resolve orbital capture + no-entry collision. Returns one world-space delta applied to
        // the ship every frame (and the camera too while it's rigidly coupled in free-fly).
        var prevCaptured = _captureController?.CapturedBody;
        Vector3 finalDelta = _captureController != null
            ? _captureController.Resolve(_parent.GlobalPosition, ref currentVelocity, deltaTime)
            : currentVelocity * deltaTime;
        _parent.GlobalPosition = _parent.GlobalPosition + finalDelta;

        // One capture sound covers both entering a body's capture frame and hitting its no-entry shell.
        if (_captureController != null
            && ((prevCaptured == null && _captureController.CapturedBody != null)
                || _captureController.CollidedThisFrame))
        {
            AudioBus.Instance?.Play(CaptureSfx);
        }

        // While focused, the camera FSM owns the camera transform + look — don't fight it.
        // Only the ship moves (decel + body-follow); the camera stays under FSM control.
        if (freeFly)
        {
            _camera.GlobalPosition = _camera.GlobalPosition + finalDelta;
            UpdateCamera();
        }
    }

    private void OnCastRay()
    {
        var mousePos = GetViewport().GetMousePosition();
        Vector3 origin = _camera!.ProjectRayOrigin(mousePos);
        var direction = origin + _camera.ProjectRayNormal(mousePos) * 1000f;
        var query = PhysicsRayQueryParameters3D.Create(origin, direction);
        query.CollideWithAreas = true;
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0)
        {
            SignalBus.Instance!.EmitExportRaycastResult(new Dictionary());
            return;
        }

        var collider = (Node3D)result["collider"];

        var selectableBody = FindSelectableBody(collider);
        if (selectableBody == null)
        {
            // Check if we hit a logistics unit
            var logisticsUnit = FindLogisticsUnit(collider);
            if (logisticsUnit != null)
            {
                Dictionary logisticsResult = new Dictionary();
                logisticsResult["logistics_unit"] = (Node)logisticsUnit;
                SignalBus.Instance!.EmitExportRaycastResult(logisticsResult);
                return;
            }

            SignalBus.Instance!.EmitExportRaycastResult(new Dictionary());
            return;
        }

        var faceIndex = (int)result["face_index"];
        var selectionResult = selectableBody.GetFaceFromIndex(faceIndex);

        Dictionary exportResult = new Dictionary();
        exportResult["hit_result"] = result;
        exportResult["selectable_body"] = (Node3D)selectableBody;
        exportResult["cell"] = selectionResult!.Cell;
        exportResult["cell_continent"] = selectionResult.CellContinent!;

        SignalBus.Instance!.EmitExportRaycastResult(exportResult);
    }

    private static ISelectableBody? FindSelectableBody(Node node)
    {
        Node? current = node;
        while (current != null)
        {
            if (current is ISelectableBody body)
                return body;
            current = current.GetParentOrNull<Node>();
        }
        return null;
    }

    private static LogisticsUnit? FindLogisticsUnit(Node node)
    {
        Node? current = node;
        while (current != null)
        {
            if (current is LogisticsUnit unit)
                return unit;
            current = current.GetParentOrNull<Node>();
        }
        return null;
    }

    private void UpdateCamera()
    {
        if (_camera == null || _shipMovement == null)
            return;

        if (_mousePosition.LengthSquared() < 0.1f)
            return;
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
            AudioBus.Instance?.Play(BoostSfx);
        }
        else
        {
            Acceleration /= 2.0f; // Reset
        }
    }

    private void OnMakeCameraIndependent(bool isMouseButtonPressed)
    {
        if (_camera == null || _pointerNode == null)
            return;

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
