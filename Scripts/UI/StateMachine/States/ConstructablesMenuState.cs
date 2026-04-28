using Godot;
using UtilityLibrary;

namespace UI.StateMachine;

/// <summary>
/// State for the tabbed construction item selection menu.
/// Shows the ConstructionMenu panel on Enter, hides on Exit.
/// Translates item selection signals into HSM dispatch events.
/// </summary>
public partial class ConstructablesMenuState : LimboState
{
    private Construction.ConstructionMenu? _menuPanel;

    public override void _Enter()
    {
        base._Enter();
        GameLogger.EnterFunction(nameof(_Enter), "ConstructablesMenuState");

        _menuPanel = GetNodeOrNull<Construction.ConstructionMenu>("ConstructionMenu");
        if (_menuPanel == null)
        {
            GameLogger.Error("ConstructablesMenuState: ConstructionMenu panel not found as child");
            return;
        }

        _menuPanel.ItemSelectedForPlacement += OnItemSelectedForPlacement;
        _menuPanel.MenuClosed += OnMenuClosed;
        _menuPanel.Visible = true;

        Input.SetMouseMode(Input.MouseModeEnum.Visible);
        GameLogger.Debug("ConstructablesMenuState: Menu shown");
    }

    public override void _Exit()
    {
        base._Exit();

        if (_menuPanel != null)
        {
            _menuPanel.ItemSelectedForPlacement -= OnItemSelectedForPlacement;
            _menuPanel.MenuClosed -= OnMenuClosed;
            _menuPanel.ClearSelection();
            _menuPanel.Visible = false;
        }

        _menuPanel = null;
    }

    private void OnItemSelectedForPlacement(string itemType, string definitionName)
    {
        GameLogger.Debug($"ConstructablesMenuState: Item selected — {itemType}:{definitionName}");

        Blackboard?.Top().SetVar("ConstructionItemType", itemType);
        Blackboard?.Top().SetVar("ConstructionDefinitionName", definitionName);

        switch (itemType)
        {
            case "Station":
                Dispatch("station_selected");
                break;
            case "Ship":
                Dispatch("ship_selected");
                break;
            case "Building":
                Dispatch("building_selected");
                break;
            default:
                GameLogger.Warning($"ConstructablesMenuState: Unknown item type '{itemType}'");
                break;
        }
    }

    private void OnMenuClosed()
    {
        GameLogger.Debug("ConstructablesMenuState: Menu closed");
        Dispatch("window_closed");
    }
}
