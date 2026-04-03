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
        private Control? _templateMenu;

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

            if (_startButton != null)
            {
                _startButton.Pressed += OnStartButtonPressed;
            }

            if (_settingsButton != null)
            {
                _settingsButton.Pressed += OnSettingsButtonPressed;
            }

            if (_debugButton != null)
            {
                _debugButton.Pressed += OnDebugButtonPressed;
            }

            if (_exitButton != null)
            {
                _exitButton.Pressed += OnExitButtonPressed;
            }

            // Hide popups initially
            if (_settingsPanel != null)
            {
                _settingsPanel.Visible = false;
            }

            if (_debugMenu != null)
            {
                _debugMenu.Visible = false;
            }

            GameLogger.ExitFunction(nameof(InitializeUI));
        }

        /// <summary>
        /// Called when the Start Game button is pressed.
        /// Shows the template selection menu.
        /// </summary>
        private void OnStartButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnStartButtonPressed));
            GameLogger.Info("Start Game button pressed");

            if (_templateMenu != null)
            {
                _templateMenu.Visible = true;
            }
            else
            {
                CreateTemplateSelectionMenu();
            }

            GameLogger.ExitFunction(nameof(OnStartButtonPressed));
        }

        /// <summary>
        /// Creates a template selection menu listing available system templates.
        /// </summary>
        private void CreateTemplateSelectionMenu()
        {
            GameLogger.EnterFunction(nameof(CreateTemplateSelectionMenu));

            var panel = new Panel();
            panel.Name = "TemplateSelectionMenu";
            panel.Size = new Vector2(500, 400);
            panel.Position = (new Vector2(Size.X, Size.Y) - panel.Size) / 2;
            AddChild(panel);

            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            marginContainer.AddThemeConstantOverride("margin_left", 15);
            marginContainer.AddThemeConstantOverride("margin_right", 15);
            marginContainer.AddThemeConstantOverride("margin_top", 15);
            marginContainer.AddThemeConstantOverride("margin_bottom", 15);
            panel.AddChild(marginContainer);

            var vbox = new VBoxContainer();
            vbox.Name = "VBoxContainer";
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            marginContainer.AddChild(vbox);

            // Title
            var titleLabel = new Label();
            titleLabel.Text = "Select System Template";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(titleLabel);

            // Scrollable list of templates
            var scrollContainer = new ScrollContainer();
            scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(scrollContainer);

            var templateList = new VBoxContainer();
            templateList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scrollContainer.AddChild(templateList);

            // Scan for template files
            var dir = DirAccess.Open("res://Configuration/SystemTemplate/");
            if (dir != null)
            {
                var files = DirAccess.GetFilesAt("res://Configuration/SystemTemplate/");
                foreach (var file in files)
                {
                    if (!file.EndsWith(".yaml"))
                        continue;

                    var templateName = file.Replace(".yaml", "");
                    var button = new Button();
                    button.Text = templateName;
                    button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    var capturedFile = file;
                    button.Pressed += () => OnTemplateSelected(capturedFile);
                    templateList.AddChild(button);
                }
            }
            else
            {
                var errorLabel = new Label();
                errorLabel.Text = "No templates found.";
                errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
                templateList.AddChild(errorLabel);
            }

            // Close button
            var closeButton = new Button();
            closeButton.Text = "Cancel";
            closeButton.Pressed += () =>
            {
                panel.QueueFree();
                _templateMenu = null;
            };
            vbox.AddChild(closeButton);

            _templateMenu = panel;

            GameLogger.ExitFunction(nameof(CreateTemplateSelectionMenu));
        }

        /// <summary>
        /// Called when a template is selected. Stores the selection and transitions to LoadingScreen.
        /// </summary>
        private void OnTemplateSelected(string templateFileName)
        {
            GameLogger.Info($"Template selected: {templateFileName}");

            // Store the selected template in SignalBus for LoadingScreen to consume
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.SelectedTemplate = templateFileName;
            }

            try
            {
                GetTree().ChangeSceneToFile("res://Scenes/LoadingScreen.tscn");
                GameLogger.Info(
                    "Transitioning to LoadingScreen with template: " + templateFileName
                );
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to transition to loading screen: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the Settings button is pressed.
        /// </summary>
        private void OnSettingsButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnSettingsButtonPressed));
            GameLogger.Info("Settings button pressed");

            if (_settingsPanel != null)
            {
                _settingsPanel.Visible = true;
            }
            else
            {
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
                var settingsPanelPrefab = ResourceLoader.Load<PackedScene>(
                    "res://UI/Debug/Settings/SettingsPanel.tscn"
                );
                if (settingsPanelPrefab != null)
                {
                    var settingsPanelInstance = settingsPanelPrefab.Instantiate<Control>();
                    AddChild(settingsPanelInstance);
                    _settingsPanel = settingsPanelInstance;
                    _settingsPanel.Visible = true;

                    var panelSize = new Vector2(_settingsPanel.Size.X, _settingsPanel.Size.Y);
                    _settingsPanel.Position = (new Vector2(Size.X, Size.Y) - panelSize) / 2;

                    GameLogger.Info("Loaded and displayed SettingsPanel");
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

            if (_debugMenu != null)
            {
                _debugMenu.Visible = true;
            }
            else
            {
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

            var debugMenuPanel = new Panel();
            debugMenuPanel.Name = "SimpleDebugMenu";
            debugMenuPanel.Size = new Vector2(400, 300);
            debugMenuPanel.Position = (new Vector2(Size.X, Size.Y) - debugMenuPanel.Size) / 2;
            AddChild(debugMenuPanel);

            var vbox = new VBoxContainer();
            vbox.Name = "VBoxContainer";
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            debugMenuPanel.AddChild(vbox);

            var titleLabel = new Label();
            titleLabel.Text = "Debug Scenes";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(titleLabel);

            // System Generation debug scene
            if (ResourceLoader.Exists("res://Scenes/SystemGeneration.tscn"))
            {
                var sysGenButton = new Button();
                sysGenButton.Text = "System Generation";
                sysGenButton.Pressed += () =>
                {
                    try
                    {
                        GetTree().ChangeSceneToFile("res://Scenes/SystemGeneration.tscn");
                        GameLogger.Info("Transitioning to SystemGeneration.tscn");
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Error($"Failed to load SystemGeneration.tscn: {ex.Message}");
                        ShowNotification($"Error: {ex.Message}");
                    }
                };
                vbox.AddChild(sysGenButton);
            }

            // Planet Generation debug scene
            if (ResourceLoader.Exists("res://Scenes/PlanetGeneration.tscn"))
            {
                var planetGenButton = new Button();
                planetGenButton.Text = "Planet Generation";
                planetGenButton.Pressed += () =>
                {
                    try
                    {
                        GetTree().ChangeSceneToFile("res://Scenes/PlanetGeneration.tscn");
                        GameLogger.Info("Transitioning to PlanetGeneration.tscn");
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Error($"Failed to load PlanetGeneration.tscn: {ex.Message}");
                        ShowNotification($"Error: {ex.Message}");
                    }
                };
                vbox.AddChild(planetGenButton);
            }

            // Test scene
            if (ResourceLoader.Exists("res://test_scene.tscn"))
            {
                var testSceneButton = new Button();
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

            // Close button
            var closeButton = new Button();
            closeButton.Text = "Close";
            closeButton.Pressed += () =>
            {
                debugMenuPanel.QueueFree();
                _debugMenu = null;
            };
            vbox.AddChild(closeButton);

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
            ShowExitConfirmation();
            GameLogger.ExitFunction(nameof(OnExitButtonPressed));
        }

        /// <summary>
        /// Shows an exit confirmation dialog.
        /// </summary>
        private void ShowExitConfirmation()
        {
            var confirmDialog = new AcceptDialog();
            confirmDialog.Title = "Exit Game";
            confirmDialog.DialogText = "Are you sure you want to exit the game?";
            confirmDialog.Size = new Vector2I(300, 150);
            confirmDialog.CloseRequested += () => confirmDialog.QueueFree();
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
        }

        /// <summary>
        /// Shows a simple notification to the user.
        /// </summary>
        private void ShowNotification(string message)
        {
            var notificationLabel = new Label();
            notificationLabel.Text = message;
            notificationLabel.HorizontalAlignment = HorizontalAlignment.Center;
            notificationLabel.VerticalAlignment = VerticalAlignment.Center;
            notificationLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            notificationLabel.Size = new Vector2(400, 100);
            notificationLabel.Position = new Vector2I((int)((Size.X - 400) / 2), 50);

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

            var timer = new Timer();
            timer.WaitTime = 3.0;
            timer.OneShot = true;
            timer.Timeout += () =>
            {
                notificationLabel.QueueFree();
                timer.QueueFree();
            };
            AddChild(timer);
            timer.Start();
        }

        public override void _ExitTree()
        {
            if (_startButton != null)
                _startButton.Pressed -= OnStartButtonPressed;
            if (_settingsButton != null)
                _settingsButton.Pressed -= OnSettingsButtonPressed;
            if (_debugButton != null)
                _debugButton.Pressed -= OnDebugButtonPressed;
            if (_exitButton != null)
                _exitButton.Pressed -= OnExitButtonPressed;

            base._ExitTree();
        }
    }
}
