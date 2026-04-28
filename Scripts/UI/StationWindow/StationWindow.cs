using Constructables;
using Godot;
using UtilityLibrary;

namespace UI.StationWindow;

/// <summary>
/// Diegetic GUI window for inspecting orbital stations (StationSatellite and subclasses).
/// UI panels are arranged around the screen edges. Opened via cross-window transition
/// from OrbitalBodyWindow or direct selection; closed via Escape or close button.
/// </summary>
public partial class StationWindow : Control
{
    public static StationWindow? Instance { get; private set; }

    [Export]
    private Button? _closeButton;

    [Export]
    private StationHeaderPanel? _headerPanel;

    [Export]
    private StationTabbedPanel? _tabbedPanel;

    [Export]
    private StationDetailsPanel? _detailsPanel;

    [Export]
    private StationBespokeBar? _bespokeBar;

    private StationSatellite? _currentStation;

    /// <summary>Whether this window is currently open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The station currently being inspected, or null.</summary>
    public StationSatellite? CurrentStation => _currentStation;

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void BespokeFeatureRequestedEventHandler(string featureId);

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

    public void ShowWindow(StationSatellite station)
    {
        if (IsOpen)
            HideWindow();

        _currentStation = station;
        IsOpen = true;
        Visible = true;

        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        // Populate panels
        _headerPanel?.Populate(station);
        _tabbedPanel?.Initialize(station);
        if (_tabbedPanel != null && _detailsPanel != null)
            _detailsPanel.Initialize(station, _tabbedPanel);
        _bespokeBar?.Initialize(station);

        // Connect bespoke bar signal relay
        if (_bespokeBar != null)
            _bespokeBar.FeatureRequested += OnBespokeFeatureRequested;

        // Listen for station removal
        if (station is Node stationNode)
            stationNode.TreeExiting += OnStationExiting;

        GameLogger.Info($"[StationWindow] Opened for '{station.Name}'");
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
        _bespokeBar?.Clear();

        // Disconnect bespoke bar signal
        if (_bespokeBar != null)
            _bespokeBar.FeatureRequested -= OnBespokeFeatureRequested;

        // Disconnect station signal
        if (_currentStation is Node stationNode && IsInstanceValid(stationNode))
            stationNode.TreeExiting -= OnStationExiting;

        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        _currentStation = null;
        Visible = false;

        GameLogger.Info("[StationWindow] Closed");
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

    private void OnBespokeFeatureRequested(string featureId)
    {
        EmitSignal(SignalName.BespokeFeatureRequested, featureId);
    }

    private void OnStationExiting()
    {
        GameLogger.Warning(
            "[StationWindow] Inspected station is leaving the tree — closing window"
        );
        HideWindow();
    }
}
