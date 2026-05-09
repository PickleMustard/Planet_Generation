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
    private readonly HashSet<string> _expandedManufacturingTypes = new();

    // Active transfers / schedules snapshots so details can resolve indices stably
    private readonly List<BodyTransferManager.ActiveTransfer> _activeTransferSnapshot = new();
    private readonly List<TransferSchedule> _scheduleSnapshot = new();

    public int ActiveTabIndex => _activeTabIndex;
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
    {}

    // ───────── Power Tab ─────────

    private void PopulatePower()
    {}

    // ───────── Transfers Tab ─────────

    private void PopulateTransfers()
    {}

    // ───────── Helpers ─────────

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
