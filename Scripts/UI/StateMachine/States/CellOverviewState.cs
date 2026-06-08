using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UI.StateMachine;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for displaying the VoronoiCellInfoWindow.
/// Shows the window on Enter, hides on Exit.
/// Translates window signals into HSM dispatch events.
/// </summary>
public partial class CellOverviewState : InGamePanelState
{
    private CellInfo.VoronoiCellInfoWindow? _window;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "CellOverviewState");

        _window = GetNodeOrNull<CellInfo.VoronoiCellInfoWindow>("VoronoiCellInfoWindow");
        if (_window == null)
        {
            GameLogger.Error("CellOverviewState: VoronoiCellInfoWindow not found as child");
            return;
        }

        var cell = Blackboard?.Top().GetVar("SelectedCell").As<VoronoiCell>();
        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();

        if (cell == null || body == null)
        {
            GameLogger.Warning("CellOverviewState: Missing cell/body data in blackboard");
            Dispatch("window_closed");
            return;
        }

        _window.WindowCloseRequested += HandleClose;
        _window.BackRequested += HandleBack;
        _window.BuildingDetailsRequested += OnBuildingDetailsRequested;
        _window.OrbitalBodyViewRequested += OnOrbitalBodyViewRequested;

        _window.ShowWindow(cell, body);

        GameLogger.Debug("CellOverviewState: Cell info window shown");
    }

    public override void _Exit()
    {
        base._Exit();

        if (_window != null)
        {
            _window.WindowCloseRequested -= HandleClose;
            _window.BackRequested -= HandleBack;
            _window.BuildingDetailsRequested -= OnBuildingDetailsRequested;
            _window.OrbitalBodyViewRequested -= OnOrbitalBodyViewRequested;
            _window.HideWindow();
            _window.Clear();
        }

        _window = null;
    }

    private void OnBuildingDetailsRequested(Building building)
    {
        if (building == null) return;

        Blackboard?.Top()?.SetVar("SelectedBuilding", building);
        PushAndNavigate("building_details_opened", "cell_selected",
            "SelectedCell", "SelectedBody", "BodyType");
    }

    private void OnOrbitalBodyViewRequested()
    {
        PushAndNavigate("orbital_body_selected", "cell_selected",
            "SelectedCell", "SelectedBody", "BodyType");
    }
}
