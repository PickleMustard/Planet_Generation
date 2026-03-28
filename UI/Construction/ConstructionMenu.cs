using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace UI.Construction;

public partial class ConstructionMenu : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedForPlacementEventHandler(string itemType, string definitionName);

    [Signal]
    public delegate void MenuClosedEventHandler();

    private TabContainer _tabContainer = null!;
    private ItemList _stationsList = null!;
    private ItemList _shipsList = null!;
    private VBoxContainer _buildingsPlaceholder = null!;
    private RichTextLabel _detailLabel = null!;
    private Button _buildButton = null!;
    private Button _cancelButton = null!;

    private string? _selectedItemType;
    private string? _selectedDefinitionName;

    public override void _Ready()
    {
        BuildUI();
        PopulateStations();
        PopulateShips();
    }

    private void BuildUI()
    {
        CustomMinimumSize = new Vector2(600, 500);
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0.5f;
        AnchorBottom = 0.5f;
        OffsetLeft = -300;
        OffsetRight = 300;
        OffsetTop = -250;
        OffsetBottom = 250;

        var mainVBox = new VBoxContainer();
        AddChild(mainVBox);

        var titleLabel = new Label
        {
            Text = "Construction",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        mainVBox.AddChild(titleLabel);

        _tabContainer = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        mainVBox.AddChild(_tabContainer);

        // Stations tab
        var stationsContainer = new VBoxContainer { Name = "Stations" };
        var stationsScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _stationsList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutoHeight = true,
        };
        _stationsList.ItemSelected += OnStationItemSelected;
        stationsScroll.AddChild(_stationsList);
        stationsContainer.AddChild(stationsScroll);
        _tabContainer.AddChild(stationsContainer);

        // Ships tab
        var shipsContainer = new VBoxContainer { Name = "Ships" };
        var shipsScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _shipsList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutoHeight = true,
        };
        _shipsList.ItemSelected += OnShipItemSelected;
        shipsScroll.AddChild(_shipsList);
        shipsContainer.AddChild(shipsScroll);
        _tabContainer.AddChild(shipsContainer);

        // Buildings tab (placeholder)
        _buildingsPlaceholder = new VBoxContainer { Name = "Buildings" };
        var comingSoonLabel = new Label
        {
            Text = "Buildings - Coming Soon",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _buildingsPlaceholder.AddChild(comingSoonLabel);
        _tabContainer.AddChild(_buildingsPlaceholder);

        // Footer
        var footerVBox = new VBoxContainer();
        mainVBox.AddChild(footerVBox);

        _detailLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 80),
            FitContent = true,
            ScrollActive = false,
        };
        footerVBox.AddChild(_detailLabel);

        var buttonBar = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        footerVBox.AddChild(buttonBar);

        _buildButton = new Button
        {
            Text = "Build Selected",
            Disabled = true,
        };
        _buildButton.Pressed += OnBuildPressed;
        buttonBar.AddChild(_buildButton);

        _cancelButton = new Button { Text = "Cancel" };
        _cancelButton.Pressed += OnCancelPressed;
        buttonBar.AddChild(_cancelButton);
    }

    private void PopulateStations()
    {
        _stationsList.Clear();
        var stations = LogisticsConfigLoader.LoadAllStations();
        foreach (var station in stations)
        {
            int idx = _stationsList.AddItem(station.Name);
            _stationsList.SetItemMetadata(idx, station.Name);
        }
    }

    private void PopulateShips()
    {
        _shipsList.Clear();
        var ships = LogisticsConfigLoader.LoadAllShips();
        foreach (var ship in ships)
        {
            int idx = _shipsList.AddItem(ship.Name);
            _shipsList.SetItemMetadata(idx, ship.Name);
        }
    }

    private void OnStationItemSelected(long index)
    {
        _shipsList.DeselectAll();
        string name = _stationsList.GetItemMetadata((int)index).AsString();
        _selectedItemType = "Station";
        _selectedDefinitionName = name;
        _buildButton.Disabled = false;
        ShowStationDetails(name);
    }

    private void OnShipItemSelected(long index)
    {
        _stationsList.DeselectAll();
        string name = _shipsList.GetItemMetadata((int)index).AsString();
        _selectedItemType = "Ship";
        _selectedDefinitionName = name;
        _buildButton.Disabled = false;
        ShowShipDetails(name);
    }

    private void ShowStationDetails(string name)
    {
        var def = LogisticsConfigLoader.GetStationDefinition(name);
        if (def == null)
        {
            _detailLabel.Text = "";
            return;
        }

        var text = $"[b]{def.Name}[/b]  ({def.StationType})\n";
        text += $"Construction Time: {def.ConstructionTime}s";
        text += def.CanBuildShips ? "  |  Can Build Ships" : "";
        if (def.RequiredResources.Count > 0)
        {
            text += "\nResources: ";
            var parts = new List<string>();
            foreach (var kvp in def.RequiredResources)
                parts.Add($"{kvp.Key} x{kvp.Value}");
            text += string.Join(", ", parts);
        }
        _detailLabel.Text = text;
    }

    private void ShowShipDetails(string name)
    {
        var def = LogisticsConfigLoader.GetShipDefinition(name);
        if (def == null)
        {
            _detailLabel.Text = "";
            return;
        }

        var text = $"[b]{def.Name}[/b]  (Mass: {def.DryMass}kg)\n";
        text += $"Construction Time: {def.ConstructionTime}s  |  Engine: {def.EngineCategory}";
        text += $"\nCargo: {def.CargoCapacity}  |  Fuel: {def.FuelCapacity}";
        if (def.RequiredResources.Count > 0)
        {
            text += "\nResources: ";
            var parts = new List<string>();
            foreach (var kvp in def.RequiredResources)
                parts.Add($"{kvp.Key} x{kvp.Value}");
            text += string.Join(", ", parts);
        }
        _detailLabel.Text = text;
    }

    private void OnBuildPressed()
    {
        if (_selectedItemType != null && _selectedDefinitionName != null)
        {
            EmitSignal(SignalName.ItemSelectedForPlacement, _selectedItemType, _selectedDefinitionName);
        }
    }

    private void OnCancelPressed()
    {
        ClearSelection();
        Visible = false;
        EmitSignal(SignalName.MenuClosed);
    }

    public void ClearSelection()
    {
        _stationsList.DeselectAll();
        _shipsList.DeselectAll();
        _selectedItemType = null;
        _selectedDefinitionName = null;
        _buildButton.Disabled = true;
        _detailLabel.Text = "";
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
