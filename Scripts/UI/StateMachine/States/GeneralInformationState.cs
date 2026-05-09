using Constructables;
using Godot;
using UtilityLibrary;

namespace UI.StateMachine.States;

/// <summary>
/// Primary sub-state within OrbitalBodyHSM. Shows the OrbitalBodyWindow when entered,
/// wires window signals for close and station inspection dispatching.
/// </summary>
public partial class GeneralInformationState : LimboState
{
    private OrbitalBodyWindow? _window;

    [Export]
    public OrbitalBodyWindow? window
    {
        get => _window;
        set => _window = value;
    }

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "GeneralInformationState");

        if (_window == null)
        {
            GameLogger.Warning("GeneralInformationState: OrbitalBodyWindow instance not found");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        var camera = Blackboard?.Top()?.GetVar("PlayerCamera").As<Camera3D>();
        if (camera == null)
        {
            GameLogger.Warning("GeneralInformationState: Missing camera in blackboard");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        bool shown = false;
        OrbitalBodyConverter.GetFromBlackboard(
            Blackboard?.Top(),
            "SelectedBody",
            onCelestialBody: cb =>
            {
                _window.ShowWindow(cb, camera);
                shown = true;
            },
            onSatelliteBody: sb =>
            {
                _window.ShowWindow(sb, camera);
                shown = true;
            }
        );

        if (!shown)
        {
            GameLogger.Warning("GeneralInformationState: No valid body in blackboard");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.StationInspectRequested += OnStationInspectRequested;
        _window.LogisticsUnitInspectRequested += OnLogisticsUnitInspectRequested;
        _window.BuildingInspectRequested += OnBuildingInspectRequested;

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "GeneralInformationState");

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.StationInspectRequested -= OnStationInspectRequested;
            _window.LogisticsUnitInspectRequested -= OnLogisticsUnitInspectRequested;
            _window.BuildingInspectRequested -= OnBuildingInspectRequested;
            _window.HideWindow();
        }

        GameLogger.ExitFunction(nameof(_Exit));
    }

    private void OnWindowCloseRequested()
    {
        GameLogger.Debug("GeneralInformationState: Window close requested");
        Dispatch("window_closed");
    }

    private void OnStationInspectRequested(int stationIndex)
    {
        if (_window?.CurrentBody?.SatellitesContainer == null)
            return;

        int idx = 0;
        foreach (var child in _window.CurrentBody.SatellitesContainer.GetChildren())
        {
            if (child is Constructables.StationSatellite station)
            {
                if (idx == stationIndex)
                {
                    Blackboard?.Top()?.SetVar("SelectedStation", (Node)station);
                    Dispatch("station_opened");
                    return;
                }
                idx++;
            }
        }

        GameLogger.Warning($"GeneralInformationState: Station index {stationIndex} not found");
    }

    private void OnLogisticsUnitInspectRequested(int unitIndex)
    {
        if (_window?.CurrentBody?.SatellitesContainer == null)
            return;

        int idx = 0;
        foreach (var child in _window.CurrentBody.SatellitesContainer.GetChildren())
        {
            if (child is Constructables.LogisticsUnit unit)
            {
                if (idx == unitIndex)
                {
                    Blackboard?.Top()?.SetVar("SelectedLogisticsUnit", (Node)unit);
                    Blackboard?.Top()?.SetVar("LogisticsUnitSource", "orbital_body");
                    Dispatch("logistics_unit_opened");
                    return;
                }
                idx++;
            }
        }

        GameLogger.Warning($"GeneralInformationState: LogisticsUnit index {unitIndex} not found");
    }

    private void OnBuildingInspectRequested(Building building)
    {
        if (building == null) return;

        Blackboard?.Top()?.SetVar("SelectedBuilding", building);
        Blackboard?.Top()?.SetVar("BuildingReturnTo", "body");
        Dispatch("building_details_opened");
    }
}
