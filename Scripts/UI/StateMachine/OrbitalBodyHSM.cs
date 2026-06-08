using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Sub-HSM for OrbitalBody window management.
/// Handles tab transitions between GeneralInformation, ContinentsInformation,
/// StationsInformation, and TransfersInformation states.
/// Attached to GUIController/OrbitalBody node.
/// </summary>
public partial class OrbitalBodyHSM : LimboHsm
{
    private LimboState? _generalInfo;
    private LimboState? _continentsInfo;
    private LimboState? _stationsInfo;
    private LimboState? _transfersInfo;

    [Export]
    public LimboState? GeneralInfo
    {
        get => _generalInfo;
        set => _generalInfo = value;
    }

    [Export]
    public LimboState? ContinentsInfo
    {
        get => _continentsInfo;
        set => _continentsInfo = value;
    }

    [Export]
    public LimboState? StationsInfo
    {
        get => _stationsInfo;
        set => _stationsInfo = value;
    }

    [Export]
    public LimboState? TransfersInfo
    {
        get => _transfersInfo;
        set => _transfersInfo = value;
    }

    /// <summary>
    /// Called when the node enters the scene tree for the first time.
    /// Registers tab transitions and initializes the sub-HSM.
    /// </summary>
    public override void _Ready()
    {
        base._Ready();

        // Register tab transitions only for states that exist in the scene.
        // Future children (ContinentsInfo, StationsInfo, TransfersInfo) can be
        // wired up as they are added to the scene tree.
        if (_continentsInfo != null)
        {
            AddTransition(_generalInfo, _continentsInfo, "tab_continents");
            AddTransition(_continentsInfo, _generalInfo, "tab_general");

            if (_stationsInfo != null)
                AddTransition(_continentsInfo, _stationsInfo, "tab_stations");
            if (_transfersInfo != null)
                AddTransition(_continentsInfo, _transfersInfo, "tab_transfers");
        }

        if (_stationsInfo != null)
        {
            AddTransition(_generalInfo, _stationsInfo, "tab_stations");
            AddTransition(_stationsInfo, _generalInfo, "tab_general");

            if (_continentsInfo != null)
                AddTransition(_stationsInfo, _continentsInfo, "tab_continents");
            if (_transfersInfo != null)
                AddTransition(_stationsInfo, _transfersInfo, "tab_transfers");
        }

        if (_transfersInfo != null)
        {
            AddTransition(_generalInfo, _transfersInfo, "tab_transfers");
            AddTransition(_transfersInfo, _generalInfo, "tab_general");

            if (_continentsInfo != null)
                AddTransition(_transfersInfo, _continentsInfo, "tab_continents");
            if (_stationsInfo != null)
                AddTransition(_transfersInfo, _stationsInfo, "tab_stations");
        }

        InitialState = _generalInfo;

        GameLogger.Info("OrbitalBodyHSM initialized");
    }

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "OrbitalBodyHSM");
        // World-input suppression is owned by PlayerCameraController.EnterFocus /
        // ExitFocus, so it covers all focus windows uniformly.
        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "OrbitalBodyHSM");
        GameLogger.ExitFunction(nameof(_Exit));
    }
}
