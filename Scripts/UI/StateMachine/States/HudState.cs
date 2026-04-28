using Godot;
using Godot.Collections;
using PlayerInteraction.CellSelection;
using Structures.GameState;
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

    private Button? _constructionButton;
    private Button? _demolishButton;
    private Button? _researchButton;
    private Button? _transferButton;

    [Export]
    public Control? hudUI;

    [Export]
    public Button? constructionButton
    {
        get => _constructionButton;
        set => _constructionButton = value;
    }

    [Export]
    public Button? demolishButton
    {
        get => _demolishButton;
        set => _demolishButton = value;
    }

    [Export]
    public Button? researchButton
    {
        get => _researchButton;
        set => _researchButton = value;
    }

    [Export]
    public Button? transferButton
    {
        get => _transferButton;
        set => _transferButton = value;
    }

    public override void _Setup()
    {
        _constructionButton!.ButtonDown += ConstructionButtonPressed;
        _demolishButton!.ButtonDown += DemolishButtonPressed;
        _researchButton!.ButtonDown += ResearchButtonPressed;
        _transferButton!.ButtonDown += TransferButtonPressed;
    }

    public void ConstructionButtonPressed()
    {
        GD.Print(Dispatch(new StringName("construction_menu_opened")));
    }

    public void DemolishButtonPressed()
    {
        Dispatch("demolish_interface");
    }

    public void ResearchButtonPressed()
    {
        Dispatch("research_interface");
    }

    public void TransferButtonPressed()
    {
        Dispatch("transfer_interface");
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
                Blackboard.Top().SetVar("LogisticsUnitSource", "hud");
                Dispatch("logistics_unit_selected");
            }
            _clickEvent = ClickEvent.IDLE;
            return;
        }

        var bodyNode = (Node)results["selectable_body"];
        var cell = results["cell"].As<VoronoiCell>();
        var continent = results["cell_continent"].As<Continent>();

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
                    Dispatch("orbital_body_selected");
                }
                _clickEvent = ClickEvent.IDLE;
                break;

            case ClickEvent.RIGHT_CLICK:
                if (cell != null)
                {
                    int continentIndex = cell.ContinentIndex;
                    CellSelectionManager.Instance?.SelectContinent(
                        continentIndex,
                        (Node3D)bodyNode
                    );

                    Blackboard.Top().SetVar("SelectedContinentIndex", continentIndex);
                    Blackboard.Top().SetVar("SelectedContinent", continent);
                    Blackboard.Top().SetVar("SelectedBody", bodyNode);
                    Blackboard.Top().SetVar("BodyType", bodyNode.GetType().Name);
                    Dispatch("continent_selected");
                }
                _clickEvent = ClickEvent.IDLE;
                break;

            case ClickEvent.CTRL_CLICK:
                if (cell != null)
                {
                    Blackboard.Top().SetVar("SelectedCell", cell);
                    Blackboard.Top().SetVar("SelectedBody", bodyNode);
                    Blackboard.Top().SetVar("BodyType", bodyNode.GetType().Name);
                    Blackboard.Top().SetVar("SelectedContinent", continent);
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
