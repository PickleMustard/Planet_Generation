using Constructables;
using Godot;
using UI.StateMachine;
using UtilityLibrary;

namespace UI.StateMachine.States;

/// <summary>
/// Main sub-state within LogisticsUnitHSM. Shows the LogisticsUnitWindow when entered,
/// wires window close signal and dispatches return based on source tracking.
/// </summary>
public partial class LogisticsUnitViewState : LimboState
{
    private LogisticsUnitWindow.LogisticsUnitWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "LogisticsUnitViewState");

        _window = LogisticsUnitWindow.LogisticsUnitWindow.Instance;

        if (_window == null)
        {
            GameLogger.Warning("LogisticsUnitViewState: LogisticsUnitWindow instance not found");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        // Read unit from Blackboard
        var unitVariant = Blackboard?.Top()?.GetVar("SelectedLogisticsUnit");
        if (unitVariant == null || unitVariant.Value.VariantType == Variant.Type.Nil)
        {
            GameLogger.Warning("LogisticsUnitViewState: No SelectedLogisticsUnit in blackboard");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        var unitNode = unitVariant.Value.As<Node>();
        if (unitNode is not LogisticsUnit unit)
        {
            GameLogger.Warning("LogisticsUnitViewState: SelectedLogisticsUnit is not a LogisticsUnit");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.BackRequested += OnBackRequested;

        _window.ShowWindow(unit);

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "LogisticsUnitViewState");

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.BackRequested -= OnBackRequested;
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
}
