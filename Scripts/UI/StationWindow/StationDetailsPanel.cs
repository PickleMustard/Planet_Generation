using Constructables;
using Godot;
using UI.Components;

namespace UI.StationWindow;

/// <summary>
/// Bottom details panel that updates its content based on selection in the tabbed panel.
/// Shows detailed info for resources, buildings, and ships selected from tab content.
/// </summary>
public partial class StationDetailsPanel : PanelContainer
{
    private Label? _titleLabel;
    private VBoxContainer? _contentContainer;
    private StationSatellite? _station;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBoxContainer/DetailTitleLabel");
        _contentContainer = GetNode<VBoxContainer>("VBoxContainer/DetailContent");
    }

    public void Initialize(StationSatellite station, StationTabbedPanel tabbedPanel)
    {
        _station = station;
        tabbedPanel.ItemSelected += OnItemSelected;
    }

    public void Disconnect(StationTabbedPanel tabbedPanel)
    {
        tabbedPanel.ItemSelected -= OnItemSelected;
    }

    public void Clear()
    {
        _station = null;
        ClearContent();
        if (_titleLabel != null)
            _titleLabel.Text = "Details";
    }

    private void OnItemSelected(string itemType, int itemIndex)
    {
        ClearContent();

        switch (itemType)
        {
            case "resource":
                ShowResourceDetails(itemIndex);
                break;
            case "active_building":
                ShowBuildingDetails(itemIndex);
                break;
            case "active_ship":
            case "queued_ship":
                ShowShipDetails(itemType, itemIndex);
                break;
        }
    }

    // ───────── Resource Details ─────────

    private void ShowResourceDetails(int resourceIndex)
    {
        if (_station?.Economy == null || _titleLabel == null || _contentContainer == null)
            return;

        var stockpiles = _station.Economy.GetAllStockpiles();
        int idx = 0;
        foreach (var kvp in stockpiles)
        {
            if (idx == resourceIndex)
            {
                _titleLabel.Text = kvp.Key;
                AddDetailRow("Stockpile", $"{kvp.Value:F1}");
                AddDetailRow("Production", $"{_station.Economy.GetProductionRate(kvp.Key):F2}/s");
                AddDetailRow("Consumption", $"{_station.Economy.GetConsumptionRate(kvp.Key):F2}/s");
                AddDetailRow("Net Rate", $"{_station.Economy.GetNetRate(kvp.Key):F2}/s");
                return;
            }
            idx++;
        }
    }

    // ───────── Building Details ─────────

    private void ShowBuildingDetails(int buildingIndex)
    {
        if (_station == null || _titleLabel == null || _contentContainer == null)
            return;

        var constructionMgr = _station.ParentBody?.BuildingConstructionMgr;
        if (constructionMgr == null)
            return;

        var activeBuildings = constructionMgr.GetActiveBuildings();
        if (buildingIndex < 0 || buildingIndex >= activeBuildings.Count)
            return;

        var building = activeBuildings[buildingIndex];
        _titleLabel.Text = building.Name;
        AddDetailRow("Status", building.GetStatus());
        AddProgressRow("Progress", building.GetProgress());
    }

    // ───────── Ship Details ─────────

    private void ShowShipDetails(string type, int shipIndex)
    {
        if (_station is not ConstructionYardStation shipyard || _titleLabel == null || _contentContainer == null)
            return;

        if (type == "active_ship")
        {
            var activeBuilds = shipyard.GetActiveBuilds();
            if (shipIndex < 0 || shipIndex >= activeBuilds.Count)
                return;

            var ship = activeBuilds[shipIndex];
            _titleLabel.Text = ship.Name;
            AddDetailRow("Status", ship.GetStatus());
            AddProgressRow("Progress", ship.GetProgress());
        }
        else if (type == "queued_ship")
        {
            var queuedShips = shipyard.GetShipBuildQueue();
            if (shipIndex < 0 || shipIndex >= queuedShips.Count)
                return;

            var ship = queuedShips[shipIndex];
            _titleLabel.Text = ship.Name;
            AddDetailRow("Status", "Queued");
        }
    }

    // ───────── Helpers ─────────

    private void ClearContent() => DetailRowBuilder.Clear(_contentContainer);

    private void AddDetailRow(string key, string value)
        => DetailRowBuilder.AddRow(_contentContainer, key, value);

    private void AddProgressRow(string key, float ratio,
        DonutChart.ColorMode mode = DonutChart.ColorMode.RedToGreen)
        => DetailRowBuilder.AddProgressRow(_contentContainer, key, ratio, mode);

    private void AddDetailHeader(string text)
        => DetailRowBuilder.AddHeader(_contentContainer, text);

    private void AddAlertRow(string message)
        => DetailRowBuilder.AddAlert(_contentContainer, message);
}
