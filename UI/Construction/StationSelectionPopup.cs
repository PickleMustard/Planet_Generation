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

    private ItemList _stationList = null!;
    private Button _selectButton = null!;
    private Button _cancelButton = null!;
    private Label _emptyLabel = null!;
    private List<StationSatellite> _stations = new();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(400, 350);
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -200;
        OffsetRight = 200;
        OffsetTop = -175;
        OffsetBottom = 175;

        var vbox = new VBoxContainer();
        AddChild(vbox);

        var title = new Label
        {
            Text = "Select Shipyard Station",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(title);

        _emptyLabel = new Label
        {
            Text = "No shipyard stations available.\nBuild a station with ship-building capability first.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Visible = false,
        };
        vbox.AddChild(_emptyLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        vbox.AddChild(scroll);

        _stationList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutoHeight = true,
        };
        _stationList.ItemSelected += OnStationItemSelected;
        scroll.AddChild(_stationList);

        var buttonBar = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        vbox.AddChild(buttonBar);

        _selectButton = new Button
        {
            Text = "Select",
            Disabled = true,
        };
        _selectButton.Pressed += OnSelectPressed;
        buttonBar.AddChild(_selectButton);

        _cancelButton = new Button { Text = "Cancel" };
        _cancelButton.Pressed += OnCancelPressed;
        buttonBar.AddChild(_cancelButton);
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
