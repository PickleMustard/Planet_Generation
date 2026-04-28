using Constructables;
using Godot;
using UtilityLibrary;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// GUI window for inspecting logistics units (ships).
/// Panels arranged around screen edges: header (top-left), tabs (right), details (bottom).
/// Opened via HUD click or cross-window transition from OrbitalBody/Station windows.
/// </summary>
public partial class LogisticsUnitWindow : Control
{
    public static LogisticsUnitWindow? Instance { get; private set; }

    [Export]
    private Button? _closeButton;

    [Export]
    private LogisticsUnitHeaderPanel? _headerPanel;

    [Export]
    private LogisticsUnitTabbedPanel? _tabbedPanel;

    [Export]
    private LogisticsUnitDetailsPanel? _detailsPanel;

    private LogisticsUnit? _currentUnit;

    /// <summary>Whether this window is currently open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The logistics unit currently being inspected, or null.</summary>
    public LogisticsUnit? CurrentUnit => _currentUnit;

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        if (_closeButton != null)
            _closeButton.Pressed += OnCloseButtonPressed;
    }

    private void OnCloseButtonPressed()
    {
        EmitSignal(SignalName.WindowCloseRequested);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    // ───────── Open / Close ─────────

    public void ShowWindow(LogisticsUnit unit)
    {
        if (IsOpen)
            HideWindow();

        _currentUnit = unit;
        IsOpen = true;
        Visible = true;

        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        // Populate panels
        _headerPanel?.Populate(unit);
        _tabbedPanel?.Initialize(unit);
        if (_tabbedPanel != null && _detailsPanel != null)
            _detailsPanel.Initialize(unit, _tabbedPanel);

        // Listen for unit removal
        if (unit is Node unitNode)
            unitNode.TreeExiting += OnUnitExiting;

        GameLogger.Info($"[LogisticsUnitWindow] Opened for '{unit.Name}'");
    }

    public void HideWindow()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        // Clear panels
        _headerPanel?.Clear();
        if (_tabbedPanel != null && _detailsPanel != null)
            _detailsPanel.Disconnect(_tabbedPanel);
        _tabbedPanel?.Clear();
        _detailsPanel?.Clear();

        // Disconnect unit signal
        if (_currentUnit is Node unitNode && IsInstanceValid(unitNode))
            unitNode.TreeExiting -= OnUnitExiting;

        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        _currentUnit = null;
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

    private void OnUnitExiting()
    {
        GameLogger.Warning(
            "[LogisticsUnitWindow] Inspected unit is leaving the tree — closing window"
        );
        HideWindow();
    }
}
