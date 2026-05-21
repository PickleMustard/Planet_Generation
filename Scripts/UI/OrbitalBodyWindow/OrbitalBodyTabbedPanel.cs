using System.Collections.Generic;
using Constructables;
using Constructables.Power;
using Godot;
using ProceduralGeneration;
using ProceduralGeneration.PlanetGeneration;
using UI.Components;

namespace UI;

/// <summary>
/// Right-side tabbed panel: Overview / Buildings / Grids / Stations.
/// Each tab renders a scrollable list whose rows emit ItemSelected so the
/// details panel below can populate inline. Buildings and Stations are split
/// into collapsible categories; Grids is a flat list; Overview is 3 fixed
/// navigation rows (Economy / Resources / Geography).
/// </summary>
public partial class OrbitalBodyTabbedPanel : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    private IOrbitalBody? _body;
    private int _activeTabIndex = -1;
    private Button[]? _tabButtons;
    private VBoxContainer? _contentContainer;

    // Stable index maps populated during Populate*; details panel resolves via accessors.
    private readonly List<Building> _buildingIndexMap = new();
    private readonly List<PowerGrid> _gridIndexMap = new();
    private readonly List<StationSatellite> _stationIndexMap = new();
    private readonly List<LogisticsUnit> _logisticsIndexMap = new();

    // Per-window-session collapse state, keyed by category title. Empty = all expanded.
    private readonly HashSet<string> _collapsedCategories = new();

    public int ActiveTabIndex => _activeTabIndex;

    public Building? GetBuildingByIndex(int i) =>
        (i < 0 || i >= _buildingIndexMap.Count) ? null : _buildingIndexMap[i];

    public PowerGrid? GetGridByIndex(int i) =>
        (i < 0 || i >= _gridIndexMap.Count) ? null : _gridIndexMap[i];

    public StationSatellite? GetStationByIndex(int i) =>
        (i < 0 || i >= _stationIndexMap.Count) ? null : _stationIndexMap[i];

    public LogisticsUnit? GetLogisticsUnitByIndex(int i) =>
        (i < 0 || i >= _logisticsIndexMap.Count) ? null : _logisticsIndexMap[i];

    public override void _Ready()
    {
        var tabBar = GetNode<HBoxContainer>("VBoxContainer/TabBar");
        _contentContainer = GetNode<VBoxContainer>("VBoxContainer/TabContent/ContentContainer");

        _tabButtons = new Button[4];
        _tabButtons[0] = tabBar.GetNode<Button>("OverviewBtn");
        _tabButtons[1] = tabBar.GetNode<Button>("BuildingsBtn");
        _tabButtons[2] = tabBar.GetNode<Button>("GridsBtn");
        _tabButtons[3] = tabBar.GetNode<Button>("StationsBtn");

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int idx = i;
            _tabButtons[i].Pressed += () => SwitchTab(idx);
        }
        UpdateTabButtonStyles();
    }

    public void Initialize(IOrbitalBody body)
    {
        _body = body;
        _collapsedCategories.Clear();
        SwitchTab(0);
    }

    public void Clear()
    {
        _body = null;
        ClearContent();
        _buildingIndexMap.Clear();
        _gridIndexMap.Clear();
        _stationIndexMap.Clear();
        _logisticsIndexMap.Clear();
        _collapsedCategories.Clear();
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
            case 1: PopulateBuildings(); break;
            case 2: PopulateGrids(); break;
            case 3: PopulateStations(); break;
        }

        // Default selection per tab so the details pane isn't blank.
        switch (tabIndex)
        {
            case 0:
                EmitSignal(SignalName.ItemSelected, "economy", 0);
                break;
            case 2:
                if (_gridIndexMap.Count > 0)
                    EmitSignal(SignalName.ItemSelected, "grid", 0);
                else
                    EmitSignal(SignalName.ItemSelected, "economy_summary", 0);
                break;
            // Buildings/Stations: no default — list-only.
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
            case 1: PopulateBuildings(); break;
            case 2: PopulateGrids(); break;
            case 3: PopulateStations(); break;
        }
    }

    private void UpdateTabButtonStyles()
    {
        if (_tabButtons == null)
            return;
        for (int i = 0; i < _tabButtons.Length; i++)
            _tabButtons[i].ThemeTypeVariation = i == _activeTabIndex ? "TabActive" : "TabInactive";
    }

    private void ClearContent()
    {
        if (_contentContainer == null)
            return;
        foreach (var child in _contentContainer.GetChildren())
            child.QueueFree();
    }

    // ───────── Overview Tab (3 nav rows) ─────────

    private void PopulateOverview()
    {
        if (_body == null || _contentContainer == null)
            return;

        var economyMgr = _body.EconomyMgr;
        int buildingCount = economyMgr?.GetTotalBuildingCount() ?? _body.GetAllBuildings().Count;
        int continentCount = _body.Mesh?.Continents?.Count ?? 0;

        AddNavRow("Economy", "economy", 0, $"{buildingCount} buildings");
        AddNavRow("Resources", "resources", 0, null);
        AddNavRow("Geography", "geography", 0, $"{continentCount} continents");
        AddNavRow("Planet Board", "planet_board", 0, "Wire buildings");
    }

    // ───────── Buildings Tab (categorized, collapsible) ─────────

    private void PopulateBuildings()
    {
        if (_body == null || _contentContainer == null)
            return;

        _buildingIndexMap.Clear();

        var all = _body.GetAllBuildings();
        if (all.Count == 0)
        {
            AddEmptyLabel("No buildings on this body");
            return;
        }

        var groups = new SortedDictionary<string, List<Building>>();
        foreach (var b in all)
        {
            string cat = string.IsNullOrEmpty(b.Definition?.Category)
                ? "Uncategorized"
                : b.Definition!.Category!;
            if (!groups.TryGetValue(cat, out var list))
                groups[cat] = list = new List<Building>();
            list.Add(b);
        }

        foreach (var (cat, list) in groups)
        {
            string capturedCat = cat;
            var section = new CollapsibleSection(
                cat,
                list.Count,
                _collapsedCategories.Contains(cat),
                collapsed =>
                {
                    if (collapsed)
                        _collapsedCategories.Add(capturedCat);
                    else
                        _collapsedCategories.Remove(capturedCat);
                });
            _contentContainer.AddChild(section);

            foreach (var b in list)
            {
                int idx = _buildingIndexMap.Count;
                _buildingIndexMap.Add(b);
                section.Body.AddChild(BuildBuildingRow(b, idx));
            }
        }
    }

    private HBoxContainer BuildBuildingRow(Building building, int idx)
    {
        var row = CreateClickableRow(idx, "building");

        string status = building.IsUnderConstruction ? "Building..." : (building.PoweredOn ? "Active" : "Offline");
        string recipe = building.ActiveRecipeId ?? building.Definition?.DisplayName ?? "—";

        var label = new Label
        {
            Text = $"{building.Name}  |  {recipe}  |  {status}",
            ThemeTypeVariation = "LabelMono",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(label);
        return row;
    }

    // ───────── Grids Tab (flat list of power grids) ─────────

    private void PopulateGrids()
    {
        if (_body == null || _contentContainer == null)
            return;

        _gridIndexMap.Clear();

        var ce = _body as CelestialBody;
        var grids = ce?.PowerGridMgr?.Grids;
        if (grids == null || grids.Count == 0)
        {
            AddEmptyLabel("No power grids");
            return;
        }

        for (int i = 0; i < grids.Count; i++)
        {
            var g = grids[i];
            _gridIndexMap.Add(g);

            var row = CreateClickableRow(i, "grid");
            float battPct = g.BatteryCapacity > 0
                ? g.BatteryStored / g.BatteryCapacity * 100f
                : 0f;
            string statusTxt = g.IsBrownedOut ? "BROWNOUT" : "OK";

            var label = new Label
            {
                Text =
                    $"Grid {g.Id}  |  gen {g.LastGeneration:F1} / draw {g.LastDraw:F1}  |  batt {battPct:F0}%  |  {statusTxt}",
                ThemeTypeVariation = "LabelMono",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            row.AddChild(label);
            _contentContainer.AddChild(row);
        }
    }

    // ───────── Stations Tab (categorized, collapsible) + Ships ─────────

    private void PopulateStations()
    {
        if (_body == null || _contentContainer == null)
            return;

        _stationIndexMap.Clear();
        _logisticsIndexMap.Clear();

        if (_body.SatellitesContainer != null)
        {
            // Walk once, populate both index maps in iteration order.
            foreach (var child in _body.SatellitesContainer.GetChildren())
            {
                if (child is StationSatellite s)
                    _stationIndexMap.Add(s);
                else if (child is LogisticsUnit u)
                    _logisticsIndexMap.Add(u);
            }
        }

        // Stations grouped by StationType.
        if (_stationIndexMap.Count == 0)
        {
            AddEmptyLabel("No stations in orbit");
        }
        else
        {
            var groups = new SortedDictionary<string, List<int>>();
            for (int i = 0; i < _stationIndexMap.Count; i++)
            {
                var s = _stationIndexMap[i];
                string cat = string.IsNullOrEmpty(s.StationType) ? "Uncategorized" : s.StationType!;
                if (!groups.TryGetValue(cat, out var list))
                    groups[cat] = list = new List<int>();
                list.Add(i);
            }

            foreach (var (cat, indices) in groups)
            {
                string capturedCat = cat;
                var section = new CollapsibleSection(
                    cat,
                    indices.Count,
                    _collapsedCategories.Contains(cat),
                    collapsed =>
                    {
                        if (collapsed)
                            _collapsedCategories.Add(capturedCat);
                        else
                            _collapsedCategories.Remove(capturedCat);
                    });
                _contentContainer.AddChild(section);

                foreach (var idx in indices)
                    section.Body.AddChild(BuildStationRow(_stationIndexMap[idx], idx));
            }
        }

        // Ships section (flat list inside its own collapsible).
        if (_logisticsIndexMap.Count > 0)
        {
            const string shipsCat = "Ships";
            var section = new CollapsibleSection(
                shipsCat,
                _logisticsIndexMap.Count,
                _collapsedCategories.Contains(shipsCat),
                collapsed =>
                {
                    if (collapsed)
                        _collapsedCategories.Add(shipsCat);
                    else
                        _collapsedCategories.Remove(shipsCat);
                });
            _contentContainer.AddChild(section);

            for (int i = 0; i < _logisticsIndexMap.Count; i++)
                section.Body.AddChild(BuildLogisticsUnitRow(_logisticsIndexMap[i], i));
        }
    }

    private HBoxContainer BuildStationRow(StationSatellite s, int idx)
    {
        var row = CreateClickableRow(idx, "station");
        string status = s.IsUnderConstruction ? "Building..." : (s.IsActive ? "Active" : "Inactive");
        var label = new Label
        {
            Text = $"{s.Name}  |  Band {s.BandIndex}  |  {status}",
            ThemeTypeVariation = "LabelMono",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(label);
        return row;
    }

    private HBoxContainer BuildLogisticsUnitRow(LogisticsUnit u, int idx)
    {
        var row = CreateClickableRow(idx, "logistics_unit");
        string typeName = u.ShipDef?.Name ?? "Ship";
        var label = new Label
        {
            Text = $"{u.Name}  |  {typeName}  |  {u.State}",
            ThemeTypeVariation = "LabelMono",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(label);
        return row;
    }

    // ───────── Helpers ─────────

    private void AddNavRow(string title, string itemType, int idx, string? hint)
    {
        if (_contentContainer == null)
            return;

        var row = CreateClickableRow(idx, itemType);
        var titleLabel = new Label
        {
            Text = title,
            ThemeTypeVariation = "LabelHand",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(titleLabel);

        if (!string.IsNullOrEmpty(hint))
        {
            var hintLabel = new Label
            {
                Text = hint,
                ThemeTypeVariation = "LabelFaint",
            };
            row.AddChild(hintLabel);
        }

        _contentContainer.AddChild(row);
    }

    private HBoxContainer CreateClickableRow(int index, string itemType)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        row.AddThemeConstantOverride("separation", 4);

        row.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton btn
                && btn.ButtonIndex == MouseButton.Left
                && btn.Pressed)
            {
                EmitSignal(SignalName.ItemSelected, itemType, index);
                row.GetViewport().SetInputAsHandled();
            }
        };

        return row;
    }

    private void AddEmptyLabel(string text)
    {
        if (_contentContainer == null)
            return;
        _contentContainer.AddChild(new Label { Text = text, ThemeTypeVariation = "LabelFaint" });
    }
}
