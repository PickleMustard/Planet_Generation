using Godot;

namespace UI.Wireframe;

/// <summary>
/// Control that draws a paper-grain background with optional horizontal ruled
/// lines. Used inside slip cards and panel shells to evoke ledger paper.
/// </summary>
[GlobalClass]
public partial class PaperBackground : Control
{
    [Export] public bool DrawRuledLines { get; set; } = true;
    [Export] public float RuleSpacing { get; set; } = 24f;
    [Export] public bool DrawDotGrain { get; set; } = false;
    [Export] public float DotGrainSpacing { get; set; } = 14f;
    [Export] public Color BaseColor { get; set; } = WireColors.Paper;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, BaseColor, filled: true);

        if (DrawRuledLines && RuleSpacing > 0f)
        {
            for (float y = RuleSpacing; y < Size.Y; y += RuleSpacing)
            {
                DrawLine(new Vector2(0f, y), new Vector2(Size.X, y), WireColors.InkLine, 1f);
            }
        }

        if (DrawDotGrain && DotGrainSpacing > 0f)
        {
            var dotColor = new Color(WireColors.Ink, 0.05f);
            for (float y = DotGrainSpacing * 0.5f; y < Size.Y; y += DotGrainSpacing)
            {
                for (float x = DotGrainSpacing * 0.5f; x < Size.X; x += DotGrainSpacing)
                {
                    DrawCircle(new Vector2(x, y), 1f, dotColor);
                }
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }
}
