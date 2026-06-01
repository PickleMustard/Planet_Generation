using Constructables;
using Godot;
using UtilityLibrary;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Diegetic full-screen GUI window for inspecting a logistics unit (ship).
/// The player camera is repurposed (via <see cref="LogisticsUnitOrbitCamera"/>)
/// to frame the ship in the window's negative space; the cartouche sits upper-
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

    [Export]
    private LogisticsUnitOrbitCamera? _orbitCamera;

    private LogisticsUnit? _currentUnit;
    private Camera3D? _playerCamera;

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
        IsOpen = true;
        Visible = true;

        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        if (_orbitCamera != null && playerCamera != null)
        {
            _orbitCamera.ScreenOffset = ComputeCameraOffset();
            _orbitCamera.BeginOrbit(playerCamera, unit);
        }

        _cartouchePanel?.Populate(unit);
        _infoPanel?.Initialize(unit);
        _tabbedPanel?.Initialize(unit); // emits SectionSelected → info panel populates

        if (unit is Node unitNode)
            unitNode.TreeExiting += OnUnitExiting;

        GameLogger.Info($"[LogisticsUnitWindow] Opened for '{unit.Name}'");
    }

    public void HideWindow()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        _orbitCamera?.EndOrbit();
        _cartouchePanel?.Clear();
        _tabbedPanel?.Clear();
        _infoPanel?.Clear();

        if (_currentUnit is Node unitNode && IsInstanceValid(unitNode))
            unitNode.TreeExiting -= OnUnitExiting;

        Input.SetMouseMode(Input.MouseModeEnum.Captured);

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
