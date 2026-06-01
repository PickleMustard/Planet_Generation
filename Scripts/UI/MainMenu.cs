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
        private Button? _loadButton;
        private Control? _loadMenu;
        private Button? _settingsButton;
        private Button? _debugButton;
        private Button? _developerToolsButton;
        private Button? _exitButton;

        // Popup references
        private Control? _settingsPanel;
        private Control? _debugMenu;
        private Control? _developerToolsMenu;
        private Control? _resourceEditorOverlay;
        private Control? _recipeEditorOverlay;
        private Control? _buildingEditorOverlay;
        private Control? _building2DEditorOverlay;
        private Control? _linkProfileEditorOverlay;
        private Control? _biomeEditorOverlay;
        private Control? _shipEditorOverlay;
        private Control? _stationEditorOverlay;
        private Control? _engineEditorOverlay;
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
                _loadButton = vboxContainer.GetNodeOrNull<Button>("LoadButton");
                _settingsButton = vboxContainer.GetNodeOrNull<Button>("SettingsButton");
                _debugButton = vboxContainer.GetNodeOrNull<Button>("DebugButton");
                _developerToolsButton = vboxContainer.GetNodeOrNull<Button>("DeveloperToolsButton");
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

            // Create a Load Game button programmatically when the scene doesn't define one.
            if (_loadButton == null)
            {
                var vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
                if (vbox != null)
                {
                    _loadButton = new Button { Text = "Load Game", Name = "LoadButton" };
                    vbox.AddChild(_loadButton);
                    if (_startButton != null)
                        vbox.MoveChild(_loadButton, _startButton.GetIndex() + 1);
                }
            }
            if (_loadButton != null)
            {
                _loadButton.Pressed += OnLoadButtonPressed;
            }

            if (_settingsButton != null)
            {
                _settingsButton.Pressed += OnSettingsButtonPressed;
            }

            if (_debugButton != null)
            {
                _debugButton.Pressed += OnDebugButtonPressed;
            }

            if (_developerToolsButton != null)
            {
                _developerToolsButton.Pressed += OnDeveloperToolsButtonPressed;
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
        /// Called when the Load Game button is pressed. Lists save files in user://saves.
        /// </summary>
        private void OnLoadButtonPressed()
        {
            GameLogger.Info("Load Game button pressed");
            if (_loadMenu != null)
            {
                _loadMenu.Visible = true;
                return;
            }
            CreateSaveSelectionMenu();
        }

        /// <summary>
        /// Builds a menu listing available save files (user://saves/*.yaml).
        /// </summary>
        private void CreateSaveSelectionMenu()
        {
            var panel = new Panel { Name = "SaveSelectionMenu", Size = new Vector2(500, 400) };
            panel.Position = (new Vector2(Size.X, Size.Y) - panel.Size) / 2;
            AddChild(panel);

            var marginContainer = new MarginContainer();
            marginContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            marginContainer.AddThemeConstantOverride("margin_left", 15);
            marginContainer.AddThemeConstantOverride("margin_right", 15);
            marginContainer.AddThemeConstantOverride("margin_top", 15);
            marginContainer.AddThemeConstantOverride("margin_bottom", 15);
            panel.AddChild(marginContainer);

            var vbox = new VBoxContainer { Name = "VBoxContainer" };
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            marginContainer.AddChild(vbox);

            var titleLabel = new Label
            {
                Text = "Load Saved Game",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            vbox.AddChild(titleLabel);

            var scrollContainer = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            vbox.AddChild(scrollContainer);

            var saveList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            scrollContainer.AddChild(saveList);

            const string saveDir = "user://saves";
            DirAccess.MakeDirRecursiveAbsolute(saveDir);
            var files = DirAccess.GetFilesAt(saveDir);
            bool anySaves = false;
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (!file.EndsWith(".yaml"))
                        continue;
                    anySaves = true;
                    var fullPath = $"{saveDir}/{file}";
                    var button = new Button
                    {
                        Text = file.Replace(".yaml", ""),
                        SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    };
                    button.Pressed += () => OnSaveSelected(fullPath);
                    saveList.AddChild(button);
                }
            }

            if (!anySaves)
            {
                saveList.AddChild(new Label
                {
                    Text = "No saved games found.",
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            var closeButton = new Button { Text = "Cancel" };
            closeButton.Pressed += () =>
            {
                panel.QueueFree();
                _loadMenu = null;
            };
            vbox.AddChild(closeButton);

            _loadMenu = panel;
        }

        /// <summary>
        /// Called when a save file is selected. Stores the path and transitions to LoadingScreen.
        /// </summary>
        private void OnSaveSelected(string savePath)
        {
            GameLogger.Info($"Save selected: {savePath}");
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.SaveToLoadPath = savePath;
                SignalBus.Instance.SelectedTemplate = null;
            }
            try
            {
                GetTree().ChangeSceneToFile("res://Scenes/LoadingScreen.tscn");
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
                    "res://DeveloperTools/Settings/SettingsPanel.tscn"
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

            // PlanetBoard test harness
            if (ResourceLoader.Exists("res://Scenes/TestPlanetBoard.tscn"))
            {
                var planetBoardTestBtn = new Button();
                planetBoardTestBtn.Text = "Test PlanetBoard";
                planetBoardTestBtn.Pressed += () =>
                {
                    try
                    {
                        GetTree().ChangeSceneToFile("res://Scenes/TestPlanetBoard.tscn");
                        GameLogger.Info("Transitioning to TestPlanetBoard.tscn");
                    }
                    catch (Exception ex)
                    {
                        GameLogger.Error($"Failed to load TestPlanetBoard.tscn: {ex.Message}");
                        ShowNotification($"Error: {ex.Message}");
                    }
                };
                vbox.AddChild(planetBoardTestBtn);
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
        /// Called when the Developer Tools button is pressed.
        /// </summary>
        private void OnDeveloperToolsButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnDeveloperToolsButtonPressed));
            GameLogger.Info("Developer Tools button pressed");

            if (_developerToolsMenu != null)
            {
                _developerToolsMenu.Visible = true;
            }
            else
            {
                CreateDeveloperToolsMenu();
            }

            GameLogger.ExitFunction(nameof(OnDeveloperToolsButtonPressed));
        }

        /// <summary>
        /// Creates the Developer Tools menu containing System Generation and Resource Editor entries.
        /// </summary>
        private void CreateDeveloperToolsMenu()
        {
            GameLogger.EnterFunction(nameof(CreateDeveloperToolsMenu));

            var panel = new Panel();
            panel.Name = "DeveloperToolsMenu";
            panel.Size = new Vector2(400, 300);
            panel.Position = (new Vector2(Size.X, Size.Y) - panel.Size) / 2;
            AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.Name = "VBoxContainer";
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            panel.AddChild(vbox);

            var titleLabel = new Label();
            titleLabel.Text = "Developer Tools";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(titleLabel);

            // System Generation
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

#if DEBUG
            // Resource Editor
            if (ResourceLoader.Exists("res://DeveloperTools/ResourceEditor/ResourceEditorModule.tscn"))
            {
                var resourceEditorButton = new Button();
                resourceEditorButton.Text = "Resource Editor";
                resourceEditorButton.Pressed += OnResourceEditorButtonPressed;
                vbox.AddChild(resourceEditorButton);
            }

            // Recipe Editor
            if (ResourceLoader.Exists("res://DeveloperTools/RecipeEditor/RecipeEditorModule.tscn"))
            {
                var recipeEditorButton = new Button();
                recipeEditorButton.Text = "Recipe Editor";
                recipeEditorButton.Pressed += OnRecipeEditorButtonPressed;
                vbox.AddChild(recipeEditorButton);
            }

            // Building Editor
            if (ResourceLoader.Exists("res://DeveloperTools/BuildingEditor/BuildingEditorModule.tscn"))
            {
                var buildingEditorButton = new Button();
                buildingEditorButton.Text = "Building Editor";
                buildingEditorButton.Pressed += OnBuildingEditorButtonPressed;
                vbox.AddChild(buildingEditorButton);
            }

            // Building2D Shape Editor
            if (ResourceLoader.Exists("res://DeveloperTools/Building2DEditor/Building2DEditorModule.tscn"))
            {
                var building2DEditorButton = new Button();
                building2DEditorButton.Text = "Building Shape (2D) Editor";
                building2DEditorButton.Pressed += OnBuilding2DEditorButtonPressed;
                vbox.AddChild(building2DEditorButton);
            }

            // Link Profile Editor
            if (ResourceLoader.Exists("res://DeveloperTools/LinkProfileEditor/LinkProfileEditorModule.tscn"))
            {
                var linkProfileEditorButton = new Button();
                linkProfileEditorButton.Text = "Link Profile Editor";
                linkProfileEditorButton.Pressed += OnLinkProfileEditorButtonPressed;
                vbox.AddChild(linkProfileEditorButton);
            }

            // Biome Editor
            if (ResourceLoader.Exists("res://DeveloperTools/BiomeEditor/BiomeEditorModule.tscn"))
            {
                var biomeEditorButton = new Button();
                biomeEditorButton.Text = "Biome Editor";
                biomeEditorButton.Pressed += OnBiomeEditorButtonPressed;
                vbox.AddChild(biomeEditorButton);
            }

            // Ship Editor
            if (ResourceLoader.Exists("res://DeveloperTools/ShipEditor/ShipEditorModule.tscn"))
            {
                var shipEditorButton = new Button();
                shipEditorButton.Text = "Ship Editor";
                shipEditorButton.Pressed += OnShipEditorButtonPressed;
                vbox.AddChild(shipEditorButton);
            }

            // Station Editor
            if (ResourceLoader.Exists("res://DeveloperTools/StationEditor/StationEditorModule.tscn"))
            {
                var stationEditorButton = new Button();
                stationEditorButton.Text = "Station Editor";
                stationEditorButton.Pressed += OnStationEditorButtonPressed;
                vbox.AddChild(stationEditorButton);
            }

            // Engine Editor
            if (ResourceLoader.Exists("res://DeveloperTools/EngineEditor/EngineEditorModule.tscn"))
            {
                var engineEditorButton = new Button();
                engineEditorButton.Text = "Engine Editor";
                engineEditorButton.Pressed += OnEngineEditorButtonPressed;
                vbox.AddChild(engineEditorButton);
            }
#endif

            // Close button
            var closeButton = new Button();
            closeButton.Text = "Close";
            closeButton.Pressed += () =>
            {
                panel.QueueFree();
                _developerToolsMenu = null;
            };
            vbox.AddChild(closeButton);

            _developerToolsMenu = panel;

            GameLogger.Info("Created Developer Tools menu");
            GameLogger.ExitFunction(nameof(CreateDeveloperToolsMenu));
        }

#if DEBUG
        /// <summary>
        /// Opens the Resource Editor as a fullscreen overlay with a close button.
        /// </summary>
        private void OnResourceEditorButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnResourceEditorButtonPressed));

            if (_resourceEditorOverlay != null)
            {
                _resourceEditorOverlay.Visible = true;
                GameLogger.ExitFunction(nameof(OnResourceEditorButtonPressed));
                return;
            }

            try
            {
                var prefab = ResourceLoader.Load<PackedScene>(
                    "res://DeveloperTools/ResourceEditor/ResourceEditorModule.tscn"
                );
                if (prefab == null)
                {
                    GameLogger.Warning("ResourceEditorModule.tscn not found");
                    ShowNotification("Resource Editor not available.");
                    GameLogger.ExitFunction(nameof(OnResourceEditorButtonPressed));
                    return;
                }

                var overlay = new Panel();
                overlay.Name = "ResourceEditorOverlay";
                overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                AddChild(overlay);

                var vbox = new VBoxContainer();
                vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                overlay.AddChild(vbox);

                var headerBar = new HBoxContainer();
                headerBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                vbox.AddChild(headerBar);

                var titleLabel = new Label();
                titleLabel.Text = "Resource Editor";
                titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                headerBar.AddChild(titleLabel);

                var backButton = new Button();
                backButton.Text = "Back";
                backButton.Pressed += () =>
                {
                    overlay.QueueFree();
                    _resourceEditorOverlay = null;
                };
                headerBar.AddChild(backButton);

                var moduleInstance = prefab.Instantiate<Control>();
                moduleInstance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                moduleInstance.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(moduleInstance);

                // BaseDebugModule._Ready() hides the module by default (TabContainer un-hides
                // the active tab in the debug menu). Outside that context, force-show after
                // _Ready runs so the panel renders.
                moduleInstance.CallDeferred(Control.MethodName.Show);

                _resourceEditorOverlay = overlay;
                GameLogger.Info("Resource Editor overlay opened");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Resource Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(OnResourceEditorButtonPressed));
        }

        /// <summary>
        /// Opens the Recipe Editor as a fullscreen overlay with a close button.
        /// </summary>
        private void OnRecipeEditorButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnRecipeEditorButtonPressed));

            if (_recipeEditorOverlay != null)
            {
                _recipeEditorOverlay.Visible = true;
                GameLogger.ExitFunction(nameof(OnRecipeEditorButtonPressed));
                return;
            }

            try
            {
                var prefab = ResourceLoader.Load<PackedScene>(
                    "res://DeveloperTools/RecipeEditor/RecipeEditorModule.tscn"
                );
                if (prefab == null)
                {
                    GameLogger.Warning("RecipeEditorModule.tscn not found");
                    ShowNotification("Recipe Editor not available.");
                    GameLogger.ExitFunction(nameof(OnRecipeEditorButtonPressed));
                    return;
                }

                var overlay = new Panel();
                overlay.Name = "RecipeEditorOverlay";
                overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                AddChild(overlay);

                var vbox = new VBoxContainer();
                vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                overlay.AddChild(vbox);

                var headerBar = new HBoxContainer();
                headerBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                vbox.AddChild(headerBar);

                var titleLabel = new Label();
                titleLabel.Text = "Recipe Editor";
                titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                headerBar.AddChild(titleLabel);

                var backButton = new Button();
                backButton.Text = "Back";
                backButton.Pressed += () =>
                {
                    overlay.QueueFree();
                    _recipeEditorOverlay = null;
                };
                headerBar.AddChild(backButton);

                var moduleInstance = prefab.Instantiate<Control>();
                moduleInstance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                moduleInstance.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(moduleInstance);

                moduleInstance.CallDeferred(Control.MethodName.Show);

                _recipeEditorOverlay = overlay;
                GameLogger.Info("Recipe Editor overlay opened");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Recipe Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(OnRecipeEditorButtonPressed));
        }

        /// <summary>
        /// Opens the Building Editor as a fullscreen overlay with a close button.
        /// </summary>
        private void OnBuildingEditorButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnBuildingEditorButtonPressed));

            if (_buildingEditorOverlay != null)
            {
                _buildingEditorOverlay.Visible = true;
                GameLogger.ExitFunction(nameof(OnBuildingEditorButtonPressed));
                return;
            }

            try
            {
                var prefab = ResourceLoader.Load<PackedScene>(
                    "res://DeveloperTools/BuildingEditor/BuildingEditorModule.tscn"
                );
                if (prefab == null)
                {
                    GameLogger.Warning("BuildingEditorModule.tscn not found");
                    ShowNotification("Building Editor not available.");
                    GameLogger.ExitFunction(nameof(OnBuildingEditorButtonPressed));
                    return;
                }

                var overlay = new Panel();
                overlay.Name = "BuildingEditorOverlay";
                overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                AddChild(overlay);

                var vbox = new VBoxContainer();
                vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                overlay.AddChild(vbox);

                var headerBar = new HBoxContainer();
                headerBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                vbox.AddChild(headerBar);

                var titleLabel = new Label();
                titleLabel.Text = "Building Editor";
                titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                headerBar.AddChild(titleLabel);

                var backButton = new Button();
                backButton.Text = "Back";
                backButton.Pressed += () =>
                {
                    overlay.QueueFree();
                    _buildingEditorOverlay = null;
                };
                headerBar.AddChild(backButton);

                var moduleInstance = prefab.Instantiate<Control>();
                moduleInstance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                moduleInstance.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(moduleInstance);

                moduleInstance.CallDeferred(Control.MethodName.Show);

                _buildingEditorOverlay = overlay;
                GameLogger.Info("Building Editor overlay opened");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Building Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(OnBuildingEditorButtonPressed));
        }

        /// <summary>
        /// Opens the Building Shape (2D) Editor as a fullscreen overlay with a close button.
        /// </summary>
        private void OnBuilding2DEditorButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnBuilding2DEditorButtonPressed));

            if (_building2DEditorOverlay != null)
            {
                _building2DEditorOverlay.Visible = true;
                GameLogger.ExitFunction(nameof(OnBuilding2DEditorButtonPressed));
                return;
            }

            try
            {
                var prefab = ResourceLoader.Load<PackedScene>(
                    "res://DeveloperTools/Building2DEditor/Building2DEditorModule.tscn"
                );
                if (prefab == null)
                {
                    GameLogger.Warning("Building2DEditorModule.tscn not found");
                    ShowNotification("Building Shape (2D) Editor not available.");
                    GameLogger.ExitFunction(nameof(OnBuilding2DEditorButtonPressed));
                    return;
                }

                var overlay = new Panel();
                overlay.Name = "Building2DEditorOverlay";
                overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                AddChild(overlay);

                var vbox = new VBoxContainer();
                vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                overlay.AddChild(vbox);

                var headerBar = new HBoxContainer();
                headerBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                vbox.AddChild(headerBar);

                var titleLabel = new Label();
                titleLabel.Text = "Building Shape (2D) Editor";
                titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                headerBar.AddChild(titleLabel);

                var backButton = new Button();
                backButton.Text = "Back";
                backButton.Pressed += () =>
                {
                    overlay.QueueFree();
                    _building2DEditorOverlay = null;
                };
                headerBar.AddChild(backButton);

                var moduleInstance = prefab.Instantiate<Control>();
                moduleInstance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                moduleInstance.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(moduleInstance);

                moduleInstance.CallDeferred(Control.MethodName.Show);

                _building2DEditorOverlay = overlay;
                GameLogger.Info("Building Shape (2D) Editor overlay opened");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Building Shape (2D) Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(OnBuilding2DEditorButtonPressed));
        }

        /// <summary>
        /// Opens the Link Profile Editor as a fullscreen overlay with a close button.
        /// </summary>
        private void OnLinkProfileEditorButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnLinkProfileEditorButtonPressed));

            if (_linkProfileEditorOverlay != null)
            {
                _linkProfileEditorOverlay.Visible = true;
                GameLogger.ExitFunction(nameof(OnLinkProfileEditorButtonPressed));
                return;
            }

            try
            {
                var prefab = ResourceLoader.Load<PackedScene>(
                    "res://DeveloperTools/LinkProfileEditor/LinkProfileEditorModule.tscn"
                );
                if (prefab == null)
                {
                    GameLogger.Warning("LinkProfileEditorModule.tscn not found");
                    ShowNotification("Link Profile Editor not available.");
                    GameLogger.ExitFunction(nameof(OnLinkProfileEditorButtonPressed));
                    return;
                }

                var overlay = new Panel();
                overlay.Name = "LinkProfileEditorOverlay";
                overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                AddChild(overlay);

                var vbox = new VBoxContainer();
                vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                overlay.AddChild(vbox);

                var headerBar = new HBoxContainer();
                headerBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                vbox.AddChild(headerBar);

                var titleLabel = new Label();
                titleLabel.Text = "Link Profile Editor";
                titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                headerBar.AddChild(titleLabel);

                var backButton = new Button();
                backButton.Text = "Back";
                backButton.Pressed += () =>
                {
                    overlay.QueueFree();
                    _linkProfileEditorOverlay = null;
                };
                headerBar.AddChild(backButton);

                var moduleInstance = prefab.Instantiate<Control>();
                moduleInstance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                moduleInstance.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(moduleInstance);

                moduleInstance.CallDeferred(Control.MethodName.Show);

                _linkProfileEditorOverlay = overlay;
                GameLogger.Info("Link Profile Editor overlay opened");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Link Profile Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(OnLinkProfileEditorButtonPressed));
        }

        /// <summary>
        /// Opens the Biome Editor as a fullscreen overlay with a close button.
        /// </summary>
        private void OnBiomeEditorButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnBiomeEditorButtonPressed));

            if (_biomeEditorOverlay != null)
            {
                _biomeEditorOverlay.Visible = true;
                GameLogger.ExitFunction(nameof(OnBiomeEditorButtonPressed));
                return;
            }

            try
            {
                var prefab = ResourceLoader.Load<PackedScene>(
                    "res://DeveloperTools/BiomeEditor/BiomeEditorModule.tscn"
                );
                if (prefab == null)
                {
                    GameLogger.Warning("BiomeEditorModule.tscn not found");
                    ShowNotification("Biome Editor not available.");
                    GameLogger.ExitFunction(nameof(OnBiomeEditorButtonPressed));
                    return;
                }

                var overlay = new Panel();
                overlay.Name = "BiomeEditorOverlay";
                overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                AddChild(overlay);

                var vbox = new VBoxContainer();
                vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                overlay.AddChild(vbox);

                var headerBar = new HBoxContainer();
                headerBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                vbox.AddChild(headerBar);

                var titleLabel = new Label();
                titleLabel.Text = "Biome Editor";
                titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                headerBar.AddChild(titleLabel);

                var backButton = new Button();
                backButton.Text = "Back";
                backButton.Pressed += () =>
                {
                    overlay.QueueFree();
                    _biomeEditorOverlay = null;
                };
                headerBar.AddChild(backButton);

                var moduleInstance = prefab.Instantiate<Control>();
                moduleInstance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                moduleInstance.SizeFlagsVertical = SizeFlags.ExpandFill;
                vbox.AddChild(moduleInstance);

                moduleInstance.CallDeferred(Control.MethodName.Show);

                _biomeEditorOverlay = overlay;
                GameLogger.Info("Biome Editor overlay opened");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Biome Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }

            GameLogger.ExitFunction(nameof(OnBiomeEditorButtonPressed));
        }

        /// <summary>
        /// Builds a fullscreen editor overlay (header bar + Back button + module
        /// instance) for the given module scene. Returns the overlay, or null if
        /// the scene could not be loaded. <paramref name="onClosed"/> runs when the
        /// Back button is pressed so the caller can clear its overlay field.
        /// </summary>
        private Control? OpenEditorOverlay(string title, string scenePath, Action onClosed)
        {
            var prefab = ResourceLoader.Load<PackedScene>(scenePath);
            if (prefab == null)
            {
                GameLogger.Warning($"{scenePath} not found");
                ShowNotification($"{title} not available.");
                return null;
            }

            var overlay = new Panel { Name = $"{title.Replace(" ", "")}Overlay" };
            overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(overlay);

            var vbox = new VBoxContainer();
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            overlay.AddChild(vbox);

            var headerBar = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            vbox.AddChild(headerBar);

            var titleLabel = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            headerBar.AddChild(titleLabel);

            var backButton = new Button { Text = "Back" };
            backButton.Pressed += () =>
            {
                overlay.QueueFree();
                onClosed();
            };
            headerBar.AddChild(backButton);

            var moduleInstance = prefab.Instantiate<Control>();
            moduleInstance.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            moduleInstance.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(moduleInstance);

            // BaseDebugModule._Ready() hides the module by default; force-show outside
            // the debug TabContainer so the panel renders.
            moduleInstance.CallDeferred(Control.MethodName.Show);

            GameLogger.Info($"{title} overlay opened");
            return overlay;
        }

        private void OnShipEditorButtonPressed()
        {
            if (_shipEditorOverlay != null) { _shipEditorOverlay.Visible = true; return; }
            try
            {
                _shipEditorOverlay = OpenEditorOverlay(
                    "Ship Editor",
                    "res://DeveloperTools/ShipEditor/ShipEditorModule.tscn",
                    () => _shipEditorOverlay = null);
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Ship Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }
        }

        private void OnStationEditorButtonPressed()
        {
            if (_stationEditorOverlay != null) { _stationEditorOverlay.Visible = true; return; }
            try
            {
                _stationEditorOverlay = OpenEditorOverlay(
                    "Station Editor",
                    "res://DeveloperTools/StationEditor/StationEditorModule.tscn",
                    () => _stationEditorOverlay = null);
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Station Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }
        }

        private void OnEngineEditorButtonPressed()
        {
            if (_engineEditorOverlay != null) { _engineEditorOverlay.Visible = true; return; }
            try
            {
                _engineEditorOverlay = OpenEditorOverlay(
                    "Engine Editor",
                    "res://DeveloperTools/EngineEditor/EngineEditorModule.tscn",
                    () => _engineEditorOverlay = null);
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to open Engine Editor: {ex.Message}");
                ShowNotification($"Error: {ex.Message}");
            }
        }
#endif

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
            if (_loadButton != null)
                _loadButton.Pressed -= OnLoadButtonPressed;
            if (_settingsButton != null)
                _settingsButton.Pressed -= OnSettingsButtonPressed;
            if (_debugButton != null)
                _debugButton.Pressed -= OnDebugButtonPressed;
            if (_developerToolsButton != null)
                _developerToolsButton.Pressed -= OnDeveloperToolsButtonPressed;
            if (_exitButton != null)
                _exitButton.Pressed -= OnExitButtonPressed;

            base._ExitTree();
        }
    }
}
