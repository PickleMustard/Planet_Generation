using Constructables;
using Godot;
using UtilityLibrary;

namespace UI.StationWindow;

/// <summary>
/// Left-side vertical button bar for station-specific bespoke features.
/// Dynamically creates buttons based on station capabilities; emits FeatureRequested
/// when a button is pressed, which the StationWindow relays upward.
/// </summary>
public partial class StationBespokeBar : VBoxContainer
{
    [Signal]
    public delegate void FeatureRequestedEventHandler(string featureId);

    private StationSatellite? _station;

    public void Initialize(StationSatellite station)
    {
        Clear();
        _station = station;

        if (station is ConstructionYardStation)
            AddFeatureButton("Manage Ship Queue", "ship_queue");

        // Visible only if we added buttons
        Visible = GetChildCount() > 0;
    }

    public void Clear()
    {
        _station = null;
        foreach (var child in GetChildren())
            child.QueueFree();
        Visible = false;
    }

    private void AddFeatureButton(string text, string featureId)
    {
        var button = new Button { Text = text };
        button.AddThemeFontSizeOverride("font_size", 13);

        var normalStyle = new StyleBoxFlat
        {
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6,
            BgColor = new Color(0.15f, 0.15f, 0.18f, 0.92f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4,
        };

        var hoverStyle = new StyleBoxFlat
        {
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6,
            BgColor = new Color(0.25f, 0.25f, 0.3f, 0.95f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4,
        };

        button.AddThemeStyleboxOverride("normal", normalStyle);
        button.AddThemeStyleboxOverride("hover", hoverStyle);

        button.Pressed += () =>
        {
            EmitSignal(SignalName.FeatureRequested, featureId);
        };

        AddChild(button);

        GameLogger.Debug($"[StationBespokeBar] Added feature button: '{text}' (id: {featureId})");
    }
}
