using Godot;
using UtilityLibrary;

namespace Scenes;

/// <summary>
/// Lightweight scene navigation helper for dev/utility scenes (PlanetGeneration,
/// SystemGeneration). Provides a "Back to Menu" button in the top-right corner.
/// Escape-to-main-menu behavior is gone — gameplay scenes use the pause menu instead.
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

    private void OnBackPressed()
    {
        GameLogger.Info("Returning to MainMenu");
        GetTree().ChangeSceneToFile(MAIN_MENU_PATH);
    }
}
