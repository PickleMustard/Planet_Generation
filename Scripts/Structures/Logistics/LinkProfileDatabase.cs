using System;
using System.Collections.Generic;
using Godot;
using Structures.Logistics;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using Debug;
#endif

namespace Structures.Resources
{
#if DEBUG
    [DebugData("LinkProfiles", Category = "Game")]
#endif
    public partial class LinkProfileDatabase : ILoadableDatabase
    {
        private static LinkProfileDatabase? _instance;
        private static readonly object _lock = new object();
        public static LinkProfileDatabase Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new LinkProfileDatabase();
                    }
                    return _instance;
                }
            }
        }

        private Dictionary<string, LinkProfile> _profiles = new();

        public string DatabaseName => "LinkProfileDatabase";
        public bool IsLoaded { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f;

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private LinkProfileDatabase()
        {
            // Private constructor for singleton pattern
            // Database must be loaded via LoadData() or CreateLoadPackage()
        }

        public void LoadData()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            GameLogger.Info($"LinkProfileDatabase: Starting load of '{DatabaseName}'");

            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                // Step 1: Parse YAML configuration
                LoadProgress = 0.3f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                string filePath = "res://Configuration/LinkProfiles/link_profiles.yaml";
                var definitions = LinkConfigLoader.LoadLinkProfiles(filePath);

                // Step 2: Process definitions and populate dictionary
                LoadProgress = 0.6f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                _profiles.Clear();
                foreach (var definition in definitions)
                {
                    if (string.IsNullOrEmpty(definition.IdName))
                    {
                        GameLogger.Error("LinkProfileDatabase: Link profile definition missing 'id_name' field");
                        continue;
                    }

                    if (_profiles.ContainsKey(definition.IdName))
                    {
                        GameLogger.Error($"LinkProfileDatabase: Duplicate link profile id_name '{definition.IdName}'");
                        continue;
                    }

                    _profiles[definition.IdName] = definition;
                }

                // Step 3: Finalize database
                LoadProgress = 1.0f;
                IsLoaded = true;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                OnLoadCompleted?.Invoke(DatabaseName, true);

                GameLogger.Info($"LinkProfileDatabase: '{DatabaseName}' loaded successfully with {_profiles.Count} profiles");
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GameLogger.Error($"LinkProfileDatabase: '{DatabaseName}' failed to load: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            _profiles.Clear();
            IsLoaded = false;
            LoadProgress = 0f;
            GameLogger.Info($"LinkProfileDatabase: '{DatabaseName}' unloaded");
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

        public bool TryGetProfile(string id, out LinkProfile? profile)
        {
            EnsureLoaded();
            return _profiles.TryGetValue(id, out profile);
        }

        public IReadOnlyDictionary<string, LinkProfile> GetAllProfiles()
        {
            EnsureLoaded();
            return _profiles;
        }

        public bool ValidateProfileExists(string id)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(id) && _profiles.ContainsKey(id);
        }
    }
}
