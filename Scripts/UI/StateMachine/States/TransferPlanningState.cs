using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for managing the TransferPlanningWindow.
/// Displays transfer planning interface for resource logistics between continents.
/// </summary>
public partial class TransferPlanningState : LimboState
{
    private TransferPlanning.TransferPlanningWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "TransferPlanningState");

        _window = GetNodeOrNull<TransferPlanning.TransferPlanningWindow>("TransferPlanningWindow");
        if (_window == null)
        {
            GameLogger.Error("TransferPlanningState: TransferPlanningWindow not found as child");
            return;
        }

        // Read data from blackboard
        var continentIndex = Blackboard?.Top().GetVar("SelectedContinentIndex").AsInt32() ?? -1;
        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();
        var continent = Blackboard?.Top().GetVar("SelectedContinent").As<Continent>();

        if (body == null || continentIndex < 0)
        {
            GameLogger.Warning("TransferPlanningState: Missing body/continent data in blackboard");
            Dispatch("transfer_closed");
            return;
        }

        // Connect window signals
        _window.WindowCloseRequested += OnWindowCloseRequested;

        // Show the transfer planning window
        _window.ShowWindow(continentIndex, body, continent!);

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.Debug($"TransferPlanningState: Window shown for continent {continentIndex}");
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
