using System;
using Godot;
using UtilityLibrary;

namespace UI
{
    /// <summary>
    /// Main menu scene controller with navigation to different parts of the game.
    /// </summary>
    public partial class MainMenu : Control
    {
        // UI References
        private Button? _startButton;
        private Button? _settingsButton;
        private Button? _debugButton;
        private Button? _exitButton;

        // Popup references
        private Control? _settingsPanel;
        private Control? _debugMenu;

        public override void _Ready()
        {
            GameLogger.EnterFunction(nameof(_Ready));

            // Get UI references
            GetUIReferences();

            // Initialize UI
            InitializeUI();

            GameLogger.ExitFunction(nameof(_Ready));
        }

        /// <summary>
        /// Gets references to UI nodes.
        /// </summary>
        private void GetUIReferences()
        {
            GameLogger.EnterFunction(nameof(GetUIReferences));

            // Get button references
            var vboxContainer = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            if (vboxContainer != null)
            {
                _startButton = vboxContainer.GetNodeOrNull<Button>("StartButton");
                _settingsButton = vboxContainer.GetNodeOrNull<Button>("SettingsButton");
                _debugButton = vboxContainer.GetNodeOrNull<Button>("DebugButton");
                _exitButton = vboxContainer.GetNodeOrNull<Button>("ExitButton");
            }

            // Try to load popup references if they exist in the scene
            _settingsPanel = GetNodeOrNull<Control>("SettingsPanel");
            _debugMenu = GetNodeOrNull<Control>("DebugMenu");

            GameLogger.ExitFunction(nameof(GetUIReferences));
        }

        /// <summary>
        /// Initializes the UI components and connects signals.
        /// </summary>
        private void InitializeUI()
        {
            GameLogger.EnterFunction(nameof(InitializeUI));

            // Connect button signals
            if (_startButton != null)
            {
                _startButton.Pressed += OnStartButtonPressed;
                GameLogger.Debug("Connected StartButton signal");
            }

            if (_settingsButton != null)
            {
                _settingsButton.Pressed += OnSettingsButtonPressed;
                GameLogger.Debug("Connected SettingsButton signal");
            }

            if (_debugButton != null)
            {
                _debugButton.Pressed += OnDebugButtonPressed;
                GameLogger.Debug("Connected DebugButton signal");
            }

            if (_exitButton != null)
            {
                _exitButton.Pressed += OnExitButtonPressed;
                GameLogger.Debug("Connected ExitButton signal");
            }

            // Hide popups initially
            if (_settingsPanel != null)
            {
                _settingsPanel.Visible = false;
                GameLogger.Debug("SettingsPanel hidden");
            }

            if (_debugMenu != null)
            {
                _debugMenu.Visible = false;
                GameLogger.Debug("DebugMenu hidden");
            }

            GameLogger.ExitFunction(nameof(InitializeUI));
        }

        /// <summary>
        /// Called when the Start Game button is pressed.
        /// </summary>
        private void OnStartButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnStartButtonPressed));
            GameLogger.Info("Start Game button pressed");

            // Transition to the main game scene
            try
            {
                if (ResourceLoader.Exists("res://Scenes/GameScene.tscn"))
                {
                    GetTree().ChangeSceneToFile("res://Scenes/GameScene.tscn");
                    GameLogger.Info("Transitioning to GameScene.tscn");
                }
                else if (ResourceLoader.Exists("res://test_scene.tscn"))
                {
                    // Fallback to test scene
                    GetTree().ChangeSceneToFile("res://test_scene.tscn");
                    GameLogger.Info("Transitioning to test_scene.tscn (fallback)");
                }
                else
                {
                    GameLogger.Warning("Game scene not found.");
                    ShowNotification("Game scene not available yet.");
                }
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to transition to game scene: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(OnStartButtonPressed));
        }

        /// <summary>
        /// Called when the Settings button is pressed.
        /// </summary>
        private void OnSettingsButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnSettingsButtonPressed));
            GameLogger.Info("Settings button pressed");

            // Check if we have a SettingsPanel in the scene
            if (_settingsPanel != null)
            {
                _settingsPanel.Visible = true;
                GameLogger.Info("Showing SettingsPanel");
            }
            else
            {
                GameLogger.Warning("SettingsPanel not found in scene");
                // Try to load the SettingsPanel from the Debug folder
                LoadAndShowSettingsPanel();
            }

            GameLogger.ExitFunction(nameof(OnSettingsButtonPressed));
        }

        /// <summary>
        /// Loads and shows the SettingsPanel from the Debug folder.
        /// </summary>
        private void LoadAndShowSettingsPanel()
        {
            GameLogger.EnterFunction(nameof(LoadAndShowSettingsPanel));

            try
            {
                // Try to load the SettingsPanel from the Debug folder
                var settingsPanelPrefab = ResourceLoader.Load<PackedScene>("res://UI/Debug/Settings/SettingsPanel.tscn");
                if (settingsPanelPrefab != null)
                {
                    var settingsPanelInstance = settingsPanelPrefab.Instantiate<Control>();
                    AddChild(settingsPanelInstance);
                    _settingsPanel = settingsPanelInstance;
                    _settingsPanel.Visible = true;

                    // Position it in the center
                    var panelSize = new Vector2(_settingsPanel.Size.X, _settingsPanel.Size.Y);
                    _settingsPanel.Position = (new Vector2(Size.X, Size.Y) - panelSize) / 2;

                    GameLogger.Info("Loaded and displayed SettingsPanel from Debug folder");
                }
                else
                {
                    GameLogger.Warning("SettingsPanel.tscn not found in UI/Debug/Settings/");
                    ShowNotification("Settings panel not available yet.");
                }
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to load SettingsPanel: {ex.Message}");
                ShowNotification($"Error loading settings: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(LoadAndShowSettingsPanel));
        }

        /// <summary>
        /// Called when the Debug button is pressed.
        /// </summary>
        private void OnDebugButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnDebugButtonPressed));
            GameLogger.Info("Debug button pressed");

            // Check if we have a DebugMenu in the scene
            if (_debugMenu != null)
            {
                _debugMenu.Visible = true;
                GameLogger.Info("Showing DebugMenu");
            }
            else
            {
                GameLogger.Warning("DebugMenu not found in scene");
                // Create a simple debug menu
                CreateSimpleDebugMenu();
            }

            GameLogger.ExitFunction(nameof(OnDebugButtonPressed));
        }

        /// <summary>
        /// Creates a simple debug menu with available debug scenes.
        /// </summary>
        private void CreateSimpleDebugMenu()
        {
            GameLogger.EnterFunction(nameof(CreateSimpleDebugMenu));

            // Create a popup panel
            var debugMenuPanel = new Panel();
            debugMenuPanel.Name = "SimpleDebugMenu";
            debugMenuPanel.Size = new Vector2(400, 300);
            var debugPanelSize = new Vector2(debugMenuPanel.Size.X, debugMenuPanel.Size.Y);
            debugMenuPanel.Position = (new Vector2(Size.X, Size.Y) - debugPanelSize) / 2;
            debugMenuPanel.Visible = true;
            AddChild(debugMenuPanel);

            // Create a VBoxContainer for layout
            var vbox = new VBoxContainer();
            vbox.Name = "VBoxContainer";
            vbox.AnchorsPreset = (int)Control.LayoutPreset.FullRect;
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            debugMenuPanel.AddChild(vbox);

            // Add title
            var titleLabel = new Label();
            titleLabel.Name = "TitleLabel";
            titleLabel.Text = "Debug Scenes";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(titleLabel);

            // Add delauney_sphere.tscn button
            if (ResourceLoader.Exists("res://delauney_sphere.tscn"))
            {
                var delaunayButton = new Button();
                delaunayButton.Name = "DelaunaySphereButton";
                delaunayButton.Text = "Delaunay Sphere";
                delaunayButton.Pressed += () =>
                {
                    try
                    {
                        GetTree().ChangeSceneToFile("res://delauney_sphere.tscn");
                        GameLogger.Info("Transitioning to delauney_sphere.tscn");
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Error($"Failed to load delauney_sphere.tscn: {ex.Message}");
                        ShowNotification($"Error: {ex.Message}");
                    }
                };
                vbox.AddChild(delaunayButton);
            }

            // Add test_scene.tscn button
            if (ResourceLoader.Exists("res://test_scene.tscn"))
            {
                var testSceneButton = new Button();
                testSceneButton.Name = "TestSceneButton";
                testSceneButton.Text = "Test Scene";
                testSceneButton.Pressed += () =>
                {
                    try
                    {
                        GetTree().ChangeSceneToFile("res://test_scene.tscn");
                        GameLogger.Info("Transitioning to test_scene.tscn");
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Error($"Failed to load test_scene.tscn: {ex.Message}");
                        ShowNotification($"Error: {ex.Message}");
                    }
                };
                vbox.AddChild(testSceneButton);
            }

            // Add close button
            var closeButton = new Button();
            closeButton.Name = "CloseButton";
            closeButton.Text = "Close";
            closeButton.Pressed += () =>
            {
                debugMenuPanel.QueueFree();
                _debugMenu = null;
            };
            vbox.AddChild(closeButton);

            // Store reference
            _debugMenu = debugMenuPanel;

            GameLogger.Info("Created simple debug menu");
            GameLogger.ExitFunction(nameof(CreateSimpleDebugMenu));
        }

        /// <summary>
        /// Called when the Exit button is pressed.
        /// </summary>
        private void OnExitButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnExitButtonPressed));
            GameLogger.Info("Exit button pressed");

            // Show confirmation dialog
            ShowExitConfirmation();

            GameLogger.ExitFunction(nameof(OnExitButtonPressed));
        }

        /// <summary>
        /// Shows an exit confirmation dialog.
        /// </summary>
        private void ShowExitConfirmation()
        {
            GameLogger.EnterFunction(nameof(ShowExitConfirmation));

            // Create a confirmation dialog
            var confirmDialog = new AcceptDialog();
            confirmDialog.Title = "Exit Game";
            confirmDialog.DialogText = "Are you sure you want to exit the game?";
            confirmDialog.Size = new Vector2I(300, 150);
            confirmDialog.CloseRequested += () =>
            {
                confirmDialog.QueueFree();
            };
            confirmDialog.Confirmed += () =>
            {
                GameLogger.Info("User confirmed exit");
                GetTree().Quit();
            };
            confirmDialog.Canceled += () =>
            {
                GameLogger.Info("User cancelled exit");
                confirmDialog.QueueFree();
            };

            AddChild(confirmDialog);
            confirmDialog.PopupCentered();

            GameLogger.Info("Showing exit confirmation dialog");
            GameLogger.ExitFunction(nameof(ShowExitConfirmation));
        }

        /// <summary>
        /// Shows a simple notification to the user.
        /// </summary>
        /// <param name="message">The message to display.</param>
        private void ShowNotification(string message)
        {
            GameLogger.EnterFunction(nameof(ShowNotification), message);

            // Create a simple notification label
            var notificationLabel = new Label();
            notificationLabel.Name = "NotificationLabel";
            notificationLabel.Text = message;
            notificationLabel.HorizontalAlignment = HorizontalAlignment.Center;
            notificationLabel.VerticalAlignment = VerticalAlignment.Center;
            notificationLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            notificationLabel.Size = new Vector2(400, 100);
            notificationLabel.Position = new Vector2I((int)((Size.X - 400) / 2), 50);
            
            // Style it
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            styleBox.BorderColor = new Color(0.2f, 0.2f, 0.4f);
            styleBox.BorderWidthLeft = 2;
            styleBox.BorderWidthTop = 2;
            styleBox.BorderWidthRight = 2;
            styleBox.BorderWidthBottom = 2;
            styleBox.CornerRadiusTopLeft = 5;
            styleBox.CornerRadiusTopRight = 5;
            styleBox.CornerRadiusBottomRight = 5;
            styleBox.CornerRadiusBottomLeft = 5;
            notificationLabel.AddThemeStyleboxOverride("normal", styleBox);

            AddChild(notificationLabel);

            // Remove after 3 seconds
            var timer = new Timer();
            timer.Name = "NotificationTimer";
            timer.WaitTime = 3.0;
            timer.OneShot = true;
            timer.Timeout += () =>
            {
                notificationLabel.QueueFree();
                timer.QueueFree();
            };
            AddChild(timer);
            timer.Start();

            GameLogger.Info($"Showing notification: {message}");
            GameLogger.ExitFunction(nameof(ShowNotification));
        }

        /// <summary>
        /// Cleans up resources when the scene is exiting.
        /// </summary>
        public override void _ExitTree()
        {
            GameLogger.EnterFunction(nameof(_ExitTree));

            // Disconnect button signals
            if (_startButton != null)
            {
                _startButton.Pressed -= OnStartButtonPressed;
            }

            if (_settingsButton != null)
            {
                _settingsButton.Pressed -= OnSettingsButtonPressed;
            }

            if (_debugButton != null)
            {
                _debugButton.Pressed -= OnDebugButtonPressed;
            }

            if (_exitButton != null)
            {
                _exitButton.Pressed -= OnExitButtonPressed;
            }

            base._ExitTree();

            GameLogger.ExitFunction(nameof(_ExitTree));
        }
    }
}