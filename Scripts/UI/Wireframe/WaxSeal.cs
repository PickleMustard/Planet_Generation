using Godot;

namespace UI.Wireframe;

/// <summary>
/// Decorative wax-seal stamp. Tilted -5 degrees, two rotated text rows.
/// </summary>
[GlobalClass]
public partial class WaxSeal : Control
{
    [Export] public string MainText { get; set; } = "TRX·04";
    [Export] public string SubText { get; set; } = "1905";
    [Export] public float SealSize { get; set; } = 64f;

    private Label? _mainLabel;
    private Label? _subLabel;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(SealSize, SealSize);
        MouseFilter = MouseFilterEnum.Ignore;
        PivotOffset = new Vector2(SealSize * 0.5f, SealSize * 0.5f);
        Rotation = Mathf.DegToRad(-5f);

        var stack = new VBoxContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        AddChild(stack);

        _mainLabel = new Label
        {
            Text = MainText,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = "LabelMono",
        };
        _mainLabel.AddThemeColorOverride("font_color", WireColors.Red);
        _mainLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(SealSize * 0.18f));
        stack.AddChild(_mainLabel);

        _subLabel = new Label
        {
            Text = SubText,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = "LabelMono",
        };
        var subColor = WireColors.Red;
        subColor.A = 0.8f;
        _subLabel.AddThemeColorOverride("font_color", subColor);
        _subLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(SealSize * 0.13f));
        stack.AddChild(_subLabel);
    }

    public void SetText(string main, string sub)
    {
        MainText = main;
        SubText = sub;
        if (_mainLabel != null) _mainLabel.Text = main;
        if (_subLabel != null) _subLabel.Text = sub;
    }

    public override void _Draw()
    {
        float r = SealSize * 0.5f;
        var center = new Vector2(r, r);
        var fill = new Color(0.690f, 0.227f, 0.122f, 0.32f);
        DrawCircle(center, r - 1f, fill);
        DrawArc(center, r - 1f, 0f, Mathf.Tau, 64, WireColors.Red, 2f);
        var highlight = new Color(1f, 1f, 1f, 0.38f);
        DrawCircle(center - new Vector2(r * 0.3f, r * 0.4f), r * 0.18f, highlight);
    }
}
