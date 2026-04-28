using Constructables;
using Godot;
using Structures.GameState;
using System.Collections.Generic;
using System.Linq;
using UI.Components;

namespace UI;

/// <summary>
/// Bottom details panel that updates its content based on selection in the tabbed panel.
/// Shows economy, transfer, and alert summaries for the selected item.
/// </summary>
public partial class OrbitalBodyDetailsPanel : PanelContainer
{
    private Label? _titleLabel;
    private VBoxContainer? _contentContainer;
    private IOrbitalBody? _body;
    private string? _lastItemType;
    private int _lastItemIndex = -1;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBoxContainer/DetailTitleLabel");
        _contentContainer = GetNode<VBoxContainer>("VBoxContainer/DetailContent");
    }

    public void Initialize(IOrbitalBody body, OrbitalBodyTabbedPanel tabbedPanel)
    {
        _body = body;
        tabbedPanel.ItemSelected += OnItemSelected;
    }

    public void Disconnect(OrbitalBodyTabbedPanel tabbedPanel)
    {
        tabbedPanel.ItemSelected -= OnItemSelected;
    }

    public void Clear()
    {
        _body = null;
        _lastItemType = null;
        _lastItemIndex = -1;
        ClearContent();
        if (_titleLabel != null)
            _titleLabel.Text = "Details";
    }

    public void RefreshCurrentDetails()
    {
        if (_body == null || _lastItemType == null || _lastItemIndex < 0)
            return;

        OnItemSelected(_lastItemType, _lastItemIndex);
    }

    private void OnItemSelected(string itemType, int itemIndex)
    {
        _lastItemType = itemType;
        _lastItemIndex = itemIndex;
        ClearContent();

        switch (itemType)
        {
            case "body":
                ShowBodyDetails();
                break;
            case "continent":
                ShowContinentDetails(itemIndex);
                break;
            case "station":
                ShowStationDetails(itemIndex);
                break;
            case "logistics_unit":
                ShowLogisticsUnitDetails(itemIndex);
                break;
            case "transfer":
            case "schedule":
                ShowTransferDetails(itemType, itemIndex);
                break;
            case "economy_summary":
                ShowEconomySummary();
                break;
            case "continent_economy":
                ShowContinentEconomyDetails(itemIndex);
                break;
            case "station_economy":
                ShowStationEconomyDetails(itemIndex);
                break;
        }
    }

    // ───────── Body Details (Overview tab) ─────────

    private void ShowBodyDetails()
    {
        if (_body == null || _titleLabel == null || _contentContainer == null)
            return;

        _titleLabel.Text = _body.BodyName;

        AddDetailHeader("Classification");
        AddDetailRow("Type", _body.Classification.TypeName);
        string? subtype = _body.Classification.SubtypeAsObject?.ToString();
        if (subtype != null)
            AddDetailRow("Subtype", subtype);

        AddSeparator();
        AddDetailHeader("Physical");
        AddDetailRow("Mass", $"{_body.Mass:F2}");
        AddDetailRow("Radius", $"{_body.Radius:F2}");
        AddDetailRow("Velocity", $"{_body.Velocity.Length():F2}");
        AddDetailRow("Position", $"({_body.BodyPosition.X:F1}, {_body.BodyPosition.Y:F1}, {_body.BodyPosition.Z:F1})");
        AddDetailRow("Dist from Origin", $"{_body.BodyPosition.Length():F2}");

        int bandCount = _body.GetBandCount();
        if (bandCount > 0)
        {
            AddSeparator();
            AddDetailHeader("Orbit Bands");
            AddDetailRow("Count", bandCount.ToString());
            AddDetailRow("Inner Radius", $"{_body.GetOrbitBandRadius(0):F2}");
            if (bandCount > 1)
                AddDetailRow("Outer Radius", $"{_body.GetOrbitBandRadius(bandCount - 1):F2}");

            int totalSats = 0;
            for (int i = 0; i < bandCount; i++)
                totalSats += _body.GetBandSatelliteCount(i);
            AddDetailRow("Satellites", totalSats.ToString());
        }

        if (_body.Mesh?.Continents != null && _body.Mesh.Continents.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Surface");
            int continentCount = _body.Mesh.Continents.Count;
            int totalCells = 0;
            int activeEco = 0;
            int totalBuildings = 0;
            int deficitCount = 0;
            float totalGen = 0;
            float totalUse = 0;
            foreach (var continent in _body.Mesh.Continents.Values)
            {
                totalCells += continent.cells.Count;
                if (continent.Economy != null)
                {
                    activeEco++;
                    totalBuildings += continent.Economy.ActiveBuildingCount;
                    totalGen += continent.Economy.PowerGeneration;
                    totalUse += continent.Economy.PowerConsumption;
                    if (continent.Economy.IsPowerDeficit)
                        deficitCount++;
                }
            }
            AddDetailRow("Continents", continentCount.ToString());
            AddDetailRow("Surface Cells", totalCells.ToString());
            AddDetailRow("Active Economies", activeEco.ToString());
            AddDetailRow("Total Buildings", totalBuildings.ToString());
            AddDetailRow("Power Gen / Use", $"{totalGen:F1} / {totalUse:F1}");
            AddDetailRow("Net Power", $"{(totalGen - totalUse):F1}");
            if (deficitCount > 0)
                AddAlertRow($"{deficitCount} CONTINENT(S) IN POWER DEFICIT");
        }

        if (_body.TransferMgr != null)
        {
            AddSeparator();
            AddDetailHeader("Logistics");
            AddDetailRow("Active Transfers", _body.TransferMgr.ActiveTransferCount.ToString());
        }
    }

    // ───────── Continent Details ─────────

    private void ShowContinentDetails(int continentIndex)
    {
        if (_body?.Mesh?.Continents == null || _titleLabel == null || _contentContainer == null)
            return;

        if (!_body.Mesh.Continents.TryGetValue(continentIndex, out var continent))
            return;

        _titleLabel.Text = $"Continent {continentIndex}";

        // Elevation & size
        AddDetailRow("Elevation Type", continent.elevation.ToString());
        AddDetailRow("Cells", continent.cells.Count.ToString());
        AddDetailRow("Avg Height", $"{continent.averageHeight:F2}");
        AddDetailRow("Avg Moisture", $"{continent.averageMoisture:F2}");

        // Economy
        if (continent.Economy != null)
        {
            var eco = continent.Economy;
            AddSeparator();
            AddDetailHeader("Economy");
            AddDetailRow("Power", $"{eco.PowerGeneration:F1} gen / {eco.PowerConsumption:F1} use");
            AddPercentRow("Power Stored", eco.PowerStored, eco.PowerStorageCapacity, mode: DonutChart.ColorMode.RedToGreen);
            AddDetailRow("Buildings", eco.ActiveBuildingCount.ToString());

            if (eco.IsPowerDeficit)
                AddAlertRow("POWER DEFICIT");

            // Top stockpiled resources
            var stockpiles = eco.GetAllStockpiles();
            if (stockpiles.Count > 0)
            {
                AddDetailHeader("Top Resources");
                int shown = 0;
                foreach (var kvp in stockpiles)
                {
                    if (kvp.Value > 0 && shown < 5)
                    {
                        AddDetailRow(kvp.Key, $"{kvp.Value:F1}");
                        shown++;
                    }
                }
            }
        }

        // Transfers from this continent
        if (_body.TransferMgr != null)
        {
            int activeFromHere = _body.TransferMgr.GetActiveTransferCountForContinent(continentIndex);
            if (activeFromHere > 0)
            {
                AddSeparator();
                AddDetailRow("Active Transfers", activeFromHere.ToString());
            }

            var schedules = _body.TransferMgr.GetSchedulesForContinent(continentIndex);
            if (schedules.Count > 0)
                AddDetailRow("Schedules", schedules.Count.ToString());
        }
    }

    // ───────── Station Details ─────────

    private void ShowStationDetails(int stationIndex)
    {
        if (_body?.SatellitesContainer == null || _titleLabel == null || _contentContainer == null)
            return;

        int idx = 0;
        foreach (var child in _body.SatellitesContainer.GetChildren())
        {
            if (child is StationSatellite station)
            {
                if (idx == stationIndex)
                {
                    _titleLabel.Text = station.Name;

                    AddDetailRow("Type", station.StationType);
                    AddDetailRow("Band", station.BandIndex.ToString());
                    AddDetailRow("Active", station.IsActive ? "Yes" : "No");
                    AddDetailRow("Can Build Ships", station.CanBuildShips ? "Yes" : "No");

                    if (station.IsUnderConstruction)
                    {
                        AddSeparator();
                        AddDetailHeader("Construction");
                        float progress = station.workDone / Mathf.Max(station.workRequired, 1f);
                        AddDetailRow("Progress", $"{progress * 100:F0}%");
                    }

                    if (station.Economy != null)
                    {
                        AddSeparator();
                        AddDetailHeader("Economy");
                        AddDetailRow("Power", $"{station.Economy.PowerGeneration:F1} / {station.Economy.PowerConsumption:F1}");
                        AddDetailRow("Buildings", station.Economy.ActiveBuildingCount.ToString());
                    }

                    // Open Station button for cross-window transition
                    AddSeparator();
                    int capturedIndex = stationIndex;
                    var openButton = new Button { Text = "Open Station" };
                    openButton.AddThemeFontSizeOverride("font_size", 13);
                    openButton.Pressed += () =>
                    {
                        OrbitalBodyWindow.Instance?.RequestStationInspect(capturedIndex);
                    };
                    _contentContainer?.AddChild(openButton);

                    return;
                }
                idx++;
            }
        }
    }

    // ───────── Logistics Unit Details ─────────

    private void ShowLogisticsUnitDetails(int unitIndex)
    {
        if (_body?.SatellitesContainer == null || _titleLabel == null || _contentContainer == null)
            return;

        int idx = 0;
        foreach (var child in _body.SatellitesContainer.GetChildren())
        {
            if (child is LogisticsUnit unit)
            {
                if (idx == unitIndex)
                {
                    _titleLabel.Text = unit.Name;

                    AddDetailRow("Type", unit.ShipDef?.Name ?? "Ship");
                    AddDetailRow("State", unit.State.ToString());
                    AddPercentRow("Fuel", unit.Fuel, unit.MaxFuel, "kg", DonutChart.ColorMode.RedToGreen);
                    AddDetailRow("Total Mass", $"{unit.GetTotalMass():F1} kg");

                    if (unit.CurrentEngine != null)
                        AddDetailRow("Engine Isp", $"{unit.CurrentEngine.EffectiveSpecificImpulse:F1} s");

                    // Open Unit button for cross-window transition
                    AddSeparator();
                    int capturedIndex = unitIndex;
                    var openButton = new Button { Text = "Inspect Ship" };
                    openButton.AddThemeFontSizeOverride("font_size", 13);
                    openButton.Pressed += () =>
                    {
                        OrbitalBodyWindow.Instance?.RequestLogisticsUnitInspect(capturedIndex);
                    };
                    _contentContainer?.AddChild(openButton);

                    return;
                }
                idx++;
            }
        }
    }

    // ───────── Economy Summary (Economies tab default) ─────────

    private void ShowEconomySummary()
    {
        if (_body == null || _titleLabel == null || _contentContainer == null)
            return;

        _titleLabel.Text = "Body Economy Summary";

        var economyMgr = _body.EconomyMgr;
        if (economyMgr == null)
        {
            AddDetailRow("Status", "Economy manager not available");
            return;
        }

        AddDetailHeader("Continent Economies");
        AddDetailRow("Active Economies", economyMgr.ActiveEconomyCount.ToString());
        AddDetailRow("Total Buildings", economyMgr.GetTotalBuildingCount().ToString());
        AddDetailRow("Power Generation", $"{economyMgr.GetTotalPowerGeneration():F1}/s");
        AddDetailRow("Power Consumption", $"{economyMgr.GetTotalPowerConsumption():F1}/s");
        AddDetailRow("Net Power", $"{(economyMgr.GetTotalPowerGeneration() - economyMgr.GetTotalPowerConsumption()):F1}/s");

        int deficitCount = economyMgr.GetPowerDeficitCount();
        if (deficitCount > 0)
            AddAlertRow($"{deficitCount} continent(s) in power deficit");

        AddSeparator();
        AddDetailHeader("Station Economies");
        AddDetailRow("Active Economies", economyMgr.ActiveStationEconomyCount.ToString());

        // Count station buildings and power
        int stationBuildings = 0;
        float stationPowerGen = 0f;
        float stationPowerUse = 0f;
        int stationDeficits = 0;

        if (_body.SatellitesContainer != null)
        {
            foreach (var child in _body.SatellitesContainer.GetChildren())
            {
                if (child is StationSatellite station && station.Economy != null)
                {
                    var eco = station.Economy;
                    stationBuildings += eco.ActiveBuildingCount;
                    stationPowerGen += eco.PowerGeneration;
                    stationPowerUse += eco.PowerConsumption;
                    if (eco.IsPowerDeficit)
                        stationDeficits++;
                }
            }
        }

        AddDetailRow("Total Buildings", stationBuildings.ToString());
        AddDetailRow("Power Generation", $"{stationPowerGen:F1}/s");
        AddDetailRow("Power Consumption", $"{stationPowerUse:F1}/s");
        AddDetailRow("Net Power", $"{(stationPowerGen - stationPowerUse):F1}/s");

        if (stationDeficits > 0)
            AddAlertRow($"{stationDeficits} station(s) in power deficit");

        // Aggregate resource stockpiles across all continent economies
        AddSeparator();
        AddDetailHeader("Resource Stockpiles (Continents)");

        var aggregatedStockpiles = new System.Collections.Generic.Dictionary<string, float>();
        if (_body.Mesh?.Continents != null)
        {
            foreach (var continent in _body.Mesh.Continents.Values)
            {
                if (continent.Economy != null)
                {
                    foreach (var kvp in continent.Economy.GetAllStockpiles())
                    {
                        if (aggregatedStockpiles.ContainsKey(kvp.Key))
                            aggregatedStockpiles[kvp.Key] += kvp.Value;
                        else
                            aggregatedStockpiles[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        if (aggregatedStockpiles.Count > 0)
        {
            int shown = 0;
            foreach (var kvp in aggregatedStockpiles.OrderByDescending(k => k.Value))
            {
                if (kvp.Value > 0 && shown < 8)
                {
                    AddDetailRow(kvp.Key, $"{kvp.Value:F1}");
                    shown++;
                }
            }
        }
        else
        {
            AddDetailRow("Status", "No resources stockpiled");
        }
    }

    // ───────── Continent Economy Details ─────────

    private void ShowContinentEconomyDetails(int continentIndex)
    {
        if (_body?.Mesh?.Continents == null || _titleLabel == null || _contentContainer == null)
            return;

        if (!_body.Mesh.Continents.TryGetValue(continentIndex, out var continent))
            return;

        _titleLabel.Text = $"Continent {continentIndex} Economy";

        if (continent.Economy == null)
        {
            AddDetailRow("Status", "No economy initialized");
            AddDetailRow("Cells", continent.cells.Count.ToString());
            return;
        }

        var eco = continent.Economy;

        AddDetailHeader("Power");
        AddDetailRow("Generation", $"{eco.PowerGeneration:F1}/s");
        AddDetailRow("Consumption", $"{eco.PowerConsumption:F1}/s");
        AddDetailRow("Net", $"{(eco.PowerGeneration - eco.PowerConsumption):F1}/s");
        AddPercentRow("Stored", eco.PowerStored, eco.PowerStorageCapacity, mode: DonutChart.ColorMode.RedToGreen);

        if (eco.IsPowerDeficit)
            AddAlertRow("POWER DEFICIT - Buildings paused");

        AddSeparator();
        AddDetailHeader("Buildings");
        AddDetailRow("Active Buildings", eco.ActiveBuildingCount.ToString());

        // Group buildings by type
        var buildingGroups = new System.Collections.Generic.Dictionary<string, int>();
        int pausedCount = 0;
        foreach (var reg in eco.ActiveBuildings)
        {
            string typeName = reg.BuildingNode.Definition?.DisplayName ?? "Unknown";
            if (buildingGroups.ContainsKey(typeName))
                buildingGroups[typeName]++;
            else
                buildingGroups[typeName] = 1;

            if (reg.IsPaused)
                pausedCount++;
        }

        if (pausedCount > 0)
            AddDetailRow("Paused Buildings", pausedCount.ToString());

        if (buildingGroups.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Building Types");
            foreach (var kvp in buildingGroups.OrderByDescending(k => k.Value))
            {
                AddDetailRow(kvp.Key, kvp.Value.ToString());
            }
        }

        // Stockpiles
        var stockpiles = eco.GetAllStockpiles();
        if (stockpiles.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Resource Stockpiles");

            int shown = 0;
            foreach (var kvp in stockpiles.OrderByDescending(k => k.Value))
            {
                if (kvp.Value > 0 && shown < 10)
                {
                    AddDetailRow(kvp.Key, $"{kvp.Value:F1}");
                    shown++;
                }
            }
        }

        // Production rates
        var netRates = eco.GetAllNetRates();
        if (netRates.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Production Rates");

            foreach (var kvp in netRates.OrderByDescending(k => k.Value))
            {
                if (System.MathF.Abs(kvp.Value) > 0.01f)
                {
                    string rateStr = kvp.Value >= 0 ? $"+{kvp.Value:F2}/s" : $"{kvp.Value:F2}/s";
                    AddDetailRow(kvp.Key, rateStr);
                }
            }
        }

        // Transfers
        if (_body.TransferMgr != null)
        {
            int activeTransfers = _body.TransferMgr.GetActiveTransferCountForContinent(continentIndex);
            var schedules = _body.TransferMgr.GetSchedulesForContinent(continentIndex);

            if (activeTransfers > 0 || schedules.Count > 0)
            {
                AddSeparator();
                AddDetailHeader("Logistics");
                if (activeTransfers > 0)
                    AddDetailRow("Active Transfers", activeTransfers.ToString());
                if (schedules.Count > 0)
                    AddDetailRow("Schedules", schedules.Count.ToString());
            }
        }

        // Button to open full continent window
        AddSeparator();
        var openButton = new Button { Text = "Open Continent Details" };
        openButton.AddThemeFontSizeOverride("font_size", 13);
        openButton.Pressed += () =>
        {
            OrbitalBodyWindow.Instance?.RequestContinentInspect(continentIndex);
        };
        _contentContainer.AddChild(openButton);
    }

    // ───────── Station Economy Details ─────────

    private void ShowStationEconomyDetails(int stationIndex)
    {
        if (_body?.SatellitesContainer == null || _titleLabel == null || _contentContainer == null)
            return;

        int idx = 0;
        foreach (var child in _body.SatellitesContainer.GetChildren())
        {
            if (child is StationSatellite station && station.Economy != null)
            {
                if (idx == stationIndex)
                {
                    _titleLabel.Text = $"{station.Name} Economy";

                    var eco = station.Economy;

                    AddDetailHeader("Power");
                    AddDetailRow("Generation", $"{eco.PowerGeneration:F1}/s");
                    AddDetailRow("Consumption", $"{eco.PowerConsumption:F1}/s");
                    AddDetailRow("Net", $"{(eco.PowerGeneration - eco.PowerConsumption):F1}/s");
                    AddPercentRow("Stored", eco.PowerStored, eco.PowerStorageCapacity, mode: DonutChart.ColorMode.RedToGreen);

                    if (eco.IsPowerDeficit)
                        AddAlertRow("POWER DEFICIT - Buildings paused");

                    AddSeparator();
                    AddDetailHeader("Buildings");
                    AddDetailRow("Active Buildings", eco.ActiveBuildingCount.ToString());

                    // Group buildings by type
                    var buildingGroups = new System.Collections.Generic.Dictionary<string, int>();
                    int pausedCount = 0;
                    foreach (var reg in eco.ActiveBuildings)
                    {
                        string typeName = reg.BuildingNode.Definition?.DisplayName ?? "Unknown";
                        if (buildingGroups.ContainsKey(typeName))
                            buildingGroups[typeName]++;
                        else
                            buildingGroups[typeName] = 1;

                        if (reg.IsPaused)
                            pausedCount++;
                    }

                    if (pausedCount > 0)
                        AddDetailRow("Paused Buildings", pausedCount.ToString());

                    if (buildingGroups.Count > 0)
                    {
                        AddSeparator();
                        AddDetailHeader("Building Types");
                        foreach (var kvp in buildingGroups.OrderByDescending(k => k.Value))
                        {
                            AddDetailRow(kvp.Key, kvp.Value.ToString());
                        }
                    }

                    // Stockpiles
                    var stockpiles = eco.GetAllStockpiles();
                    if (stockpiles.Count > 0)
                    {
                        AddSeparator();
                        AddDetailHeader("Resource Stockpiles");

                        int shown = 0;
                        foreach (var kvp in stockpiles.OrderByDescending(k => k.Value))
                        {
                            if (kvp.Value > 0 && shown < 8)
                            {
                                AddDetailRow(kvp.Key, $"{kvp.Value:F1}");
                                shown++;
                            }
                        }
                    }

                    // Button to open full station window
                    AddSeparator();
                    var openButton = new Button { Text = "Open Station Details" };
                    openButton.AddThemeFontSizeOverride("font_size", 13);
                    openButton.Pressed += () =>
                    {
                        OrbitalBodyWindow.Instance?.RequestStationInspect(stationIndex);
                    };
                    _contentContainer.AddChild(openButton);

                    return;
                }
                idx++;
            }
        }
    }

    // ───────── Transfer Details ─────────

    private void ShowTransferDetails(string type, int index)
    {
        if (_titleLabel == null || _contentContainer == null)
            return;

        _titleLabel.Text = type == "schedule" ? "Transfer Schedule" : "Transfer";

        // For now, show basic info - can be expanded when BodyTransferManager
        // exposes active transfer details
        AddDetailRow("Type", type);
        AddDetailRow("Index", index.ToString());
    }

    // ───────── Helpers ─────────

    private void ClearContent() => DetailRowBuilder.Clear(_contentContainer);

    private void AddDetailRow(string key, string value)
        => DetailRowBuilder.AddRow(_contentContainer, key, value);

    private void AddPercentRow(string key, float current, float max, string? unit = null,
        DonutChart.ColorMode mode = DonutChart.ColorMode.GreenToRed)
        => DetailRowBuilder.AddPercentRow(_contentContainer, key, current, max, unit, "F1", mode);

    private void AddDetailHeader(string text)
        => DetailRowBuilder.AddHeader(_contentContainer, text);

    private void AddAlertRow(string message)
        => DetailRowBuilder.AddAlert(_contentContainer, message);

    private void AddSeparator()
        => DetailRowBuilder.AddSeparator(_contentContainer);
}
