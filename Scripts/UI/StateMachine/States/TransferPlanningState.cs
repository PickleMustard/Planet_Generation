using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UI.StateMachine;
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

        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();
        var continent = Blackboard?.Top().GetVar("SelectedContinent").As<Continent>();
        var continentIndex = Blackboard?.Top().GetVar("SelectedContinentIndex").AsInt32() ?? -1;

        if (body == null)
        {
            GameLogger.Warning("TransferPlanningState: SelectedBody missing in blackboard");
            Dispatch("transfer_closed");
            return;
        }

        if (body is not IOrbitalBody orbitalBody)
        {
            GameLogger.Warning("TransferPlanningState: body does not implement IOrbitalBody");
            Dispatch("transfer_closed");
            return;
        }

        Building? originBuilding = ResolveSelectedBuilding(orbitalBody);
        if (originBuilding == null)
        {
            if (continentIndex < 0)
            {
                GameLogger.Warning("TransferPlanningState: no SelectedBuilding and no SelectedContinentIndex");
                Dispatch("transfer_closed");
                return;
            }

            var hubIds = orbitalBody.GetTransferEndpointsOnContinent(continentIndex);
            if (hubIds.Count == 0)
            {
                GameLogger.Warning(
                    $"TransferPlanningState: continent {continentIndex} has no transfer hubs"
                );
                Dispatch("transfer_closed");
                return;
            }

            GameLogger.Warning(
                $"TransferPlanningState: SelectedBuilding missing; falling back to first hub on continent {continentIndex}"
            );
            var originOwner = orbitalBody.GetTransferEndpointOwner(hubIds[0]);
            originBuilding = originOwner as Building;
            if (originBuilding == null)
            {
                GameLogger.Warning("TransferPlanningState: hub owner missing or not a Building");
                Dispatch("transfer_closed");
                return;
            }
        }

        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.HudCloseRequested += OnHudCloseRequested;
        _window.ShowWindow(originBuilding, body, continent);

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.Debug(
            $"TransferPlanningState: Window shown for hub '{originBuilding.Name}' (continent {continentIndex})"
        );
    }

    private Building? ResolveSelectedBuilding(IOrbitalBody orbitalBody)
    {
        var buildingVariant = Blackboard?.Top()?.GetVar("SelectedBuilding");
        if (buildingVariant == null || buildingVariant.Value.VariantType == Variant.Type.Nil)
            return null;
        var building = buildingVariant.Value.As<Building>();
        if (building == null || string.IsNullOrEmpty(building.Id))
            return null;
        if (!orbitalBody.HasTransferEndpoint(building.Id))
            return null;
        return building;
    }

    public override void _Exit()
    {
        base._Exit();

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.HudCloseRequested -= OnHudCloseRequested;
            _window.HideWindow();
        }

        _window = null;
    }

    private void OnWindowCloseRequested()
    {
        GameLogger.Debug("TransferPlanningState: Window close requested (back to building details)");
        Dispatch("transfer_closed");
    }

    private void OnHudCloseRequested()
    {
        GameLogger.Debug("TransferPlanningState: HUD close requested (return to HUD)");
        InteractionStack.Clear(Blackboard?.Top());
        Dispatch("window_closed");
    }
}
