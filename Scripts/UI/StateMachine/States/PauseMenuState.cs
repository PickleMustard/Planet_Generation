using Godot;
using UI;
using UtilityLibrary;

public partial class PauseMenuState : LimboState
{
    private const string MAIN_MENU_PATH = "res://Scenes/MainMenu.tscn";

    [Export]
    public Control? pauseMenuUI;

    [Export]
    public Button? resumeButton;

    [Export]
    public Button? settingsButton;

    [Export]
    public Button? quitButton;

    public override void _Setup()
    {
        if (resumeButton != null)
            resumeButton.Pressed += OnResumePressed;
        if (quitButton != null)
            quitButton.Pressed += OnQuitPressed;
        if (settingsButton != null)
            settingsButton.Disabled = true;
    }

    public override void _Enter()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.Visible = true;
        GetTree().Paused = true;
        WorldInputController.Instance?.PushDisable();
        GameLogger.Info("Entering PauseMenuState");
    }

    public override void _Exit()
    {
        GetTree().Paused = false;
        if (pauseMenuUI != null)
            pauseMenuUI.Visible = false;
        WorldInputController.Instance?.PopDisable();
        GameLogger.Info("Exiting PauseMenuState");
    }

    private void OnResumePressed()
    {
        Dispatch("resume_requested");
    }

    private void OnQuitPressed()
    {
        GameLogger.Info("Returning to MainMenu from pause menu");
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(MAIN_MENU_PATH);
    }
}
