using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Sub-HSM for Construction menu workflow.
/// Manages transitions between item selection, planet targeting,
/// orbital placement, station selection, and building placement states.
/// </summary>
public partial class ConstructionMenuHSM : LimboHsm
{
    private LimboState? _constructablesMenu;
    private LimboState? _planetSelection;
    private LimboState? _orbitalPlacement;
    private LimboState? _stationSelection;
    private LimboState? _buildingPlacement;

    [Export]
    public LimboState? ConstructablesMenu
    {
        get => _constructablesMenu;
        set => _constructablesMenu = value;
    }

    [Export]
    public LimboState? PlanetSelection
    {
        get => _planetSelection;
        set => _planetSelection = value;
    }

    [Export]
    public LimboState? OrbitalPlacement
    {
        get => _orbitalPlacement;
        set => _orbitalPlacement = value;
    }

    [Export]
    public LimboState? StationSelection
    {
        get => _stationSelection;
        set => _stationSelection = value;
    }

    [Export]
    public LimboState? BuildingPlacement
    {
        get => _buildingPlacement;
        set => _buildingPlacement = value;
    }

    public override void _Ready()
    {
        //base._Ready();

        // From ConstructablesMenu
        AddTransition(_constructablesMenu, _planetSelection, "station_selected");
        AddTransition(_constructablesMenu, _planetSelection, "building_selected");
        AddTransition(_constructablesMenu, _stationSelection, "ship_selected");

        // From PlanetSelection — routes based on item type
        AddTransition(_planetSelection, _orbitalPlacement, "body_confirmed_station");
        AddTransition(_planetSelection, _buildingPlacement, "body_confirmed_building");
        AddTransition(_planetSelection, _constructablesMenu, "placement_cancelled");

        // From OrbitalPlacement
        AddTransition(_orbitalPlacement, _planetSelection, "placement_cancelled");

        // From StationSelection
        AddTransition(_stationSelection, _constructablesMenu, "placement_cancelled");

        // From BuildingPlacement
        AddTransition(_buildingPlacement, _constructablesMenu, "placement_cancelled");

        InitialState = _constructablesMenu;

        GameLogger.Info("ConstructionMenuHSM initialized");
    }

    public override void _Enter()
    {
        GameLogger.EnterFunction(nameof(_Enter), "ConstructionMenuHSM");
        GD.Print("Entering ConstructionMenuHSM");
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "ConstructionMenuHSM");

        Blackboard?.Top().SetVar("ConstructionItemType", default(Variant));
        Blackboard?.Top().SetVar("ConstructionDefinitionName", default(Variant));
        Blackboard?.Top().SetVar("ConstructionTargetBody", default(Variant));

        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        GameLogger.ExitFunction(nameof(_Exit));
    }
}
