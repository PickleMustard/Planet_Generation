using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using UtilityLibrary.TaskSystem;
#if DEBUG
using Debug;
#endif

namespace Logistics.Resources
{
#if DEBUG
    [DebugData("Ships", Category = "Game")]
#endif
    public partial class ShipDatabase : ILoadableDatabase
    {
        private static ShipDatabase? _instance;
        private static readonly object _lock = new object();
        public static ShipDatabase Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new ShipDatabase();
                    }
                    return _instance;
                }
            }
        }

        private Dictionary<string, ShipDefinition> _ships = new();

        public string DatabaseName => "ShipDatabase";
        public bool IsLoaded { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f;

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private ShipDatabase()
        {
        }

        public void LoadData()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            GD.Print($"ShipDatabase: Starting load of '{DatabaseName}'");

            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                // Step 1: Begin loading
                LoadProgress = 0.25f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                // Step 2: Load all ship definitions via ShipConfigLoader
                LoadProgress = 0.5f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                var allShips = ShipConfigLoader.LoadAllShips();

                _ships.Clear();
                foreach (var ship in allShips)
                {
                    if (string.IsNullOrEmpty(ship.Name))
                    {
                        GD.PrintErr("Ship definition missing 'name' field");
                        continue;
                    }

                    if (_ships.ContainsKey(ship.Name))
                    {
                        GD.PrintErr($"Duplicate ship name '{ship.Name}'");
                        continue;
                    }

                    _ships[ship.Name] = ship;
                }

                // Step 3: Finalize
                LoadProgress = 0.75f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                if (_ships.Count == 0)
                {
                    GD.Print(
                        $"ShipDatabase: '{DatabaseName}' loaded but contains no ship definitions"
                    );
                }

                // Step 4: Complete
                LoadProgress = 1.0f;
                IsLoaded = true;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                OnLoadCompleted?.Invoke(DatabaseName, true);

                GameLogger.Info($"ShipDatabase: '{DatabaseName}' loaded successfully with {_ships.Count} ships, " +
                                $"{ShipConfigLoader.ModelsLoadedCount} models loaded, " +
                                $"{ShipConfigLoader.ModelsFailedCount} models failed");
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GD.PrintErr($"ShipDatabase: '{DatabaseName}' failed to load: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            _ships.Clear();
            IsLoaded = false;
            LoadProgress = 0f;
            GD.Print($"ShipDatabase: '{DatabaseName}' unloaded");
        }

        public WorkPackage CreateLoadPackage()
        {
            var builder = new WorkPackageBuilder()
                .WithName($"Load_{DatabaseName}")
                .WithPriority(TaskPriority.Normal)
                .AddStep(
                    $"Initialize_Ship_Loading",
                    () =>
                    {
                        LoadProgress = 0.25f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Parse_Ship_Templates",
                    () =>
                    {
                        LoadProgress = 0.5f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Process_Ship_Definitions",
                    () =>
                    {
                        LoadProgress = 0.75f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Finalize_Ship_Database",
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

        public bool TryGetShip(string name, out ShipDefinition? ship)
        {
            EnsureLoaded();
            return _ships.TryGetValue(name, out ship);
        }

        public IReadOnlyDictionary<string, ShipDefinition> GetAllShips()
        {
            EnsureLoaded();
            return _ships;
        }

        public List<ShipDefinition> GetShipsByCategory(string engineCategory)
        {
            EnsureLoaded();
            var result = new List<ShipDefinition>();

            foreach (var ship in _ships.Values)
            {
                if (ship.EngineCategory?.Equals(engineCategory, StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.Add(ship);
                }
            }

            return result;
        }

        public bool ValidateShipExists(string name)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(name) && _ships.ContainsKey(name);
        }

        /// <summary>
        /// Gets the icon for a ship at a specific size.
        /// Always returns a valid texture (uses fallback if needed).
        /// </summary>
        public Texture2D GetShipIcon(string name, IconSize size = IconSize.Large)
        {
            EnsureLoaded();
            if (TryGetShip(name, out var ship) && ship != null)
            {
                var texture = ship.Icon?.GetTexture(size);
                if (texture != null)
                    return texture;
            }

            return IconDataLoader.GetFallbackIcon(size);
        }

        /// <summary>
        /// Gets the full IconDefinition for a ship.
        /// </summary>
        public IconDefinition? GetShipIconDefinition(string name)
        {
            EnsureLoaded();
            if (TryGetShip(name, out var ship) && ship != null)
            {
                return ship.Icon;
            }
            return null;
        }
    }
}
