using Godot;
public partial class ShipMovement : Node3D
{
    [Export]
    public float MaxSpeed { get; set; } = 10.0f;
    [Export]
    public float Acceleration { get; set; } = 5.0f;
    [Export]
    public float DecelerationTime { get; set; } = 2.0f;
    [Export]
    public float ShipTurnSpeed { get; set; } = .2f;

    private Node3D _parent;
    private Basis _startBasis { get; set; }
    private Quaternion _startingRotation { get; set; }
    private Quaternion _desiredRotation { get; set; }
    private Quaternion _desiredPositionRotation { get; set; }
    private float _rotationWeight = 0.0f;
    private float _rotationDifference;

    public override void _Ready()
    {
        _parent = GetParent() as Node3D;
        _startingRotation = this.Basis.GetRotationQuaternion();
        _startBasis = this.Basis;
        GD.Print($"Starting rotation: {_startingRotation}");
    }

    public override void _PhysicsProcess(double delta)
    {
        GD.Print($"Rotation Difference: {_rotationDifference} | Rotation Weight: {_rotationWeight}");
        if (_rotationWeight < _rotationDifference)
        {
            this.Basis = _startBasis.Slerp(_startBasis * new Basis(_desiredRotation), _rotationWeight);
            GD.Print($"Rotating, {_startingRotation.Slerpni(_desiredRotation, _rotationWeight)}");
            _rotationWeight += (float)delta * ShipTurnSpeed;
        }


    }

    public void SetDesiredRotation(float yaw, float pitch)
    {
        _startBasis = this.Basis;
        _startingRotation = _startBasis.GetRotationQuaternion();
        Quaternion pitchRotation = new Quaternion(_parent.Basis.X.Normalized(), Mathf.DegToRad(-pitch));
        Quaternion yawRotation = new Quaternion(_parent.Basis.Y.Normalized(), Mathf.DegToRad(-yaw));
        _desiredRotation = yawRotation * pitchRotation;
        _rotationDifference = _startingRotation.AngleTo(_desiredRotation);
        Basis endBasis = _startBasis * new Basis(_desiredRotation);
        if (_rotationDifference < .0001f)
            _rotationWeight = 0.0f;
        else
            _rotationWeight = _rotationDifference;
        GD.Print($"Rotation Difference: {_rotationDifference}");
    }


}
