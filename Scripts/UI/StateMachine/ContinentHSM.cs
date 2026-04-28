using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Sub-HSM for Continent window management.
/// Handles internal transitions between ContinentView and TransferPlanning states.
/// Attached to GUIController/Continent node.
/// </summary>
public partial class ContinentHSM : LimboHsm
{
    private LimboState? _continentView;
    private LimboState? _transferPlanning;

    [Export]
    public LimboState? ContinentView
    {
        get => _continentView;
        set => _continentView = value;
    }

    [Export]
    public LimboState? TransferPlanning
    {
        get => _transferPlanning;
        set => _transferPlanning = value;
    }

    [Signal]
    public delegate void TransferOpenedEventHandler();

    /// <summary>
    /// Called when the node enters the scene tree for the first time.
    /// Registers internal transitions. Parent HSM handles initialization.
    /// </summary>
    public override void _Ready()
    {
        // Register internal transitions
        AddTransition(_continentView, _transferPlanning, "transfer_opened");
        AddTransition(_transferPlanning, _continentView, "transfer_closed");
        AddTransition(_transferPlanning, _continentView, "escape_pressed");

        // When escape at ContinentView level, it's unhandled by this sub-HSM
        // and bubbles to GUIControllerHSM which transitions Continent -> HUD

        InitialState = _continentView;

        GameLogger.Info("ContinentHSM initialized");
    }

    public override void _Enter()
    {
        GameLogger.EnterFunction(nameof(_Enter), "ContinentHSM");
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "ContinentHSM");

        Blackboard.Top()?.SetVar("SelectedContinentIndex", default(Variant));
        Blackboard.Top()?.SetVar("SelectedContinent", default(Variant));

        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        GameLogger.ExitFunction(nameof(_Exit));
    }
}
