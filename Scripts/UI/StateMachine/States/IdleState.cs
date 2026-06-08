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

        // Back at the HUD: drop any leaked cursor requests and restore the
        // gameplay base mode (Captured, or Confined if the player toggled it).
        WorldInputController.Instance?.ResetCursorMode();

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
