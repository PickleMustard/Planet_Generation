using Godot;
using ProceduralGeneration.PlanetGeneration;
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
    private LimboHsm? _orbitalBodyHSM;
    private LimboHsm? _stationHSM;
    private LimboHsm? _logisticsUnitHSM;
    private LimboHsm? _shipsOverviewHSM;
    private LimboHsm? _buildingHSM;
    private LimboState? _planetBoard;
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
    public LimboHsm? OrbitalBody
    {
        get => _orbitalBodyHSM;
        set => _orbitalBodyHSM = value;
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
    public LimboHsm? ShipsOverview
    {
        get => _shipsOverviewHSM;
        set => _shipsOverviewHSM = value;
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

    [Export]
    public LimboState? PlanetBoard
    {
        get => _planetBoard;
        set => _planetBoard = value;
    }

    /// <summary>
    /// Called when the node enters the scene tree for the first time.
    /// Resolves child state references, registers transitions, and initializes the HSM.
    /// </summary>
    public override void _Ready()
    {
        Camera3D playerCamera = GetViewport().GetCamera3D();
        Blackboard.Top().SetVar("PlayerCamera", playerCamera);
        Blackboard.Top().SetVar(InteractionStack.BB_KEY, Blackboard.Top());

        // Call base._Ready() after setting up blackboard but before AddTransition
        // This ensures child HSMs get their blackboard before they initialize
        //base._Ready();
        var mainGameUI = GetNode<Control>("..");

        AddTransition(_hud, _voronoiCell, new StringName("cell_selected"));
        AddTransition(_hud, _orbitalBodyHSM, new StringName("orbital_body_selected"));

        AddTransition(_voronoiCell, _hud, "window_closed");
        AddTransition(_orbitalBodyHSM, _hud, "window_closed");
        AddTransition(_stationHSM, _hud, "window_closed");

        // Cross-window transitions
        AddTransition(_orbitalBodyHSM, _voronoiCell, "cell_selected");
        AddTransition(_voronoiCell, _orbitalBodyHSM, "orbital_body_selected");

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

        // Fleet overview transitions
        AddTransition(_hud, _shipsOverviewHSM, "ships_overview_opened");
        AddTransition(_shipsOverviewHSM, _hud, "window_closed");
        AddTransition(_shipsOverviewHSM, _logisticsUnitHSM, "logistics_unit_opened");
        AddTransition(_logisticsUnitHSM, _shipsOverviewHSM, "back_to_ships_overview");

        AddTransition(_voronoiCell, _buildingHSM, "building_details_opened");
        AddTransition(_orbitalBodyHSM, _buildingHSM, "building_details_opened");
        AddTransition(_stationHSM, _buildingHSM, "building_details_opened");
        AddTransition(_buildingHSM, _hud, "window_closed");
        AddTransition(_buildingHSM, _voronoiCell, "cell_selected");
        AddTransition(_buildingHSM, _orbitalBodyHSM, "orbital_body_selected");
        // Back from building → orbital uses the canonical return event (matches
        // the station/logistics "back_to_orbital_body" convention). Without this
        // the Back button from a building opened off the orbital window is a no-op.
        AddTransition(_buildingHSM, _orbitalBodyHSM, "back_to_orbital_body");
        AddTransition(_buildingHSM, _stationHSM, "station_opened");

        // Game start flow: headquarters placement routes through dedicated state
        AddTransition(ANYSTATE, _gameStart, "headquarters_placed");
        AddTransition(_gameStart, _hud, "game_started");

        // Pause menu transitions
        AddTransition(_hud, _pauseMenu, "pause_requested");
        AddTransition(_pauseMenu, _hud, "resume_requested");

        // PlanetBoard transitions — forward from any state (B-key works everywhere)
        AddTransition(ANYSTATE, _planetBoard, "planet_board_opened");

        // PlanetBoard back — return to the state that opened it
        AddTransition(_planetBoard, _orbitalBodyHSM, "back_to_orbital_body");
        AddTransition(_planetBoard, _buildingHSM, "back_to_building");
        AddTransition(_planetBoard, _stationHSM, "back_to_station");

        // PlanetBoard close → HUD
        AddTransition(_planetBoard, _hud, "window_closed");

        // Universal placement confirmation still routes anywhere back to HUD
        AddTransition(ANYSTATE, _hud, "placement_confirmed");

        InitialState = _hud;
        Initialize(this);
        SetActive(true);

        // Subscribe to SignalBus for PlanetBoard requests
        var bus = SignalBus.Instance;
        if (bus != null)
        {
            bus.OpenPlanetBoardRequested += OnOpenPlanetBoardRequested;
            bus.PlanetBoardCloseRequested += OnPlanetBoardCloseRequested;
            bus.PlanetBoardBackRequested += OnPlanetBoardBackRequested;
        }

        GameLogger.Info("GUIControllerHSM initialized");
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        var bus = SignalBus.Instance;
        if (bus != null)
        {
            bus.OpenPlanetBoardRequested -= OnOpenPlanetBoardRequested;
            bus.PlanetBoardCloseRequested -= OnPlanetBoardCloseRequested;
            bus.PlanetBoardBackRequested -= OnPlanetBoardBackRequested;
        }
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
            InteractionStack.Clear(Blackboard?.Top());
            Dispatch("window_closed");
        }

        GetViewport().SetInputAsHandled();
    }

    // ───────── PlanetBoard Signal Handlers ─────────

    /// <summary>
    /// Routes <c>SignalBus.OpenPlanetBoardRequested</c> into the HSM.
    /// Sets blackboard vars, pushes InteractionStack for return navigation,
    /// and dispatches <c>"planet_board_opened"</c>.
    /// </summary>
    private void OnOpenPlanetBoardRequested(CelestialBody body, string mode)
    {
        var bb = Blackboard?.Top();
        if (bb == null) return;

        var active = GetActiveState();
        string? returnEvent = DeterminePlanetBoardReturnEvent(active, bb, out var snapshot);

        if (returnEvent != null)
            InteractionStack.Push(bb, returnEvent, snapshot);

        bb.SetVar("SelectedBody", (Node3D)body);
        bb.SetVar("PlanetBoardMode", mode);

        Dispatch("planet_board_opened");
    }

    /// <summary>
    /// Close (✕ button, backdrop click): clear stack → HUD.
    /// </summary>
    private void OnPlanetBoardCloseRequested()
    {
        var bb = Blackboard?.Top();
        InteractionStack.Clear(bb);
        Dispatch("window_closed");
    }

    /// <summary>
    /// Back (← button): pop stack → return to previous panel (or HUD if stack empty).
    /// </summary>
    private void OnPlanetBoardBackRequested()
    {
        Dispatch(InteractionStack.ResolveBack(Blackboard?.Top()));
    }

    /// <summary>
    /// Determines the InteractionStack return event and snapshot based on
    /// the currently active HSM state. Returns null for states that should
    /// not push a return entry (i.e. close goes straight to HUD).
    /// </summary>
    private string? DeterminePlanetBoardReturnEvent(
        LimboState? active,
        Blackboard bb,
        out Godot.Collections.Dictionary snapshot)
    {
        if (active == _orbitalBodyHSM)
        {
            snapshot = InteractionStack.SnapshotVars(bb, "SelectedBody");
            return "back_to_orbital_body";
        }
        if (active == _buildingHSM)
        {
            snapshot = InteractionStack.SnapshotVars(bb,
                "SelectedBuilding", "SelectedBody");
            return "back_to_building";
        }
        if (active == _stationHSM)
        {
            snapshot = InteractionStack.SnapshotVars(bb,
                "SelectedStation", "SelectedBody");
            return "back_to_station";
        }
        // Cell, LogisticsUnit, HUD, etc. → back goes to HUD
        snapshot = new Godot.Collections.Dictionary();
        return null;
    }
}
