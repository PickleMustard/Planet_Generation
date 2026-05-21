using System.Collections.Generic;
using Constructables.Power;
using Godot;

namespace UI.Components;

/// <summary>
/// Time-series line chart for grid telemetry. Plots generation (kW), draw (kW), and
/// battery stored (kWh) over time with optional brownout band overlay. Power series share
/// a left Y axis (kW); battery uses the right Y axis (kWh).
///
/// Call <see cref="SetSamples"/> on every refresh; the control auto-scales and redraws.
/// </summary>
public partial class LineChart : Control
{
    [Export] public Color BackgroundColor { get; set; } = new(0.10f, 0.10f, 0.12f, 0.85f);
    [Export] public Color AxisColor { get; set; } = new(0.55f, 0.55f, 0.6f);
    [Export] public Color GridColor { get; set; } = new(0.30f, 0.30f, 0.35f, 0.6f);
    [Export] public Color GenerationColor { get; set; } = new(0.29f, 0.78f, 0.42f);
    [Export] public Color DrawColor { get; set; } = new(0.95f, 0.55f, 0.25f);
    [Export] public Color BatteryColor { get; set; } = new(0.40f, 0.70f, 0.95f);
    [Export] public Color BrownoutColor { get; set; } = new(0.85f, 0.20f, 0.20f, 0.25f);

    public bool ShowGeneration { get; set; } = true;
    public bool ShowDraw { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool ShowBrownouts { get; set; } = true;

    private List<GridSample> _samples = new();

    public void SetSamples(List<GridSample> samples)
    {
        _samples = samples ?? new List<GridSample>();
        QueueRedraw();
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(200, 120);
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, BackgroundColor, true);

        // Plot area inset for axis labels.
        const float padLeft = 44f;
        const float padRight = 44f;
        const float padTop = 22f;
        const float padBottom = 18f;

        float plotW = Size.X - padLeft - padRight;
        float plotH = Size.Y - padTop - padBottom;
        if (plotW <= 0 || plotH <= 0)
            return;

        var plotOrigin = new Vector2(padLeft, padTop);

        // Frame + grid lines.
        DrawRect(new Rect2(plotOrigin, new Vector2(plotW, plotH)), AxisColor, false, 1f);
        const int gridLines = 4;
        var font = ThemeDB.FallbackFont;
        int fontSize = 10;
        for (int i = 1; i < gridLines; i++)
        {
            float y = plotOrigin.Y + plotH * i / gridLines;
            DrawLine(new Vector2(plotOrigin.X, y), new Vector2(plotOrigin.X + plotW, y), GridColor, 1f);
        }

        if (_samples.Count == 0)
        {
            DrawString(font, plotOrigin + new Vector2(plotW * 0.5f - 32f, plotH * 0.5f), "no data", HorizontalAlignment.Left, -1, fontSize, AxisColor);
            return;
        }

        // Compute scales.
        float maxPower = 1f;
        float maxBattery = 1f;
        foreach (var s in _samples)
        {
            if (s.Generation > maxPower) maxPower = s.Generation;
            if (s.Draw > maxPower) maxPower = s.Draw;
            if (s.BatteryStored > maxBattery) maxBattery = s.BatteryStored;
        }
        // Headroom.
        maxPower *= 1.1f;
        maxBattery *= 1.1f;

        int n = _samples.Count;
        float dx = n > 1 ? plotW / (n - 1) : plotW;

        // Brownout bands first (background).
        if (ShowBrownouts)
        {
            int bandStart = -1;
            for (int i = 0; i < n; i++)
            {
                bool b = _samples[i].BrownedOut;
                if (b && bandStart < 0)
                    bandStart = i;
                if ((!b || i == n - 1) && bandStart >= 0)
                {
                    int bandEnd = b ? i : i - 1;
                    float x0 = plotOrigin.X + bandStart * dx;
                    float x1 = plotOrigin.X + (bandEnd + 0.5f) * dx;
                    DrawRect(new Rect2(x0, plotOrigin.Y, Mathf.Max(x1 - x0, 1f), plotH), BrownoutColor, true);
                    bandStart = -1;
                }
            }
        }

        // Series.
        if (ShowGeneration)
            DrawSeries(s => s.Generation, maxPower, plotOrigin, plotW, plotH, dx, GenerationColor);
        if (ShowDraw)
            DrawSeries(s => s.Draw, maxPower, plotOrigin, plotW, plotH, dx, DrawColor);
        if (ShowBattery)
            DrawSeries(s => s.BatteryStored, maxBattery, plotOrigin, plotW, plotH, dx, BatteryColor);

        // Y axis labels (left = kW, right = kWh).
        DrawString(font, new Vector2(2, plotOrigin.Y + 8), $"{maxPower:F0} kW", HorizontalAlignment.Left, -1, fontSize, AxisColor);
        DrawString(font, new Vector2(2, plotOrigin.Y + plotH - 2), "0", HorizontalAlignment.Left, -1, fontSize, AxisColor);
        DrawString(font, new Vector2(plotOrigin.X + plotW + 4, plotOrigin.Y + 8), $"{maxBattery:F0}", HorizontalAlignment.Left, -1, fontSize, AxisColor);
        DrawString(font, new Vector2(plotOrigin.X + plotW + 4, plotOrigin.Y + plotH - 2), "kWh", HorizontalAlignment.Left, -1, fontSize, AxisColor);

        // X axis: total span (samples are 1Hz, so seconds == count).
        DrawString(font, new Vector2(plotOrigin.X, Size.Y - 4), $"-{n}s", HorizontalAlignment.Left, -1, fontSize, AxisColor);
        DrawString(font, new Vector2(plotOrigin.X + plotW - 14, Size.Y - 4), "now", HorizontalAlignment.Left, -1, fontSize, AxisColor);

        // Legend.
        DrawLegend(font, fontSize);
    }

    private delegate float SampleAccessor(GridSample s);

    private void DrawSeries(SampleAccessor accessor, float maxValue, Vector2 origin, float plotW, float plotH, float dx, Color color)
    {
        if (_samples.Count < 2)
        {
            float v0 = accessor(_samples[0]);
            float y0 = origin.Y + plotH - (v0 / maxValue) * plotH;
            DrawCircle(new Vector2(origin.X, y0), 2f, color);
            return;
        }
        var pts = new Vector2[_samples.Count];
        for (int i = 0; i < _samples.Count; i++)
        {
            float v = accessor(_samples[i]);
            float x = origin.X + i * dx;
            float y = origin.Y + plotH - Mathf.Clamp(v / maxValue, 0f, 1f) * plotH;
            pts[i] = new Vector2(x, y);
        }
        DrawPolyline(pts, color, 1.5f, true);
    }

    private void DrawLegend(Font font, int fontSize)
    {
        float x = 6f;
        float y = 12f;
        if (ShowGeneration) x = DrawLegendEntry(font, fontSize, x, y, GenerationColor, "gen");
        if (ShowDraw) x = DrawLegendEntry(font, fontSize, x, y, DrawColor, "draw");
        if (ShowBattery) x = DrawLegendEntry(font, fontSize, x, y, BatteryColor, "battery");
    }

    private float DrawLegendEntry(Font font, int fontSize, float x, float y, Color color, string label)
    {
        DrawRect(new Rect2(x, y - 7, 10, 4), color, true);
        DrawString(font, new Vector2(x + 14, y), label, HorizontalAlignment.Left, -1, fontSize, color);
        return x + 14 + label.Length * 6.5f + 8f;
    }
}
