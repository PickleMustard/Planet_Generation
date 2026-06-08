using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Transfers;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.TransferPlanning;

/// <summary>
/// Top-level Dispatch Slips window. Mirrors the wireframe panel: paper-styled
/// shell, top breadcrumb bar, four interchangeable views (slips list, priority
/// edit, pick destination, manifest editor), and an action footer per view.
/// API-compatible with the legacy <c>TransferPlanningWindow</c>.
/// </summary>
public partial class DispatchSlipsWindow : Control
{
    public static DispatchSlipsWindow? Instance { get; private set; }

    [Signal] public delegate void WindowCloseRequestedEventHandler();
    [Signal] public delegate void HudCloseRequestedEventHandler();

    private enum ViewKind { Slips, Priority, PickDest, Manifest }

    private const int PanelWidth = 1280;
    private const int PanelHeight = 760;

    private static readonly Theme? PaperTheme = ResourceLoader.Load<Theme>(
        "res://UI/Theme/wireframe_paper/wireframe_paper.tres");

    private ColorRect? _backdrop;
    private PanelContainer? _panel;
    private TransferTopBar? _topBar;
    private Control? _viewHost;
    private SlipsListView? _slipsView;
    private PriorityEditView? _priorityView;
    private PickDestinationView? _pickDestView;
    private ManifestEditorView? _manifestView;

    private Node3D? _currentBody;
    private Continent? _currentContinent;
    private Building? _originBuilding;
    private string _originBuildingId = "";
    private int _originContinentIndex = -1;
    private TransferStationBehavior? _behavior;
    private TransferDestination? _draftDestination;
    private ViewKind _activeView = ViewKind.Slips;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.TransferDispatched -= OnTransferDispatched;
            SignalBus.Instance.TransferArrived -= OnTransferArrived;
            SignalBus.Instance.TransferScheduleStateChanged -= OnScheduleStateChanged;
        }
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (PaperTheme != null) Theme = PaperTheme;
        BuildLayout();
        Hide();

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.TransferDispatched += OnTransferDispatched;
            SignalBus.Instance.TransferArrived += OnTransferArrived;
            SignalBus.Instance.TransferScheduleStateChanged += OnScheduleStateChanged;
        }
    }

    private void BuildLayout()
    {
        _backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.6f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _backdrop.GuiInput += OnBackdropInput;
        AddChild(_backdrop);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _panel = new PanelContainer
        {
            ThemeTypeVariation = "PaperPanel",
            CustomMinimumSize = new Vector2(PanelWidth, PanelHeight),
        };
        center.AddChild(_panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 0);
        _panel.AddChild(col);

        _topBar = new TransferTopBar();
        _topBar.HudCloseRequested += OnHudCloseRequested;
        _topBar.BackRequested += OnBackRequested;
        col.AddChild(_topBar);

        _viewHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 600),
        };
        col.AddChild(_viewHost);

        _slipsView = new SlipsListView { Visible = true };
        _slipsView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _slipsView.AddRouteRequested += OnAddRouteRequested;
        _slipsView.EditPriorityRequested += () => SwitchView(ViewKind.Priority);
        _slipsView.DeleteSlipRequested += OnDeleteSlipRequested;
        _slipsView.EditSlipRequested += OnEditSlipRequested;
        _viewHost.AddChild(_slipsView);

        _priorityView = new PriorityEditView { Visible = false };
        _priorityView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _priorityView.DoneRequested += OnPriorityDone;
        _viewHost.AddChild(_priorityView);

        _pickDestView = new PickDestinationView { Visible = false };
        _pickDestView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pickDestView.DestinationConfirmed += OnDestinationConfirmed;
        _pickDestView.Cancelled += () => SwitchView(ViewKind.Slips);
        _viewHost.AddChild(_pickDestView);

        _manifestView = new ManifestEditorView { Visible = false };
        _manifestView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _manifestView.BackRequested += () => SwitchView(ViewKind.PickDest);
        _manifestView.Cancelled += () => SwitchView(ViewKind.Slips);
        _manifestView.RouteFiled += OnRouteFiled;
        _viewHost.AddChild(_manifestView);
    }

    public void ShowWindow(Building originBuilding, Node3D body, Continent? continent)
    {
        _originBuilding = originBuilding;
        _originBuildingId = originBuilding?.Id ?? "";
        _originContinentIndex = originBuilding?.PrimaryCell?.ContinentIndex ?? -1;
        _currentBody = body;
        _currentContinent = continent;
        _draftDestination = null;

        _behavior = originBuilding?.GetBehavior<TransferStationBehavior>();
        if (_behavior == null || string.IsNullOrEmpty(_originBuildingId) || !_behavior.HasEndpoint(_originBuildingId))
        {
            ToastSystem.Instance?.ShowError("Origin has no transfer station");
            return;
        }

        _slipsView?.Bind(_behavior, _originBuildingId);
        _priorityView?.Bind(_behavior, _originBuildingId);
        _pickDestView?.Bind(_behavior, _originBuildingId, body);
        _manifestView?.Bind(_behavior, _originBuildingId, ResourceLoader.Load<Theme>(
            "res://UI/Theme/wireframe_paper/wireframe_paper.tres"));

        SwitchView(ViewKind.Slips);
        Show();
        GameLogger.Info($"DispatchSlipsWindow shown for hub '{_originBuilding?.Name}' ({_originBuildingId[..System.Math.Min(8, _originBuildingId.Length)]})");
    }

    public void HideWindow()
    {
        Hide();
        _originBuilding = null;
        _originBuildingId = "";
        _originContinentIndex = -1;
        _currentBody = null;
        _currentContinent = null;
        _behavior = null;
        _draftDestination = null;
        GameLogger.Info("DispatchSlipsWindow hidden");
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            RequestClose();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            RequestClose();
    }

    private void OnHudCloseRequested()
    {
        EmitSignal(SignalName.HudCloseRequested);
    }

    private void OnBackRequested()
    {
        if (_activeView != ViewKind.Slips) SwitchView(ViewKind.Slips);
        else RequestClose();
    }

    private void RequestClose()
    {
        EmitSignal(SignalName.WindowCloseRequested);
    }

    private void SwitchView(ViewKind kind)
    {
        _activeView = kind;
        if (_slipsView != null) _slipsView.Visible = kind == ViewKind.Slips;
        if (_priorityView != null) _priorityView.Visible = kind == ViewKind.Priority;
        if (_pickDestView != null) _pickDestView.Visible = kind == ViewKind.PickDest;
        if (_manifestView != null) _manifestView.Visible = kind == ViewKind.Manifest;

        var trail = BuildTrail(kind);
        _topBar?.SetTrail(trail);

        if (kind == ViewKind.Slips) _slipsView?.Refresh();
        else if (kind == ViewKind.Priority) _priorityView?.Refresh();
        else if (kind == ViewKind.PickDest) _pickDestView?.Refresh();
    }

    private List<string> BuildTrail(ViewKind kind)
    {
        var hubName = _originBuilding?.Name
            ?? (_originContinentIndex >= 0 ? $"Continent {_originContinentIndex:D2}" : "Hub");
        var trail = new List<string> { hubName, "Transfer Routes" };
        switch (kind)
        {
            case ViewKind.Priority: trail.Add("Edit Priority"); break;
            case ViewKind.PickDest: trail.Add("Add Route"); trail.Add("Pick Destination"); break;
            case ViewKind.Manifest: trail.Add("Add Route"); trail.Add("Build Manifest"); break;
            default: trail.Add("Dispatch Slips"); break;
        }
        return trail;
    }

    private void OnAddRouteRequested()
    {
        _draftDestination = null;
        SwitchView(ViewKind.PickDest);
    }

    private void OnPriorityDone()
    {
        SwitchView(ViewKind.Slips);
    }

    private void OnDestinationConfirmed(string destinationBuildingId)
    {
        _draftDestination = TransferDestination.ForBuilding(destinationBuildingId);
        _manifestView?.SetDraftDestination(_draftDestination);
        SwitchView(ViewKind.Manifest);
    }

    private void OnRouteFiled()
    {
        SwitchView(ViewKind.Slips);
    }

    private void OnEditSlipRequested(string scheduleId)
    {
        // Re-open manifest editor against the selected schedule's destination.
        if (_behavior == null) return;
        TransferSchedule? target = null;
        foreach (var s in _behavior.GetSchedulesForOrigin(_originBuildingId))
            if (s.ScheduleId == scheduleId) { target = s; break; }
        if (target == null) return;
        _draftDestination = target.Destination;
        _manifestView?.SetEditTarget(target);
        SwitchView(ViewKind.Manifest);
    }

    private void OnDeleteSlipRequested(string scheduleId)
    {
        _behavior?.RemoveSchedule(scheduleId);
        if (_activeView == ViewKind.Slips) _slipsView?.Refresh();
    }

    private void OnTransferDispatched(string orderId, int origin)
    {
        if (Visible && _activeView == ViewKind.Slips)
            _slipsView?.Refresh();
    }

    private void OnTransferArrived(string orderId, bool fully)
    {
        if (Visible && _activeView == ViewKind.Slips)
            _slipsView?.Refresh();
    }

    private void OnScheduleStateChanged(string scheduleId, int state)
    {
        if (Visible && _activeView == ViewKind.Slips)
            _slipsView?.Refresh();
    }
}
