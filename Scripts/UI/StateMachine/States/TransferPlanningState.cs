using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for managing the DispatchSlipsWindow.
/// Displays the dispatch-slips paper UI for the first transfer hub on the
/// selected continent. For per-building dispatch, prefer the HubPanelDetails
/// "Manage Routes" entry point.
/// </summary>
public partial class TransferPlanningState : LimboState
{
    private TransferPlanning.DispatchSlipsWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "TransferPlanningState");

        _window = GetNodeOrNull<TransferPlanning.DispatchSlipsWindow>("DispatchSlipsWindow");
        if (_window == null)
        {
            GameLogger.Error("TransferPlanningState: DispatchSlipsWindow not found as child");
            return;
        }

        var continentIndex = Blackboard?.Top().GetVar("SelectedContinentIndex").AsInt32() ?? -1;
        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();
        var continent = Blackboard?.Top().GetVar("SelectedContinent").As<Continent>();

        if (body == null || continentIndex < 0)
        {
            GameLogger.Warning("TransferPlanningState: Missing body/continent data in blackboard");
            Dispatch("transfer_closed");
            return;
        }

        var transferMgr = body switch
        {
            CelestialBody cb => cb.TransferMgr,
            SatelliteBody sb => sb.TransferMgr,
            _ => null,
        };
        if (transferMgr == null)
        {
            GameLogger.Warning("TransferPlanningState: body has no transfer manager");
            Dispatch("transfer_closed");
            return;
        }

        var hubIds = transferMgr.GetEndpointsOnContinent(continentIndex);
        if (hubIds.Count == 0)
        {
            GameLogger.Warning(
                $"TransferPlanningState: continent {continentIndex} has no transfer hubs"
            );
            Dispatch("transfer_closed");
            return;
        }

        // Pick the first hub on the continent. Per-building dispatch is the new
        // norm; multi-hub UI ergonomics belong in HubPanelDetails.
        var originBuilding = transferMgr.GetEndpointBuilding(hubIds[0]);
        if (originBuilding == null)
        {
            GameLogger.Warning("TransferPlanningState: hub building reference missing");
            Dispatch("transfer_closed");
            return;
        }

        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.ShowWindow(originBuilding, body, continent);

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.Debug(
            $"TransferPlanningState: Window shown for hub '{originBuilding.Name}' on continent {continentIndex}"
        );
    }

    public override void _Exit()
    {
        base._Exit();

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.HideWindow();
        }

        _window = null;
    }

    private void OnWindowCloseRequested()
    {
        GameLogger.Debug("TransferPlanningState: Window close requested");
        Dispatch("transfer_closed");
    }
}
