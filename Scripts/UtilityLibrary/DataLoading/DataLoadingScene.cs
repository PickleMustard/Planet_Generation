using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Logistics.Resources;
using Structures.Resources;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;

namespace UtilityLibrary.DataLoading
{
    /// <summary>
    /// Main scene for handling database loading with progress visualization.
    /// </summary>
    public partial class DataLoadingScene : Node
    {
        // UI References
        private ColorRect? _background;
        private Control? _loadingUI;
        private Label? _titleLabel;
        private ProgressBar? _overallProgressBar;
        private Label? _statusLabel;
        private VBoxContainer? _databaseProgressContainer;
        private Panel? _errorPanel;
        private Label? _errorMessageLabel;
        private Button? _retryButton;
        private Button? _skipButton;

        // Prefabs
        private PackedScene? _databaseProgressItemPrefab;

        // State
        private DatabaseLoadManager? _loadManager;
        private readonly Dictionary<string, Control> _progressItems = new();
        private bool _allDatabasesLoaded;
        private bool _loadingInProgress;
        private string? _currentBatchId;

        public override void _Ready()
        {
            GameLogger.EnterFunction(nameof(_Ready));

            // Get UI references
            GetUIReferences();

            // Initialize UI
            InitializeUI();

            // Initialize load manager
            InitializeLoadManager();

            // Start loading databases
            StartDatabaseLoading();

            GameLogger.ExitFunction(nameof(_Ready));
        }

        public override void _Process(double delta)
        {
            // Update overall progress if loading is in progress
            if (_loadingInProgress && _loadManager != null && _overallProgressBar != null)
            {
                float overallProgress = CalculateOverallProgress();
                _overallProgressBar.Value = overallProgress * 100;

                // Update status label
                if (_statusLabel != null)
                {
                    int loadedCount = GetLoadedDatabaseCount();
                    int totalCount = GetTotalDatabaseCount();
                    _statusLabel.Text =
                        $"Loading databases... {loadedCount}/{totalCount} ({Math.Round(overallProgress * 100)}%)";
                }

                // Check if all databases are loaded
                if (overallProgress >= 1.0f && !_allDatabasesLoaded)
                {
                    OnAllDatabasesLoaded();
                }
            }
        }

        /// <summary>
        /// Gets references to UI nodes.
        /// </summary>
        private void GetUIReferences()
        {
            GameLogger.EnterFunction(nameof(GetUIReferences));

            // Get UI nodes
            _background = GetNodeOrNull<ColorRect>("Background");
            _loadingUI = GetNodeOrNull<Control>("LoadingUI");

            if (_loadingUI != null)
            {
                _titleLabel = _loadingUI.GetNodeOrNull<Label>("VBoxContainer/TitleLabel");
                _overallProgressBar = _loadingUI.GetNodeOrNull<ProgressBar>(
                    "VBoxContainer/OverallProgressBar"
                );
                _statusLabel = _loadingUI.GetNodeOrNull<Label>("VBoxContainer/StatusLabel");
                _databaseProgressContainer = _loadingUI.GetNodeOrNull<VBoxContainer>(
                    "VBoxContainer/DatabaseProgressContainer"
                );
                _errorPanel = _loadingUI.GetNodeOrNull<Panel>("VBoxContainer/ErrorPanel");

                if (_errorPanel != null)
                {
                    _errorMessageLabel = _errorPanel.GetNodeOrNull<Label>(
                        "VBoxContainer2/ErrorMessageLabel"
                    );
                    _retryButton = _errorPanel.GetNodeOrNull<Button>(
                        "VBoxContainer2/HBoxContainer/RetryButton"
                    );
                    _skipButton = _errorPanel.GetNodeOrNull<Button>(
                        "VBoxContainer2/HBoxContainer/SkipButton"
                    );
                }
            }

            // Try to load the progress item prefab
            if (_databaseProgressItemPrefab == null)
            {
                _databaseProgressItemPrefab = ResourceLoader.Load<PackedScene>(
                    "res://UI/DataLoading/DatabaseProgressItem.tscn"
                );
            }

            GameLogger.ExitFunction(nameof(GetUIReferences));
        }

        /// <summary>
        /// Initializes the UI components.
        /// </summary>
        private void InitializeUI()
        {
            GameLogger.EnterFunction(nameof(InitializeUI));

            // Set initial UI state
            if (_overallProgressBar != null)
            {
                _overallProgressBar.MinValue = 0;
                _overallProgressBar.MaxValue = 100;
                _overallProgressBar.Value = 0;
            }

            if (_statusLabel != null)
            {
                _statusLabel.Text = "Initializing...";
            }

            if (_titleLabel != null)
            {
                _titleLabel.Text = "Loading Game Data";
            }

            // Hide error panel initially
            if (_errorPanel != null)
            {
                _errorPanel.Visible = false;
            }

            // Connect button signals
            if (_retryButton != null)
            {
                _retryButton.Pressed += OnRetryButtonPressed;
            }

            if (_skipButton != null)
            {
                _skipButton.Pressed += OnSkipButtonPressed;
            }

            GameLogger.ExitFunction(nameof(InitializeUI));
        }

        /// <summary>
        /// Initializes the database load manager.
        /// </summary>
        private void InitializeLoadManager()
        {
            GameLogger.EnterFunction(nameof(InitializeLoadManager));

            // Get or create load manager
            _loadManager = DatabaseLoadManager.Instance;
            if (_loadManager == null)
            {
                _loadManager = new DatabaseLoadManager();
                AddChild(_loadManager);
                GameLogger.Info("Created new DatabaseLoadManager instance");
            }
            else
            {
                GameLogger.Info("Using existing DatabaseLoadManager instance");
            }

            // Register databases
            RegisterDatabases();

            GameLogger.ExitFunction(nameof(InitializeLoadManager));
        }

        /// <summary>
        /// Registers all databases with the load manager.
        /// </summary>
        private void RegisterDatabases()
        {
            GameLogger.EnterFunction(nameof(RegisterDatabases));

            if (_loadManager == null)
            {
                GameLogger.Error("Load manager not initialized");
                return;
            }

            // Register ResourceDatabase
            var resourceDb = ResourceDatabase.Instance;
            if (resourceDb != null && !_loadManager.IsDatabaseRegistered(resourceDb.DatabaseName))
            {
                if (_loadManager.RegisterDatabase(resourceDb))
                {
                    GameLogger.Info($"Registered database: {resourceDb.DatabaseName}");
                }
                else
                {
                    GameLogger.Warning($"Failed to register database: {resourceDb.DatabaseName}");
                }
            }

            // Register BuildingDatabase
            var buildingDb = BuildingDatabase.Instance;
            if (buildingDb != null && !_loadManager.IsDatabaseRegistered(buildingDb.DatabaseName))
            {
                if (_loadManager.RegisterDatabase(buildingDb))
                {
                    GameLogger.Info($"Registered database: {buildingDb.DatabaseName}");
                }
                else
                {
                    GameLogger.Warning($"Failed to register database: {buildingDb.DatabaseName}");
                }
            }

            // Register ShipDatabase
            var shipDb = ShipDatabase.Instance;
            if (shipDb != null && !_loadManager.IsDatabaseRegistered(shipDb.DatabaseName))
            {
                if (_loadManager.RegisterDatabase(shipDb))
                {
                    GameLogger.Info($"Registered database: {shipDb.DatabaseName}");
                }
                else
                {
                    GameLogger.Warning($"Failed to register database: {shipDb.DatabaseName}");
                }
            }

            // Register RecipeDatabase
            var recipeDb = RecipeDatabase.Instance;
            if (recipeDb != null && !_loadManager.IsDatabaseRegistered(recipeDb.DatabaseName))
            {
                if (_loadManager.RegisterDatabase(recipeDb))
                {
                    GameLogger.Info($"Registered database: {recipeDb.DatabaseName}");
                }
                else
                {
                    GameLogger.Warning($"Failed to register database: {recipeDb.DatabaseName}");
                }
            }

            // Register StationDatabase
            var stationDb = StationDatabase.Instance;
            if (stationDb != null && !_loadManager.IsDatabaseRegistered(stationDb.DatabaseName))
            {
                if (_loadManager.RegisterDatabase(stationDb))
                {
                    GameLogger.Info($"Registered database: {stationDb.DatabaseName}");
                }
                else
                {
                    GameLogger.Warning($"Failed to register database: {stationDb.DatabaseName}");
                }
            }

            var TagConfigDb = ResourceGenerationConfigDatabase.Instance;
            if (TagConfigDb != null && !_loadManager.IsDatabaseRegistered(TagConfigDb.DatabaseName))
            {
                if (_loadManager.RegisterDatabase(TagConfigDb))
                {
                    GameLogger.Info($"Registered database: {TagConfigDb.DatabaseName}");
                }
                else
                {
                    GameLogger.Warning($"Failed to register database: {TagConfigDb.DatabaseName}");
                }
            }

            GameLogger.ExitFunction(
                nameof(RegisterDatabases),
                $"Registered {GetTotalDatabaseCount()} databases"
            );
        }

        /// <summary>
        /// Starts loading all registered databases.
        /// </summary>
        private void StartDatabaseLoading()
        {
            GameLogger.EnterFunction(nameof(StartDatabaseLoading));

            if (_loadManager == null)
            {
                ShowError("Load manager not initialized");
                return;
            }

            _currentBatchId = Guid.NewGuid().ToString();
            _loadingInProgress = true;
            _allDatabasesLoaded = false;

            // Create progress items for each database
            CreateProgressItems();

            // Start loading
            if (_loadManager.LoadAllDatabases(_currentBatchId))
            {
                GameLogger.Info($"Started database loading with batch ID: {_currentBatchId}");

                if (_statusLabel != null)
                {
                    _statusLabel.Text = "Starting database loading...";
                }
            }
            else
            {
                ShowError("Failed to start database loading");
                GameLogger.Error("Failed to start database loading");
            }

            GameLogger.ExitFunction(nameof(StartDatabaseLoading));
        }

        /// <summary>
        /// Creates progress UI items for each registered database.
        /// </summary>
        private void CreateProgressItems()
        {
            GameLogger.EnterFunction(nameof(CreateProgressItems));

            if (
                _loadManager == null
                || _databaseProgressContainer == null
                || _databaseProgressItemPrefab == null
            )
            {
                GameLogger.Warning("Missing required references for progress items");
                return;
            }

            // Clear existing items
            foreach (var child in _databaseProgressContainer.GetChildren())
            {
                child.QueueFree();
            }
            _progressItems.Clear();

            // Create progress items for each database
            foreach (var dbName in _loadManager.GetRegisteredDatabaseNames())
            {
                var progressItem = _databaseProgressItemPrefab.Instantiate<Control>();
                _databaseProgressContainer.AddChild(progressItem);
                _progressItems[dbName] = progressItem;

                // Initialize progress item
                InitializeProgressItem(progressItem, dbName);

                GameLogger.Debug($"Created progress item for database: {dbName}");
            }

            GameLogger.ExitFunction(
                nameof(CreateProgressItems),
                $"Created {_progressItems.Count} progress items"
            );
        }

        /// <summary>
        /// Initializes a progress item with database information.
        /// </summary>
        private void InitializeProgressItem(Control progressItem, string databaseName)
        {
            // Set database name label
            var nameLabel = progressItem.GetNodeOrNull<Label>("HBoxContainer/NameLabel");
            if (nameLabel != null)
            {
                nameLabel.Text = databaseName;
            }

            // Initialize progress bar
            var progressBar = progressItem.GetNodeOrNull<ProgressBar>("HBoxContainer/ProgressBar");
            if (progressBar != null)
            {
                progressBar.MinValue = 0;
                progressBar.MaxValue = 100;
                progressBar.Value = 0;
            }

            // Initialize status label
            var statusLabel = progressItem.GetNodeOrNull<Label>("HBoxContainer/StatusLabel");
            if (statusLabel != null)
            {
                statusLabel.Text = "Waiting...";
            }
        }

        /// <summary>
        /// Updates a progress item with current loading status.
        /// </summary>
        private void UpdateProgressItem(string databaseName, float progress, string status)
        {
            if (
                _progressItems.TryGetValue(databaseName, out var progressItem)
                && progressItem != null
            )
            {
                // Update progress bar
                var progressBar = progressItem.GetNodeOrNull<ProgressBar>(
                    "HBoxContainer/ProgressBar"
                );
                if (progressBar != null)
                {
                    progressBar.Value = progress * 100;
                }

                // Update status label
                var statusLabel = progressItem.GetNodeOrNull<Label>("HBoxContainer/StatusLabel");
                if (statusLabel != null)
                {
                    statusLabel.Text = status;
                }
            }
        }

        /// <summary>
        /// Calculates the overall loading progress across all databases.
        /// </summary>
        private float CalculateOverallProgress()
        {
            if (_loadManager == null)
                return 0f;

            float totalProgress = 0f;
            int count = 0;

            foreach (var dbName in _loadManager.GetRegisteredDatabaseNames())
            {
                totalProgress += _loadManager.GetDatabaseProgress(dbName);
                count++;
            }

            return count > 0 ? totalProgress / count : 0f;
        }

        /// <summary>
        /// Gets the count of loaded databases.
        /// </summary>
        private int GetLoadedDatabaseCount()
        {
            if (_loadManager == null)
                return 0;

            int loadedCount = 0;
            foreach (var dbName in _loadManager.GetRegisteredDatabaseNames())
            {
                if (_loadManager.GetDatabaseLoaded(dbName))
                    loadedCount++;
            }

            return loadedCount;
        }

        /// <summary>
        /// Gets the total count of registered databases.
        /// </summary>
        private int GetTotalDatabaseCount()
        {
            if (_loadManager == null)
                return 0;

            return _loadManager.GetRegisteredDatabaseNames().Count();
        }

        /// <summary>
        /// Called when all databases have finished loading.
        /// </summary>
        private void OnAllDatabasesLoaded()
        {
            GameLogger.EnterFunction(nameof(OnAllDatabasesLoaded));

            _allDatabasesLoaded = true;
            _loadingInProgress = false;

            // Update status
            if (_statusLabel != null)
            {
                _statusLabel.Text = "All databases loaded successfully!";
            }

            GameLogger.Info("All databases loaded successfully");

            // Transition to main menu after a short delay
            CallDeferred(nameof(TransitionToMainMenu));

            GameLogger.ExitFunction(nameof(OnAllDatabasesLoaded));
        }

        /// <summary>
        /// Transitions to the main menu scene.
        /// </summary>
        private void TransitionToMainMenu()
        {
            GameLogger.EnterFunction(nameof(TransitionToMainMenu));

            GameLogger.Info("Transitioning to main menu...");

            // Add a small delay for visual feedback
            CallDeferred(nameof(PerformSceneTransition));

            GameLogger.ExitFunction(nameof(TransitionToMainMenu));
        }

        /// <summary>
        /// Performs the actual scene transition.
        /// </summary>
        private void PerformSceneTransition()
        {
            GameLogger.EnterFunction(nameof(PerformSceneTransition));

            try
            {
                // Check if main menu scene exists
                if (ResourceLoader.Exists("res://Scenes/MainMenu.tscn"))
                {
                    // Change to main menu scene
                    GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
                    GameLogger.Info("Successfully transitioned to MainMenu scene");
                }
                else
                {
                    // Fallback: create a simple main menu in code
                    GameLogger.Warning("MainMenu scene not found. Creating fallback menu...");
                    CreateFallbackMainMenu();
                }
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to transition to main menu: {ex.Message}");
                // Try fallback
                CreateFallbackMainMenu();
            }

            GameLogger.ExitFunction(nameof(PerformSceneTransition));
        }

        /// <summary>
        /// Creates a fallback main menu when the scene file is not found.
        /// </summary>
        private void CreateFallbackMainMenu()
        {
            GameLogger.EnterFunction(nameof(CreateFallbackMainMenu));

            // Create a simple fallback UI
            var fallbackControl = new Control();
            fallbackControl.Name = "FallbackMainMenu";
            fallbackControl.AnchorsPreset = (int)Control.LayoutPreset.FullRect;

            var background = new ColorRect();
            background.Name = "Background";
            background.AnchorsPreset = (int)Control.LayoutPreset.FullRect;
            background.Color = new Color(0.05f, 0.05f, 0.1f);
            fallbackControl.AddChild(background);

            var titleLabel = new Label();
            titleLabel.Name = "TitleLabel";
            titleLabel.Text = "Game Loaded Successfully!";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.VerticalAlignment = VerticalAlignment.Center;
            titleLabel.AnchorsPreset = (int)Control.LayoutPreset.Center;
            titleLabel.Position = new Vector2(-200, -50);
            titleLabel.Size = new Vector2(400, 100);
            fallbackControl.AddChild(titleLabel);

            var messageLabel = new Label();
            messageLabel.Name = "MessageLabel";
            messageLabel.Text = "All databases loaded successfully.\n\nThe game is ready to play!";
            messageLabel.HorizontalAlignment = HorizontalAlignment.Center;
            messageLabel.VerticalAlignment = VerticalAlignment.Center;
            messageLabel.AnchorsPreset = (int)Control.LayoutPreset.Center;
            messageLabel.Position = new Vector2(-200, 50);
            messageLabel.Size = new Vector2(400, 100);
            fallbackControl.AddChild(messageLabel);

            // Replace current scene with fallback
            GetTree().Root.AddChild(fallbackControl);
            GetTree().CurrentScene.QueueFree();
            GetTree().CurrentScene = fallbackControl;

            GameLogger.Info("Created fallback main menu");

            GameLogger.ExitFunction(nameof(CreateFallbackMainMenu));
        }

        /// <summary>
        /// Shows an error message to the user.
        /// </summary>
        private void ShowError(string message)
        {
            GameLogger.EnterFunction(nameof(ShowError), message);

            if (_errorPanel != null && _errorMessageLabel != null)
            {
                _errorPanel.Visible = true;
                _errorMessageLabel.Text = message;
                _loadingInProgress = false;
            }

            GameLogger.Error($"Loading error: {message}");

            GameLogger.ExitFunction(nameof(ShowError));
        }

        /// <summary>
        /// Called when the retry button is pressed.
        /// </summary>
        private void OnRetryButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnRetryButtonPressed));

            // Hide error panel
            if (_errorPanel != null)
            {
                _errorPanel.Visible = false;
            }

            // Restart loading
            StartDatabaseLoading();

            GameLogger.ExitFunction(nameof(OnRetryButtonPressed));
        }

        /// <summary>
        /// Called when the skip button is pressed.
        /// </summary>
        private void OnSkipButtonPressed()
        {
            GameLogger.EnterFunction(nameof(OnSkipButtonPressed));

            // Hide error panel
            if (_errorPanel != null)
            {
                _errorPanel.Visible = false;
            }

            // Skip to main menu (with potential missing data)
            GameLogger.Warning("Skipping database loading - some features may not work properly");
            TransitionToMainMenu();

            GameLogger.ExitFunction(nameof(OnSkipButtonPressed));
        }

        /// <summary>
        /// Cleans up resources when the scene is exiting.
        /// </summary>
        public override void _ExitTree()
        {
            // Disconnect signals
            if (_retryButton != null)
            {
                _retryButton.Pressed -= OnRetryButtonPressed;
            }

            if (_skipButton != null)
            {
                _skipButton.Pressed -= OnSkipButtonPressed;
            }

            base._ExitTree();
        }
    }
}

