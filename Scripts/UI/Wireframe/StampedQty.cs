using Godot;

namespace UI.Wireframe;

/// <summary>
/// Quantity readout styled like a rubber-stamp impression: bold mono numerals
/// followed by a slightly lighter unit suffix.
/// </summary>
[GlobalClass]
public partial class StampedQty : HBoxContainer
{
    [Export] public string Suffix { get; set; } = "t";
    [Export] public int FontSize { get; set; } = 18;

    private readonly Label _value = new();
    private readonly Label _suffix = new();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 2);
        _value.ThemeTypeVariation = "LabelMono";
        _value.AddThemeFontSizeOverride("font_size", FontSize);
        _value.AddThemeColorOverride("font_color", WireColors.Ink);
        AddChild(_value);

        _suffix.ThemeTypeVariation = "LabelMono";
        _suffix.AddThemeFontSizeOverride("font_size", Mathf.Max(6, FontSize - 4));
        _suffix.AddThemeColorOverride("font_color", WireColors.InkSoft);
        AddChild(_suffix);

        Refresh(0);
    }

    public void Refresh(float value)
    {
        _value.Text = value % 1f == 0f ? ((int)value).ToString() : value.ToString("0.#");
        _suffix.Text = Suffix;
    }

    public void SetValueAndSuffix(float value, string suffix)
    {
        Suffix = suffix;
        Refresh(value);
    }
}
