using Godot;

namespace UI.Components;

/// <summary>
/// Screen-space reticle: short orange vertical tick at the top and bottom of
/// the viewport, anchored on a configurable horizontal center. Stays fixed in
/// 2D regardless of camera orientation. Pointer-transparent so it never
/// captures clicks intended for the camera.
/// </summary>
[Tool]
public partial class ReticleOverlay : Control
{
    [Export]
    public Color TickColor
    {
        get => _tickColor;
        set { _tickColor = value; QueueRedraw(); }
    }
    private Color _tickColor = new Color(0.769f, 0.329f, 0.102f, 0.85f);

    [Export]
    public float TickLength
    {
        get => _tickLength;
        set { _tickLength = Mathf.Max(2f, value); QueueRedraw(); }
    }
    private float _tickLength = 28f;

    [Export]
    public float TickWidth
    {
        get => _tickWidth;
        set { _tickWidth = Mathf.Max(0.5f, value); QueueRedraw(); }
    }
    private float _tickWidth = 1.5f;

    /// <summary>
    /// Horizontal offset from the center of this Control's rect. Use this to
    /// align the ticks with the planet's negative-space center when the camera
    /// view is offset by surrounding UI.
    /// </summary>
    [Export]
    public float CenterOffsetX
    {
        get => _centerOffsetX;
        set { _centerOffsetX = value; QueueRedraw(); }
    }
    private float _centerOffsetX;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

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

        float x = size.X * 0.5f + _centerOffsetX;
        // Top tick
        DrawLine(new Vector2(x, 0f), new Vector2(x, _tickLength), _tickColor, _tickWidth);
        // Bottom tick
        DrawLine(new Vector2(x, size.Y - _tickLength), new Vector2(x, size.Y), _tickColor, _tickWidth);
    }
}
