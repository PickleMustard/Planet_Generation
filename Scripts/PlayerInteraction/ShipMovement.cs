using Godot;
public partial class ShipMovement : Node3D
{

    [Export]
    public float ShipTurnSpeed { get; set; } = .2f;
    [Export]
    Node3D Target;

    private Node3D _parent;
    private Basis _startBasis { get; set; }
    private Basis _endBasis { get; set; }
    private Quaternion _startingRotation { get; set; }
    private Quaternion _desiredRotation { get; set; }
    private Quaternion _desiredPositionRotation { get; set; }


    public override void _Ready()
    {
        _parent = GetParent() as Node3D;
        _endBasis = this.Basis;
        GD.Print($"Starting rotation: {_startingRotation}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Target == null) return;

        var rotateFactor = 1f - Mathf.Pow(1 - ShipTurnSpeed, 3.45233f);
        var targetXForm = Target.GlobalTransform;
        var localTransformOnlyOrigin = new Transform3D(Basis.Identity, this.GlobalTransform.Origin);
        var localTransformOnlyBasis = new Transform3D(this.GlobalTransform.Basis, Vector3.Zero);

        localTransformOnlyBasis = localTransformOnlyBasis.InterpolateWith(targetXForm, (float)rotateFactor);
        this.Basis = localTransformOnlyBasis.Basis;
    }

    public void SetDesiredRotation(float yaw, float pitch)
    {
        _startBasis = this.Basis;
        _startingRotation = _startBasis.GetRotationQuaternion();
        Quaternion pitchRotation = new Quaternion(Vector3.Right, Mathf.DegToRad(pitch));
        Quaternion yawRotation = new Quaternion(Vector3.Up, Mathf.DegToRad(-yaw));
        //Order of multiplication matters here!!
        _desiredRotation = (yawRotation * _startingRotation) * pitchRotation;
        _endBasis = new Basis(_desiredRotation);
    }


}
