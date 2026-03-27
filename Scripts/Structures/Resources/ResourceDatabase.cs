using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using UI.Debug;
#endif

namespace Structures.Resources
{
#if DEBUG
    [DebugData("IngameResources", Category = "Game")]
#endif
    public partial class ResourceDatabase : ILoadableDatabase
    {
        private static ResourceDatabase? _instance;
        private static readonly object _lock = new object();
        public static ResourceDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ResourceDatabase();
                }
                return _instance;
            }
        }

        private Dictionary<string, ResourceDefinition> _resources = new();

        public string DatabaseName => "ResourceDatabase";
        public bool IsLoaded { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f;

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private ResourceDatabase()
        {
            // Private constructor for singleton pattern
            // Database must be loaded via LoadAsync() or CreateLoadPackage()
        }

        public void LoadData()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            GD.Print($"ResourceDatabase: Starting load of '{DatabaseName}'");

            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                // Step 1: Parse YAML configuration
                LoadProgress = 0.3f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                string configPath =
                    "res://Configuration/ResourceDefinition/ResourceDefinition.yaml";
                var definitions = ResourceConfigLoader.LoadResourceDefinitions(configPath);

                // Step 2: Process definitions and populate dictionary
                LoadProgress = 0.6f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                _resources.Clear();
                foreach (var definition in definitions)
                {
                    if (string.IsNullOrEmpty(definition.IdName))
                    {
                        GD.PrintErr("Resource definition missing 'id_name' field");
                        continue;
                    }

                    if (_resources.ContainsKey(definition.IdName))
                    {
                        throw ResourceValidationError.DuplicateResource(definition.IdName);
                    }

                    _resources[definition.IdName] = definition;
                }

                // Step 3: Finalize database
                LoadProgress = 1.0f;
                IsLoaded = true;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                OnLoadCompleted?.Invoke(DatabaseName, true);

                GD.Print(
                    $"ResourceDatabase: '{DatabaseName}' loaded successfully with {_resources.Count} resources"
                );
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GD.PrintErr($"ResourceDatabase: '{DatabaseName}' failed to load: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            _resources.Clear();
            IsLoaded = false;
            LoadProgress = 0f;
            GD.Print($"ResourceDatabase: '{DatabaseName}' unloaded");
        }

        public WorkPackage CreateLoadPackage()
        {
            var builder = new WorkPackageBuilder()
                .WithName($"Load_{DatabaseName}")
                .WithPriority(TaskPriority.Normal)
                .AddStep(
                    $"Load_Database_{DatabaseName}",
                    () =>
                    {
                        LoadData();
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

        public bool TryGetResource(string resourceId, out ResourceDefinition? resource)
        {
            EnsureLoaded();
            return _resources.TryGetValue(resourceId, out resource);
        }

        public IReadOnlyDictionary<string, ResourceDefinition> GetAllResources()
        {
            EnsureLoaded();
            return _resources;
        }

        public Color GetResourceColor(string resourceId)
        {
            EnsureLoaded();
            if (TryGetResource(resourceId, out var resource) && resource != null)
            {
                return resource.DisplayColor;
            }
            return Colors.White;
        }

        public bool ValidateResourceExists(string resourceId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(resourceId) && _resources.ContainsKey(resourceId);
        }

        public void ValidateAllBodyConfigResources(
            string bodyConfigName,
            IEnumerable<string> resourceIds
        )
        {
            EnsureLoaded();
            if (resourceIds == null)
            {
                return;
            }

            foreach (var resourceId in resourceIds)
            {
                if (!string.IsNullOrEmpty(resourceId) && !_resources.ContainsKey(resourceId))
                {
                    throw ResourceValidationError.ResourceNotFound(resourceId, bodyConfigName);
                }
            }
        }
    }
}
