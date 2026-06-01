using Constructables;
using Godot;
using UI.LogisticsOverview;
using UI.StateMachine;
using UtilityLibrary;

namespace UI.StateMachine.States;

/// <summary>
/// Main sub-state within ShipsOverviewHSM. Shows the fleet overview window and
/// routes its signals: close returns to the HUD; "View In-Depth" stashes the
/// selected unit and opens the per-unit details window, registering a
/// "back_to_ships_overview" return so the details window's Back button returns
/// here.
/// </summary>
public partial class ShipsOverviewState : LimboState
{
    private LogisticsOverviewWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "ShipsOverviewState");

        _window = LogisticsOverviewWindow.Instance;

        if (_window == null)
        {
            GameLogger.Warning("ShipsOverviewState: LogisticsOverviewWindow instance not found");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.LogisticsUnitInspectRequested += OnLogisticsUnitInspectRequested;

        _window.ShowWindow();

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "ShipsOverviewState");

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.LogisticsUnitInspectRequested -= OnLogisticsUnitInspectRequested;
            _window.HideWindow();
        }

        _window = null;

        GameLogger.ExitFunction(nameof(_Exit));
    }

    private void OnWindowCloseRequested()
    {
        InteractionStack.Clear(Blackboard?.Top());
        Dispatch("window_closed");
    }

    private void OnLogisticsUnitInspectRequested(Node unitNode)
    {
        if (unitNode is not LogisticsUnit)
            return;

        var bb = Blackboard?.Top();
        bb?.SetVar("SelectedLogisticsUnit", unitNode);
        if (bb != null)
            InteractionStack.Push(bb, "back_to_ships_overview", new Godot.Collections.Dictionary());

        Dispatch("logistics_unit_opened");
    }
}
