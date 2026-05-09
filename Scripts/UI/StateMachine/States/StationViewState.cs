using Constructables;
using Godot;
using UtilityLibrary;

namespace UI.StateMachine.States;

/// <summary>
/// Main sub-state within StationHSM. Shows the StationWindow when entered,
/// wires window signals for close and bespoke feature dispatching.
/// </summary>
public partial class StationViewState : LimboState
{
    private StationWindow.StationWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "StationViewState");

        _window = StationWindow.StationWindow.Instance;

        if (_window == null)
        {
            GameLogger.Warning("StationViewState: StationWindow instance not found");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        // Read station from Blackboard
        var stationVariant = Blackboard?.Top()?.GetVar("SelectedStation");
        if (stationVariant == null || stationVariant.Value.VariantType == Variant.Type.Nil)
        {
            GameLogger.Warning("StationViewState: No SelectedStation in blackboard");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        var stationNode = stationVariant.Value.As<Node>();
        if (stationNode is not StationSatellite station)
        {
            GameLogger.Warning("StationViewState: SelectedStation is not a StationSatellite");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.BespokeFeatureRequested += OnBespokeFeatureRequested;
        _window.BuildingInspectRequested += OnBuildingInspectRequested;

        _window.ShowWindow(station);

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "StationViewState");

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.BespokeFeatureRequested -= OnBespokeFeatureRequested;
            _window.BuildingInspectRequested -= OnBuildingInspectRequested;
            _window.HideWindow();
        }

        _window = null;

        GameLogger.ExitFunction(nameof(_Exit));
    }

    private void OnWindowCloseRequested()
    {
        Dispatch("window_closed");
    }

    private void OnBespokeFeatureRequested(string featureId)
    {
        switch (featureId)
        {
            case "ship_queue":
                Dispatch("ship_queue_opened");
                break;
            default:
                GameLogger.Warning($"StationViewState: Unknown bespoke feature '{featureId}'");
                break;
        }
    }

    private void OnBuildingInspectRequested(Building building)
    {
        if (building == null) return;
        Blackboard?.Top()?.SetVar("SelectedBuilding", building);
        Blackboard?.Top()?.SetVar("BuildingReturnTo", "station");
        Dispatch("building_details_opened");
    }
}
