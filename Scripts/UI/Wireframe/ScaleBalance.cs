using Godot;

namespace UI.Wireframe;

/// <summary>
/// Old-fashioned balance scale rendered with vector primitives. The beam tilts
/// according to <see cref="Load"/> vs <see cref="Limit"/>; an "OVER" stamp
/// appears when load exceeds limit.
/// </summary>
[GlobalClass]
public partial class ScaleBalance : Control
{
    private float _load;
    private float _limit = 1200f;

    [Export]
    public float Load
    {
        get => _load;
        set { _load = value; QueueRedraw(); }
    }

    [Export]
    public float Limit
    {
        get => _limit;
        set { _limit = Mathf.Max(1f, value); QueueRedraw(); }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        if (Size.X < 8f || Size.Y < 8f) return;

        float frac = Mathf.Clamp(_load / _limit, 0f, 1.05f);
        float tiltDeg = (frac - 1f) * 12f;
        bool over = _load > _limit;
        float cx = Size.X * 0.5f;
        float beamY = Size.Y * 0.42f;
        float armLen = Mathf.Min(Size.X * 0.42f, Size.Y * 0.95f);
        float panRadius = Mathf.Min(armLen * 0.24f, 26f);
        float sin = Mathf.Sin(Mathf.DegToRad(tiltDeg));
        float cos = Mathf.Cos(Mathf.DegToRad(tiltDeg));
        var pivot = new Vector2(cx, beamY);
        var lEnd = pivot + new Vector2(-armLen * cos, armLen * sin);
        var rEnd = pivot + new Vector2(armLen * cos, -armLen * sin);

        DrawLine(new Vector2(cx - 40f, Size.Y - 8f), new Vector2(cx + 40f, Size.Y - 8f), WireColors.Ink, 2f);
        DrawLine(new Vector2(cx - 30f, Size.Y - 14f), new Vector2(cx + 30f, Size.Y - 14f), new Color(WireColors.Ink, 0.5f), 1f);

        DrawLine(new Vector2(cx, beamY + 10f), new Vector2(cx, Size.Y - 8f), WireColors.Ink, 2f);

        Vector2[] colTri =
        [
            new(cx - 8f, beamY + 10f),
            new(cx + 8f, beamY + 10f),
            new(cx + 5f, beamY + 18f),
            new(cx - 5f, beamY + 18f),
        ];
        DrawColoredPolygon(colTri, WireColors.Ink);

        DrawCircle(pivot, 4f, WireColors.Paper);
        DrawArc(pivot, 4f, 0f, Mathf.Tau, 24, WireColors.Ink, 1.5f);

        DrawLine(lEnd, rEnd, WireColors.Ink, 2.5f);

        DrawLine(lEnd, lEnd + new Vector2(-12f, 18f), WireColors.Ink, 1f);
        DrawLine(lEnd, lEnd + new Vector2(12f, 18f), WireColors.Ink, 1f);
        DrawLine(rEnd, rEnd + new Vector2(-12f, 18f), WireColors.Ink, 1f);
        DrawLine(rEnd, rEnd + new Vector2(12f, 18f), WireColors.Ink, 1f);

        var lPan = lEnd + new Vector2(0f, 22f);
        var rPan = rEnd + new Vector2(0f, 22f);
        DrawArc(lPan, panRadius, 0f, Mathf.Pi, 16, WireColors.Ink, 1.5f);
        DrawArc(rPan, panRadius, 0f, Mathf.Pi, 16, WireColors.Ink, 1.5f);

        if (over)
        {
            var stamp = new Rect2(Size.X - 56f, 4f, 50f, 18f);
            DrawRect(stamp, new Color(1f, 1f, 1f, 0.6f), filled: true);
            DrawRect(stamp, WireColors.Red, filled: false, width: 1.5f);
        }
    }
}
