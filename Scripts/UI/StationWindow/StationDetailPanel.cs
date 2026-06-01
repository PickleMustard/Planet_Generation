using Constructables;
using Constructables.Stations;
using Godot;
using Structures.Logistics;

namespace UI.StationWindow;

/// <summary>
/// Base class for a single station behavior tab. Mirrors <see cref="UI.BuildingInfo.BaseBuildingDetails"/>
/// on the station side: <see cref="SetStation"/> binds the station + (optional) behavior and triggers
/// <see cref="UpdateDisplay"/>. Subclasses build either bespoke UI (shipyard, architect, transfer) or
/// generic rows. Tabs live as children of a native <c>TabContainer</c>, so the container owns
/// visibility; polling subclasses must guard refreshes on <see cref="Node.IsVisibleInTree"/>.
/// </summary>
public abstract partial class StationDetailPanel : Control
{
    protected StationSatellite? _station;
    protected IStationBehavior? _behavior;

    public virtual void SetStation(StationSatellite station, IStationBehavior? behavior)
    {
        _station = station;
        _behavior = behavior;
        UpdateDisplay();
    }

    public virtual void Clear()
    {
        _station = null;
        _behavior = null;
    }

    protected abstract void UpdateDisplay();

    // --- shared row builders (ported from the former StationBehaviorTab) ---

    protected static void AddSectionHeader(Container target, string text)
    {
        var header = new Label { Text = text };
        header.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.95f));
        header.AddThemeFontSizeOverride("font_size", 15);
        target.AddChild(header);
        target.AddChild(new HSeparator());
    }

    protected static void AddInfoRow(Container target, string key, string value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var keyLabel = new Label { Text = key + ":", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        keyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        keyLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(keyLabel);

        var valLabel = new Label { Text = value };
        valLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(valLabel);

        target.AddChild(row);
    }

    protected static void AddEmptyLabel(Container target, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        label.AddThemeFontSizeOverride("font_size", 13);
        target.AddChild(label);
    }

    protected static void ClearChildren(Node? container)
    {
        if (container == null)
            return;
        foreach (var child in container.GetChildren())
            child.QueueFree();
    }

    protected static (int occupied, int total, float used, float capacity) SummarizeStorage(Storage storage)
    {
        int occupied = 0, total = 0;
        float used = 0f, capacity = 0f;
        foreach (var slot in storage.Slots)
        {
            total++;
            if (!slot.IsEmpty)
                occupied++;
            used += slot.Quantity;
            capacity += slot.Capacity;
        }
        return (occupied, total, used, capacity);
    }
}
