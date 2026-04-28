using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for displaying detailed building information.
/// Currently a minimal stub — transition exists in VoronoiCellHSM but
/// will not be triggered until a standalone building detail view is created.
/// </summary>
public partial class BuildingDetailsState : LimboState
{
    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "BuildingDetailsState");
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.Debug("BuildingDetailsState: Exiting");
    }
}
