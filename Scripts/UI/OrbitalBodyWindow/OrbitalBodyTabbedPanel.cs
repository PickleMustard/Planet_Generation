using Constructables;
using Godot;
using Structures.GameState;

namespace UI;

/// <summary>
/// Right-side tabbed panel with Overview, Continents, Stations, Economies, Transfers tabs.
/// Switching tabs populates the content area; clicking items emits ItemSelected.
/// </summary>
public partial class OrbitalBodyTabbedPanel : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    private IOrbitalBody? _body;
    private int _activeTabIndex = -1;
    private Button[]? _tabButtons;

    public int ActiveTabIndex => _activeTabIndex;
    private VBoxContainer? _contentContainer;

    // Tab style resources (loaded from scene)
    private StyleBox? _activeTabStyle;
    private StyleBox? _inactiveTabStyle;

    public override void _Ready()
    {
        var tabBar = GetNode<HBoxContainer>("VBoxContainer/TabBar");
        _contentContainer = GetNode<VBoxContainer>("VBoxContainer/TabContent/ContentContainer");

        _tabButtons = new Button[5];
        _tabButtons[0] = tabBar.GetNode<Button>("OverviewBtn");
        _tabButtons[1] = tabBar.GetNode<Button>("ContinentsBtn");
        _tabButtons[2] = tabBar.GetNode<Button>("StationsBtn");
        _tabButtons[3] = tabBar.GetNode<Button>("EconomiesBtn");
        _tabButtons[4] = tabBar.GetNode<Button>("TransfersBtn");

        // Cache styles from the first two buttons
        _activeTabStyle = _tabButtons[0].GetThemeStylebox("normal");
        _inactiveTabStyle = _tabButtons[1].GetThemeStylebox("normal");

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int idx = i; // capture
            _tabButtons[i].Pressed += () => SwitchTab(idx);
        }
    }

    public void Initialize(IOrbitalBody body)
    {
        _body = body;
        SwitchTab(0); // Default to Overview
    }

    public void Clear()
    {
        _body = null;
        ClearContent();
        _activeTabIndex = -1;
    }

    public void SwitchTab(int tabIndex)
    {
        if (tabIndex == _activeTabIndex)
            return;

        _activeTabIndex = tabIndex;
        UpdateTabButtonStyles();
        ClearContent();

        if (_body == null || _contentContainer == null)
            return;

        switch (tabIndex)
        {
            case 0: PopulateOverview(); break;
            case 1: PopulateContinents(); break;
            case 2: PopulateStations(); break;
            case 3: PopulateEconomies(); break;
            case 4: PopulateTransfers(); break;
        }

        // Overview tab has no clickable rows — surface body-level details in
        // the DetailsPanel automatically so it isn't empty.
        if (tabIndex == 0)
            EmitSignal(SignalName.ItemSelected, "body", 0);
        // Economies tab defaults to body economy summary
        else if (tabIndex == 3)
            EmitSignal(SignalName.ItemSelected, "economy_summary", 0);
    }

    private void UpdateTabButtonStyles()
    {
        if (_tabButtons == null || _activeTabStyle == null || _inactiveTabStyle == null)
            return;

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            _tabButtons[i].AddThemeStyleboxOverride("normal",
                i == _activeTabIndex ? _activeTabStyle : _inactiveTabStyle);
        }
    }

    private void ClearContent()
    {
        if (_contentContainer == null) return;
        foreach (var child in _contentContainer.GetChildren())
            child.QueueFree();
    }

    // ───────── Overview Tab ─────────

    private void PopulateOverview()
    {
        if (_body == null || _contentContainer == null) return;

        AddSectionHeader("Physical Properties");
        AddInfoRow("Name", _body.BodyName);
        AddInfoRow("Type", _body.Classification.TypeName);

        string? subtype = _body.Classification.SubtypeAsObject?.ToString();
        if (subtype != null)
            AddInfoRow("Subtype", subtype);

        AddInfoRow("Mass", $"{_body.Mass:F2}");
        AddInfoRow("Radius", $"{_body.Radius:F2}");
        AddInfoRow("Velocity", $"{_body.Velocity.Length():F2}");
        AddInfoRow("Distance from Origin", $"{_body.BodyPosition.Length():F2}");

        // Orbital bands
        if (_body.OrbitBands != null && _body.OrbitBands.Count > 0)
        {
            AddSectionHeader("Orbital System");
            int bandCount = _body.GetBandCount();
            AddInfoRow("Orbit Bands", bandCount.ToString());

            if (bandCount > 0)
            {
                float innerR = _body.GetOrbitBandRadius(0);
                float outerR = _body.GetOrbitBandRadius(bandCount - 1);
                AddInfoRow("Inner Band Radius", $"{innerR:F2}");
                if (bandCount > 1)
                    AddInfoRow("Outer Band Radius", $"{outerR:F2}");
            }

            int totalSats = 0;
            for (int i = 0; i < bandCount; i++)
                totalSats += _body.GetBandSatelliteCount(i);
            AddInfoRow("Satellites", totalSats.ToString());
        }

        // Surface summary (mesh / continents)
        if (_body.Mesh?.Continents != null && _body.Mesh.Continents.Count > 0)
        {
            AddSectionHeader("Surface");
            int continentCount = _body.Mesh.Continents.Count;
            int totalCells = 0;
            int continentsWithEconomy = 0;
            foreach (var continent in _body.Mesh.Continents.Values)
            {
                totalCells += continent.cells.Count;
                if (continent.Economy != null)
                    continentsWithEconomy++;
            }
            AddInfoRow("Continents", continentCount.ToString());
            AddInfoRow("Surface Cells", totalCells.ToString());
            AddInfoRow("Active Economies", continentsWithEconomy.ToString());
        }

        // Economy summary across continents
        if (_body.Mesh?.Continents != null && _body.Mesh.Continents.Count > 0)
        {
            AddSectionHeader("Economy Summary");
            float totalPowerGen = 0;
            float totalPowerCon = 0;
            int totalBuildings = 0;
            int deficitContinents = 0;

            foreach (var continent in _body.Mesh.Continents.Values)
            {
                if (continent.Economy != null)
                {
                    totalPowerGen += continent.Economy.PowerGeneration;
                    totalPowerCon += continent.Economy.PowerConsumption;
                    totalBuildings += continent.Economy.ActiveBuildingCount;
                    if (continent.Economy.IsPowerDeficit)
                        deficitContinents++;
                }
            }

            AddInfoRow("Total Buildings", totalBuildings.ToString());
            AddInfoRow("Power Gen", $"{totalPowerGen:F1}");
            AddInfoRow("Power Use", $"{totalPowerCon:F1}");
            AddInfoRow("Net Power", $"{(totalPowerGen - totalPowerCon):F1}");
            if (deficitContinents > 0)
                AddInfoRow("Power Deficit Continents", deficitContinents.ToString());
        }

        // Active transfers
        if (_body.TransferMgr != null)
        {
            AddSectionHeader("Logistics");
            AddInfoRow("Active Transfers", _body.TransferMgr.ActiveTransferCount.ToString());
        }
    }

    // ───────── Continents Tab ─────────

    private void PopulateContinents()
    {
        if (_body == null || _contentContainer == null) return;

        if (_body.Mesh?.Continents == null || _body.Mesh.Continents.Count == 0)
        {
            AddEmptyLabel("No continents on this body");
            return;
        }

        foreach (var kvp in _body.Mesh.Continents)
        {
            int index = kvp.Key;
            var continent = kvp.Value;

            var row = CreateClickableRow(index, "continent");

            string elevation = continent.elevation.ToString();
            int cellCount = continent.cells.Count;
            int buildings = 0;
            foreach (var cell in continent.cells)
            {
                if (cell.Building != null) buildings++;
            }

            var label = new Label
            {
                Text = $"Continent {index}  |  {elevation}  |  {cellCount} cells  |  {buildings} buildings",
            };
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
            label.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(label);

            bool hasEconomy = continent.Economy != null;
            if (hasEconomy && continent.Economy!.IsPowerDeficit)
            {
                var warning = new Label { Text = " [POWER DEFICIT]" };
                warning.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.3f));
                warning.AddThemeFontSizeOverride("font_size", 12);
                row.AddChild(warning);
            }

            _contentContainer.AddChild(row);
        }
    }

    // ───────── Stations Tab ─────────

    private void PopulateStations()
    {
        if (_body == null || _contentContainer == null) return;

        int stationIndex = 0;
        bool found = false;

        if (_body.SatellitesContainer != null)
        {
            foreach (var child in _body.SatellitesContainer.GetChildren())
            {
                if (child is StationSatellite station)
                {
                    found = true;
                    var row = CreateClickableRow(stationIndex, "station");

                    string status = station.IsUnderConstruction ? "Building..." : (station.IsActive ? "Active" : "Inactive");
                    var label = new Label
                    {
                        Text = $"{station.Name}  |  {station.StationType}  |  Band {station.BandIndex}  |  {status}",
                    };
                    label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                    label.AddThemeFontSizeOverride("font_size", 13);
                    row.AddChild(label);

                    _contentContainer.AddChild(row);
                    stationIndex++;
                }
            }
        }

        if (!found)
            AddEmptyLabel("No stations in orbit");

        // Logistics units in orbit
        int unitIndex = 0;
        bool unitsFound = false;

        if (_body.SatellitesContainer != null)
        {
            foreach (var child in _body.SatellitesContainer.GetChildren())
            {
                if (child is LogisticsUnit unit)
                {
                    if (!unitsFound)
                    {
                        AddSectionHeader("Ships");
                        unitsFound = true;
                    }

                    var row = CreateClickableRow(unitIndex, "logistics_unit");

                    string state = unit.State.ToString();
                    string typeName = unit.ShipDef?.Name ?? "Ship";
                    var label = new Label
                    {
                        Text = $"{unit.Name}  |  {typeName}  |  {state}",
                    };
                    label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                    label.AddThemeFontSizeOverride("font_size", 13);
                    row.AddChild(label);

                    _contentContainer.AddChild(row);
                    unitIndex++;
                }
            }
        }
    }

    // ───────── Economies Tab ─────────

    private void PopulateEconomies()
    {
        if (_body == null || _contentContainer == null) return;

        // Body-level economy summary
        AddSectionHeader("Body Economy Summary");

        var economyMgr = _body.EconomyMgr;
        if (economyMgr != null)
        {
            float totalGen = economyMgr.GetTotalPowerGeneration();
            float totalUse = economyMgr.GetTotalPowerConsumption();
            int totalBuildings = economyMgr.GetTotalBuildingCount();
            int deficitCount = economyMgr.GetPowerDeficitCount();

            AddInfoRow("Continent Economies", economyMgr.ActiveEconomyCount.ToString());
            AddInfoRow("Station Economies", economyMgr.ActiveStationEconomyCount.ToString());
            AddInfoRow("Total Buildings", totalBuildings.ToString());
            AddInfoRow("Power Generation", $"{totalGen:F1}/s");
            AddInfoRow("Power Consumption", $"{totalUse:F1}/s");
            AddInfoRow("Net Power", $"{(totalGen - totalUse):F1}/s");

            if (deficitCount > 0)
            {
                var deficitLabel = new Label { Text = $" [!] {deficitCount} continent(s) in power deficit" };
                deficitLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.3f));
                deficitLabel.AddThemeFontSizeOverride("font_size", 13);
                _contentContainer.AddChild(deficitLabel);
            }
        }
        else
        {
            AddEmptyLabel("Economy manager not available");
        }

        // Continent economies list
        if (_body.Mesh?.Continents != null && _body.Mesh.Continents.Count > 0)
        {
            AddSectionHeader($"Continents ({_body.Mesh.Continents.Count})");

            foreach (var kvp in _body.Mesh.Continents)
            {
                int index = kvp.Key;
                var continent = kvp.Value;

                var row = CreateClickableRow(index, "continent_economy");

                string name = $"Continent {index}";
                string status = "";
                Color textColor = new Color(0.85f, 0.85f, 0.9f);

                if (continent.Economy != null)
                {
                    int buildings = continent.Economy.ActiveBuildingCount;
                    float netPower = continent.Economy.PowerGeneration - continent.Economy.PowerConsumption;
                    string powerStr = netPower >= 0 ? $"+{netPower:F1}" : $"{netPower:F1}";

                    status = $" | {buildings} buildings | {powerStr}/s";

                    if (continent.Economy.IsPowerDeficit)
                    {
                        var warning = new Label { Text = " [POWER DEFICIT]" };
                        warning.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.3f));
                        warning.AddThemeFontSizeOverride("font_size", 12);
                        row.AddChild(warning);
                    }
                }
                else
                {
                    status = " | No economy";
                    textColor = new Color(0.5f, 0.5f, 0.5f);
                }

                var label = new Label { Text = name + status };
                label.AddThemeColorOverride("font_color", textColor);
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);

                _contentContainer.AddChild(row);
            }
        }

        // Station economies list
        if (_body.SatellitesContainer != null)
        {
            int stationCount = 0;
            foreach (var child in _body.SatellitesContainer.GetChildren())
            {
                if (child is StationSatellite station && station.Economy != null)
                    stationCount++;
            }

            if (stationCount > 0)
            {
                AddSectionHeader($"Stations with Economy ({stationCount})");

                int stationIndex = 0;
                foreach (var child in _body.SatellitesContainer.GetChildren())
                {
                    if (child is StationSatellite station && station.Economy != null)
                    {
                        var row = CreateClickableRow(stationIndex, "station_economy");

                        var eco = station.Economy;
                        float netPower = eco.PowerGeneration - eco.PowerConsumption;
                        string powerStr = netPower >= 0 ? $"+{netPower:F1}" : $"{netPower:F1}";

                        var label = new Label
                        {
                            Text = $"{station.Name}  |  {eco.ActiveBuildingCount} buildings  |  {powerStr}/s",
                        };
                        label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                        label.AddThemeFontSizeOverride("font_size", 13);
                        row.AddChild(label);

                        if (eco.IsPowerDeficit)
                        {
                            var warning = new Label { Text = " [DEFICIT]" };
                            warning.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.3f));
                            warning.AddThemeFontSizeOverride("font_size", 12);
                            row.AddChild(warning);
                        }

                        _contentContainer.AddChild(row);
                        stationIndex++;
                    }
                }
            }
        }
    }

    // ───────── Transfers Tab ─────────

    private void PopulateTransfers()
    {
        if (_body == null || _contentContainer == null) return;

        var transferMgr = _body.TransferMgr;
        if (transferMgr == null)
        {
            AddEmptyLabel("Transfer system not available");
            return;
        }

        // Active transfers
        int activeCount = transferMgr.ActiveTransferCount;
        AddSectionHeader($"Active Transfers ({activeCount})");

        if (activeCount == 0)
            AddEmptyLabel("No active transfers");

        // Schedules per continent
        if (_body.Mesh?.Continents != null)
        {
            bool hasSchedules = false;
            foreach (var kvp in _body.Mesh.Continents)
            {
                var schedules = transferMgr.GetSchedulesForContinent(kvp.Key);
                if (schedules.Count > 0)
                {
                    hasSchedules = true;
                    AddSectionHeader($"Schedules - Continent {kvp.Key}");

                    for (int i = 0; i < schedules.Count; i++)
                    {
                        var schedule = schedules[i];
                        var row = CreateClickableRow(i, "schedule");

                        string dest = schedule.Destination.IsOrbitalStation
                            ? $"Station {schedule.Destination.StationSatelliteId?[..8]}"
                            : $"Continent {schedule.Destination.ContinentIndex}";
                        string state = schedule.State.ToString();

                        var label = new Label
                        {
                            Text = $"{schedule.ScheduleId[..8]}...  |  To: {dest}  |  {state}",
                        };
                        label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                        label.AddThemeFontSizeOverride("font_size", 13);
                        row.AddChild(label);

                        _contentContainer.AddChild(row);
                    }
                }
            }

            if (!hasSchedules && activeCount == 0)
                AddEmptyLabel("No transfer schedules configured");
        }
    }

    public void RefreshCurrentTab()
    {
        if (_body == null || _contentContainer == null || _activeTabIndex < 0)
            return;

        ClearContent();

        switch (_activeTabIndex)
        {
            case 0: PopulateOverview(); break;
            case 1: PopulateContinents(); break;
            case 2: PopulateStations(); break;
            case 3: PopulateEconomies(); break;
            case 4: PopulateTransfers(); break;
        }
    }

    // ───────── Helpers ─────────

    private HBoxContainer CreateClickableRow(int index, string itemType)
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Stop;
        row.AddThemeConstantOverride("separation", 4);

        // Make the row clickable
        row.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left && btn.Pressed)
            {
                EmitSignal(SignalName.ItemSelected, itemType, index);
                row.GetViewport().SetInputAsHandled();
            }
        };

        return row;
    }

    private void AddSectionHeader(string text)
    {
        if (_contentContainer == null) return;

        var header = new Label { Text = text };
        header.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.95f));
        header.AddThemeFontSizeOverride("font_size", 15);
        _contentContainer.AddChild(header);

        var sep = new HSeparator();
        _contentContainer.AddChild(sep);
    }

    private void AddInfoRow(string labelText, string value)
    {
        if (_contentContainer == null) return;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var keyLabel = new Label { Text = labelText + ":" };
        keyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        keyLabel.AddThemeFontSizeOverride("font_size", 13);
        keyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(keyLabel);

        var valLabel = new Label { Text = value };
        valLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(valLabel);

        _contentContainer.AddChild(row);
    }

    private void AddEmptyLabel(string text)
    {
        if (_contentContainer == null) return;

        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        label.AddThemeFontSizeOverride("font_size", 13);
        _contentContainer.AddChild(label);
    }
}
