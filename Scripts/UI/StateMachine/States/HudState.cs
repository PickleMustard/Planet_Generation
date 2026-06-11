using Godot;
using Godot.Collections;
using PlayerInteraction.CellSelection;
using Structures.GameState;
using UI;
using UI.StateMachine;
using UtilityLibrary;

public partial class HudState : LimboState
{
    private enum ClickEvent
    {
        IDLE,
        LEFT_CLICK,
        RIGHT_CLICK,
        CTRL_CLICK,
    }

    private Button? _shipsOverviewButton;

    [Export]
    public Control? hudUI;

    // Construct / Demolish / Research / Transfer moved to the persistent
    // CommandLayer (CommandLayerController). Only the ships-overview button
    // (under OverviewCartouche) still lives in HUD.tscn.
    [Export]
    public Button? shipsOverviewButton
    {
        get => _shipsOverviewButton;
        set => _shipsOverviewButton = value;
    }

    public override void _Setup()
    {
        if (_shipsOverviewButton != null)
            _shipsOverviewButton.ButtonDown += ShipsOverviewButtonPressed;
    }

    public void ShipsOverviewButtonPressed()
    {
        AudioBus.Instance?.Play(MainGameUI.Instance?.GuiClick);
        InteractionStack.Push(Blackboard.Top(), "window_closed", new Godot.Collections.Dictionary());
        Dispatch("ships_overview_opened");
    }

    public override void _Enter()
    {
        hudUI!.Visible = true;
        GD.Print("Entering HudState");
    }

    public override void _Exit()
    {
        hudUI!.Visible = false;
        GD.Print("Exiting HudState");
    }

    private ClickEvent _clickEvent = ClickEvent.IDLE;

    public override void _Ready()
    {
        Callable handleRaycastResult = new Callable(this, "HandleRaycastResult");
        SignalBus.Instance!.ConnectToSignal("ExportRaycastResult", handleRaycastResult);
    }

    public void HandleRaycastResult(Dictionary results)
    {
        if (results == null || results.Keys.Count == 0)
        {
            GD.Print("No results");
            _clickEvent = ClickEvent.IDLE;
            return;
        }

        // Check for logistics unit click
        if (results.ContainsKey("logistics_unit"))
        {
            if (_clickEvent == ClickEvent.LEFT_CLICK)
            {
                var unit = (Node)results["logistics_unit"];
                Blackboard.Top().SetVar("SelectedLogisticsUnit", unit);
                InteractionStack.Push(Blackboard.Top(), "window_closed", new Godot.Collections.Dictionary());
                Dispatch("logistics_unit_selected");
            }
            _clickEvent = ClickEvent.IDLE;
            return;
        }

        var bodyNode = (Node)results["selectable_body"];
        var cell = results["cell"].As<VoronoiCell>();

        switch (_clickEvent)
        {
            case ClickEvent.LEFT_CLICK:
                if (bodyNode is IOrbitalBody orbitalBody)
                {
                    var camera = GetViewport().GetCamera3D();
                    OrbitalBodyConverter.SetToBlackboard(
                        Blackboard.Top(),
                        "SelectedBody",
                        orbitalBody
                    );
                    Blackboard.Top().SetVar("PlayerCamera", camera);
                    InteractionStack.Push(Blackboard.Top(), "window_closed", new Godot.Collections.Dictionary());
                    Dispatch("orbital_body_selected");
                }
                _clickEvent = ClickEvent.IDLE;
                break;

            case ClickEvent.RIGHT_CLICK:
                _clickEvent = ClickEvent.IDLE;
                break;

            case ClickEvent.CTRL_CLICK:
                if (cell != null)
                {
                    Blackboard.Top().SetVar("SelectedCell", cell);
                    Blackboard.Top().SetVar("SelectedBody", bodyNode);
                    Blackboard.Top().SetVar("BodyType", bodyNode.GetType().Name);
                    InteractionStack.Push(Blackboard.Top(), "window_closed", new Godot.Collections.Dictionary());
                    Dispatch("cell_selected");
                }
                _clickEvent = ClickEvent.IDLE;
                break;

            default:
                _clickEvent = ClickEvent.IDLE;
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            GD.Print(mouseEvent.AsText());
            if (mouseEvent.ButtonIndex == MouseButton.Left && mouseEvent.Pressed)
            {
                if (mouseEvent.CtrlPressed)
                {
                    _clickEvent = ClickEvent.CTRL_CLICK;
                }
                else
                {
                    _clickEvent = ClickEvent.LEFT_CLICK;
                }
                HandleInput();
                SignalBus.Instance!.Emit("RequestRayCast");
            }
            else if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                _clickEvent = ClickEvent.RIGHT_CLICK;
                HandleInput();
                SignalBus.Instance!.Emit("RequestRayCast");
            }
        }
    }

    private void HandleInput()
    {
        GetViewport().SetInputAsHandled();
    }
}
