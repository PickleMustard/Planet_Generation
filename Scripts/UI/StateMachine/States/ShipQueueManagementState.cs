using Constructables;
using Constructables.Stations.Behaviors;
using Godot;
using UI.ConstructionYard;
using UtilityLibrary;

namespace UI.StateMachine.States;

/// <summary>
/// Sub-state within StationHSM for interactive ship construction queue management.
/// Active only for stations with a ShipyardBehavior.
/// Instantiates the ConstructionYardWindow scene on entry and frees it on exit.
/// </summary>
public partial class ShipQueueManagementState : LimboState
{
    [Export]
    private PackedScene? _constructionYardWindowScene;

    private StationWindow.StationWindow? _window;
    private ShipyardBehavior? _shipyard;
    private ConstructionYardWindow? _gui;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "ShipQueueManagementState");

        _window = StationWindow.StationWindow.Instance;

        var stationVariant = Blackboard?.Top()?.GetVar("SelectedStation");
        if (stationVariant == null || stationVariant.Value.VariantType == Variant.Type.Nil)
        {
            GameLogger.Warning("ShipQueueManagementState: No SelectedStation in blackboard");
            Dispatch("back_to_station");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        var stationNode = stationVariant.Value.As<Node>();
        if (stationNode is StationSatellite stationSat
            && stationSat.GetBehavior<ShipyardBehavior>() is { } shipyard)
        {
            _shipyard = shipyard;
        }
        else
        {
            GameLogger.Warning("ShipQueueManagementState: Station has no ShipyardBehavior");
            Dispatch("back_to_station");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        _shipyard = shipyard;

        if (_window != null && _window.IsOpen)
            _window.HideWindow();

        if (_constructionYardWindowScene != null)
        {
            _gui = _constructionYardWindowScene.Instantiate<ConstructionYardWindow>();
            AddChild(_gui);
            _gui.BackRequested += OnBackRequested;
            _gui.Bind(stationSat);
        }
        else
        {
            GameLogger.Warning("ShipQueueManagementState: ConstructionYardWindow scene not assigned");
        }

        GameLogger.Info($"ShipQueueManagementState: Managing queue for '{shipyard.Owner?.Name ?? "unknown"}'");
        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "ShipQueueManagementState");

        if (_gui != null)
        {
            _gui.BackRequested -= OnBackRequested;
            _gui.Unbind();
            _gui.QueueFree();
            _gui = null;
        }

        _shipyard = null;
        _window = null;

        GameLogger.ExitFunction(nameof(_Exit));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            Dispatch("back_to_station");
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBackRequested()
    {
        Dispatch("back_to_station");
    }
}
