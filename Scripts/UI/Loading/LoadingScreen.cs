using System;
using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace UI.Loading
{
    /// <summary>
    /// Main loading screen controller that displays generation progress.
    /// Shows overall progress, template details, and individual body status.
    /// </summary>
    public partial class LoadingScreen : Control
    {
        // UI node references - set via [Export] in Godot editor
        [Export]
        private Label? _loadingLabel;

        [Export]
        private ProgressBar? _progressBar;

        [Export]
        private Label? _progressText;

        [Export]
        private Label? _templateLabel;

        [Export]
        private Label? _totalBodiesLabel;

        [Export]
        private Label? _timeElapsedLabel;

        [Export]
        private Label? _bodiesHeaderLabel;

        [Export]
        private VBoxContainer? _bodyListContainer;

        [Export]
        private PackedScene? _bodyItemScene;

        // State tracking
        private string? _selectedTemplate;
        private int _totalBodies = 0;
        private int _completedBodies = 0;
        private float _startTime = 0f;
        private float _lastUpdateTime = 0f;
        private readonly Dictionary<string, LoadingBodyItem> _bodyItems = new();
        private readonly Dictionary<string, BodyStatus> _bodyStatuses = new();

        // Background loading
        private Node3D? _gameSceneInstance;
        private Node? _gameSystemContainer;
        private Node3D? _generationContainer;

        // Body status tracking
        private class BodyStatus
        {
            public string Name { get; set; } = string.Empty;
            public string BodyType { get; set; } = string.Empty;
            public string Stage { get; set; } = "Initializing";
            public bool IsComplete { get; set; } = false;
            public float StartTime { get; set; }
            public float? CompletionTime { get; set; }
            public string[] StepNames { get; set; } = Array.Empty<string>();
            public int CurrentStepIndex { get; set; } = 0;
            public int IndentLevel { get; set; } = 0;
            public float? GenerationTime =>
                IsComplete && CompletionTime.HasValue ? CompletionTime.Value - StartTime : null;
        }

        /// <summary>
        /// Called when the node enters the scene tree.
        /// Initializes the loading screen and starts generation.
        /// </summary>
        public override void _Ready()
        {
            GameLogger.EnterFunction(nameof(_Ready));

            // Initialize UI
            InitializeUI();

            // Connect to SignalBus for progress updates
            ConnectToSignals();

            // Load template and start generation
            LoadTemplateAndStartGeneration();

            // Start update timer
            _startTime = (float)Time.GetTicksMsec() / 1000f;
            _lastUpdateTime = _startTime;

            GameLogger.ExitFunction(nameof(_Ready));
        }

        /// <summary>
        /// Called every frame. Updates time elapsed display.
        /// </summary>
        public override void _Process(double delta)
        {
            float currentTime = (float)Time.GetTicksMsec() / 1000f;
            float elapsed = currentTime - _startTime;

            // Update time display every 0.1 seconds
            if (currentTime - _lastUpdateTime >= 0.1f)
            {
                _lastUpdateTime = currentTime;
                UpdateTimeElapsed(elapsed);
            }
        }

        /// <summary>
        /// Initializes UI components with default values.
        /// </summary>
        private void InitializeUI()
        {
            if (_loadingLabel != null)
                _loadingLabel.Text = "LOADING";

            if (_progressBar != null)
            {
                _progressBar.Value = 0;
                _progressBar.MaxValue = 100;
            }

            if (_progressText != null)
                _progressText.Text = "0%";

            if (_templateLabel != null)
                _templateLabel.Text = "None";

            if (_totalBodiesLabel != null)
                _totalBodiesLabel.Text = "0";

            if (_timeElapsedLabel != null)
                _timeElapsedLabel.Text = "0.0s";

            if (_bodiesHeaderLabel != null)
                _bodiesHeaderLabel.Text = "Orbital Bodies (0/0) 0.0s";

            if (_bodyListContainer != null)
            {
                // Clear any existing children (in case of scene reuse)
                foreach (Node child in _bodyListContainer.GetChildren())
                {
                    child.QueueFree();
                }
            }
        }

        /// <summary>
        /// Connects to SignalBus for progress updates.
        /// </summary>
        private void ConnectToSignals()
        {
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.ConnectStartTimer(new Callable(this, nameof(OnStartTimer)));
                SignalBus.Instance.ConnectIncrementTimerStep(
                    new Callable(this, nameof(OnIncrementTimerStep))
                );
                SignalBus.Instance.ConnectStopTimer(new Callable(this, nameof(OnStopTimer)));
                SignalBus.Instance.ConnectSystemGenerationComplete(
                    new Callable(this, nameof(OnSystemGenerationComplete))
                );

                GameLogger.Info("Connected to SignalBus for loading screen updates");
            }
            else
            {
                GameLogger.Warning(
                    "SignalBus.Instance is null - cannot connect to progress signals"
                );
            }
        }

        /// <summary>
        /// Loads the selected template and starts system generation.
        /// </summary>
        private void LoadTemplateAndStartGeneration()
        {
            _selectedTemplate = SignalBus.Instance?.SelectedTemplate;

            if (string.IsNullOrEmpty(_selectedTemplate))
            {
                GameLogger.Error("No template selected for loading screen");
                ShowErrorAndReturn(
                    "No template selected. Please select a system template from the main menu."
                );
                return;
            }

            try
            {
                // Load template to get body count (including satellites)
                var templateData = TemplateHelpers.LoadSystemTemplate(_selectedTemplate);
                _totalBodies =
                    templateData.Dominant.Count
                    + templateData.Planetary.Count;

                // Count belt satellites
                foreach (var belt in templateData.Belts)
                {
                    if (belt.ContainsKey("belt_number"))
                        _totalBodies += (int)belt["belt_number"];
                }

                // Count planetary satellites
                foreach (var body in templateData.Planetary)
                {
                    if (
                        body.ContainsKey("satellites")
                        && body["satellites"].Obj
                            is Godot.Collections.Array<Godot.Collections.Dictionary> sats
                    )
                    {
                        _totalBodies += sats.Count;
                    }
                    else if (
                        body.ContainsKey("satellites")
                        && body["satellites"].Obj is Godot.Collections.Array satArray
                    )
                    {
                        _totalBodies += satArray.Count;
                    }
                }

                // Update UI with template info
                UpdateTemplateInfo();

                // Load GameScene in background for body parenting
                LoadGameSceneInBackground();

                // Create initial body status entries
                CreateInitialBodyStatuses(templateData);

                // Start system generation
                StartSystemGeneration(templateData);

                GameLogger.Info(
                    $"Loading screen initialized for template: {_selectedTemplate} with {_totalBodies} bodies"
                );
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to load template '{_selectedTemplate}': {ex.Message}");
                ShowErrorAndReturn($"Failed to load template: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the GameScene in background for body parenting.
        /// </summary>
        private void LoadGameSceneInBackground()
        {
            GameLogger.EnterFunction(nameof(LoadGameSceneInBackground));

            try
            {
                // Load GameScene as PackedScene
                var gameScene = GD.Load<PackedScene>("res://Scenes/GameScene.tscn");
                if (gameScene == null)
                {
                    GameLogger.Error("Failed to load GameScene PackedScene");
                    return;
                }

                // Instantiate but don't add to scene tree yet
                _gameSceneInstance = gameScene.Instantiate<Node3D>();

                // Get reference to GameScene's system_container
                _gameSystemContainer = _gameSceneInstance.GetNode<Node>("system_container");

                if (_gameSystemContainer == null)
                {
                    GameLogger.Error("GameScene's system_container not found");
                    _gameSceneInstance = null;
                    return;
                }

                // Create a temporary container in the LoadingScreen's tree for generation.
                // Bodies need to be in the scene tree for GlobalPosition to work.
                // We can't add GameScene to the tree yet because its _Ready() has side effects.
                _generationContainer = new Node3D();
                _generationContainer.Name = "GenerationContainer";
                AddChild(_generationContainer);

                GameLogger.Info("GameScene loaded in background, system_container found");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to load GameScene in background: {ex.Message}");
                _gameSceneInstance = null;
                _gameSystemContainer = null;
            }

            GameLogger.ExitFunction(nameof(LoadGameSceneInBackground));
        }

        /// <summary>
        /// Updates UI with template information.
        /// </summary>
        private void UpdateTemplateInfo()
        {
            if (_templateLabel != null)
                _templateLabel.Text = _selectedTemplate ?? "Unknown";

            if (_totalBodiesLabel != null)
                _totalBodiesLabel.Text = _totalBodies.ToString();

            UpdateBodiesHeader();
        }

        /// <summary>
        /// Creates initial body status entries for all bodies in the template.
        /// </summary>
        private void CreateInitialBodyStatuses(SystemTemplateData templateData)
        {
            _bodyStatuses.Clear();

            // Add dominant bodies
            foreach (var body in templateData.Dominant)
            {
                var name = body.GetValueOrDefault("name", Variant.From(string.Empty)).AsString();
                if (string.IsNullOrEmpty(name))
                    continue;

                var status = new BodyStatus
                {
                    Name = name,
                    BodyType = "Dominant",
                    Stage = "Waiting",
                    StartTime = (float)Time.GetTicksMsec() / 1000f,
                };
                _bodyStatuses[name] = status;
                CreateBodyItem(status);
            }

            // Add satellite belt bodies (indented, grouped by belt)
            foreach (var belt in templateData.Belts)
            {
                if (
                    belt.ContainsKey("satellite_names")
                    && belt["satellite_names"].Obj
                        is Godot.Collections.Array<string> satelliteNames
                )
                {
                    foreach (var satName in satelliteNames)
                    {
                        var status = new BodyStatus
                        {
                            Name = satName,
                            BodyType = "Belt Satellite",
                            Stage = "Waiting",
                            StartTime = (float)Time.GetTicksMsec() / 1000f,
                            IndentLevel = 1,
                        };
                        _bodyStatuses[satName] = status;
                        CreateBodyItem(status);
                    }
                }
            }

            // Add planetary bodies and their satellites
            foreach (var body in templateData.Planetary)
            {
                var name = body.GetValueOrDefault("name", Variant.From(string.Empty)).AsString();
                if (string.IsNullOrEmpty(name))
                    continue;

                var status = new BodyStatus
                {
                    Name = name,
                    BodyType = "Planetary",
                    Stage = "Waiting",
                    StartTime = (float)Time.GetTicksMsec() / 1000f,
                };
                _bodyStatuses[name] = status;
                CreateBodyItem(status);

                // Add satellites indented under their parent
                if (
                    body.ContainsKey("satellites")
                    && body["satellites"].Obj is Godot.Collections.Array satellites
                )
                {
                    foreach (Godot.Collections.Dictionary sat in satellites)
                    {
                        var satName = sat
                            .GetValueOrDefault("name", Variant.From(string.Empty))
                            .AsString();
                        if (string.IsNullOrEmpty(satName))
                            continue;

                        var satStatus = new BodyStatus
                        {
                            Name = satName,
                            BodyType = "Satellite",
                            Stage = "Waiting",
                            StartTime = (float)Time.GetTicksMsec() / 1000f,
                            IndentLevel = 1,
                        };
                        _bodyStatuses[satName] = satStatus;
                        CreateBodyItem(satStatus);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a UI item for a body and adds it to the list.
        /// </summary>
        private void CreateBodyItem(BodyStatus status)
        {
            if (_bodyItemScene == null || _bodyListContainer == null)
            {
                GD.PrintErr("Body item scene or container not set");
                GameLogger.Warning("Body item scene or container not set");
                return;
            }

            GD.Print(
                $"Creating body item for {status.Name}. Values:\n{status.BodyType}\n{status.Stage}\n{status.IsComplete}\n{status.StartTime}\n{status.CompletionTime}"
            );

            var bodyItemInstance = (LoadingBodyItem)_bodyItemScene.Instantiate<LoadingBodyItem>();
            if (bodyItemInstance == null)
            {
                GD.PrintErr("Failed to instantiate body item");
                GameLogger.Error("Failed to instantiate body item");
                return;
            }

            _bodyListContainer.AddChild(bodyItemInstance);
            bodyItemInstance.BodyName = status.Name;
            bodyItemInstance.Stage = status.Stage;
            bodyItemInstance.IsComplete = status.IsComplete;
            bodyItemInstance.IndentLevel = status.IndentLevel;

            _bodyItems[status.Name] = bodyItemInstance;

            GameLogger.Info($"Created body item for: {status.Name}");
        }

        /// <summary>
        /// Updates the time elapsed display.
        /// </summary>
        private void UpdateTimeElapsed(float elapsedSeconds)
        {
            if (_timeElapsedLabel != null)
                _timeElapsedLabel.Text = $"{elapsedSeconds:F1}s";

            UpdateBodiesHeader();
        }

        /// <summary>
        /// Updates the bodies header with completion count and time.
        /// </summary>
        private void UpdateBodiesHeader()
        {
            if (_bodiesHeaderLabel != null)
            {
                float elapsed = (float)Time.GetTicksMsec() / 1000f - _startTime;
                _bodiesHeaderLabel.Text =
                    $"Orbital Bodies ({_completedBodies}/{_totalBodies}) {elapsed:F1}s";
            }
        }

        /// <summary>
        /// Updates the progress bar and text.
        /// </summary>
        private void UpdateProgress(float progress)
        {
            if (_progressBar != null)
            {
                _progressBar.Value = progress * 100f;
            }

            if (_progressText != null)
            {
                _progressText.Text = $"{progress * 100f:F0}%";
            }
        }

        /// <summary>
        /// Updates a body's status in the UI.
        /// </summary>
        private void UpdateBodyStatus(
            string bodyName,
            string stage,
            bool isComplete = false,
            float? generationTime = null
        )
        {
            if (_bodyStatuses.TryGetValue(bodyName, out var status))
            {
                status.Stage = stage;
                status.IsComplete = isComplete;

                if (isComplete && generationTime.HasValue)
                {
                    status.CompletionTime = (float)Time.GetTicksMsec() / 1000f;
                }

                if (_bodyItems.TryGetValue(bodyName, out var bodyItem))
                {
                    bodyItem.Stage = stage;
                    bodyItem.IsComplete = isComplete;
                    if (isComplete && generationTime.HasValue)
                    {
                        bodyItem.GenerationTime = generationTime.Value;
                    }
                }

                if (isComplete)
                {
                    _completedBodies++;
                    UpdateBodiesHeader();

                    // Update overall progress
                    if (_totalBodies > 0)
                    {
                        float progress = (float)_completedBodies / _totalBodies;
                        UpdateProgress(progress);
                    }
                }
            }
            else
            {
                GameLogger.Warning($"Body '{bodyName}' not found in status tracking");
            }
        }

        /// <summary>
        /// Starts system generation using the loaded template data.
        /// </summary>
        private void StartSystemGeneration(SystemTemplateData templateData)
        {
            GameLogger.EnterFunction(nameof(StartSystemGeneration));

            // Find the SystemGenerator node in our scene
            var systemGenerator =
                GetNodeOrNull<ProceduralGeneration.PlanetGeneration.SystemGenerator>(
                    "system_generator"
                );
            if (systemGenerator == null)
            {
                GameLogger.Error("SystemGenerator not found in LoadingScreen scene");
                ShowErrorAndReturn("System generation component not found");
                return;
            }

            // Configure SystemGenerator to use the in-tree generation container.
            // Bodies must be in the scene tree for GlobalPosition to work during generation.
            if (_generationContainer != null)
            {
                systemGenerator.TargetContainer = _generationContainer;
                GameLogger.Info(
                    "Configured SystemGenerator.TargetContainer to in-tree GenerationContainer"
                );
            }
            else
            {
                GameLogger.Warning(
                    "GenerationContainer not available, using default SystemContainer"
                );
            }

            // Compute barycenter from dominant body positions and masses
            var barycenter = ComputeBarycenter(templateData.Dominant);
            AssignSystemIdentity(barycenter, _selectedTemplate);

            // Trigger generation through SignalBus (SystemGenerator listens to this)
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.EmitGenerateSystemRequested(
                    templateData.Dominant,
                    templateData.Belts,
                    templateData.Planetary,
                    barycenter
                );
                GameLogger.Info("System generation triggered");
            }
            else
            {
                GameLogger.Error("SignalBus.Instance is null - cannot trigger generation");
                ShowErrorAndReturn("Cannot trigger system generation");
            }

            GameLogger.ExitFunction(nameof(StartSystemGeneration));
        }

        /// <summary>
        /// Computes barycenter from dominant body template data.
        /// </summary>
        private static Barycenter ComputeBarycenter(
            Godot.Collections.Array<Godot.Collections.Dictionary> dominantBodies
        )
        {
            float totalMass = 0f;
            var weightedPosition = Vector3.Zero;

            foreach (var body in dominantBodies)
            {
                if (!body.ContainsKey("template"))
                    continue;

                var template = (Godot.Collections.Dictionary)body["template"];
                float mass = template.ContainsKey("mass") ? (float)template["mass"] : 0f;
                var position = template.ContainsKey("position")
                    ? (Vector3)template["position"]
                    : Vector3.Zero;

                weightedPosition += position * mass;
                totalMass += mass;
            }

            if (totalMass > 0f)
            {
                weightedPosition /= totalMass;
                float averageMass = totalMass / dominantBodies.Count;
                return new Barycenter(weightedPosition, Vector3.Zero, averageMass);
            }

            return new Barycenter(Vector3.Zero, Vector3.Zero, 0f);
        }

        /// <summary>
        /// Stamps the barycenter with a system name and sector identifier derived
        /// from the loaded template name. Deterministic per template so the same
        /// system always shows the same designation.
        /// </summary>
        private static void AssignSystemIdentity(Barycenter barycenter, string? templateName)
        {
            string baseName = string.IsNullOrEmpty(templateName) ? "system" : templateName;
            int seed = StableHash(baseName);

            barycenter.SystemName = !string.IsNullOrEmpty(templateName)
                ? templateName!
                : $"System-{seed % 9999:D4}";

            const string roman = "I,II,III,IV,V,VI,VII,VIII,IX,X,XI,XII";
            string[] romanNumerals = roman.Split(',');
            barycenter.SectorId =
                $"KP-{seed % 100:D2} · Sector {romanNumerals[seed % romanNumerals.Length]}";
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in value)
                    hash = hash * 31 + c;
                return Math.Abs(hash);
            }
        }

        /// <summary>
        /// Shows an error message and returns to main menu.
        /// </summary>
        private void ShowErrorAndReturn(string errorMessage)
        {
            GameLogger.Error($"Loading screen error: {errorMessage}");
            GD.Print($"Loading screen error: {errorMessage}");

            // TODO: Show error UI
            // For now, just return to main menu after a delay
            var timer = new Timer();
            timer.WaitTime = 3.0;
            timer.OneShot = true;
            timer.Timeout += () =>
            {
                GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
            };
            AddChild(timer);
            timer.Start();
        }

        /// <summary>
        /// Transitions to the game scene after loading is complete.
        /// </summary>
        private void TransitionToGameScene()
        {
            GameLogger.Info("Loading complete, transitioning to GameScene");

            // Clear the selected template from SignalBus
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.SelectedTemplate = null;
            }

            // Save reference to generated bodies for GameScene to use
            SaveGeneratedSystem();

            // Manual scene swapping: add GameScene to root, remove LoadingScreen
            if (_gameSceneInstance != null)
            {
                PerformManualSceneSwap();
            }
            else
            {
                // Fallback: use traditional scene transition
                GameLogger.Warning(
                    "GameScene instance not available, using ChangeSceneToFile fallback"
                );
                GetTree().ChangeSceneToFile("res://Scenes/GameScene.tscn");
            }
        }

        /// <summary>
        /// Performs manual scene swap: adds GameScene to root, removes LoadingScreen.
        /// Based on pattern from DataLoadingScene.
        /// </summary>
        private void PerformManualSceneSwap()
        {
            GameLogger.EnterFunction(nameof(PerformManualSceneSwap));

            try
            {
                var tree = GetTree();

                // Reparent generated bodies from the in-tree container to GameScene's system_container
                if (_generationContainer != null && _gameSystemContainer != null)
                {
                    var children = _generationContainer.GetChildren();
                    foreach (var child in children)
                    {
                        _generationContainer.RemoveChild(child);
                        _gameSystemContainer.AddChild(child);
                    }
                    GameLogger.Info(
                        $"Reparented {children.Count} nodes to GameScene's system_container"
                    );
                }

                // Add GameScene to root
                tree.Root.AddChild(_gameSceneInstance!);

                // Remove LoadingScreen from tree
                tree.CurrentScene?.QueueFree();

                // Update current scene reference
                tree.CurrentScene = _gameSceneInstance;

                GameLogger.Info("Manual scene swap completed successfully");
            }
            catch (Exception ex)
            {
                GameLogger.Error($"Failed to perform manual scene swap: {ex.Message}");

                // Try fallback
                GameLogger.Info("Attempting ChangeSceneToFile fallback");
                GetTree().ChangeSceneToFile("res://Scenes/GameScene.tscn");
            }

            GameLogger.ExitFunction(nameof(PerformManualSceneSwap));
        }

        /// <summary>
        /// Saves references to the generated system so GameScene can use it.
        /// Updated to handle both TargetContainer and default SystemContainer.
        /// </summary>
        private void SaveGeneratedSystem()
        {
            GameLogger.EnterFunction(nameof(SaveGeneratedSystem));

            // Find SystemGenerator
            var systemGenerator =
                GetNodeOrNull<ProceduralGeneration.PlanetGeneration.SystemGenerator>(
                    "system_generator"
                );
            if (systemGenerator == null)
            {
                GameLogger.Warning("SystemGenerator not found when saving generated system");
                GameLogger.ExitFunction(nameof(SaveGeneratedSystem));
                return;
            }

            // Check where bodies were actually added
            Node? actualContainer = _generationContainer;
            int bodyCount = _generationContainer?.GetChildCount() ?? 0;

            if (bodyCount > 0)
            {
                GameLogger.Info($"Bodies generated into GenerationContainer: {bodyCount} bodies");
            }
            else
            {
                GameLogger.Warning("GenerationContainer is empty - no bodies were generated");
            }

            // Store information about generated bodies in SignalBus or global state
            // For now, we'll just mark that generation happened
            // GameScene will need to handle this
            if (actualContainer != null)
            {
                GameLogger.Info(
                    $"Generated system saved with {bodyCount} bodies in {actualContainer.Name}"
                );
            }

            GameLogger.ExitFunction(nameof(SaveGeneratedSystem));
        }

        /// <summary>
        /// Converts a PascalCase step name into a human-readable display string.
        /// E.g., "GenerateVoronoiCells" -> "Generate Voronoi Cells"
        /// </summary>
        private static string FormatStepName(string stepName)
        {
            if (string.IsNullOrEmpty(stepName))
                return string.Empty;

            var sb = new System.Text.StringBuilder(stepName.Length + 8);
            sb.Append(stepName[0]);

            for (int i = 1; i < stepName.Length; i++)
            {
                if (char.IsUpper(stepName[i]) && !char.IsUpper(stepName[i - 1]))
                {
                    sb.Append(' ');
                }
                else if (
                    char.IsUpper(stepName[i])
                    && i + 1 < stepName.Length
                    && char.IsUpper(stepName[i - 1])
                    && !char.IsUpper(stepName[i + 1])
                )
                {
                    sb.Append(' ');
                }
                sb.Append(stepName[i]);
            }

            return sb.ToString();
        }

        #region Signal Handlers

        /// <summary>
        /// Called when a timer starts (body generation begins).
        /// </summary>
        private void OnStartTimer(string name, int totalSteps, int startingStep, string[] stepNames)
        {
            CallDeferred(nameof(HandleStartTimer), name, totalSteps, startingStep, stepNames);
        }

        private void HandleStartTimer(
            string name,
            int totalSteps,
            int startingStep,
            string[] stepNames
        )
        {
            GameLogger.Debug($"Timer started for: {name} (steps: {totalSteps})");

            if (_bodyStatuses.TryGetValue(name, out var status))
            {
                status.StepNames = stepNames;
                status.CurrentStepIndex = startingStep;

                string initialStage =
                    startingStep < stepNames.Length
                        ? FormatStepName(stepNames[startingStep])
                        : "Starting generation...";

                UpdateBodyStatus(name, initialStage);
            }
        }

        /// <summary>
        /// Called when a timer step increments (body generation progresses).
        /// </summary>
        private void OnIncrementTimerStep(string name)
        {
            CallDeferred(nameof(HandleIncrementTimerStep), name);
        }

        private void HandleIncrementTimerStep(string name)
        {
            GameLogger.Debug($"Timer step incremented for: {name}");

            if (_bodyStatuses.TryGetValue(name, out var status))
            {
                status.CurrentStepIndex++;

                string stage;
                if (status.CurrentStepIndex < status.StepNames.Length)
                {
                    stage = FormatStepName(status.StepNames[status.CurrentStepIndex]);
                }
                else
                {
                    stage = "Finalizing...";
                }

                UpdateBodyStatus(name, stage);
            }
        }

        /// <summary>
        /// Called when a timer stops (body generation completes).
        /// </summary>
        private void OnStopTimer(string name)
        {
            CallDeferred(nameof(HandleStopTimer), name);
        }

        private void HandleStopTimer(string name)
        {
            GameLogger.Debug($"Timer stopped for: {name}");

            // Mark body as complete
            if (_bodyStatuses.TryGetValue(name, out var status))
            {
                float generationTime = (float)Time.GetTicksMsec() / 1000f - status.StartTime;
                UpdateBodyStatus(name, "Completed", true, generationTime);
            }
        }

        /// <summary>
        /// Called when system generation is complete.
        /// </summary>
        private void OnSystemGenerationComplete(
            string batchId,
            int totalBodies,
            int successfulBodies
        )
        {
            CallDeferred(
                nameof(HandleSystemGenerationComplete),
                batchId,
                totalBodies,
                successfulBodies
            );
        }

        private void HandleSystemGenerationComplete(
            string batchId,
            int totalBodies,
            int successfulBodies
        )
        {
            GameLogger.Info(
                $"System generation complete: {successfulBodies}/{totalBodies} bodies successful"
            );

            // Update progress to 100%
            UpdateProgress(1.0f);

            // Update loading label
            if (_loadingLabel != null)
                _loadingLabel.Text = "LOADING COMPLETE";

            // Transition to game scene after a short delay
            var timer = new Timer();
            timer.WaitTime = 1.5; // 1.5 second delay to show completion
            timer.OneShot = true;
            timer.Timeout += TransitionToGameScene;
            AddChild(timer);
            timer.Start();
        }

        #endregion

        /// <summary>
        /// Clean up signal connections when node exits tree.
        /// </summary>
        public override void _ExitTree()
        {
            if (SignalBus.Instance != null)
            {
                SignalBus.Instance.Disconnect(
                    SignalBus.SignalName.StartTimer,
                    new Callable(this, nameof(OnStartTimer))
                );
                SignalBus.Instance.Disconnect(
                    SignalBus.SignalName.IncrementTimerStep,
                    new Callable(this, nameof(OnIncrementTimerStep))
                );
                SignalBus.Instance.Disconnect(
                    SignalBus.SignalName.StopTimer,
                    new Callable(this, nameof(OnStopTimer))
                );
                SignalBus.Instance.Disconnect(
                    SignalBus.SignalName.SystemGenerationComplete,
                    new Callable(this, nameof(OnSystemGenerationComplete))
                );
            }

            base._ExitTree();
        }
    }
}
