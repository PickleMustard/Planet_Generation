using Godot;
using UI.Wireframe;

namespace UI.Components;

/// <summary>
/// A single clickable row for a <see cref="FilterableSidebar"/>. The whole row
/// is the click target — clicking anywhere on it emits <see cref="Activated"/>
/// carrying the row's id. Hovering darkens the row with a translucent ink
/// overlay; clicking plays a small scale "punch".
/// <para>
/// Cells (labels, etc.) are added by the consumer via <see cref="AddCell"/>;
/// they are made mouse-transparent so clicks fall through to the row itself.
/// </para>
/// </summary>
public partial class SidebarListItem : PanelContainer
{
    [Signal]
    public delegate void ActivatedEventHandler(string id);

    // Ink at low alpha — the "darken on hover" overlay target.
    private const float HOVER_ALPHA = 0.12f;
    private const float HOVER_FADE = 0.12f;

    private readonly string _id;
    private readonly ColorRect _hoverOverlay;
    private readonly HBoxContainer _content;
    private Tween? _hoverTween;

    public SidebarListItem(string id)
    {
        _id = id;

        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0, 28);

        // Strip the theme panel so only our hover overlay paints a background,
        // and so children fill the full rect (StyleBoxEmpty has no margins).
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        _hoverOverlay = new ColorRect
        {
            Color = new Color(WireColors.Ink.R, WireColors.Ink.G, WireColors.Ink.B, 0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_hoverOverlay);

        _content = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _content.AddThemeConstantOverride("separation", 8);
        AddChild(_content);
    }

    public override void _Ready()
    {
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
    }

    /// <summary>Adds a display cell to the row and makes it mouse-transparent.</summary>
    public void AddCell(Control cell)
    {
        cell.MouseFilter = MouseFilterEnum.Ignore;
        _content.AddChild(cell);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.Pressed && btn.ButtonIndex == MouseButton.Left)
        {
            PlayClickPunch();
            EmitSignal(SignalName.Activated, _id);
            AcceptEvent();
        }
    }

    private void OnMouseEntered() => FadeOverlay(HOVER_ALPHA);

    private void OnMouseExited() => FadeOverlay(0f);

    private void FadeOverlay(float targetAlpha)
    {
        if (_hoverTween != null && _hoverTween.IsValid())
            _hoverTween.Kill();

        _hoverTween = CreateTween();
        _hoverTween.TweenProperty(_hoverOverlay, "color:a", targetAlpha, HOVER_FADE);
    }

    private void PlayClickPunch()
    {
        PivotOffset = Size / 2f;
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", new Vector2(0.95f, 0.95f), 0.06f);
        tween.TweenProperty(this, "scale", Vector2.One, 0.10f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }
}
