using System;
using Constructables;
using Godot;
using Structures.Logistics;

namespace UI.StationWindow;

/// <summary>
/// Reusable slot table reading <see cref="StationSatellite.BulkStorage"/>. Subscribes to
/// <see cref="Storage.StorageUpdated"/> so the table refreshes within one tick of any
/// deposit or withdraw. Stations always carry storage, so this is the universal panel
/// docked beneath every signature behavior tab.
/// </summary>
public partial class StationStorageSection : PanelContainer
{
    private StationSatellite? _station;
    private VBoxContainer? _root;
    private Label? _summaryLabel;
    private VBoxContainer? _rowsContainer;

    public override void _Ready()
    {
        _root = GetNodeOrNull<VBoxContainer>("Margin/VBox");
        _summaryLabel = GetNodeOrNull<Label>("Margin/VBox/Summary");
        _rowsContainer = GetNodeOrNull<VBoxContainer>("Margin/VBox/Rows");

        if (_root == null)
            BuildFallbackLayout();
    }

    public void Initialize(StationSatellite station)
    {
        Detach();

        _station = station;
        station.BulkStorage.StorageUpdated += OnStorageUpdated;
        Refresh();
    }

    public void Clear()
    {
        Detach();
        _station = null;
        if (_rowsContainer != null)
            ClearRows();
        if (_summaryLabel != null)
            _summaryLabel.Text = "";
    }

    public override void _ExitTree() => Detach();

    private void Detach()
    {
        if (_station != null)
            _station.BulkStorage.StorageUpdated -= OnStorageUpdated;
    }

    private void OnStorageUpdated(string resourceId, float delta)
    {
        if (Engine.IsEditorHint())
            return;
        CallDeferred(MethodName.Refresh);
    }

    public void Refresh()
    {
        if (_station == null || _rowsContainer == null)
            return;

        ClearRows();

        var slots = _station.BulkStorage.Slots;
        int occupied = 0;
        float totalUsed = 0f, totalCapacity = 0f;

        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.OccupiedResourceId) && slot.Quantity > 0)
            {
                occupied++;
                totalUsed += slot.Quantity;
                AppendSlotRow(slot);
            }
            totalCapacity += Mathf.Max(slot.Capacity, slot.OccupiedResourceId == null ? StorageSlot.FallbackCapacity : 0f);
        }

        if (_summaryLabel != null)
            _summaryLabel.Text = $"Storage · {occupied}/{slots.Count} slots · {Mathf.RoundToInt(totalUsed)}u";

        if (occupied == 0)
        {
            var empty = new Label
            {
                Text = "Storage empty",
                ThemeTypeVariation = "LabelFaint",
            };
            _rowsContainer.AddChild(empty);
        }
    }

    private void AppendSlotRow(StorageSlot slot)
    {
        if (_rowsContainer == null)
            return;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var name = new Label
        {
            Text = slot.OccupiedResourceId ?? "—",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        name.AddThemeFontSizeOverride("font_size", 12);

        float capacity = slot.Capacity > 0 ? slot.Capacity : StorageSlot.FallbackCapacity;
        var qty = new Label
        {
            Text = $"{Mathf.RoundToInt(slot.Quantity)} / {Mathf.RoundToInt(capacity)}",
            ThemeTypeVariation = "LabelMono",
        };
        qty.AddThemeFontSizeOverride("font_size", 12);

        row.AddChild(name);
        row.AddChild(qty);
        _rowsContainer.AddChild(row);
    }

    private void ClearRows()
    {
        if (_rowsContainer == null)
            return;
        foreach (var child in _rowsContainer.GetChildren())
            child.QueueFree();
    }

    private void BuildFallbackLayout()
    {
        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(margin);

        _root = new VBoxContainer { Name = "VBox" };
        _root.AddThemeConstantOverride("separation", 6);
        margin.AddChild(_root);

        _summaryLabel = new Label { Name = "Summary" };
        _summaryLabel.AddThemeFontSizeOverride("font_size", 13);
        _root.AddChild(_summaryLabel);

        _root.AddChild(new HSeparator());

        _rowsContainer = new VBoxContainer { Name = "Rows" };
        _rowsContainer.AddThemeConstantOverride("separation", 2);
        _root.AddChild(_rowsContainer);
    }
}
