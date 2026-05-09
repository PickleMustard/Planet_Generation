using Godot;

namespace UI.Wireframe;

[GlobalClass]
public partial class StateDot : Control
{
    public enum DotState { Run, Idle, Block }

    private DotState _state = DotState.Idle;
    [Export]
    public DotState State
    {
        get => _state;
        set { _state = value; QueueRedraw(); }
    }

    [Export] public float Radius { get; set; } = 4.5f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Radius * 2f + 2f, Radius * 2f + 2f);
    }

    public override void _Draw()
    {
        Color c = _state switch
        {
            DotState.Run => WireColors.Green,
            DotState.Block => WireColors.Red,
            _ => WireColors.InkFaint,
        };
        DrawCircle(Size * 0.5f, Radius, c);
    }
}
