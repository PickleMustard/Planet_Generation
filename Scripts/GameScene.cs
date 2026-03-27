using System;
using Godot;
using UtilityLibrary;

namespace Scenes
{
    /// <summary>
    /// Main game scene controller.
    /// </summary>
    public partial class GameScene : Node3D
    {
        // UI References
        private Button? _backToMenuButton;

        public override void _Ready()
        {
            GameLogger.EnterFunction(nameof(_Ready));

            // Get UI references
            var ui = GetNodeOrNull<Control>("UI");
            if (ui != null)
            {
                var vbox = ui.GetNodeOrNull<VBoxContainer>("VBoxContainer");
                if (vbox != null)
                {
                    _backToMenuButton = vbox.GetNodeOrNull<Button>("BackToMenuButton");
                }
            }

            // Connect button signal
            if (_backToMenuButton != null)
            {
                _backToMenuButton.Pressed += OnBackToMenuButtonPressed;
                GameLogger.Debug("Connected BackToMenuButton signal");
            }

            GameLogger.Info("Game scene loaded");
            GameLogger.ExitFunction(nameof(_Ready));
        }

        /// <summary>
        /// Called when the Back to Main Menu button is pressed.
        /// </summary>
        private void OnBackToMenuButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnBackToMenuButtonPressed));
            GameLogger.Info("Back to Main Menu button pressed");

            try
            {
                GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
                GameLogger.Info("Returning to MainMenu");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to return to main menu: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(OnBackToMenuButtonPressed));
        }

        /// <summary>
        /// Cleans up resources when the scene is exiting.
        /// </summary>
        public override void _ExitTree()
        {
            // Disconnect button signal
            if (_backToMenuButton != null)
            {
                _backToMenuButton.Pressed -= OnBackToMenuButtonPressed;
            }

            base._ExitTree();
        }
    }
}