using Constructables;
using Godot;
using PlayerInteraction.CellSelection;
using Structures.GameState;
using UtilityLibrary;

namespace UI.ContinentInfo;

/// <summary>
/// Diegetic GUI window for inspecting a single Continent. Layout mirrors
/// OrbitalBodyWindow: header top-left, tabbed panel on the right, details
/// panel along the bottom. The 3D scene remains visible and interactable;
/// cells are selected via the existing CellSelectionManager.
/// </summary>
public partial class ContinentInfoWindow : Control
{
    public static ContinentInfoWindow? Instance { get; private set; }

    [Export]
    private Button? _closeButton;

    [Export]
    private ContinentHeaderPanel? _headerPanel;

    [Export]
    private ContinentTabbedPanel? _tabbedPanel;

    [Export]
    private ContinentDetailsPanel? _detailsPanel;

    private int _continentIndex = -1;
    private Node3D? _bodyNode;
    private IOrbitalBody? _body;
    private Continent? _continent;

    private double _lastEconomyRefreshTime;
    private const double ECONOMY_DEBOUNCE_SECONDS = 0.5;

    public bool IsOpen { get; private set; }

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void TransferRequestedEventHandler();

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        if (_closeButton != null)
            _closeButton.Pressed += OnCloseButtonPressed;
    }

    public override void _ExitTree()
    {
        UnsubscribeSignals();
        if (Instance == this)
            Instance = null;
    }

    // ───────── Open / Close ─────────

    public void ShowWindow(int continentIndex, Node3D body, Continent? continent)
    {
        if (continent == null)
        {
            GameLogger.Warning("ContinentInfoWindow: null continent provided");
            return;
        }

        if (body is not IOrbitalBody orbitalBody)
        {
            GameLogger.Warning(
                $"ContinentInfoWindow: body '{body?.Name}' does not implement IOrbitalBody");
            return;
        }

        _continentIndex = continentIndex;
        _bodyNode = body;
        _body = orbitalBody;
        _continent = continent;

        Visible = true;
        IsOpen = true;
        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        _headerPanel?.Populate(continentIndex, continent, orbitalBody);
        _tabbedPanel?.Initialize(continentIndex, orbitalBody, continent);
        if (_tabbedPanel != null && _detailsPanel != null)
        {
            _detailsPanel.Initialize(continentIndex, orbitalBody, continent, _tabbedPanel);
            _tabbedPanel.TransferPlanRequested += OnTransferPlanRequested;
        }

        body.TreeExiting += OnBodyExiting;
        SubscribeSignals();

        GameLogger.Info(
            $"[ContinentInfoWindow] Opened for continent {continentIndex} on {body.Name}");
    }

    public void HideWindow()
    {
        if (!IsOpen)
            return;

        UnsubscribeSignals();
        IsOpen = false;

        _headerPanel?.Clear();
        if (_tabbedPanel != null && _detailsPanel != null)
        {
            _detailsPanel.Disconnect(_tabbedPanel);
            _tabbedPanel.TransferPlanRequested -= OnTransferPlanRequested;
        }
        _tabbedPanel?.Clear();
        _detailsPanel?.Clear();

        if (_bodyNode != null && IsInstanceValid(_bodyNode))
            _bodyNode.TreeExiting -= OnBodyExiting;

        _bodyNode = null;
        _body = null;
        _continent = null;
        _continentIndex = -1;
        Visible = false;

        GameLogger.Info("[ContinentInfoWindow] Closed");
    }

    public void Clear()
    {
        HideWindow();
    }

    // ───────── Input ─────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen) return;
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            EmitSignal(SignalName.WindowCloseRequested);
            GetViewport().SetInputAsHandled();
        }
    }

    // ───────── Callbacks ─────────

    private void OnCloseButtonPressed()
    {
        EmitSignal(SignalName.WindowCloseRequested);
    }

    private void OnTransferPlanRequested()
    {
        EmitSignal(SignalName.TransferRequested);
    }

    private void OnBodyExiting()
    {
        GameLogger.Warning(
            "[ContinentInfoWindow] Inspected body is leaving the tree — closing window");
        EmitSignal(SignalName.WindowCloseRequested);
    }

    // ───────── Signal Wiring ─────────

    private void SubscribeSignals()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.ContinentEconomyTicked += OnContinentEconomyTicked;
            SignalBus.Instance.ContinentPowerStateChanged += OnContinentPowerStateChanged;
            SignalBus.Instance.ContinentResourceShortage += OnContinentResourceShortage;
            SignalBus.Instance.TransferDispatched += OnTransferDispatched;
            SignalBus.Instance.TransferArrived += OnTransferArrived;
            SignalBus.Instance.TransferScheduleStateChanged += OnTransferScheduleStateChanged;
            SignalBus.Instance.BuildingConstructed += OnBuildingCountChanged;
            SignalBus.Instance.BuildingRemoved += OnBuildingCountChanged;
        }

        if (CellSelectionManager.Instance != null)
            CellSelectionManager.Instance.CellSelected += OnCellSelected;
    }

    private void UnsubscribeSignals()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.ContinentEconomyTicked -= OnContinentEconomyTicked;
            SignalBus.Instance.ContinentPowerStateChanged -= OnContinentPowerStateChanged;
            SignalBus.Instance.ContinentResourceShortage -= OnContinentResourceShortage;
            SignalBus.Instance.TransferDispatched -= OnTransferDispatched;
            SignalBus.Instance.TransferArrived -= OnTransferArrived;
            SignalBus.Instance.TransferScheduleStateChanged -= OnTransferScheduleStateChanged;
            SignalBus.Instance.BuildingConstructed -= OnBuildingCountChanged;
            SignalBus.Instance.BuildingRemoved -= OnBuildingCountChanged;
        }

        if (CellSelectionManager.Instance != null)
            CellSelectionManager.Instance.CellSelected -= OnCellSelected;
    }

    private bool IsRelevant(int continentIndex)
    {
        return IsOpen && continentIndex == _continentIndex;
    }

    // ───────── Signal Handlers ─────────

    private void OnContinentEconomyTicked(int continentIndex)
    {
        if (!IsRelevant(continentIndex)) return;

        double now = Time.GetUnixTimeFromSystem();
        if (now - _lastEconomyRefreshTime < ECONOMY_DEBOUNCE_SECONDS)
            return;
        _lastEconomyRefreshTime = now;

        RefreshAll();
    }

    private void OnContinentPowerStateChanged(int continentIndex, bool isDeficit)
    {
        if (!IsRelevant(continentIndex)) return;
        RefreshAll();
    }

    private void OnContinentResourceShortage(int continentIndex, string resourceId, bool isShortage)
    {
        if (!IsRelevant(continentIndex)) return;
        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnTransferDispatched(string orderId, int originContinentIndex)
    {
        if (!IsRelevant(originContinentIndex)) return;
        RefreshAll();
    }

    private void OnTransferArrived(string orderId, bool fullyAccepted)
    {
        if (!IsOpen) return;
        RefreshAll();
    }

    private void OnTransferScheduleStateChanged(string scheduleId, int newState)
    {
        if (!IsOpen) return;
        int tab = _tabbedPanel?.ActiveTabIndex ?? -1;
        if (tab == 3)
            _tabbedPanel?.RefreshCurrentTab();
        _detailsPanel?.RefreshCurrentDetails();
    }

    private void OnBuildingCountChanged(int continentIndex)
    {
        if (!IsRelevant(continentIndex)) return;
        RefreshAll();
    }

    private void OnCellSelected(VoronoiCell cell, Node3D body, Continent continent)
    {
        if (!IsOpen || _body == null || cell == null) return;

        // Only react if this cell belongs to the currently inspected continent
        if (continent == null || continent != _continent)
            return;

        _tabbedPanel?.SwitchTab(0);
        _tabbedPanel?.EmitSignal(
            ContinentTabbedPanel.SignalName.ItemSelected, "cell", cell.Index);
    }

    private void RefreshAll()
    {
        if (_body == null || _continent == null) return;
        _headerPanel?.Populate(_continentIndex, _continent, _body);
        _tabbedPanel?.RefreshCurrentTab();
        _detailsPanel?.RefreshCurrentDetails();
    }
}
