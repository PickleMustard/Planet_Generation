using Godot;

namespace UI.Wireframe;

/// <summary>
/// Thin Control that draws a perforated edge — a row of light-colored holes
/// punched through ink. Pin to the top of a slip card to evoke a tear-off slip.
/// </summary>
[GlobalClass]
public partial class PerforatedEdge : Control
{
    [Export] public float HoleSpacing { get; set; } = 8f;
    [Export] public float HoleRadius { get; set; } = 1.4f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0f, 6f);
    }

    public override void _Draw()
    {
        float cy = Size.Y * 0.5f;
        for (float x = HoleSpacing * 0.5f; x < Size.X; x += HoleSpacing)
        {
            DrawCircle(new Vector2(x, cy), HoleRadius, WireColors.Paper);
        }
    }
}
