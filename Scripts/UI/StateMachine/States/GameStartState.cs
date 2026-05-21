using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.NameGeneration;

namespace UI.StateMachine;

/// <summary>
/// State entered after the player places the company headquarters (any building
/// carrying <see cref="Constructables.Buildings.Behaviors.GameStartBehavior"/>).
/// Shows the naming dialogue, pauses gameplay, and releases the mouse.
/// Confirming dispatches "game_started"; escape routes through the HSM's
/// universal escape transition and rolls back the placed building via the
/// generic teardown path (decrement limit count + DestroyConstructable).
/// </summary>
public partial class GameStartState : LimboState
{
    private const string DialogNodeName = "GameStartDialog";
    private const string CompanyInputPath =
        DialogNodeName
        + "/PanelContainer/CenterContainer/DialogPanel/MarginContainer/VBoxContainer/CompanyNameInput";
    private const string SystemInputPath =
        DialogNodeName
        + "/PanelContainer/CenterContainer/DialogPanel/MarginContainer/VBoxContainer/SystemNameInput";
    private const string ConfirmButtonPath =
        DialogNodeName
        + "/PanelContainer/CenterContainer/DialogPanel/MarginContainer/VBoxContainer/ConfirmButton";

    private Control? _dialog;
    private LineEdit? _companyNameInput;
    private LineEdit? _systemNameInput;
    private Button? _confirmButton;
    private Input.MouseModeEnum _previousMouseMode;
    private Building? _placedHq;
    private SystemData? _systemData;
    private bool _confirmed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _dialog = GetNodeOrNull<Control>(DialogNodeName);
        _companyNameInput = GetNodeOrNull<LineEdit>(CompanyInputPath);
        _systemNameInput = GetNodeOrNull<LineEdit>(SystemInputPath);
        _confirmButton = GetNodeOrNull<Button>(ConfirmButtonPath);

        if (_dialog != null)
            _dialog.Visible = false;

        if (_confirmButton != null)
            _confirmButton.Pressed += OnConfirmPressed;
    }

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), nameof(GameStartState));

        _confirmed = false;
        _placedHq = Blackboard?.Top().GetVar("PlacedHq").As<Building>();
        _systemData = ResolveSystemData();

        if (_placedHq == null)
        {
            GameLogger.Warning(
                "GameStartState: no placed headquarters on blackboard; aborting back to HUD"
            );
            Dispatch("escape_pressed");
            return;
        }

        if (_companyNameInput != null)
            _companyNameInput.Text = NameGenerator.GenerateCompanyName();
        if (_systemNameInput != null)
            _systemNameInput.Text = NameGenerator.GenerateSystemName();

        if (_dialog != null)
            _dialog.Visible = true;

        _previousMouseMode = Input.MouseMode;
        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GetTree().Paused = true;
        WorldInputController.Instance?.PushDisable();

        GameLogger.ExitFunction(nameof(_Enter));
    }

    public override void _Exit()
    {
        base._Exit();
        GameLogger.EnterFunction(nameof(_Exit), nameof(GameStartState));

        if (_dialog != null)
            _dialog.Visible = false;

        GetTree().Paused = false;
        Input.SetMouseMode(_previousMouseMode);
        WorldInputController.Instance?.PopDisable();

        if (!_confirmed && _placedHq != null)
        {
            var def = _placedHq.Definition;
            if (def?.BuildingLimit > 0 && !string.IsNullOrEmpty(def.IdName))
                BuildingDatabase.Instance?.DecrementGlobalPlacement(def.IdName);
            _placedHq.DestroyConstructable();
            ToastSystem.Instance?.Show("Headquarters placement cancelled");
        }

        Blackboard?.Top().SetVar("PlacedHq", default(Variant));
        _placedHq = null;
        _systemData = null;

        GameLogger.ExitFunction(nameof(_Exit));
    }

    private void OnConfirmPressed()
    {
        if (_placedHq == null)
            return;

        string companyName = _companyNameInput?.Text?.Trim() ?? string.Empty;
        string systemName = _systemNameInput?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(systemName))
            return;

        // Engine already running (booted by SystemData._Ready when GameScene loaded).
        // StartGame here only commits the company/system names and flips IsGameStarted.
        _systemData?.StartGame(companyName, systemName);

        ToastSystem.Instance?.Show(
            $"Welcome to the {systemName} system, {companyName}!"
        );

        _confirmed = true;
        Dispatch("game_started");
    }

    private SystemData? ResolveSystemData()
    {
        var systemContainer = GetNodeOrNull<Node>("/root/GameScene/system_container");
        if (systemContainer == null)
            return null;

        var data = systemContainer.GetNodeOrNull<SystemData>("SystemData");
        if (data != null)
            return data;

        data = new SystemData();
        systemContainer.AddChild(data);
        return data;
    }
}
