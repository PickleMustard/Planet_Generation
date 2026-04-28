using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for displaying the ContinentInfoWindow.
/// Shows the window on Enter, hides on Exit.
/// Translates window signals into HSM dispatch events.
/// </summary>
public partial class ContinentViewState : LimboState
{
    private ContinentInfo.ContinentInfoWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "ContinentViewState");

        _window = GetNodeOrNull<ContinentInfo.ContinentInfoWindow>("ContinentInfoWindow");
        if (_window == null)
        {
            GameLogger.Error("ContinentViewState: ContinentInfoWindow not found as child");
            return;
        }

        // Read data from blackboard
        var continentIndex = Blackboard?.Top().GetVar("SelectedContinentIndex").AsInt32() ?? -1;
        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();
        var continent = Blackboard?.Top().GetVar("SelectedContinent").As<Continent>();

        if (body == null || continentIndex < 0)
        {
            GameLogger.Warning("ContinentViewState: Missing body/continent data in blackboard");
            Dispatch("window_closed");
            return;
        }

        // Connect window signals
        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.TransferRequested += OnTransferRequested;

        // Show window with data
        _window.ShowWindow(continentIndex, body, continent);

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.Debug(
            $"ContinentViewState: Continent info window shown for continent {continentIndex}"
        );
    }

    public override void _Exit()
    {
        base._Exit();

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.TransferRequested -= OnTransferRequested;
            _window.HideWindow();
            _window.Clear();
        }

        _window = null;
    }

    private void OnWindowCloseRequested()
    {
        GameLogger.Debug("ContinentViewState: Window close requested");
        Dispatch("window_closed");
    }

    private void OnTransferRequested()
    {
        GameLogger.Debug("ContinentViewState: Transfer requested");
        Dispatch("transfer_opened");
    }
}
