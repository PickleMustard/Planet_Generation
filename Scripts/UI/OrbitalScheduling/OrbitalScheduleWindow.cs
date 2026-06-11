using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.OrbitalScheduling;

/// <summary>
/// Full-screen window for building and controlling a logistics unit's orbital
/// transfer schedule. Mirrors <c>DispatchSlipsWindow</c>'s paper shell: dark
/// backdrop, centred paper panel, breadcrumb top bar, a swappable view host (leg
/// list ↔ leg wizard). Edits a draft schedule held on the unit's executor so state
/// survives closing/reopening; only valid schedules are allowed to start.
/// </summary>
public sealed partial class OrbitalScheduleWindow : Control, IOverlayPanel
{
    public static OrbitalScheduleWindow? Instance { get; private set; }

    [Signal] public delegate void ClosedEventHandler();

    private const int PanelWidth = 1280;
    private const int PanelHeight = 760;

    private static readonly Theme? PaperTheme = ResourceLoader.Load<Theme>(
        "res://UI/Theme/wireframe_paper/wireframe_paper.tres");

    private TransferTopBar? _topBar;
    private Control? _viewHost;
    private LegListView? _listView;
    private LegWizardView? _wizardView;

    private LogisticsUnit? _unit;
    private OrbitalTransferSchedule? _schedule;
    private LegEndpoint? _unitLocation;
    private bool _inWizard;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (PaperTheme != null) Theme = PaperTheme;
        BuildLayout();
        Hide();
    }

    private void BuildLayout()
    {
        var backdrop = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.6f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.GuiInput += OnBackdropInput;
        AddChild(backdrop);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            ThemeTypeVariation = "PaperPanel",
            CustomMinimumSize = new Vector2(PanelWidth, PanelHeight),
        };
        center.AddChild(panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 0);
        panel.AddChild(col);

        _topBar = TransferTopBar.Create();
        _topBar.HudCloseRequested += RequestClose;
        _topBar.BackRequested += RequestBack;
        col.AddChild(_topBar);

        _viewHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 640),
        };
        col.AddChild(_viewHost);

        _listView = new LegListView { Visible = true };
        _listView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _listView.AddLegRequested += OnAddLeg;
        _listView.EditLegRequested += OnEditLeg;
        _viewHost.AddChild(_listView);

        _wizardView = LegWizardView.Create();
        _wizardView.Visible = false;
        _wizardView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _wizardView.Committed += OnWizardCommitted;
        _wizardView.Cancelled += OnWizardCancelled;
        _viewHost.AddChild(_wizardView);
    }

    public void ShowWindow(LogisticsUnit unit)
    {
        _unit = unit;
        _unitLocation = OrbitalScheduleUiHelpers.GetUnitLocationEndpoint(unit);

        // Reuse the executor's draft if present; otherwise start a new repeating schedule.
        _schedule = unit.ScheduleExecutor?.ActiveSchedule ?? new OrbitalTransferSchedule { IsRepeating = true };
        if (_schedule.Legs.Count > 0 && _unitLocation != null)
            OrbitalScheduleEditor.Normalize(_schedule, _unitLocation);

        ShowList();
        Show();
        GameLogger.Info($"[OrbitalScheduleWindow] Opened for '{unit.Name}'");
    }

    public void HideWindow()
    {
        EnsureAssigned();
        Hide();
        _unit = null;
        _schedule = null;
        _unitLocation = null;
        GameLogger.Info("[OrbitalScheduleWindow] Closed");
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
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

    public void RequestClose()
    {
        HideWindow();
        EmitSignal(SignalName.Closed);
    }

    /// <summary>
    /// Back one level: inside the leg wizard returns to the leg list; on the list
    /// (no inner stack left) closes the overlay.
    /// </summary>
    public void RequestBack()
    {
        if (_inWizard) ShowList();
        else RequestClose();
    }

    private void ShowList()
    {
        _inWizard = false;
        if (_unit != null && _schedule != null)
            _listView?.Bind(_unit, _schedule, _unitLocation);
        if (_listView != null) _listView.Visible = true;
        if (_wizardView != null) _wizardView.Visible = false;
        _topBar?.SetTrail(BuildTrail("Schedule"));
    }

    private void ShowWizard(int legIndex)
    {
        if (_unit == null || _schedule == null || _unitLocation == null)
            return;
        _inWizard = true;
        _wizardView?.Begin(_unit, _schedule, _unitLocation, legIndex);
        if (_listView != null) _listView.Visible = false;
        if (_wizardView != null) _wizardView.Visible = true;
        _topBar?.SetTrail(BuildTrail(legIndex < 0 ? "Add Leg" : "Edit Leg"));
    }

    private List<string> BuildTrail(string leaf)
    {
        var name = _unit?.Name ?? "Unit";
        return new List<string> { name, "Orbital Schedule", leaf };
    }

    private void OnAddLeg() => ShowWizard(-1);

    private void OnEditLeg(int index) => ShowWizard(index);

    private void OnWizardCommitted()
    {
        EnsureAssigned();
        ShowList();
    }

    private void OnWizardCancelled() => ShowList();

    /// <summary>
    /// Assigns the draft to the executor once it has at least one leg, so it persists
    /// in-session (and is serialized on save) even while invalid. Never resets an
    /// already-assigned schedule.
    /// </summary>
    private void EnsureAssigned()
    {
        var exec = _unit?.ScheduleExecutor;
        if (exec == null || _schedule == null || _schedule.Legs.Count == 0)
            return;
        if (!ReferenceEquals(exec.ActiveSchedule, _schedule)
            && exec.ActiveSchedule?.State != OrbitalScheduleState.Running)
        {
            exec.AssignSchedule(_schedule);
        }
    }
}
