using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Top-level sub-HSM under GUIControllerHSM that hosts the BuildingInfoWindow.
/// Reachable from VoronoiCell, OrbitalBody, and Station HSMs via the
/// "building_details_opened" event. Single child state: BuildingDetailsState.
/// </summary>
public partial class BuildingHSM : LimboHsm
{
    private LimboState? _buildingDetails;

    [Export]
    public LimboState? BuildingDetails
    {
        get => _buildingDetails;
        set => _buildingDetails = value;
    }

    public override void _Ready()
    {
        base._Ready();
        InitialState = _buildingDetails;
        GameLogger.Info("BuildingHSM initialized");
    }

    public override void _Enter()
    {
        base._Enter();
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
    }

    public override void _Exit()
    {
        base._Exit();

        Blackboard.Top()?.SetVar("SelectedBuilding", default(Variant));
        Blackboard.Top()?.SetVar("BuildingReturnTo", default(Variant));
    }
}
