using Godot;
using UtilityLibrary;

namespace Scenes;

/// <summary>
/// Lightweight scene navigation helper. Handles Escape key to return to main menu
/// and provides a back button in the top-right corner.
/// Attach to any scene's root node that needs menu navigation.
/// </summary>
public partial class SceneNavigator : CanvasLayer
{
    private const string MAIN_MENU_PATH = "res://Scenes/MainMenu.tscn";

    public override void _Ready()
    {
        Layer = 5;

        var button = new Button();
        button.Text = "Back to Menu";
        button.AnchorsPreset = (int)Control.LayoutPreset.TopRight;
        button.OffsetLeft = -150;
        button.OffsetRight = -10;
        button.OffsetTop = 10;
        button.OffsetBottom = 40;
        button.Pressed += OnBackPressed;
        AddChild(button);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            OnBackPressed();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBackPressed()
    {
        GameLogger.Info("Returning to MainMenu");
        GetTree().ChangeSceneToFile(MAIN_MENU_PATH);
    }
}
