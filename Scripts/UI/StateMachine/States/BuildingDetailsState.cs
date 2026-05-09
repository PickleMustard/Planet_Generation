using Constructables;
using Godot;
using Structures.GameState;
using UI.BuildingInfo;
using UtilityLibrary;

namespace UI.StateMachine.States;

/// <summary>
/// Shows the BuildingInfoWindow for the building stored in blackboard
/// at "SelectedBuilding". Back button reads "BuildingReturnTo" and dispatches
/// the matching transition (cell_selected / orbital_body_selected / station_opened)
/// so existing parent-chain transitions handle the actual back navigation.
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
            _window.HideWindow();
            _window.Clear();
        }
        _window = null;

        GameLogger.ExitFunction(nameof(_Exit));
    }

    private void OnWindowCloseRequested()
    {
        Dispatch("window_closed");
    }

    private void OnBackRequested()
    {
        var ret = Blackboard?.Top()?.GetVar("BuildingReturnTo").AsString();
        switch (ret)
        {
            case "cell":
                Dispatch("cell_selected");
                break;
            case "body":
                Dispatch("orbital_body_selected");
                break;
            case "station":
                Dispatch("station_opened");
                break;
            default:
                Dispatch("window_closed");
                break;
        }
    }
}
