using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Structures.Enums;
using Structures.GameState;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using UI.Debug;
#endif

namespace Structures.Resources
{
#if DEBUG
    [DebugData("Buildings", Category = "Game")]
#endif
    public partial class BuildingDatabase : ILoadableDatabase
    {
        private static BuildingDatabase? _instance;
        private static readonly object _lock = new object();
        public static BuildingDatabase Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new BuildingDatabase();
                    }
                    return _instance;
                }
            }
        }

        private Dictionary<string, BuildingDefinition> _buildings = new();

        public string DatabaseName => "BuildingDatabase";
        public bool IsLoaded { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f;

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private BuildingDatabase()
        {
            // Private constructor for singleton pattern
            // Database must be loaded via LoadData() or CreateLoadPackage()
        }

        public void LoadData()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            GD.Print($"BuildingDatabase: Starting load of '{DatabaseName}'");

            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                // Step 1: Scan Configuration/Buildings/ directory recursively
                LoadProgress = 0.25f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                string basePath = "res://Configuration/Buildings/";
                if (!DirAccess.DirExistsAbsolute(basePath))
                {
                    throw new DatabaseLoadFailedException(
                        DatabaseName,
                        $"Building configuration directory not found: {basePath}"
                    );
                }

                var buildingFiles = GetYamlFilesRecursive(basePath);
                if (buildingFiles.Count == 0)
                {
                    GD.Print($"BuildingDatabase: No YAML files found in {basePath}");
                }

                // Step 2: Parse each YAML file with BuildingConfigLoader
                LoadProgress = 0.5f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                _buildings.Clear();
                foreach (var filePath in buildingFiles)
                {
                    try
                    {
                        var definitions = BuildingConfigLoader.LoadBuildingDefinitions(filePath);

                        foreach (var definition in definitions)
                        {
                            if (string.IsNullOrEmpty(definition.IdName))
                            {
                                GD.PrintErr(
                                    $"Building definition missing 'id_name' field in {filePath}"
                                );
                                continue;
                            }

                            if (_buildings.ContainsKey(definition.IdName))
                            {
                                GD.PrintErr(
                                    $"Duplicate building id_name '{definition.IdName}' in {filePath}"
                                );
                                continue;
                            }

                            _buildings[definition.IdName] = definition;
                        }
                    }
                    catch (Exception e)
                    {
                        GD.PrintErr(
                            $"Error loading building definitions from {filePath}: {e.Message}"
                        );
                        // Continue loading other files, don't fail the entire database
                    }
                }

                // Step 3: Process definitions and populate dictionary
                LoadProgress = 0.75f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                // Additional processing if needed (e.g., validate building definitions)
                // For now, just ensure we have at least one building definition
                if (_buildings.Count == 0)
                {
                    GD.Print(
                        $"BuildingDatabase: '{DatabaseName}' loaded but contains no building definitions"
                    );
                }

                // Step 4: Finalize database
                LoadProgress = 1.0f;
                IsLoaded = true;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                OnLoadCompleted?.Invoke(DatabaseName, true);

                GameLogger.Info($"BuildingDatabase: '{DatabaseName}' loaded successfully with {_buildings.Count} buildings, " +
                                $"{BuildingConfigLoader.ModelsLoadedCount} models loaded, " +
                                $"{BuildingConfigLoader.ModelsFailedCount} models failed");
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GD.PrintErr($"BuildingDatabase: '{DatabaseName}' failed to load: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            _buildings.Clear();
            IsLoaded = false;
            LoadProgress = 0f;
            GD.Print($"BuildingDatabase: '{DatabaseName}' unloaded");
        }

        public WorkPackage CreateLoadPackage()
        {
            var builder = new WorkPackageBuilder()
                .WithName($"Load_{DatabaseName}")
                .WithPriority(TaskPriority.Normal)
                .AddStep(
                    $"Scan_Buildings_Directory",
                    () =>
                    {
                        // Step 1: Directory scanning
                        LoadProgress = 0.25f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Parse_Building_YAML_Files",
                    () =>
                    {
                        // Step 2: Parse YAML files
                        LoadProgress = 0.5f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Process_Building_Definitions",
                    () =>
                    {
                        // Step 3: Process definitions
                        LoadProgress = 0.75f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Finalize_Building_Database",
                    () =>
                    {
                        // Execute the full load process
                        LoadData();
                        return 0;
                    }
                );

            return builder.Build();
        }

        private void EnsureLoaded()
        {
            if (!IsLoaded)
            {
                throw new DatabaseNotLoadedException(DatabaseName);
            }
        }

        private List<string> GetYamlFilesRecursive(string directory)
        {
            var files = new List<string>();

            if (!DirAccess.DirExistsAbsolute(directory))
                return files;

            // Get files in current directory
            var currentFiles = DirAccess.GetFilesAt(directory);
            foreach (var file in currentFiles)
            {
                if (file.EndsWith(".yaml"))
                {
                    files.Add(directory + file);
                }
            }

            // Get subdirectories
            var subdirs = DirAccess.GetDirectoriesAt(directory);
            foreach (var subdir in subdirs)
            {
                files.AddRange(GetYamlFilesRecursive(directory + subdir + "/"));
            }

            return files;
        }

        public bool TryGetBuilding(string buildingId, out BuildingDefinition? building)
        {
            EnsureLoaded();
            return _buildings.TryGetValue(buildingId, out building);
        }

        public IReadOnlyDictionary<string, BuildingDefinition> GetAllBuildings()
        {
            EnsureLoaded();
            return _buildings;
        }

        public List<BuildingDefinition> GetBuildingsByCategory(string category)
        {
            EnsureLoaded();
            var result = new List<BuildingDefinition>();

            foreach (var building in _buildings.Values)
            {
                if (building.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.Add(building);
                }
            }

            return result;
        }

        public bool ValidateBuildingExists(string buildingId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(buildingId) && _buildings.ContainsKey(buildingId);
        }

        public bool ValidatePlacement(string buildingId, VoronoiCell? cell)
        {
            EnsureLoaded();
            if (!TryGetBuilding(buildingId, out var building) || building == null)
                return false;

            return building.ValidatePlacement(cell);
        }

        // Count-based tracking for buildings with placement limits
        private readonly Dictionary<string, int> _globalPlacementCounts = new();

        /// <summary>
        /// Checks if a building with a global limit has already been placed.
        /// </summary>
        public bool IsGloballyPlaced(string buildingId) =>
            _globalPlacementCounts.TryGetValue(buildingId, out int count) && count > 0;

        /// <summary>
        /// Returns the current placement count for a building type.
        /// </summary>
        public int GetGlobalPlacementCount(string buildingId) =>
            _globalPlacementCounts.TryGetValue(buildingId, out int count) ? count : 0;

        /// <summary>
        /// Increments the global placement count for a building type.
        /// </summary>
        public void MarkGloballyPlaced(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
                return;

            _globalPlacementCounts[buildingId] = GetGlobalPlacementCount(buildingId) + 1;
        }

        /// <summary>
        /// Decrements the global placement count for a building type.
        /// </summary>
        public void DecrementGlobalPlacement(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId))
                return;

            int current = GetGlobalPlacementCount(buildingId);
            if (current > 0)
                _globalPlacementCounts[buildingId] = current - 1;
        }

        /// <summary>
        /// Resets global placement tracking (for new game).
        /// </summary>
        public void ResetGlobalPlacements() => _globalPlacementCounts.Clear();

        /// <summary>
        /// Validates if placement is allowed considering global limits.
        /// </summary>
        public bool ValidateGlobalPlacement(string buildingId)
        {
            if (!TryGetBuilding(buildingId, out var def) || def == null)
                return false;

            if (def.BuildingLimit > 0 && GetGlobalPlacementCount(buildingId) >= def.BuildingLimit)
                return false;

            return true;
        }

        /// <summary>
        /// Gets the icon for a building at a specific size.
        /// Always returns a valid texture (uses fallback if needed).
        /// </summary>
        public Texture2D GetBuildingIcon(string buildingId, IconSize size = IconSize.Medium)
        {
            EnsureLoaded();
            if (TryGetBuilding(buildingId, out var building) && building != null)
            {
                var texture = building.Icon?.GetTexture(size);
                if (texture != null)
                    return texture;
            }

            return IconDataLoader.GetFallbackIcon(size);
        }

        /// <summary>
        /// Gets the full IconDefinition for a building.
        /// </summary>
        public IconDefinition? GetBuildingIconDefinition(string buildingId)
        {
            EnsureLoaded();
            if (TryGetBuilding(buildingId, out var building) && building != null)
            {
                return building.Icon;
            }
            return null;
        }
    }
}
