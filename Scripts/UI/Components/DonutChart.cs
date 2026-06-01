using Godot;

namespace UI.Components;

/// <summary>
/// A custom donut chart control with RAG (Red-Amber-Green) coloring.
/// Supports configurable color direction and inner radius ratio.
/// </summary>
public partial class DonutChart : Control
{
    public enum ColorMode
    {
        /// <summary>Green at 0, transitioning to Red at 1.0</summary>
        GreenToRed,
        /// <summary>Red at 0, transitioning to Green at 1.0</summary>
        RedToGreen
    }

    [Export] public Color BackgroundColor { get; set; } = new Color(0.15f, 0.15f, 0.15f);
    [Export] public ColorMode Mode { get; set; } = ColorMode.GreenToRed;
    [Export(PropertyHint.Range, "0.0,0.9,0.01")] public float InnerRadiusRatio { get; set; } = 0.65f;

    private float _value;
    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Mathf.Clamp(value, 0f, 1f);
            if (!Mathf.IsEqualApprox(_value, clamped))
            {
                _value = clamped;
                QueueRedraw();
            }
        }
    }

    public override void _Draw()
    {
        Vector2 center = Size / 2;
        float radius = Mathf.Min(center.X, center.Y);
        float innerRadius = radius * InnerRadiusRatio;

        // Draw background ring (full circle)
        DrawRing(center, radius, innerRadius, 0, Mathf.Tau, BackgroundColor);

        // Calculate fill color based on Value and Mode
        Color fillColor = GetFillColor(Value);

        // Draw filled arc from -90 degrees (top) clockwise
        float endAngle = -Mathf.Pi / 2 + (Value * Mathf.Tau);
        DrawRing(center, radius, innerRadius, -Mathf.Pi / 2, endAngle, fillColor);
    }

    private void DrawRing(Vector2 center, float outerRadius, float innerRadius, float startAngle, float endAngle, Color color)
    {
        // Number of segments for smooth arc
        const int segments = 64;
        float angleStep = (endAngle - startAngle) / segments;

        Vector2[] outerPoints = new Vector2[segments + 1];
        Vector2[] innerPoints = new Vector2[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + (i * angleStep);
            outerPoints[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius;
            innerPoints[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius;
        }

        // Draw the ring as a series of quads
        for (int i = 0; i < segments; i++)
        {
            Vector2[] quad = new Vector2[4]
            {
                outerPoints[i],
                outerPoints[i + 1],
                innerPoints[i + 1],
                innerPoints[i]
            };
            DrawColoredPolygon(quad, color);
        }

        // Draw caps at start and end to ensure clean edges
        if (Mathf.Abs(endAngle - startAngle) > 0.01f)
        {
            DrawCircle(outerPoints[0], 1.5f, color);
            DrawCircle(outerPoints[segments], 1.5f, color);
        }
    }

    private Color GetFillColor(float value)
    {
        // Define RAG colors
        Color red = new Color(0.9f, 0.2f, 0.2f);      // Red - danger/high usage
        Color amber = new Color(0.9f, 0.7f, 0.1f);    // Amber - warning/medium
        Color green = new Color(0.2f, 0.8f, 0.3f);    // Green - good/low

        if (Mode == ColorMode.GreenToRed)
        {
            // Green at 0, Red at 1
            if (value < 0.5f)
            {
                // Green to Amber
                return green.Lerp(amber, value * 2f);
            }
            else
            {
                // Amber to Red
                return amber.Lerp(red, (value - 0.5f) * 2f);
            }
        }
        else
        {
            // Red at 0, Green at 1
            if (value < 0.5f)
            {
                // Red to Amber
                return red.Lerp(amber, value * 2f);
            }
            else
            {
                // Amber to Green
                return amber.Lerp(green, (value - 0.5f) * 2f);
            }
        }
    }

    public override void _Ready()
    {
        // Set minimum size for the chart
        CustomMinimumSize = new Vector2(40, 40);
    }
}
