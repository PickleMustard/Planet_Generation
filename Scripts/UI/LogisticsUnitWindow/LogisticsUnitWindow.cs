using Constructables;
using Godot;
using PlayerInteraction.Camera;
using UI.OrbitalScheduling;
using UtilityLibrary;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Diegetic full-screen GUI window for inspecting a logistics unit (ship).
/// The player camera reparents under the unit's anchor (via the
/// <see cref="PlayerInteraction.Camera.PlayerCameraController"/> focus state) to
/// frame the ship in the window's negative space; the cartouche sits upper-
/// left, the tab strip upper-right, and the info panel along the bottom.
/// Opened by a direct ship click or via cross-window navigation.
/// </summary>
public partial class LogisticsUnitWindow : Control
{
    public static LogisticsUnitWindow? Instance { get; private set; }

    [Export]
    private Button? _closeButton;

    [Export]
    private Button? _backButton;

    [Export]
    private LogisticsUnitCartouchePanel? _cartouchePanel;

    [Export]
    private LogisticsUnitTabbedPanel? _tabbedPanel;

    [Export]
    private LogisticsUnitInfoPanel? _infoPanel;

    private LogisticsUnit? _currentUnit;
    private Camera3D? _playerCamera;
    private PlayerCameraController? _controller;

    // Orbital-schedule editor overlay (created lazily, child of this window).
    private OrbitalScheduleWindow? _scheduleWindow;
    private Button? _editScheduleButton;

    // Right column holds the tab strip; the bottom strip holds the info panel.
    // Used to push the ship into the open center-left negative space.
    private const float TAB_PANEL_WIDTH = 440f;
    private const float INFO_PANEL_HEIGHT = 240f;

    public bool IsOpen { get; private set; }

    public LogisticsUnit? CurrentUnit => _currentUnit;

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void BackRequestedEventHandler();

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        if (_closeButton != null)
            _closeButton.Pressed += OnCloseButtonPressed;
        if (_backButton != null)
            _backButton.Pressed += OnBackPressed;

        if (_tabbedPanel != null && _infoPanel != null)
            _tabbedPanel.SectionSelected += OnSectionSelected;

        // Orbital-schedule editor overlay + its entry button (built in code so no
        // .tscn changes are required).
        _scheduleWindow = new OrbitalScheduleWindow();
        AddChild(_scheduleWindow);

        _editScheduleButton = new Button
        {
            Text = "✎ Edit Orbital Schedule",
            ThemeTypeVariation = "ButtonPrimary",
            Visible = false,
        };
        _editScheduleButton.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.KeepSize);
        _editScheduleButton.Position += new Vector2(0, 24);
        _editScheduleButton.Pressed += OnEditSchedulePressed;
        AddChild(_editScheduleButton);
    }

    public override void _ExitTree()
    {
        if (_tabbedPanel != null)
            _tabbedPanel.SectionSelected -= OnSectionSelected;

        if (Instance == this)
            Instance = null;
    }

    private void OnCloseButtonPressed() => EmitSignal(SignalName.WindowCloseRequested);

    private void OnBackPressed() => EmitSignal(SignalName.BackRequested);

    // ───────── Open / Close ─────────

    public void ShowWindow(LogisticsUnit unit, Camera3D playerCamera)
    {
        if (IsOpen)
            HideWindow();

        _currentUnit = unit;
        _playerCamera = playerCamera;
        _controller = playerCamera as PlayerCameraController;
        IsOpen = true;
        Visible = true;

        _controller?.EnterFocus(
            unit.GetOrCreateCameraAnchor(),
            unit,
            new LogisticsUnitFramingStrategy(unit),
            ComputeCameraOffset());

        _cartouchePanel?.Populate(unit);
        _infoPanel?.Initialize(unit);
        _tabbedPanel?.Initialize(unit); // emits SectionSelected → info panel populates

        if (_editScheduleButton != null)
            _editScheduleButton.Visible = true;

        if (unit is Node unitNode)
            unitNode.TreeExiting += OnUnitExiting;

        GameLogger.Info($"[LogisticsUnitWindow] Opened for '{unit.Name}'");
    }

    public void HideWindow()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        if (_scheduleWindow != null && _scheduleWindow.Visible)
            _scheduleWindow.HideWindow();
        if (_editScheduleButton != null)
            _editScheduleButton.Visible = false;

        _controller?.ExitFocus();
        _controller = null;
        _cartouchePanel?.Clear();
        _tabbedPanel?.Clear();
        _infoPanel?.Clear();

        if (_currentUnit is Node unitNode && IsInstanceValid(unitNode))
            unitNode.TreeExiting -= OnUnitExiting;

        _currentUnit = null;
        _playerCamera = null;
        Visible = false;

        GameLogger.Info("[LogisticsUnitWindow] Closed");
    }

    // ───────── Input ─────────

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

    // ───────── Callbacks ─────────

    private void OnSectionSelected(string sectionKey) => _infoPanel?.ShowSection(sectionKey);

    private void OnEditSchedulePressed()
    {
        if (_currentUnit != null)
            _scheduleWindow?.ShowWindow(_currentUnit);
    }

    private void OnUnitExiting()
    {
        GameLogger.Warning("[LogisticsUnitWindow] Inspected unit is leaving the tree — closing window");
        HideWindow();
    }

    private Vector2 ComputeCameraOffset()
    {
        // Positive X shifts the ship left (clear of the right tab strip);
        // negative Y lifts it above the bottom info panel.
        return new Vector2(TAB_PANEL_WIDTH * 0.5f, -INFO_PANEL_HEIGHT * 0.5f);
    }
}
