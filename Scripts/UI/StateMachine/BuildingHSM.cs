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
    private LimboState? _transferPlanning;

    [Export]
    public LimboState? BuildingDetails
    {
        get => _buildingDetails;
        set => _buildingDetails = value;
    }

    [Export]
    public LimboState? TransferPlanning
    {
        get => _transferPlanning;
        set => _transferPlanning = value;
    }

    public override void _Ready()
    {
        base._Ready();
        AddTransition(_buildingDetails, _transferPlanning, "transfer_opened");
        AddTransition(_transferPlanning, _buildingDetails, "transfer_closed");
        InitialState = _buildingDetails;
        GameLogger.Info("BuildingHSM initialized");
    }

    public override void _Enter()
    {
        base._Enter();
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        WorldInputController.Instance?.PushDisable();
    }

    public override void _Exit()
    {
        base._Exit();

        Blackboard.Top()?.SetVar("SelectedBuilding", default(Variant));

        WorldInputController.Instance?.PopDisable();
    }
}
