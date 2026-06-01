using Constructables;
using Godot;
using UI.Wireframe;

namespace UI.PlanetBoard;

/// <summary>
/// A <see cref="Node2D"/> that renders a single <see cref="StationSatellite"/> on the
/// PlanetBoard's outer ring as a diamond shape. Unlike <see cref="BuildingNode2D"/>,
/// stations have no resource-link ports and no drag — they are selectable transfer
/// endpoints only.
/// </summary>
public partial class StationNode2D : Node2D
{
    private static readonly Color FillColor = new(0.78f, 0.56f, 0.18f);
    private static readonly Color UnderConstructionFill = new(0.78f, 0.56f, 0.18f, 0.25f);
    private static readonly Color OutlineColor = WireColors.Ink;
    private static readonly Color LabelColor = WireColors.Ink;
    private const float OutlineWidth = 1.5f;
    private const float LabelOffsetY = 6f;

    private bool _isSelected;
    private bool _wasUnderConstruction;

    public StationSatellite Station { get; private set; } = null!;
    public float Radius { get; private set; } = 36f;
    public string DisplayName { get; private set; } = "";

    /// <summary>Whether this station is the currently-selected transfer destination.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            QueueRedraw();
        }
    }

    public void Setup(StationSatellite station, Vector2 position, float radius = 36f)
    {
        Station = station;
        Position = position;
        Radius = radius;
        DisplayName = string.IsNullOrEmpty(station.Name) ? "Station" : station.Name;
        _wasUnderConstruction = station.IsUnderConstruction;
        SetProcess(_wasUnderConstruction);
        QueueRedraw();
    }

    /// <summary>
    /// While the station is building, redraw each frame so the progress fill animates.
    /// Once construction completes, redraw one final time in the solid style and stop
    /// processing.
    /// </summary>
    public override void _Process(double delta)
    {
        bool nowBuilding = Station != null && Station.IsUnderConstruction;
        if (nowBuilding)
        {
            QueueRedraw();
        }
        else if (_wasUnderConstruction)
        {
            _wasUnderConstruction = false;
            SetProcess(false);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Vector2[] diamond =
        {
            new(0f, -Radius),
            new(Radius, 0f),
            new(0f, Radius),
            new(-Radius, 0f),
        };

        bool underConstruction = Station != null && Station.IsUnderConstruction;
        if (underConstruction)
        {
            // Dimmed outline-only diamond with a fill that grows with construction progress.
            DrawColoredPolygon(diamond, UnderConstructionFill);

            float progress = Mathf.Clamp(Station.ConstructionProgress, 0f, 1f);
            if (progress > 0f)
            {
                float h = Radius * progress;
                Vector2[] filled =
                {
                    new(0f, -Radius),
                    new(Radius * progress, -Radius + h),
                    new(0f, -Radius + 2f * h),
                    new(-Radius * progress, -Radius + h),
                };
                DrawColoredPolygon(filled, FillColor);
            }
        }
        else
        {
            DrawColoredPolygon(diamond, FillColor);
        }

        Vector2[] outline = { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] };
        DrawPolyline(outline, OutlineColor, OutlineWidth, antialiased: true);

        if (_isSelected)
        {
            DrawArc(Vector2.Zero, Radius + 6f, 0f, Mathf.Tau, 48,
                WireColors.Orange, 2.0f, antialiased: true);
        }

        var font = ThemeDB.FallbackFont;
        if (font != null && !string.IsNullOrEmpty(DisplayName))
        {
            string label = underConstruction
                ? $"{DisplayName} ({Mathf.RoundToInt(Mathf.Clamp(Station.ConstructionProgress, 0f, 1f) * 100f)}%)"
                : DisplayName;
            var size = font.GetStringSize(label, HorizontalAlignment.Center, -1, 12);
            DrawString(font, new Vector2(-size.X / 2f, Radius + LabelOffsetY + size.Y),
                label, HorizontalAlignment.Center, -1, 12, LabelColor);
        }
    }

    /// <summary>L1-norm point-in-diamond test.</summary>
    public bool HitTestShape(Vector2 local)
        => Mathf.Abs(local.X) + Mathf.Abs(local.Y) <= Radius;
}
