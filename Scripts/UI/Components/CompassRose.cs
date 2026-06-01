using Godot;

namespace UI.Components;

/// <summary>
/// Antique compass rose rendered via _Draw. Single-axis spin via RotationRadians:
/// 0 = N up, positive rotation clockwise. Callers feed the screen-space angle
/// of the planet's north pole each frame.
/// </summary>
[Tool]
public partial class CompassRose : Control
{
    [Export]
    public float RotationRadians
    {
        get => _rotationRadians;
        set
        {
            if (Mathf.IsEqualApprox(_rotationRadians, value))
                return;
            _rotationRadians = value;
            QueueRedraw();
        }
    }
    private float _rotationRadians;

    [Export]
    public Color InkColor { get; set; } = new Color(0.165f, 0.145f, 0.125f);

    [Export]
    public Color PaperColor { get; set; } = new Color(0.957f, 0.941f, 0.902f, 0.78f);

    [Export]
    public Font? CardinalFont { get; set; }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        if (size.X <= 0f || size.Y <= 0f)
            return;

        Vector2 center = size * 0.5f;
        float r = Mathf.Min(size.X, size.Y) * 0.46f;

        // Outer disc + dashed inner ring (drawn unrotated)
        DrawCircle(center, r, PaperColor);
        DrawArc(center, r, 0f, Mathf.Tau, 64, InkColor, 1.2f);
        DrawDashedRing(center, r - 4f, InkColor, 0.6f, 14);

        // Rotated geometry: blades + cardinals
        Transform2D xform = Transform2D.Identity
            .Translated(center)
            .Rotated(_rotationRadians)
            .Translated(-center);
        DrawSetTransformMatrix(xform);

        // N-S blade (filled ink, points up)
        float blade = r * 0.92f;
        float waist = r * 0.18f;
        Vector2[] nsBlade = new[]
        {
            new Vector2(center.X, center.Y - blade),
            new Vector2(center.X + waist, center.Y),
            new Vector2(center.X, center.Y + blade),
            new Vector2(center.X - waist, center.Y),
        };
        Color bladeColor = InkColor;
        bladeColor.A *= 0.88f;
        DrawColoredPolygon(nsBlade, bladeColor);

        // E-W blade (outline only)
        Vector2[] ewBlade = new[]
        {
            new Vector2(center.X - blade, center.Y),
            new Vector2(center.X, center.Y - waist),
            new Vector2(center.X + blade, center.Y),
            new Vector2(center.X, center.Y + waist),
            new Vector2(center.X - blade, center.Y),
        };
        DrawPolyline(ewBlade, InkColor, 1.2f, true);

        // Inner hub
        DrawCircle(center, r * 0.10f, PaperColor);
        DrawArc(center, r * 0.10f, 0f, Mathf.Tau, 32, InkColor, 1.0f);
        DrawCircle(center, r * 0.04f, InkColor);

        // Cardinals
        if (CardinalFont != null)
        {
            float fontSize = Mathf.Max(8, r * 0.18f);
            DrawCardinal("N", new Vector2(center.X, center.Y - r + fontSize * 0.2f), fontSize);
            DrawCardinal("S", new Vector2(center.X, center.Y + r - fontSize * 0.6f), fontSize);
            DrawCardinal("W", new Vector2(center.X - r + fontSize * 0.55f, center.Y + fontSize * 0.35f), fontSize);
            DrawCardinal("E", new Vector2(center.X + r - fontSize * 0.55f, center.Y + fontSize * 0.35f), fontSize);
        }

        DrawSetTransformMatrix(Transform2D.Identity);
    }

    private void DrawCardinal(string letter, Vector2 pos, float fontSize)
    {
        if (CardinalFont == null)
            return;
        Vector2 textSize = CardinalFont.GetStringSize(letter, HorizontalAlignment.Center, -1, (int)fontSize);
        DrawString(
            CardinalFont,
            new Vector2(pos.X - textSize.X * 0.5f, pos.Y),
            letter,
            HorizontalAlignment.Center,
            -1,
            (int)fontSize,
            InkColor
        );
    }

    private void DrawDashedRing(Vector2 center, float radius, Color color, float width, int dashes)
    {
        float step = Mathf.Tau / dashes;
        for (int i = 0; i < dashes; i++)
        {
            float a0 = i * step;
            float a1 = a0 + step * 0.5f;
            DrawArc(center, radius, a0, a1, 8, color, width);
        }
    }
}
