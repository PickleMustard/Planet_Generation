using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Sub-HSM for Station window management.
/// Manages the main StationView state.
/// Attached to GUIController/Station node.
/// </summary>
public partial class StationHSM : LimboHsm
{
    private LimboState? _stationView;

    [Export]
    public LimboState? StationView
    {
        get => _stationView;
        set => _stationView = value;
    }

    public override void _Ready()
    {
        base._Ready();

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