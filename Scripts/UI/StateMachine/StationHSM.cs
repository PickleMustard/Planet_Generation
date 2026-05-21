using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Sub-HSM for Station window management.
/// Manages the main StationView state and sub-states for bespoke features
/// (e.g. ShipQueueManagement for Shipyard stations).
/// Attached to GUIController/Station node.
/// </summary>
public partial class StationHSM : LimboHsm
{
    private LimboState? _stationView;
    private LimboState? _shipQueueManagement;

    [Export]
    public LimboState? StationView
    {
        get => _stationView;
        set => _stationView = value;
    }

    [Export]
    public LimboState? ShipQueueManagement
    {
        get => _shipQueueManagement;
        set => _shipQueueManagement = value;
    }

    public override void _Ready()
    {
        base._Ready();

        if (_shipQueueManagement != null)
        {
            AddTransition(_stationView, _shipQueueManagement, "ship_queue_opened");
            AddTransition(_shipQueueManagement, _stationView, "back_to_station");
        }

        InitialState = _stationView;

        GameLogger.Info("StationHSM initialized");
    }

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "StationHSM");

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        WorldInputController.Instance?.PushDisable();

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "StationHSM");

        Blackboard.Top()?.SetVar("SelectedStation", default(Variant));

        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        WorldInputController.Instance?.PopDisable();

        GameLogger.ExitFunction(nameof(_Exit));
    }
}
