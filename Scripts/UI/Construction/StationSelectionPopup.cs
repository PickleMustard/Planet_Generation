using System.Collections.Generic;
using Constructables.ArtificialSatellites;
using Godot;

namespace UI.Construction;

public partial class StationSelectionPopup : PanelContainer
{
    [Signal]
    public delegate void StationSelectedEventHandler(int stationIndex);

    [Signal]
    public delegate void PopupCancelledEventHandler();

    [Export]
    private ItemList _stationList = null!;

    [Export]
    private Button _selectButton = null!;

    [Export]
    private Button _cancelButton = null!;

    [Export]
    private Label _emptyLabel = null!;

    private List<StationSatellite> _stations = new();

    public override void _Ready()
    {
        // Set up popup positioning
        CustomMinimumSize = new Vector2(400, 350);
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -200;
        OffsetRight = 200;
        OffsetTop = -175;
        OffsetBottom = 175;

        // Connect signals
        _stationList.ItemSelected += OnStationItemSelected;
        _selectButton.Pressed += OnSelectPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    public void Populate(List<StationSatellite> stations)
    {
        _stations = stations;
        _stationList.Clear();
        _selectButton.Disabled = true;

        if (stations.Count == 0)
        {
            _emptyLabel.Visible = true;
            _stationList.Visible = false;
            return;
        }

        _emptyLabel.Visible = false;
        _stationList.Visible = true;

        for (int i = 0; i < stations.Count; i++)
        {
            var station = stations[i];
            // Get the parent body name from the scene tree
            string bodyName = station.GetParent()?.GetParent()?.Name ?? "Unknown";
            string label = $"{station.Name}  (orbiting {bodyName})";
            int idx = _stationList.AddItem(label);
            _stationList.SetItemMetadata(idx, i);
        }
    }

    public StationSatellite? GetSelectedStation(int index)
    {
        if (index < 0 || index >= _stations.Count) return null;
        return _stations[index];
    }

    private void OnStationItemSelected(long index)
    {
        _selectButton.Disabled = false;
    }

    private void OnSelectPressed()
    {
        var selected = _stationList.GetSelectedItems();
        if (selected.Length == 0) return;

        int stationIndex = _stationList.GetItemMetadata(selected[0]).AsInt32();
        EmitSignal(SignalName.StationSelected, stationIndex);
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.PopupCancelled);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            OnCancelPressed();
            GetViewport().SetInputAsHandled();
        }
    }
}