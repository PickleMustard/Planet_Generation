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
using UI.Debug;
#endif

namespace Logistics.Resources
{
#if DEBUG
    [DebugData("Stations", Category = "Game")]
#endif
    public partial class StationDatabase : ILoadableDatabase
    {
        private static StationDatabase? _instance;
        private static readonly object _lock = new object();
        public static StationDatabase Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new StationDatabase();
                    }
                    return _instance;
                }
            }
        }

        private Dictionary<string, StationDefinition> _stations = new();

        public string DatabaseName => "StationDatabase";
        public bool IsLoaded { get; private set; } = false;
        public float LoadProgress { get; private set; } = 0f;

        public event Action<string>? OnLoadStarted;
        public event Action<string, bool>? OnLoadCompleted;
        public event Action<string, float>? OnLoadProgressChanged;

        private StationDatabase()
        {
        }

        public void LoadData()
        {
            OnLoadStarted?.Invoke(DatabaseName);
            GD.Print($"StationDatabase: Starting load of '{DatabaseName}'");

            IsLoaded = false;
            LoadProgress = 0f;

            try
            {
                // Step 1: Begin loading
                LoadProgress = 0.25f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                // Step 2: Load all station definitions via StationConfigLoader
                LoadProgress = 0.5f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                var allStations = StationConfigLoader.LoadAllStations();

                _stations.Clear();
                foreach (var station in allStations)
                {
                    if (string.IsNullOrEmpty(station.Name))
                    {
                        GD.PrintErr("Station definition missing 'name' field");
                        continue;
                    }

                    if (_stations.ContainsKey(station.Name))
                    {
                        GD.PrintErr($"Duplicate station name '{station.Name}'");
                        continue;
                    }

                    _stations[station.Name] = station;
                }

                // Step 3: Finalize
                LoadProgress = 0.75f;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);

                if (_stations.Count == 0)
                {
                    GD.Print(
                        $"StationDatabase: '{DatabaseName}' loaded but contains no station definitions"
                    );
                }

                // Step 4: Complete
                LoadProgress = 1.0f;
                IsLoaded = true;
                OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                OnLoadCompleted?.Invoke(DatabaseName, true);

                GameLogger.Info($"StationDatabase: '{DatabaseName}' loaded successfully with {_stations.Count} stations, " +
                                $"{StationConfigLoader.ModelsLoadedCount} models loaded, " +
                                $"{StationConfigLoader.ModelsFailedCount} models failed");
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                LoadProgress = 0f;
                OnLoadCompleted?.Invoke(DatabaseName, false);
                GD.PrintErr($"StationDatabase: '{DatabaseName}' failed to load: {ex.Message}");
                throw new DatabaseLoadFailedException(DatabaseName, ex.Message, ex);
            }
        }

        public void Unload()
        {
            _stations.Clear();
            IsLoaded = false;
            LoadProgress = 0f;
            GD.Print($"StationDatabase: '{DatabaseName}' unloaded");
        }

        public WorkPackage CreateLoadPackage()
        {
            var builder = new WorkPackageBuilder()
                .WithName($"Load_{DatabaseName}")
                .WithPriority(TaskPriority.Normal)
                .AddStep(
                    $"Initialize_Station_Loading",
                    () =>
                    {
                        LoadProgress = 0.25f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Parse_Station_Templates",
                    () =>
                    {
                        LoadProgress = 0.5f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Process_Station_Definitions",
                    () =>
                    {
                        LoadProgress = 0.75f;
                        OnLoadProgressChanged?.Invoke(DatabaseName, LoadProgress);
                        return 0;
                    }
                )
                .AddStep(
                    $"Finalize_Station_Database",
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

        public bool TryGetStation(string name, out StationDefinition? station)
        {
            EnsureLoaded();
            return _stations.TryGetValue(name, out station);
        }

        public IReadOnlyDictionary<string, StationDefinition> GetAllStations()
        {
            EnsureLoaded();
            return _stations;
        }

        public List<StationDefinition> GetStationsByCategory(string stationType)
        {
            EnsureLoaded();
            var result = new List<StationDefinition>();

            foreach (var station in _stations.Values)
            {
                if (station.StationType?.Equals(stationType, StringComparison.OrdinalIgnoreCase) == true)
                {
                    result.Add(station);
                }
            }

            return result;
        }

        public bool ValidateStationExists(string name)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(name) && _stations.ContainsKey(name);
        }

        // Add tracking for globally-limited stations
        private readonly HashSet<string> _globallyPlacedStations = new();

        public bool IsGloballyPlaced(string stationName) =>
            _globallyPlacedStations.Contains(stationName);

        public void MarkGloballyPlaced(string stationName)
        {
            if (!string.IsNullOrEmpty(stationName))
                _globallyPlacedStations.Add(stationName);
        }

        public void ResetGlobalPlacements() => _globallyPlacedStations.Clear();

        /// <summary>
        /// Checks if a station can be built considering global limits.
        /// </summary>
        public bool ValidateGlobalPlacement(string stationName)
        {
            // Ramshackle_Builder can only be built once
            if (stationName == "Ramshackle_Builder" && IsGloballyPlaced(stationName))
                return false;

            return true;
        }

        /// <summary>
        /// Gets the icon for a station at a specific size.
        /// Always returns a valid texture (uses fallback if needed).
        /// </summary>
        public Texture2D GetStationIcon(string name, IconSize size = IconSize.Medium)
        {
            EnsureLoaded();
            if (TryGetStation(name, out var station) && station != null)
            {
                var texture = station.Icon?.GetTexture(size);
                if (texture != null)
                    return texture;
            }

            return IconDataLoader.GetFallbackIcon(size);
        }

        /// <summary>
        /// Gets the full IconDefinition for a station.
        /// </summary>
        public IconDefinition? GetStationIconDefinition(string name)
        {
            EnsureLoaded();
            if (TryGetStation(name, out var station) && station != null)
            {
                return station.Icon;
            }
            return null;
        }
    }
}
