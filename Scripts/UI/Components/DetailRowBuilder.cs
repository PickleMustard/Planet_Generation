using Godot;

namespace UI.Components;

public static class DetailRowBuilder
{
    // Paper-theme palette (wireframe_paper.tres):
    //   ink        #2a2520
    //   ink-faint  #2a2520 @ 45%
    //   alert      #b03a1f
    private static readonly Color KeyColor = new Color(0.165f, 0.145f, 0.125f, 0.45f);
    private static readonly Color ValueColor = new Color(0.165f, 0.145f, 0.125f);
    private static readonly Color HeaderColor = new Color(0.165f, 0.145f, 0.125f);
    private static readonly Color AlertColor = new Color(0.69f, 0.227f, 0.122f);

    private const int RowFontSize = 13;
    private const int HeaderFontSize = 18;
    private const int DonutPx = 20;

    private static readonly PackedScene DonutChartScene =
        GD.Load<PackedScene>("res://UI/Components/DonutChart.tscn");

    public static void AddRow(VBoxContainer? container, string key, string value)
    {
        if (container == null) return;
        var row = BuildKeyValueRow(key, value);
        container.AddChild(row);
    }

    public static void AddPercentRow(
        VBoxContainer? container,
        string key,
        float current,
        float max,
        string? unit = null,
        string currentFormat = "F1",
        DonutChart.ColorMode mode = DonutChart.ColorMode.GreenToRed)
    {
        if (container == null) return;

        float ratio = max > 0f ? Mathf.Clamp(current / max, 0f, 1f) : 0f;
        int pct = Mathf.RoundToInt(ratio * 100f);

        string text = unit == null
            ? $"{current.ToString(currentFormat)} / {max.ToString(currentFormat)}  ({pct}%)"
            : $"{current.ToString(currentFormat)} / {max.ToString(currentFormat)} {unit}  ({pct}%)";

        var row = BuildKeyValueRow(key, text);
        AppendDonut(row, ratio, mode);
        container.AddChild(row);
    }

    public static void AddPercentRow(
        VBoxContainer? container,
        string key,
        int current,
        int max,
        DonutChart.ColorMode mode = DonutChart.ColorMode.RedToGreen)
    {
        if (container == null) return;

        float ratio = max > 0 ? (float)current / max : 0f;
        int pct = Mathf.RoundToInt(ratio * 100f);

        var row = BuildKeyValueRow(key, $"{current} / {max}  ({pct}%)");
        AppendDonut(row, ratio, mode);
        container.AddChild(row);
    }

    public static void AddProgressRow(
        VBoxContainer? container,
        string key,
        float ratio,
        DonutChart.ColorMode mode = DonutChart.ColorMode.RedToGreen)
    {
        if (container == null) return;

        float clamped = Mathf.Clamp(ratio, 0f, 1f);
        int pct = Mathf.RoundToInt(clamped * 100f);

        var row = BuildKeyValueRow(key, $"{pct}%");
        AppendDonut(row, clamped, mode);
        container.AddChild(row);
    }

    public static void AddHeader(VBoxContainer? container, string text)
    {
        if (container == null) return;
        var header = new Label { Text = text };
        header.AddThemeColorOverride("font_color", HeaderColor);
        header.AddThemeFontSizeOverride("font_size", HeaderFontSize);
        container.AddChild(header);
    }

    public static void AddAlert(VBoxContainer? container, string message)
    {
        if (container == null) return;
        var label = new Label { Text = $"[!] {message}" };
        label.AddThemeColorOverride("font_color", AlertColor);
        label.AddThemeFontSizeOverride("font_size", RowFontSize);
        container.AddChild(label);
    }

    public static void AddSeparator(VBoxContainer? container)
    {
        container?.AddChild(new HSeparator());
    }

    public static void Clear(VBoxContainer? container)
    {
        if (container == null) return;
        foreach (var child in container.GetChildren())
            child.QueueFree();
    }

    private static HBoxContainer BuildKeyValueRow(string key, string value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var keyLabel = new Label { Text = key + ":" };
        keyLabel.AddThemeColorOverride("font_color", KeyColor);
        keyLabel.AddThemeFontSizeOverride("font_size", RowFontSize);
        keyLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        row.AddChild(keyLabel);

        var spacer = new Control();
        spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        spacer.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(spacer);

        var valLabel = new Label { Text = value };
        valLabel.AddThemeColorOverride("font_color", ValueColor);
        valLabel.AddThemeFontSizeOverride("font_size", RowFontSize);
        valLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        valLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(valLabel);

        return row;
    }

    private static void AppendDonut(HBoxContainer row, float ratio, DonutChart.ColorMode mode)
    {
        var donut = DonutChartScene.Instantiate<DonutChart>();
        donut.CustomMinimumSize = new Vector2(DonutPx, DonutPx);
        donut.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        donut.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        donut.Mode = mode;
        donut.Value = ratio;
        row.AddChild(donut);
    }
}
