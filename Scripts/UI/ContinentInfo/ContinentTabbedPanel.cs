using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using Structures.GameState;
using Structures.Transfers;

namespace UI.ContinentInfo;

/// <summary>
/// Right-side tabbed panel with Overview, Manufacturing, Power, Transfers tabs.
/// Clicks on rows emit ItemSelected. The Transfers tab exposes a "Plan Transfer"
/// button that fires TransferPlanRequested so the parent window can re-emit it
/// to the HSM.
/// </summary>
public partial class ContinentTabbedPanel : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    [Signal]
    public delegate void TransferPlanRequestedEventHandler();

    private int _continentIndex = -1;
    private IOrbitalBody? _body;
    private Continent? _continent;

    private int _activeTabIndex = -1;
    private Button[]? _tabButtons;
    private VBoxContainer? _contentContainer;

    private StyleBox? _activeTabStyle;
    private StyleBox? _inactiveTabStyle;

    // Manufacturing grouping state (snapshot used by details panel)
    private readonly List<List<ContinentEconomy.BuildingRegistration>> _manufacturingGroups = new();
    private readonly HashSet<string> _expandedManufacturingTypes = new();

    // Active transfers / schedules snapshots so details can resolve indices stably
    private readonly List<BodyTransferManager.ActiveTransfer> _activeTransferSnapshot = new();
    private readonly List<TransferSchedule> _scheduleSnapshot = new();

    public int ActiveTabIndex => _activeTabIndex;
    public IReadOnlyList<IReadOnlyList<ContinentEconomy.BuildingRegistration>> ManufacturingGroups
        => _manufacturingGroups;
    public IReadOnlyList<BodyTransferManager.ActiveTransfer> ActiveTransferSnapshot
        => _activeTransferSnapshot;
    public IReadOnlyList<TransferSchedule> ScheduleSnapshot => _scheduleSnapshot;

    public override void _Ready()
    {
        var tabBar = GetNode<HBoxContainer>("VBoxContainer/TabBar");
        _contentContainer = GetNode<VBoxContainer>("VBoxContainer/TabContent/ContentContainer");

        _tabButtons = new Button[4];
        _tabButtons[0] = tabBar.GetNode<Button>("OverviewBtn");
        _tabButtons[1] = tabBar.GetNode<Button>("ManufacturingBtn");
        _tabButtons[2] = tabBar.GetNode<Button>("PowerBtn");
        _tabButtons[3] = tabBar.GetNode<Button>("TransfersBtn");

        _activeTabStyle = _tabButtons[0].GetThemeStylebox("normal");
        _inactiveTabStyle = _tabButtons[1].GetThemeStylebox("normal");

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int idx = i;
            _tabButtons[i].Pressed += () => SwitchTab(idx);
        }
    }

    public void Initialize(int continentIndex, IOrbitalBody body, Continent continent)
    {
        _continentIndex = continentIndex;
        _body = body;
        _continent = continent;
        _activeTabIndex = -1;
        SwitchTab(0);
    }

    public void Clear()
    {
        _body = null;
        _continent = null;
        _continentIndex = -1;
        _manufacturingGroups.Clear();
        _expandedManufacturingTypes.Clear();
        _activeTransferSnapshot.Clear();
        _scheduleSnapshot.Clear();
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

        if (_continent == null || _contentContainer == null)
            return;

        switch (tabIndex)
        {
            case 0: PopulateOverview(); break;
            case 1: PopulateManufacturing(); break;
            case 2: PopulatePower(); break;
            case 3: PopulateTransfers(); break;
        }

        // Default selection per tab so the details panel is never blank
        switch (tabIndex)
        {
            case 0: EmitSignal(SignalName.ItemSelected, "continent_aggregate", _continentIndex); break;
            case 2: EmitSignal(SignalName.ItemSelected, "power_summary", _continentIndex); break;
        }
    }

    public void RefreshCurrentTab()
    {
        if (_continent == null || _contentContainer == null || _activeTabIndex < 0)
            return;

        ClearContent();
        switch (_activeTabIndex)
        {
            case 0: PopulateOverview(); break;
            case 1: PopulateManufacturing(); break;
            case 2: PopulatePower(); break;
            case 3: PopulateTransfers(); break;
        }
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

    // ───────── Overview Tab (cell list) ─────────

    private void PopulateOverview()
    {
        if (_continent == null || _contentContainer == null) return;

        AddSectionHeader($"Cells ({_continent.cells.Count})");

        if (_continent.cells.Count == 0)
        {
            AddEmptyLabel("No cells");
            return;
        }

        foreach (var cell in _continent.cells)
        {
            var row = CreateClickableRow(cell.Index, "cell");

            string occupied = cell.Building != null ? " [*]" : "";
            var label = new Label
            {
                Text = $"Cell {cell.Index}  |  {cell.Biome}  |  H={cell.Height:F2}{occupied}",
            };
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
            label.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(label);

            _contentContainer.AddChild(row);
        }
    }

    // ───────── Manufacturing Tab ─────────

    private void PopulateManufacturing()
    {
        if (_continent == null || _contentContainer == null) return;

        _manufacturingGroups.Clear();

        var economy = _continent.Economy;
        if (economy == null || economy.ActiveBuildingCount == 0)
        {
            AddEmptyLabel("No buildings");
            return;
        }

        var groups = new Dictionary<string, List<ContinentEconomy.BuildingRegistration>>();
        foreach (var reg in economy.ActiveBuildings)
        {
            if (reg.TheoreticalPowerGeneration > 0f) continue;
            if (IsBatteryBuilding(reg)) continue;

            string id = reg.BuildingNode.Definition?.IdName ?? "unknown";
            if (!groups.TryGetValue(id, out var list))
            {
                list = new List<ContinentEconomy.BuildingRegistration>();
                groups[id] = list;
            }
            list.Add(reg);
        }

        if (groups.Count == 0)
        {
            AddEmptyLabel("No manufacturing buildings");
            return;
        }

        foreach (var kvp in groups)
        {
            int groupIndex = _manufacturingGroups.Count;
            _manufacturingGroups.Add(kvp.Value);

            string idName = kvp.Key;
            bool expanded = _expandedManufacturingTypes.Contains(idName);
            string displayName = kvp.Value[0].BuildingNode.Definition?.DisplayName ?? idName;

            var header = CreateClickableRow(groupIndex, "building_type");
            var toggle = new Button
            {
                Text = expanded ? "-" : "+",
                CustomMinimumSize = new Vector2(20, 0),
            };
            toggle.AddThemeFontSizeOverride("font_size", 13);
            string capturedId = idName;
            toggle.Pressed += () =>
            {
                if (_expandedManufacturingTypes.Contains(capturedId))
                    _expandedManufacturingTypes.Remove(capturedId);
                else
                    _expandedManufacturingTypes.Add(capturedId);
                RefreshCurrentTab();
            };
            header.AddChild(toggle);

            var label = new Label { Text = $"{displayName}  ({kvp.Value.Count})" };
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
            label.AddThemeFontSizeOverride("font_size", 14);
            header.AddChild(label);

            _contentContainer.AddChild(header);

            if (expanded)
            {
                foreach (var reg in kvp.Value)
                {
                    var row = CreateClickableRow(reg.BuildingInstanceId, "building");
                    var indent = new Control { CustomMinimumSize = new Vector2(24, 0) };
                    row.AddChild(indent);

                    string status = reg.IsPaused ? " [PAUSED]" : "";
                    var childLabel = new Label
                    {
                        Text = $"{displayName} #{reg.BuildingInstanceId}{status}",
                    };
                    childLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f));
                    childLabel.AddThemeFontSizeOverride("font_size", 13);
                    row.AddChild(childLabel);

                    _contentContainer.AddChild(row);
                }
            }
        }
    }

    // ───────── Power Tab ─────────

    private void PopulatePower()
    {
        if (_continent == null || _contentContainer == null) return;

        var economy = _continent.Economy;
        if (economy == null)
        {
            AddEmptyLabel("No economy");
            return;
        }

        AddSectionHeader("Summary");
        AddInfoRow("Generation", $"{economy.PowerGeneration:F1} /s");
        AddInfoRow("Consumption", $"{economy.PowerConsumption:F1} /s");
        AddInfoRow("Net", $"{(economy.PowerGeneration - economy.PowerConsumption):F1} /s");
        AddInfoRow("Stored", $"{economy.PowerStored:F1} / {economy.PowerStorageCapacity:F1}");

        var generators = new List<ContinentEconomy.BuildingRegistration>();
        var batteries = new List<ContinentEconomy.BuildingRegistration>();
        foreach (var reg in economy.ActiveBuildings)
        {
            if (reg.TheoreticalPowerGeneration > 0f)
                generators.Add(reg);
            else if (IsBatteryBuilding(reg))
                batteries.Add(reg);
        }

        AddSectionHeader($"Generators ({generators.Count})");
        if (generators.Count == 0)
        {
            AddEmptyLabel("No generators");
        }
        else
        {
            foreach (var reg in generators)
            {
                var row = CreateClickableRow(reg.BuildingInstanceId, "power_generator");
                string name = reg.BuildingNode.Definition?.DisplayName ?? "Generator";
                string paused = reg.IsPaused ? " [PAUSED]" : "";
                var label = new Label
                {
                    Text = $"{name} #{reg.BuildingInstanceId}  |  {reg.TheoreticalPowerGeneration:F1}/s{paused}",
                };
                label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);
                _contentContainer.AddChild(row);
            }
        }

        AddSectionHeader($"Batteries ({batteries.Count})");
        if (batteries.Count == 0)
        {
            AddEmptyLabel("No batteries");
        }
        else
        {
            foreach (var reg in batteries)
            {
                var row = CreateClickableRow(reg.BuildingInstanceId, "battery");
                string name = reg.BuildingNode.Definition?.DisplayName ?? "Battery";
                float cap = GetBatteryCapacity(reg);
                var label = new Label
                {
                    Text = $"{name} #{reg.BuildingInstanceId}  |  cap {cap:F1}",
                };
                label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);
                _contentContainer.AddChild(row);
            }
        }
    }

    // ───────── Transfers Tab ─────────

    private void PopulateTransfers()
    {
        if (_body == null || _contentContainer == null) return;

        var mgr = _body.TransferMgr;
        var planBtn = new Button { Text = "+ Plan Transfer" };
        planBtn.AddThemeFontSizeOverride("font_size", 13);
        planBtn.Pressed += () => EmitSignal(SignalName.TransferPlanRequested);
        _contentContainer.AddChild(planBtn);

        _contentContainer.AddChild(new HSeparator());

        _activeTransferSnapshot.Clear();
        _scheduleSnapshot.Clear();

        if (mgr == null)
        {
            AddEmptyLabel("Transfer system unavailable");
            return;
        }

        foreach (var active in mgr.GetActiveTransfers())
        {
            var order = active.Order;
            bool relevant = order.OriginContinentIndex == _continentIndex
                || (order.Destination?.ContinentIndex == _continentIndex);
            if (!relevant) continue;
            _activeTransferSnapshot.Add(active);
        }

        _scheduleSnapshot.AddRange(mgr.GetSchedulesForContinent(_continentIndex));

        AddSectionHeader($"Active Transfers ({_activeTransferSnapshot.Count})");
        if (_activeTransferSnapshot.Count == 0)
        {
            AddEmptyLabel("No active transfers");
        }
        else
        {
            for (int i = 0; i < _activeTransferSnapshot.Count; i++)
            {
                var order = _activeTransferSnapshot[i].Order;
                var row = CreateClickableRow(i, "transfer");

                string dest = order.Destination.IsOrbitalStation
                    ? $"Station {order.Destination.StationSatelliteId?[..8]}"
                    : $"Continent {order.Destination.ContinentIndex}";
                int pct = Mathf.RoundToInt(order.Progress * 100f);
                var label = new Label
                {
                    Text = $"{order.OrderId[..8]}...  |  To: {dest}  |  {order.State}  |  {pct}%",
                };
                label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);
                _contentContainer.AddChild(row);
            }
        }

        AddSectionHeader($"Schedules ({_scheduleSnapshot.Count})");
        if (_scheduleSnapshot.Count == 0)
        {
            AddEmptyLabel("No schedules");
        }
        else
        {
            for (int i = 0; i < _scheduleSnapshot.Count; i++)
            {
                var schedule = _scheduleSnapshot[i];
                var row = CreateClickableRow(i, "schedule");

                string dest = schedule.Destination.IsOrbitalStation
                    ? $"Station {schedule.Destination.StationSatelliteId?[..8]}"
                    : $"Continent {schedule.Destination.ContinentIndex}";
                var label = new Label
                {
                    Text = $"{schedule.ScheduleId[..8]}...  |  To: {dest}  |  {schedule.State}",
                };
                label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);
                _contentContainer.AddChild(row);
            }
        }
    }

    // ───────── Helpers ─────────

    public static bool IsBatteryBuilding(ContinentEconomy.BuildingRegistration reg)
    {
        var dict = reg.BuildingNode.Definition?.StartingStorageCapacity;
        return dict != null
            && dict.TryGetValue("power", out float cap)
            && cap > 0f;
    }

    public static float GetBatteryCapacity(ContinentEconomy.BuildingRegistration reg)
    {
        var dict = reg.BuildingNode.Definition?.StartingStorageCapacity;
        if (dict != null && dict.TryGetValue("power", out float cap))
            return cap;
        return 0f;
    }

    private HBoxContainer CreateClickableRow(int index, string itemType)
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Stop;
        row.AddThemeConstantOverride("separation", 4);

        row.GuiInput += (InputEvent @event) =>
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

    private void AddSectionHeader(string text)
    {
        if (_contentContainer == null) return;

        var header = new Label { Text = text };
        header.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.95f));
        header.AddThemeFontSizeOverride("font_size", 15);
        _contentContainer.AddChild(header);
        _contentContainer.AddChild(new HSeparator());
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
