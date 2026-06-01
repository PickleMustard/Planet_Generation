using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Initial state when no GUI is open.
/// Ensures all GUI windows are hidden and the game is in normal play mode.
/// </summary>
public partial class IdleState : LimboState
{
    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "IdleState");

        // Ensure all GUI windows are hidden
        HideAllWindows();

        // Clear blackboard data
        Blackboard?.Top().Clear();

        // Ensure mouse is captured for gameplay
        Input.SetMouseMode(Input.MouseModeEnum.Captured);

        GameLogger.ExitFunction(nameof(_Enter));
    }

    private void HideAllWindows()
    {
        // Hide info windows
        CellInfo.VoronoiCellInfoWindow.Instance?.Hide();
        TransferPlanning.DispatchSlipsWindow.Instance?.Hide();
        OrbitalBodyWindow.Instance?.HideWindow();

        GameLogger.Debug("IdleState: All GUI windows hidden");
    }
}
