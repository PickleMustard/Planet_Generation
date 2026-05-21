using Godot;
using ProceduralGeneration.PlanetGeneration;
using UI.PlanetBoard;
using UtilityLibrary;

namespace UI.StateMachine.States;

/// <summary>
/// Shows the <see cref="PlanetBoardWindow"/> for the <c>CelestialBody</c> stored in
/// blackboard at <c>"SelectedBody"</c>. The window is lazy-instantiated on first entry
/// and reused on subsequent entries. Close routes through <c>SignalBus</c> →
/// <c>GUIControllerHSM</c> dispatches <c>"window_closed"</c> (→ HUD). Back routes
/// through <c>SignalBus</c> → <c>GUIControllerHSM</c> pops the
/// <c>InteractionStack</c> and dispatches the return event.
/// </summary>
public partial class PlanetBoardState : LimboState
{
    private PlanetBoardWindow? _planetBoardWindow;
    private CelestialBody? _currentBody;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "PlanetBoardState");

        WorldInputController.Instance?.PushDisable();

        var bb = Blackboard?.Top();

        // Read body from blackboard
        var bodyVariant = bb?.GetVar("SelectedBody");
        CelestialBody? body = null;
        if (bodyVariant != null && bodyVariant.Value.VariantType != Variant.Type.Nil)
        {
            // SelectedBody may be stored as Node3D; cast to CelestialBody
            var node = bodyVariant.Value.As<Node3D>();
            body = node as CelestialBody;
        }

        if (body == null)
        {
            GameLogger.Warning("PlanetBoardState: No CelestialBody in blackboard SelectedBody");
            Dispatch("window_closed");
            GameLogger.ExitFunction(nameof(_Enter));
            return;
        }

        string mode = bb?.GetVar("PlanetBoardMode").AsString() ?? "ResourceLink";

        // Lazy-instantiate PlanetBoardWindow as child of this state node
        if (_planetBoardWindow == null)
        {
            var packed = GD.Load<PackedScene>("res://UI/PlanetBoard/PlanetBoard.tscn");
            if (packed == null)
            {
                GameLogger.Error("PlanetBoardState: failed to load PlanetBoard.tscn");
                Dispatch("window_closed");
                GameLogger.ExitFunction(nameof(_Enter));
                return;
            }
            _planetBoardWindow = packed.Instantiate<PlanetBoardWindow>();
            AddChild(_planetBoardWindow);
        }

        _currentBody = body;
        _planetBoardWindow.Closed += OnPlanetBoardClosed;
        _planetBoardWindow.BackRequested += OnPlanetBoardBackRequested;
        _planetBoardWindow.OpenForBody(body, mode);

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), "PlanetBoardState");

        if (_planetBoardWindow != null)
        {
            _planetBoardWindow.Closed -= OnPlanetBoardClosed;
            _planetBoardWindow.BackRequested -= OnPlanetBoardBackRequested;
            if (_planetBoardWindow.Visible)
                _planetBoardWindow.Visible = false;
        }

        WorldInputController.Instance?.PopDisable();
        _currentBody = null;

        GameLogger.ExitFunction(nameof(_Exit));
    }

    /// <summary>
    /// Close button or backdrop click: route through SignalBus so
    /// GUIControllerHSM clears the InteractionStack and dispatches "window_closed".
    /// </summary>
    private void OnPlanetBoardClosed()
    {
        SignalBus.Instance?.EmitPlanetBoardCloseRequested();
    }

    /// <summary>
    /// Back button: route through SignalBus so GUIControllerHSM pops the
    /// InteractionStack and dispatches the return event to the previous panel.
    /// </summary>
    private void OnPlanetBoardBackRequested()
    {
        SignalBus.Instance?.EmitPlanetBoardBackRequested();
    }
}
