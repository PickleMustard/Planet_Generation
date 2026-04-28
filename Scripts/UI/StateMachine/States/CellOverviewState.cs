using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for displaying the VoronoiCellInfoWindow.
/// Shows the window on Enter, hides on Exit.
/// Translates window signals into HSM dispatch events.
/// </summary>
public partial class CellOverviewState : LimboState
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

        // Read data from blackboard
        var cell = Blackboard?.Top().GetVar("SelectedCell").As<VoronoiCell>();
        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();
        var continent = Blackboard?.Top().GetVar("SelectedContinent").As<Continent>();

        if (cell == null || body == null)
        {
            GameLogger.Warning("CellOverviewState: Missing cell/body data in blackboard");
            Dispatch("window_closed");
            return;
        }

        // Connect window signals
        _window.WindowCloseRequested += OnWindowCloseRequested;
        _window.ContinentViewRequested += OnContinentViewRequested;

        // Show window with data
        _window.ShowWindow(cell, body, continent);

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.Debug("CellOverviewState: Cell info window shown");
    }

    public override void _Exit()
    {
        base._Exit();

        if (_window != null)
        {
            _window.WindowCloseRequested -= OnWindowCloseRequested;
            _window.ContinentViewRequested -= OnContinentViewRequested;
            _window.HideWindow();
            _window.Clear();
        }

        _window = null;
    }

    private void OnWindowCloseRequested()
    {
        GameLogger.Debug("CellOverviewState: Window close requested");
        Dispatch("window_closed");
    }

    private void OnContinentViewRequested()
    {
        GameLogger.Debug("CellOverviewState: Continent view requested");

        // Derive continent index for the ContinentViewState
        var continent = Blackboard?.Top().GetVar("SelectedContinent").As<Continent>();
        var body = Blackboard?.Top().GetVar("SelectedBody").As<Node3D>();

        if (
            continent != null
            && body is ISelectableBody selectable
            && selectable.Mesh?.Continents != null
        )
        {
            foreach (var kvp in selectable.Mesh.Continents)
            {
                if (kvp.Value == continent)
                {
                    Blackboard?.Top().SetVar("SelectedContinentIndex", kvp.Key);
                    break;
                }
            }
        }

        Dispatch("continent_selected");
    }
}
