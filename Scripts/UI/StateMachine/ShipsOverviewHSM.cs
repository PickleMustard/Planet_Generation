using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Sub-HSM for the fleet overview window. Manages the single
/// <see cref="States.ShipsOverviewState"/>. Attached to GUIController/ShipsOverview.
/// </summary>
public partial class ShipsOverviewHSM : LimboHsm
{
    private LimboState? _shipsOverviewView;

    [Export]
    public LimboState? ShipsOverviewView
    {
        get => _shipsOverviewView;
        set => _shipsOverviewView = value;
    }

    public override void _Ready()
    {
        base._Ready();

        InitialState = _shipsOverviewView;

        GameLogger.Info("ShipsOverviewHSM initialized");
    }

    public override void _Enter()
    {
        base._Enter();

        WorldInputController.Instance?.PushDisable();
    }

    public override void _Exit()
    {
        base._Exit();

        WorldInputController.Instance?.PopDisable();
    }
}
