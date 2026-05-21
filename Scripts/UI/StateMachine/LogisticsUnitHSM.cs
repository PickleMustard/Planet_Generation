using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Sub-HSM for Logistics Unit window management.
/// Manages the main LogisticsUnitView state.
/// Attached to GUIController/LogisticsUnit node.
/// </summary>
public partial class LogisticsUnitHSM : LimboHsm
{
    private LimboState? _logisticsUnitView;

    [Export]
    public LimboState? LogisticsUnitView
    {
        get => _logisticsUnitView;
        set => _logisticsUnitView = value;
    }

    public override void _Ready()
    {
        base._Ready();

        InitialState = _logisticsUnitView;

        GameLogger.Info("LogisticsUnitHSM initialized");
    }

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "LogisticsUnitHSM");

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        WorldInputController.Instance?.PushDisable();

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "LogisticsUnitHSM");

        Blackboard.Top()?.SetVar("SelectedLogisticsUnit", default(Variant));

        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        WorldInputController.Instance?.PopDisable();

        GameLogger.ExitFunction(nameof(_Exit));
    }
}
