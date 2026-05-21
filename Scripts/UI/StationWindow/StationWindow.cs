using Constructables;
using Godot;
using UtilityLibrary;

namespace UI.StationWindow;

/// <summary>
/// Diegetic full-screen GUI window for inspecting orbital stations
/// (<see cref="StationSatellite"/>). The main player camera is repurposed to
/// frame the station in the left third of the viewport with the station's
/// parent body behind it; the right two thirds host the cartouche (upper
/// left) and a behavior-driven tabbed panel.
/// </summary>
public partial class StationWindow : Control
{
    public static StationWindow? Instance { get; private set; }

    [Export]
    private Button? _closeButton;

    [Export]
    private Button? _backButton;

    [Export]
    private StationCartouchePanel? _cartouchePanel;

    [Export]
    private StationTabbedPanel? _tabbedPanel;

    [Export]
    private StationOrbitCamera? _orbitCamera;

    private StationSatellite? _currentStation;
    private Camera3D? _playerCamera;

    // Right two-thirds is the interactive panel region; left third holds the
    // station view. Mirrors the negative-space pattern used by
    // OrbitalBodyWindow.ComputeCameraOffset.
    private const float TAB_PANEL_FRACTION = 2f / 3f;

    public bool IsOpen { get; private set; }

    public StationSatellite? CurrentStation => _currentStation;

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void BuildingInspectRequestedEventHandler(Building building);

    public void RequestBuildingInspect(Building building)
        => EmitSignal(SignalName.BuildingInspectRequested, building);

    public override void _Ready()
    {
        Instance = this;
        Visible = false;

        if (_closeButton != null)
            _closeButton.Pressed += OnCloseButtonPressed;
        if (_backButton != null)
            _backButton.Pressed += OnBackPressed;
    }

    private void OnCloseButtonPressed()
        => EmitSignal(SignalName.WindowCloseRequested);

    private void OnBackPressed()
        => EmitSignal(SignalName.BackRequested);

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowWindow(StationSatellite station, Camera3D playerCamera)
    {
        if (IsOpen)
            HideWindow();

        _currentStation = station;
        _playerCamera = playerCamera;
        IsOpen = true;
        Visible = true;

        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        if (_orbitCamera != null)
        {
            _orbitCamera.ScreenOffset = ComputeCameraOffset();
            _orbitCamera.BeginOrbit(playerCamera, station);
        }

        _cartouchePanel?.Populate(station);
        _tabbedPanel?.Initialize(station);

        if (station is Node stationNode)
            stationNode.TreeExiting += OnStationExiting;

        GameLogger.Info($"[StationWindow] Opened for '{station.Name}'");
    }

    public void HideWindow()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        _orbitCamera?.EndOrbit();
        _cartouchePanel?.Clear();
        _tabbedPanel?.Clear();

        if (_currentStation is Node stationNode && IsInstanceValid(stationNode))
            stationNode.TreeExiting -= OnStationExiting;

        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        _currentStation = null;
        _playerCamera = null;
        Visible = false;

        GameLogger.Info("[StationWindow] Closed");
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

    private Vector2 ComputeCameraOffset()
    {
        // Positive X shifts the station LEFT on screen — center it in the
        // left third by sliding by half the panel-region width.
        var vp = GetViewport().GetVisibleRect().Size;
        return new Vector2(vp.X * TAB_PANEL_FRACTION * 0.5f, 0f);
    }

    private void OnStationExiting()
    {
        GameLogger.Warning("[StationWindow] Inspected station is leaving the tree — closing window");
        HideWindow();
    }
}
