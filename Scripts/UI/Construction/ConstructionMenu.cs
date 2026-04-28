using System.Collections.Generic;
using Godot;
using Logistics.Resources;
using Structures.Resources;

namespace UI.Construction;

public partial class ConstructionMenu : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedForPlacementEventHandler(string itemType, string definitionName);

    [Signal]
    public delegate void MenuClosedEventHandler();

    [Export]
    private TabContainer _tabContainer = null!;

    [Export]
    private ItemList _stationsList = null!;

    [Export]
    private ItemList _shipsList = null!;

    [Export]
    private ItemList _buildingsList = null!;

    [Export]
    private RichTextLabel _detailLabel = null!;

    [Export]
    private Button _buildButton = null!;

    [Export]
    private Button _cancelButton = null!;

    private string? _selectedItemType;
    private string? _selectedDefinitionName;

    public override void _Ready()
    {
        // Connect signals to UI elements
        _stationsList.ItemSelected += OnStationItemSelected;
        _shipsList.ItemSelected += OnShipItemSelected;
        _buildingsList.ItemSelected += OnBuildingItemSelected;
        _buildButton.Pressed += OnBuildPressed;
        _cancelButton.Pressed += OnCancelPressed;

        // Populate the lists
        PopulateStations();
        PopulateShips();
        PopulateBuildings();
    }

    private void PopulateStations()
    {
        _stationsList.Clear();
        var stations = StationDatabase.Instance.GetAllStations();
        foreach (var station in stations.Values)
        {
            int idx = _stationsList.AddItem(station.Name);
            _stationsList.SetItemMetadata(idx, station.Name);
        }
    }

    private void PopulateShips()
    {
        _shipsList.Clear();
        var ships = ShipDatabase.Instance.GetAllShips();
        foreach (var ship in ships.Values)
        {
            int idx = _shipsList.AddItem(ship.Name);
            _shipsList.SetItemMetadata(idx, ship.Name);
        }
    }

    private void PopulateBuildings()
    {
        _buildingsList.Clear();
        var buildings = BuildingDatabase.Instance.GetAllBuildings();
        foreach (var building in buildings.Values)
        {
            string displayName = building.DisplayName ?? building.IdName ?? "Unknown";
            int idx = _buildingsList.AddItem(displayName);
            _buildingsList.SetItemMetadata(idx, building.IdName ?? "");
        }
    }

    private void OnStationItemSelected(long index)
    {
        _shipsList.DeselectAll();
        _buildingsList.DeselectAll();
        string name = _stationsList.GetItemMetadata((int)index).AsString();
        _selectedItemType = "Station";
        _selectedDefinitionName = name;
        _buildButton.Disabled = false;
        ShowStationDetails(name);
    }

    private void OnShipItemSelected(long index)
    {
        _stationsList.DeselectAll();
        _buildingsList.DeselectAll();
        string name = _shipsList.GetItemMetadata((int)index).AsString();
        _selectedItemType = "Ship";
        _selectedDefinitionName = name;
        _buildButton.Disabled = false;
        ShowShipDetails(name);
    }

    private void OnBuildingItemSelected(long index)
    {
        _stationsList.DeselectAll();
        _shipsList.DeselectAll();
        string name = _buildingsList.GetItemMetadata((int)index).AsString();
        _selectedItemType = "Building";
        _selectedDefinitionName = name;
        _buildButton.Disabled = false;
        ShowBuildingDetails(name);
    }

    private void ShowStationDetails(string name)
    {
        StationDatabase.Instance.TryGetStation(name, out var def);
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
        ShipDatabase.Instance.TryGetShip(name, out var def);
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

    private void ShowBuildingDetails(string name)
    {
        BuildingDatabase.Instance.TryGetBuilding(name, out var def);
        if (def == null)
        {
            _detailLabel.Text = "";
            return;
        }

        var text = $"[b]{def.DisplayName ?? def.IdName}[/b]";
        if (!string.IsNullOrEmpty(def.Category))
            text += $"  ({def.Category})";
        text += $"\nBuild Time: {def.BuildingTime}s  |  Cells: {def.Placement.CellCount}";
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
        _buildingsList.DeselectAll();
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
