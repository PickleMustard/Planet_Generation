using Godot;

namespace UI.Components;

/// <summary>
/// Builds key/value detail rows for info panels. Layout lives in
/// <c>UI/Components/DetailRow.tscn</c>; styling comes from the shared
/// wireframe_paper theme via type variations (LabelKey / LabelMono / LabelAlert).
/// This helper only instantiates the row scene and binds text — it sets no
/// colors or font sizes in code.
/// </summary>
public static class DetailRowBuilder
{
    private const int DonutPx = 20;

    private static readonly PackedScene RowScene =
        GD.Load<PackedScene>("res://UI/Components/DetailRow.tscn");

    private static readonly PackedScene DonutChartScene =
        GD.Load<PackedScene>("res://UI/Components/DonutChart.tscn");

    public static void AddRow(VBoxContainer? container, string key, string value)
    {
        if (container == null) return;
        container.AddChild(BuildKeyValueRow(key, value));
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
        // Default Label theme (ink, Caveat-Bold, size 18) is the header style.
        container?.AddChild(new Label { Text = text });
    }

    public static void AddAlert(VBoxContainer? container, string message)
    {
        container?.AddChild(new Label { Text = $"[!] {message}", ThemeTypeVariation = "LabelAlert" });
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
        var row = RowScene.Instantiate<HBoxContainer>();
        row.GetNode<Label>("KeyLabel").Text = key + ":";
        row.GetNode<Label>("ValueLabel").Text = value;
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
