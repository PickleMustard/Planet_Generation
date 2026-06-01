using Godot;

namespace UI.Components;

/// <summary>
/// Paper cartouche control with one chamfered corner. The shape can't be done
/// with a StyleBoxFlat, so the panel paints its own polygon (fill + drop shadow
/// + stroke) and lays children out in a Control's full client rect. Use a child
/// MarginContainer for inset content.
/// </summary>
[Tool]
public partial class ChamferedPanel : Control
{
    public enum CornerSpec { TL, TR, BR, BL }

    [Export]
    public CornerSpec ChamferCorner
    {
        get => _chamferCorner;
        set
        {
            _chamferCorner = value;
            QueueRedraw();
        }
    }
    private CornerSpec _chamferCorner = CornerSpec.BR;

    [Export]
    public int ChamferPx
    {
        get => _chamferPx;
        set
        {
            _chamferPx = Mathf.Max(0, value);
            QueueRedraw();
        }
    }
    private int _chamferPx = 18;

    [Export]
    public Color FillColor
    {
        get => _fillColor;
        set
        {
            _fillColor = value;
            QueueRedraw();
        }
    }
    private Color _fillColor = new Color(0.957f, 0.941f, 0.902f);

    [Export]
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            QueueRedraw();
        }
    }
    private Color _borderColor = new Color(0.165f, 0.145f, 0.125f);

    [Export]
    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = Mathf.Max(0f, value);
            QueueRedraw();
        }
    }
    private float _borderWidth = 1.5f;

    [Export]
    public Vector2 ShadowOffset
    {
        get => _shadowOffset;
        set
        {
            _shadowOffset = value;
            QueueRedraw();
        }
    }
    private Vector2 _shadowOffset = new Vector2(2, 3);

    [Export]
    public Color ShadowColor
    {
        get => _shadowColor;
        set
        {
            _shadowColor = value;
            QueueRedraw();
        }
    }
    private Color _shadowColor = new Color(0.165f, 0.145f, 0.125f, 0.18f);

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

        Vector2[] verts = BuildVerts(size);

        // Shadow
        Vector2[] shadowVerts = new Vector2[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            shadowVerts[i] = verts[i] + _shadowOffset;
        DrawColoredPolygon(shadowVerts, _shadowColor);

        // Fill
        DrawColoredPolygon(verts, _fillColor);

        // Stroke (closed polyline)
        if (_borderWidth > 0f)
        {
            Vector2[] closed = new Vector2[verts.Length + 1];
            for (int i = 0; i < verts.Length; i++)
                closed[i] = verts[i];
            closed[verts.Length] = verts[0];
            DrawPolyline(closed, _borderColor, _borderWidth, true);
        }
    }

    private Vector2[] BuildVerts(Vector2 s)
    {
        float c = Mathf.Min(_chamferPx, Mathf.Min(s.X, s.Y) * 0.49f);
        return _chamferCorner switch
        {
            CornerSpec.TR => new[]
            {
                new Vector2(0, 0),
                new Vector2(s.X - c, 0),
                new Vector2(s.X, c),
                new Vector2(s.X, s.Y),
                new Vector2(0, s.Y),
            },
            CornerSpec.BR => new[]
            {
                new Vector2(0, 0),
                new Vector2(s.X, 0),
                new Vector2(s.X, s.Y - c),
                new Vector2(s.X - c, s.Y),
                new Vector2(0, s.Y),
            },
            CornerSpec.TL => new[]
            {
                new Vector2(c, 0),
                new Vector2(s.X, 0),
                new Vector2(s.X, s.Y),
                new Vector2(0, s.Y),
                new Vector2(0, c),
            },
            CornerSpec.BL => new[]
            {
                new Vector2(0, 0),
                new Vector2(s.X, 0),
                new Vector2(s.X, s.Y),
                new Vector2(c, s.Y),
                new Vector2(0, s.Y - c),
            },
            _ => new[]
            {
                new Vector2(0, 0),
                new Vector2(s.X, 0),
                new Vector2(s.X, s.Y),
                new Vector2(0, s.Y),
            },
        };
    }
}
