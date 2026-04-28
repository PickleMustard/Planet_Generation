using Godot;
using PlayerInteraction.CellSelection;
using Structures.GameState;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// Sub-HSM for Voronoi cell viewer workflow.
/// Manages transitions between cell overview and building details states.
/// Attached to GUIController/VoronoiCell node.
/// </summary>
public partial class VoronoiCellHSM : LimboHsm
{
    private LimboState? _cellOverview;
    private LimboState? _buildingDetails;

    [Export]
    public LimboState? CellOverview
    {
        get => _cellOverview;
        set => _cellOverview = value;
    }

    [Export]
    public LimboState? BuildingDetails
    {
        get => _buildingDetails;
        set => _buildingDetails = value;
    }

    public override void _Ready()
    {
        // Future transitions
        AddTransition(_cellOverview, _buildingDetails, "building_details_opened");
        AddTransition(_buildingDetails, _cellOverview, "back_to_overview");

        InitialState = _cellOverview;

        GameLogger.Info("VoronoiCellHSM initialized");
    }

    public override void _Enter()
    {
        GameLogger.EnterFunction(nameof(_Enter), "VoronoiCellHSM");

        var cell = Blackboard?.Top().GetVar("SelectedCell").As<VoronoiCell>();
        var continent = Blackboard?.Top().GetVar("SelectedContinent").As<Continent>();
        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();
        CellSelectionManager.Instance?.SelectCell(cell, continent, body);

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "VoronoiCellHSM");

        Blackboard.Top()?.SetVar("SelectedCell", default(Variant));
        CellSelectionManager.Instance?.ClearSelection();

        Input.SetMouseMode(Input.MouseModeEnum.Captured);
        GameLogger.ExitFunction(nameof(_Exit));
    }
}
