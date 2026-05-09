using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Root Hierarchical State Machine for GUI systems.
/// Extends GuiHSM to manage top-level GUI state transitions.
/// Attached to GUIController node in MainGameUI.
/// </summary>
public partial class GUIControllerHSM : LimboHsm
{
    private LimboState? _hud;
    private LimboHsm? _voronoiCell;
    private LimboHsm? _continentHSM;
    private LimboHsm? _orbitalBodyHSM;
    private LimboHsm? _constructionHSM;
    private LimboHsm? _stationHSM;
    private LimboHsm? _logisticsUnitHSM;
    private LimboHsm? _buildingHSM;
    private LimboState? _gameStart;
    private LimboState? _pauseMenu;

    [Export]
    public LimboState? HUD
    {
        get => _hud;
        set => _hud = value;
    }

    [Export]
    public LimboHsm? VoronoiCell
    {
        get => _voronoiCell;
        set => _voronoiCell = value;
    }

    [Export]
    public LimboHsm? Continent
    {
        get => _continentHSM;
        set => _continentHSM = value;
    }

    [Export]
    public LimboHsm? OrbitalBody
    {
        get => _orbitalBodyHSM;
        set => _orbitalBodyHSM = value;
    }

    [Export]
    public LimboHsm? ConstructionMenu
    {
        get => _constructionHSM;
        set => _constructionHSM = value;
    }

    [Export]
    public LimboHsm? Station
    {
        get => _stationHSM;
        set => _stationHSM = value;
    }

    [Export]
    public LimboHsm? LogisticsUnit
    {
        get => _logisticsUnitHSM;
        set => _logisticsUnitHSM = value;
    }

    [Export]
    public LimboHsm? Building
    {
        get => _buildingHSM;
        set => _buildingHSM = value;
    }

    [Export]
    public LimboState? GameStart
    {
        get => _gameStart;
        set => _gameStart = value;
    }

    [Export]
    public LimboState? PauseMenu
    {
        get => _pauseMenu;
        set => _pauseMenu = value;
    }

    /// <summary>
    /// Called when the node enters the scene tree for the first time.
    /// Resolves child state references, registers transitions, and initializes the HSM.
    /// </summary>
    public override void _Ready()
    {
        Camera3D playerCamera = GetViewport().GetCamera3D();
        Blackboard.Top().SetVar("PlayerCamera", playerCamera);

        // Call base._Ready() after setting up blackboard but before AddTransition
        // This ensures child HSMs get their blackboard before they initialize
        //base._Ready();
        var mainGameUI = GetNode<Control>("..");

        AddTransition(_hud, _voronoiCell, new StringName("cell_selected"));
        AddTransition(_hud, _orbitalBodyHSM, new StringName("orbital_body_selected"));
        AddTransition(_hud, _constructionHSM, new StringName("construction_menu_opened"));

        AddTransition(_voronoiCell, _hud, "window_closed");
        AddTransition(_orbitalBodyHSM, _hud, "window_closed");
        AddTransition(_constructionHSM, _hud, "window_closed");
        AddTransition(_stationHSM, _hud, "window_closed");

        // Cross-window transitions
        AddTransition(_orbitalBodyHSM, _voronoiCell, "cell_selected");

        // Station cross-window transitions
        AddTransition(_orbitalBodyHSM, _stationHSM, "station_opened");
        AddTransition(_stationHSM, _orbitalBodyHSM, "back_to_orbital_body");

        // LogisticsUnit transitions
        AddTransition(_hud, _logisticsUnitHSM, "logistics_unit_selected");
        AddTransition(_logisticsUnitHSM, _hud, "window_closed");
        AddTransition(_orbitalBodyHSM, _logisticsUnitHSM, "logistics_unit_opened");
        AddTransition(_logisticsUnitHSM, _orbitalBodyHSM, "back_to_orbital_body");
        AddTransition(_stationHSM, _logisticsUnitHSM, "logistics_unit_opened");
        AddTransition(_logisticsUnitHSM, _stationHSM, "back_to_station");

        AddTransition(_voronoiCell, _buildingHSM, "building_details_opened");
        AddTransition(_orbitalBodyHSM, _buildingHSM, "building_details_opened");
        AddTransition(_stationHSM, _buildingHSM, "building_details_opened");
        AddTransition(_buildingHSM, _hud, "window_closed");
        AddTransition(_buildingHSM, _voronoiCell, "cell_selected");
        AddTransition(_buildingHSM, _orbitalBodyHSM, "orbital_body_selected");
        AddTransition(_buildingHSM, _stationHSM, "station_opened");

        // Game start flow: headquarters placement routes through dedicated state
        AddTransition(ANYSTATE, _gameStart, "headquarters_placed");
        AddTransition(_gameStart, _hud, "game_started");

        // Pause menu transitions
        AddTransition(_hud, _pauseMenu, "pause_requested");
        AddTransition(_pauseMenu, _hud, "resume_requested");

        // Universal placement confirmation still routes anywhere back to HUD
        AddTransition(ANYSTATE, _hud, "placement_confirmed");

        InitialState = _hud;
        Initialize(this);
        SetActive(true);

        GameLogger.Info("GUIControllerHSM initialized");
    }

    public override void _ExitTree()
    {
        base._ExitTree();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel", false, false))
            return;

        var active = GetActiveState();
        if (active == _hud)
        {
            Dispatch("pause_requested");
        }
        else if (active == _pauseMenu)
        {
            Dispatch("resume_requested");
        }
        else if (active == _gameStart)
        {
            // Headquarters placement — swallow escape; placement state handles its own input.
            return;
        }
        else
        {
            Dispatch("window_closed");
        }

        GetViewport().SetInputAsHandled();
    }
}
