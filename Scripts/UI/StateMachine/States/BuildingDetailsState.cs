using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UI.BuildingInfo;
using UI.StateMachine;
using UtilityLibrary;

namespace UI.StateMachine.States;

/// <summary>
/// Shows the BuildingInfoWindow for the building stored in blackboard
/// at "SelectedBuilding". Back button pops the InteractionStack and
/// dispatches the return event to navigate to the previous panel.
/// Close-to-HUD clears the stack and dispatches "window_closed".
/// Also listens for BuildingInfoWindow.ManageRoutesRequested to transition to
/// TransferPlanning.
/// </summary>
public partial class BuildingDetailsState : LimboState
{
    private BuildingInfoWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "BuildingDetailsState");
        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        _window = BuildingInfoWindow.Instance;
        if (_window == null)
        {
            GameLogger.Error("BuildingDetailsState: BuildingInfoWindow.Instance is null");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        var buildingVariant = Blackboard?.Top()?.GetVar("SelectedBuilding");
        Building? building = null;
        if (buildingVariant != null && buildingVariant.Value.VariantType != Variant.Type.Nil)
            building = buildingVariant.Value.As<Building>();

        if (building == null)
        {
            GameLogger.Warning("BuildingDetailsState: No SelectedBuilding in blackboard");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.BackRequested += OnBackRequested;
        _window.ManageRoutesRequested += OnManageRoutesRequested;

        _window.ShowWindow(building);

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "BuildingDetailsState");

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.BackRequested -= OnBackRequested;
            _window.ManageRoutesRequested -= OnManageRoutesRequested;
            _window.HideWindow();
            _window.Clear();
        }
        _window = null;

        GameLogger.ExitFunction(nameof(_Exit));
    }

    private void OnWindowCloseRequested()
    {
        InteractionStack.Clear(Blackboard?.Top());
        Dispatch("window_closed");
    }

    private void OnBackRequested()
    {
        var bb = Blackboard?.Top();
        var returnEvent = InteractionStack.Pop(bb);
        if (returnEvent == null || returnEvent == "window_closed")
        {
            InteractionStack.Clear(bb);
            Dispatch("window_closed");
            return;
        }
        Dispatch(returnEvent);
    }

    private void OnManageRoutesRequested()
    {
        var bb = Blackboard?.Top();
        if (bb == null) return;

        var buildingVariant = bb.GetVar("SelectedBuilding");
        if (buildingVariant.VariantType == Variant.Type.Nil) return;
        var building = buildingVariant.As<Building>();
        if (building == null) return;

        int continentIndex = building.PrimaryCell?.ContinentIndex ?? -1;
        bb.SetVar("SelectedContinentIndex", continentIndex);

        // Ensure SelectedBody and SelectedContinent are available for TransferPlanningState
        if (bb.GetVar("SelectedBody").VariantType == Variant.Type.Nil)
        {
            Node? cursor = building.VisualNode;
            while (cursor != null)
            {
                if (cursor is Node3D body3D)
                {
                    bb.SetVar("SelectedBody", body3D);
                    break;
                }
                cursor = cursor.GetParent();
            }
        }
        if (bb.GetVar("SelectedContinent").VariantType == Variant.Type.Nil && continentIndex >= 0)
        {
            var bodyVariant = bb.GetVar("SelectedBody");
            if (bodyVariant.VariantType != Variant.Type.Nil)
            {
                var body = bodyVariant.As<Node3D>();
                if (body is ProceduralGeneration.PlanetGeneration.CelestialBody cb)
                    bb.SetVar("SelectedContinent", cb.Mesh?.GetContinent(continentIndex) ?? default(Variant));
            }
        }

        InteractionStack.Push(bb, "transfer_closed",
            InteractionStack.SnapshotVars(bb, "SelectedBuilding", "SelectedBody", "SelectedContinent", "SelectedContinentIndex"));
        Dispatch("transfer_opened");
    }
}
