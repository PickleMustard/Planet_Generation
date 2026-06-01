using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UI.StateMachine;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for displaying the VoronoiCellInfoWindow.
/// Shows the window on Enter, hides on Exit.
/// Translates window signals into HSM dispatch events.
/// </summary>
public partial class CellOverviewState : LimboState
{
    private CellInfo.VoronoiCellInfoWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "CellOverviewState");

        _window = GetNodeOrNull<CellInfo.VoronoiCellInfoWindow>("VoronoiCellInfoWindow");
        if (_window == null)
        {
            GameLogger.Error("CellOverviewState: VoronoiCellInfoWindow not found as child");
            return;
        }

        var cell = Blackboard?.Top().GetVar("SelectedCell").As<VoronoiCell>();
        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();

        if (cell == null || body == null)
        {
            GameLogger.Warning("CellOverviewState: Missing cell/body data in blackboard");
            Dispatch("window_closed");
            return;
        }

        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.BackRequested += OnBackRequested;
        _window.BuildingDetailsRequested += OnBuildingDetailsRequested;
        _window.OrbitalBodyViewRequested += OnOrbitalBodyViewRequested;

        _window.ShowWindow(cell, body);

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.Debug("CellOverviewState: Cell info window shown");
    }

    public override void _Exit()
    {
        base._Exit();

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.BackRequested -= OnBackRequested;
            _window.BuildingDetailsRequested -= OnBuildingDetailsRequested;
            _window.OrbitalBodyViewRequested -= OnOrbitalBodyViewRequested;
            _window.HideWindow();
            _window.Clear();
        }

        _window = null;
    }

    private void OnWindowCloseRequested()
    {
        GameLogger.Debug("CellOverviewState: Window close requested");
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

    private void OnBuildingDetailsRequested(Building building)
    {
        if (building == null) return;

        Blackboard?.Top()?.SetVar("SelectedBuilding", building);
        var bb = Blackboard?.Top();
        if (bb != null)
            InteractionStack.Push(bb, "cell_selected",
                InteractionStack.SnapshotVars(bb, "SelectedCell", "SelectedBody", "BodyType"));
        Dispatch("building_details_opened");
    }

    private void OnOrbitalBodyViewRequested()
    {
        var bb = Blackboard?.Top();
        if (bb == null)
        {
            Dispatch("window_closed");
            return;
        }
        InteractionStack.Push(bb, "cell_selected",
            InteractionStack.SnapshotVars(bb, "SelectedCell", "SelectedBody", "BodyType"));
        Dispatch("orbital_body_selected");
    }
}
