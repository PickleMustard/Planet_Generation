using Constructables;
using Constructables.Power;
using Constructables.Stations.Behaviors;
using Godot;
using ProceduralGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using System.Collections.Generic;
using System.Linq;
using UI.Components;
using UI.GridWindow;
using UtilityLibrary;

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
    private OrbitalBodyTabbedPanel? _tabbedPanel;
    private string? _lastItemType;
    private int _lastItemIndex = -1;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBoxContainer/Ribbon/HBox/DetailTitleLabel");
        _contentContainer = GetNode<VBoxContainer>("VBoxContainer/DetailScroll/DetailContent");
    }

    public void Initialize(IOrbitalBody body, OrbitalBodyTabbedPanel tabbedPanel)
    {
        _body = body;
        _tabbedPanel = tabbedPanel;
        tabbedPanel.ItemSelected += OnItemSelected;
    }

    public void Disconnect(OrbitalBodyTabbedPanel tabbedPanel)
    {
        tabbedPanel.ItemSelected -= OnItemSelected;
        if (_tabbedPanel == tabbedPanel)
            _tabbedPanel = null;
    }

    public void Clear()
    {
        _body = null;
        _tabbedPanel = null;
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
        // Planet Board opens its own window instead of populating the details pane.
        if (itemType == "planet_board")
        {
            if (_body is CelestialBody cb)
                SignalBus.Instance?.EmitOpenPlanetBoardRequested(cb, "ResourceLink");
            return;
        }

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
            case "economy":
            case "economy_summary":
                ShowEconomySummary();
                break;
            case "resources":
                ShowResourcesSummary();
                break;
            case "geography":
                ShowGeographySummary();
                break;
            case "building":
                ShowBuildingDetails(itemIndex);
                break;
            case "grid":
                ShowGridDetails(itemIndex);
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

        int totalActive = _body.GetTotalActiveTransfers();
        if (totalActive > 0)
        {
            AddSeparator();
            AddDetailHeader("Logistics");
            AddDetailRow("Active Transfers", totalActive.ToString());
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

        // Transfers from hubs on this continent (per-building rollup).
        int activeFromHere = _body.GetActiveTransferCountOnContinent(continentIndex);
        int scheduleCount = _body.GetSchedulesOnContinent(continentIndex).Count;
        if (activeFromHere > 0)
        {
            AddSeparator();
            AddDetailRow("Active Transfers", activeFromHere.ToString());
        }
        if (scheduleCount > 0)
            AddDetailRow("Schedules", scheduleCount.ToString());
    }

    // ───────── Station Details ─────────

    private void ShowStationDetails(int stationIndex)
    {
        if (_titleLabel == null || _contentContainer == null)
            return;

        var station = _tabbedPanel?.GetStationByIndex(stationIndex);
        if (station == null)
            return;

        _titleLabel.Text = station.Name;

        string typeName = !string.IsNullOrEmpty(station.StationType)
            ? station.StationType!
            : station.GetBehavior<ShipyardBehavior>() != null ? "Shipyard"
            : station.GetBehavior<OrbitalConstructorBehavior>() != null ? "Orbital Architect"
            : station.GetBehavior<TransferHubBehavior>() != null ? "Refinery"
            : "Station";
        AddDetailRow("Type", typeName);
        AddDetailRow("Band", station.BandIndex.ToString());
        AddDetailRow("Active", station.IsActive ? "Yes" : "No");
        AddDetailRow("Can Build Ships", station.GetBehavior<ShipyardBehavior>() != null ? "Yes" : "No");

        if (station.IsUnderConstruction)
        {
            AddSeparator();
            AddDetailHeader("Construction");
            float progress = station.workDone / Mathf.Max(station.workRequired, 1f);
            AddDetailRow("Progress", $"{progress * 100:F0}%");
        }

        AddSeparator();
        int captured = stationIndex;
        var openButton = new Button { Text = "Open Station" };
        openButton.AddThemeFontSizeOverride("font_size", 13);
        openButton.Pressed += () => OrbitalBodyWindow.Instance?.RequestStationInspect(captured);
        _contentContainer.AddChild(openButton);
    }

    // ───────── Logistics Unit Details ─────────

    private void ShowLogisticsUnitDetails(int unitIndex)
    {
        if (_titleLabel == null || _contentContainer == null)
            return;

        var unit = _tabbedPanel?.GetLogisticsUnitByIndex(unitIndex);
        if (unit == null)
            return;

        _titleLabel.Text = unit.Name;

        AddDetailRow("Type", unit.ShipDef?.Name ?? "Ship");
        AddDetailRow("State", unit.State.ToString());
        AddPercentRow("Fuel", unit.Fuel, unit.MaxFuel, "kg", DonutChart.ColorMode.RedToGreen);
        AddDetailRow("Total Mass", $"{unit.GetTotalMass():F1} kg");

        if (unit.CurrentEngine != null)
            AddDetailRow("Engine Isp", $"{unit.CurrentEngine.EffectiveSpecificImpulse:F1} s");

        AddSeparator();
        int captured = unitIndex;
        var openButton = new Button { Text = "Inspect Ship" };
        openButton.AddThemeFontSizeOverride("font_size", 13);
        openButton.Pressed += () => OrbitalBodyWindow.Instance?.RequestLogisticsUnitInspect(captured);
        _contentContainer.AddChild(openButton);
    }

    // ───────── Resources Summary (Overview → Resources) ─────────

    private void ShowResourcesSummary()
    {
        if (_body == null || _titleLabel == null || _contentContainer == null)
            return;

        _titleLabel.Text = "Resource Stockpiles";

        var totals = new Dictionary<string, float>();

        // Buildings: aggregate via BuildingResourceEndpoint (constructed per-building on demand).
        foreach (var b in _body.GetAllBuildings())
        {
            var endpoint = new Constructables.Buildings.BuildingResourceEndpoint(b);
            foreach (var kvp in endpoint.GetAllStockpiles())
            {
                if (!totals.ContainsKey(kvp.Key))
                    totals[kvp.Key] = 0f;
                totals[kvp.Key] += kvp.Value;
            }
        }

        // Stations: aggregate via the station's ResourceEndpoint.
        if (_body.SatellitesContainer != null)
        {
            foreach (var child in _body.SatellitesContainer.GetChildren())
            {
                if (child is StationSatellite s)
                {
                    foreach (var kvp in s.ResourceEndpoint.GetAllStockpiles())
                    {
                        if (!totals.ContainsKey(kvp.Key))
                            totals[kvp.Key] = 0f;
                        totals[kvp.Key] += kvp.Value;
                    }
                }
            }
        }

        if (totals.Count == 0)
        {
            AddDetailRow("Status", "No resources stockpiled");
            return;
        }

        AddDetailHeader("Aggregate Stockpiles");
        int shown = 0;
        foreach (var kvp in totals.OrderByDescending(k => k.Value))
        {
            if (kvp.Value <= 0f)
                continue;
            AddDetailRow(kvp.Key, $"{kvp.Value:F1}");
            if (++shown >= 16)
                break;
        }
    }

    // ───────── Geography Summary (Overview → Geography) ─────────

    private void ShowGeographySummary()
    {
        if (_body == null || _titleLabel == null || _contentContainer == null)
            return;

        _titleLabel.Text = "Geography";

        var continents = _body.Mesh?.Continents;
        if (continents == null || continents.Count == 0)
        {
            AddDetailRow("Status", "No surface data");
            return;
        }

        int totalCells = 0;
        float sumHeight = 0f;
        float sumMoisture = 0f;
        var elevationGroups = new Dictionary<Continent.CRUST_TYPE, int>();

        foreach (var c in continents.Values)
        {
            totalCells += c.cells.Count;
            sumHeight += c.averageHeight;
            sumMoisture += c.averageMoisture;
            if (!elevationGroups.ContainsKey(c.elevation))
                elevationGroups[c.elevation] = 0;
            elevationGroups[c.elevation]++;
        }

        AddDetailHeader("Surface");
        AddDetailRow("Continents", continents.Count.ToString());
        AddDetailRow("Surface Cells", totalCells.ToString());
        AddDetailRow("Avg Height", $"{sumHeight / continents.Count:F2}");
        AddDetailRow("Avg Moisture", $"{sumMoisture / continents.Count:F2}");

        AddSeparator();
        AddDetailHeader("Continent Elevation");
        foreach (var kvp in elevationGroups.OrderByDescending(k => k.Value))
            AddDetailRow(kvp.Key.ToString(), kvp.Value.ToString());
    }

    // ───────── Building Details (Buildings tab) ─────────

    private void ShowBuildingDetails(int buildingIndex)
    {
        if (_titleLabel == null || _contentContainer == null)
            return;

        var b = _tabbedPanel?.GetBuildingByIndex(buildingIndex);
        if (b == null)
            return;

        _titleLabel.Text = b.Name;

        AddDetailRow("Category", b.Definition?.Category ?? "—");
        AddDetailRow("Type", b.Definition?.DisplayName ?? "—");
        AddDetailRow("Recipe", b.ActiveRecipeId ?? "—");
        AddDetailRow("Powered", b.PoweredOn ? "Yes" : "No");
        if (b.PrimaryCell != null)
            AddDetailRow("Cell", b.PrimaryCell.Index.ToString());

        if (b.IsUnderConstruction)
        {
            AddSeparator();
            AddDetailHeader("Construction");
            float progress = b.workDone / Mathf.Max(b.workRequired, 1f);
            AddDetailRow("Progress", $"{progress * 100:F0}%");
        }

        AddSeparator();
        var openButton = new Button { Text = "Open Building" };
        openButton.AddThemeFontSizeOverride("font_size", 13);
        openButton.Pressed += () => OrbitalBodyWindow.Instance?.RequestBuildingInspect(b);
        _contentContainer.AddChild(openButton);
    }

    // ───────── Power Grid Details (Grids tab) ─────────

    private void ShowGridDetails(int gridIndex)
    {
        if (_titleLabel == null || _contentContainer == null)
            return;

        var g = _tabbedPanel?.GetGridByIndex(gridIndex);
        if (g == null)
            return;

        _titleLabel.Text = $"Grid {g.Id}";

        AddDetailHeader("Power");
        AddDetailRow("Generation", $"{g.LastGeneration:F1}/s");
        AddDetailRow("Draw", $"{g.LastDraw:F1}/s");
        AddDetailRow("Net", $"{(g.LastGeneration - g.LastDraw):F1}/s");
        AddPercentRow("Battery", g.BatteryStored, g.BatteryCapacity, "kWh", DonutChart.ColorMode.RedToGreen);

        AddSeparator();
        AddDetailHeader("Members");
        AddDetailRow("Contributors", g.Contributors.Count.ToString());
        AddDetailRow("Consumers", g.Consumers.Count.ToString());
        AddDetailRow("Covered Cells", g.CoveredCells.Count.ToString());

        if (g.IsBrownedOut)
        {
            AddSeparator();
            AddAlertRow("GRID IN BROWNOUT");
        }

        AddSeparator();
        var openButton = new Button { Text = "Open Grid" };
        openButton.AddThemeFontSizeOverride("font_size", 13);
        openButton.Pressed += () => GridDetailWindow.Instance?.ShowGrid(g);
        _contentContainer.AddChild(openButton);
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
        AddDetailRow("Total Buildings", economyMgr.GetTotalBuildingCount().ToString());
        AddDetailRow("Power Generation", $"{economyMgr.GetTotalPowerGeneration():F1}/s");
        AddDetailRow("Power Consumption", $"{economyMgr.GetTotalPowerConsumption():F1}/s");
        AddDetailRow("Net Power", $"{(economyMgr.GetTotalPowerGeneration() - economyMgr.GetTotalPowerConsumption()):F1}/s");

        int deficitCount = economyMgr.GetPowerDeficitCount();
        if (deficitCount > 0)
            AddAlertRow($"{deficitCount} continent(s) in power deficit");

        AddSeparator();
        AddDetailHeader("Station Economies");

        // Count station buildings and power
        int stationBuildings = 0;
        float stationPowerGen = 0f;
        float stationPowerUse = 0f;
        int stationDeficits = 0;

        if (_body.SatellitesContainer != null)
        {
            foreach (var child in _body.SatellitesContainer.GetChildren())
            {
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

        AddSeparator();
        AddDetailHeader("Buildings");

        // Group buildings by type
        var buildingGroups = new System.Collections.Generic.Dictionary<string, int>();
        int pausedCount = 0;
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

        // Transfers (per-building rollup across hubs on this continent).
        int activeTransfers = _body.GetActiveTransferCountOnContinent(continentIndex);
        int scheduleCount = _body.GetSchedulesOnContinent(continentIndex).Count;

        if (activeTransfers > 0 || scheduleCount > 0)
        {
            AddSeparator();
            AddDetailHeader("Logistics");
            if (activeTransfers > 0)
                AddDetailRow("Active Transfers", activeTransfers.ToString());
            if (scheduleCount > 0)
                AddDetailRow("Schedules", scheduleCount.ToString());
        }
    }

    // ───────── Station Economy Details ─────────

    private void ShowStationEconomyDetails(int stationIndex)
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
                    _titleLabel.Text = $"{station.Name} Economy";

                    AddSeparator();
                    AddDetailHeader("Buildings");

                    // Group buildings by type
                    var buildingGroups = new System.Collections.Generic.Dictionary<string, int>();
                    int pausedCount = 0;

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

        // For now, show basic info - can be expanded when TransferStationBehavior
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
