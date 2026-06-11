using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UI.Components;
using UtilityLibrary;

namespace UI.LogisticsOverview;

/// <summary>
/// Full-screen fleet overview: a sortable scroll column of every logistics unit
/// in the system (built and under construction) on the left, and a large detail
/// panel on the right that updates as units are selected. A "View In-Depth"
/// button forwards the selected unit to the per-unit details window.
/// </summary>
public partial class LogisticsOverviewWindow : Control
{
    public static LogisticsOverviewWindow? Instance { get; private set; }

    private enum SortMode { Default = 0, ByType = 1, ByBody = 2 }

    [Export]
    private Button? _closeButton;

    [Export]
    private OptionButton? _sortOption;

    [Export]
    private FilterableSidebar? _sidebar;

    [Export]
    private LogisticsOverviewDetailsPanel? _detailsPanel;

    [Export]
    private Button? _viewInDepthButton;

    private readonly List<LogisticsUnit> _units = new();
    private readonly Dictionary<string, LogisticsUnit> _unitsById = new();
    private LogisticsUnit? _selectedUnit;

    // System Overview map overlay + its entry button (built in code so no .tscn edit).
    private UI.SystemBoard.SystemOverviewWindow? _systemOverviewWindow;

    public bool IsOpen { get; private set; }

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    /// <summary>Raised when the user asks to open the per-unit details window.</summary>
    [Signal]
    public delegate void LogisticsUnitInspectRequestedEventHandler(Node unit);

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        if (_closeButton != null)
            _closeButton.Pressed += OnCloseButtonPressed;

        if (_viewInDepthButton != null)
            _viewInDepthButton.Pressed += OnViewInDepthPressed;

        // System Overview map: a code-built overlay + a sibling button next to
        // "View In-Depth" so no scene edits are needed.
        _systemOverviewWindow = UI.SystemBoard.SystemOverviewWindow.Create();
        AddChild(_systemOverviewWindow);
        if (_viewInDepthButton?.GetParent() is Node btnParent)
        {
            var systemMapButton = new Button { Text = "System Map" };
            systemMapButton.Pressed += () => _systemOverviewWindow?.ShowWindow();
            btnParent.AddChild(systemMapButton);
        }

        if (_sortOption != null)
        {
            if (_sortOption.ItemCount == 0)
            {
                _sortOption.AddItem("Default", (int)SortMode.Default);
                _sortOption.AddItem("By Type", (int)SortMode.ByType);
                _sortOption.AddItem("By Body", (int)SortMode.ByBody);
            }
            _sortOption.ItemSelected += OnSortChanged;
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnCloseButtonPressed() => EmitSignal(SignalName.WindowCloseRequested);

    // ───────── Open / Close ─────────

    public void ShowWindow()
    {
        IsOpen = true;
        Visible = true;

        CollectUnits();
        RebuildList();

        GameLogger.Info($"[LogisticsOverviewWindow] Opened ({_units.Count} units)");
    }

    public void HideWindow()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        _sidebar?.ClearItems();
        _detailsPanel?.Clear();
        _units.Clear();
        _unitsById.Clear();
        _selectedUnit = null;
        UpdateViewInDepthEnabled();

        Visible = false;

        GameLogger.Info("[LogisticsOverviewWindow] Closed");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen)
            return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            EmitSignal(SignalName.WindowCloseRequested);
            GetViewport().SetInputAsHandled();
        }
    }

    // ───────── Unit collection & list ─────────

    private void CollectUnits()
    {
        _units.Clear();
        _unitsById.Clear();

        var systemData = SystemData.FindForNode(this);
        if (systemData == null)
        {
            GameLogger.Warning("[LogisticsOverviewWindow] SystemData not found; no ships collected.");
            return;
        }

        // Registry includes transit-adrift ships (reparented to system_container during
        // a transfer), which the previous per-body walk missed.
        foreach (var unit in systemData.GetAllShips())
        {
            _units.Add(unit);
            _unitsById[unit.Id] = unit;
        }
    }

    private SortMode CurrentSort =>
        _sortOption != null ? (SortMode)_sortOption.GetSelectedId() : SortMode.Default;

    private IEnumerable<LogisticsUnit> SortedUnits()
    {
        return CurrentSort switch
        {
            SortMode.ByType => _units.OrderBy(u => u.ShipDef?.Name ?? "~")
                                     .ThenBy(u => u.Name),
            SortMode.ByBody => _units.OrderBy(u => LogisticsUnitDataHelpers.GetParentBodyName(u))
                                     .ThenBy(u => u.Name),
            _ => _units.AsEnumerable(),
        };
    }

    private void RebuildList()
    {
        if (_sidebar == null)
            return;

        _sidebar.SetTitle("Fleet");
        _sidebar.SetSearchPlaceholder("Search ships…");
        _sidebar.ClearItems();

        if (_units.Count == 0)
        {
            _sidebar.ShowEmptyMessage("No logistics units");
            _detailsPanel?.Clear();
            _selectedUnit = null;
            UpdateViewInDepthEnabled();
            return;
        }

        LogisticsUnit? first = null;
        foreach (var unit in SortedUnits())
        {
            first ??= unit;
            _sidebar.AddItem(BuildRow(unit), filterText: $"{unit.Name} {unit.ShipDef?.Name}");
        }

        // Keep the prior selection if it survived a re-sort, else select the first.
        if (_selectedUnit == null || !_unitsById.ContainsValue(_selectedUnit))
            _selectedUnit = first;
        ShowDetails(_selectedUnit);
    }

    private SidebarListItem BuildRow(LogisticsUnit unit)
    {
        var row = SidebarListItem.Create(unit.Id);

        var nameLabel = new Label
        {
            Text = unit.Name,
            CustomMinimumSize = new Vector2(120, 0),
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddCell(nameLabel);

        var typeLabel = new Label { Text = unit.ShipDef?.Name ?? "—" };
        typeLabel.AddThemeFontSizeOverride("font_size", 11);
        typeLabel.ThemeTypeVariation = "LabelFaint";
        row.AddCell(typeLabel);

        var statusLabel = new Label
        {
            Text = LogisticsUnitDataHelpers.GetTransitStatus(unit),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.ThemeTypeVariation = "LabelFaint";
        row.AddCell(statusLabel);

        row.Activated += OnRowActivated;
        return row;
    }

    private void OnRowActivated(string id)
    {
        if (_unitsById.TryGetValue(id, out var unit))
        {
            _selectedUnit = unit;
            ShowDetails(unit);
        }
    }

    private void OnSortChanged(long index) => RebuildList();

    private void ShowDetails(LogisticsUnit? unit)
    {
        if (unit != null)
            _detailsPanel?.Populate(unit);
        else
            _detailsPanel?.Clear();
        UpdateViewInDepthEnabled();
    }

    private void OnViewInDepthPressed()
    {
        if (_selectedUnit != null)
            EmitSignal(SignalName.LogisticsUnitInspectRequested, _selectedUnit);
    }

    private void UpdateViewInDepthEnabled()
    {
        if (_viewInDepthButton != null)
            _viewInDepthButton.Disabled = _selectedUnit == null;
    }
}
