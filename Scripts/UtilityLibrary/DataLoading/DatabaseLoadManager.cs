using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using UtilityLibrary.TaskSystem;
#if DEBUG
using UI.Debug;
#endif

namespace UtilityLibrary.DataLoading
{
#if DEBUG
    [DebugData("Database Load Manager", Category = "System")]
#endif
    public partial class DatabaseLoadManager : Node, IConfigurable
    {
        private static DatabaseLoadManager? _instance;
        public static DatabaseLoadManager? Instance => _instance;

        private readonly ConcurrentDictionary<string, ILoadableDatabase> _registeredDatabases = new();
        private readonly ConcurrentDictionary<string, bool> _loadingStatus = new();
        private readonly ConcurrentDictionary<string, float> _loadProgress = new();
        private readonly ConcurrentDictionary<string, string?> _batchIds = new();

        private bool _isInitialized;
        private int _maxConcurrentLoads = 2;
        private string? _currentBatchId;

        public string SettingsCategory => "database_loading";

        public IEnumerable<ConfigEntry> GetConfigEntries() =>
            new[]
            {
                new ConfigEntry
                {
                    Key = "max_concurrent_loads",
                    ValueType = typeof(int),
                    DefaultValue = 2,
                    MinValue = 1,
                    MaxValue = 8,
                    Description = "Maximum number of databases to load concurrently",
                    RequiresRestart = false,
                }
            };

        public void ApplySetting(string key, object value)
        {
            switch (key)
            {
                case "max_concurrent_loads":
                    _maxConcurrentLoads = Math.Max(1, Convert.ToInt32(value));
                    GD.Print($"DatabaseLoadManager: Max concurrent loads set to {_maxConcurrentLoads}");
                    break;
            }
        }

        public object? GetSettingDefault(string key) =>
            key switch
            {
                "max_concurrent_loads" => 2,
                _ => null,
            };

        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                RuntimeSettings.Instance?.RegisterConfigurable(this);
                SignalBus.Instance?.AutoConnect(this);
                _isInitialized = true;
                GD.Print("DatabaseLoadManager initialized successfully");
            }
            else
            {
                GD.PrintErr("DatabaseLoadManager already exists. Duplicate instance detected.");
            }
        }

        /// <summary>
        /// Registers a database with the load manager.
        /// </summary>
        /// <param name="database">The database to register.</param>
        /// <returns>True if registration was successful, false otherwise.</returns>
        public bool RegisterDatabase(ILoadableDatabase database)
        {
            if (database == null)
            {
                GD.PrintErr("DatabaseLoadManager: Cannot register null database");
                return false;
            }

            if (!_isInitialized)
            {
                GD.PrintErr("DatabaseLoadManager: Not initialized. Call _Ready() first.");
                return false;
            }

            string dbName = database.DatabaseName;
            if (_registeredDatabases.ContainsKey(dbName))
            {
                GD.PrintErr($"DatabaseLoadManager: Database '{dbName}' is already registered");
                return false;
            }

            _registeredDatabases[dbName] = database;
            _loadingStatus[dbName] = false;
            _loadProgress[dbName] = 0f;

            // Subscribe to database events
            database.OnLoadStarted += (name) => OnDatabaseLoadStarted(name);
            database.OnLoadCompleted += (name, success) => OnDatabaseLoadCompleted(name, success);
            database.OnLoadProgressChanged += (name, progress) => OnDatabaseLoadProgressChanged(name, progress);

            GD.Print($"DatabaseLoadManager: Registered database '{dbName}'");
            return true;
        }

        /// <summary>
        /// Unregisters a database from the load manager.
        /// </summary>
        /// <param name="databaseName">The name of the database to unregister.</param>
        /// <returns>True if unregistration was successful, false otherwise.</returns>
        public bool UnregisterDatabase(string databaseName)
        {
            if (!_registeredDatabases.TryRemove(databaseName, out var database))
            {
                GD.PrintErr($"DatabaseLoadManager: Database '{databaseName}' is not registered");
                return false;
            }

            _loadingStatus.TryRemove(databaseName, out _);
            _loadProgress.TryRemove(databaseName, out _);
            _batchIds.TryRemove(databaseName, out _);

            GD.Print($"DatabaseLoadManager: Unregistered database '{databaseName}'");
            return true;
        }

        /// <summary>
        /// Gets the loading status of a database.
        /// </summary>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>True if the database is loaded, false otherwise.</returns>
        public bool GetDatabaseLoaded(string databaseName)
        {
            return _loadingStatus.GetValueOrDefault(databaseName, false);
        }

        /// <summary>
        /// Gets the loading progress of a database.
        /// </summary>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>The current loading progress (0.0 to 1.0).</returns>
        public float GetDatabaseProgress(string databaseName)
        {
            return _loadProgress.GetValueOrDefault(databaseName, 0f);
        }

        /// <summary>
        /// Initiates loading of all registered databases in dependency-ordered phases.
        /// Databases within the same phase (no inter-dependencies) load in parallel.
        /// Phases execute sequentially to respect dependency ordering.
        /// </summary>
        /// <param name="batchId">Optional batch identifier for grouping loads.</param>
        /// <returns>True if loading was initiated, false otherwise.</returns>
        public bool LoadAllDatabases(string? batchId = null)
        {
            if (!_isInitialized)
            {
                GD.PrintErr("DatabaseLoadManager: Not initialized. Call _Ready() first.");
                return false;
            }

            if (_registeredDatabases.IsEmpty)
            {
                GD.Print("DatabaseLoadManager: No databases registered to load");
                return false;
            }

            _currentBatchId = batchId ?? Guid.NewGuid().ToString();
            GD.Print($"DatabaseLoadManager: Starting batch load '{_currentBatchId}' for {_registeredDatabases.Count} databases");

            var phases = TopologicalSort(_registeredDatabases.Values.ToList());

            int phaseIndex = 0;
            foreach (var phase in phases)
            {
                string phaseBatchId = $"{_currentBatchId}_phase{phaseIndex++}";
                LoadDatabaseGroup(phase, phaseBatchId);
            }

            return true;
        }

        /// <summary>
        /// Performs a topological sort on databases using Kahn's algorithm.
        /// Returns ordered phases where each phase contains databases
        /// whose dependencies are satisfied by all previous phases.
        /// </summary>
        private List<List<ILoadableDatabase>> TopologicalSort(List<ILoadableDatabase> databases)
        {
            var phases = new List<List<ILoadableDatabase>>();
            var dbByName = databases.ToDictionary(db => db.DatabaseName);
            var remaining = new HashSet<string>(dbByName.Keys);
            var satisfied = new HashSet<string>();

            while (remaining.Count > 0)
            {
                var phase = new List<ILoadableDatabase>();

                foreach (var dbName in remaining.ToList())
                {
                    var db = dbByName[dbName];
                    bool allDepsSatisfied = true;

                    foreach (var dep in db.Dependencies)
                    {
                        // Dependency on an unregistered database is ignored (may be external)
                        if (remaining.Contains(dep) && !satisfied.Contains(dep))
                        {
                            allDepsSatisfied = false;
                            break;
                        }
                    }

                    if (allDepsSatisfied)
                    {
                        phase.Add(db);
                    }
                }

                if (phase.Count == 0)
                {
                    GD.PrintErr("DatabaseLoadManager: Circular dependency detected, loading remaining databases without ordering");
                    phase.AddRange(remaining.Select(name => dbByName[name]));
                    phases.Add(phase);
                    break;
                }

                foreach (var db in phase)
                {
                    remaining.Remove(db.DatabaseName);
                    satisfied.Add(db.DatabaseName);
                }

                phases.Add(phase);

                GD.Print($"DatabaseLoadManager: Phase {phases.Count - 1}: [{string.Join(", ", phase.Select(db => db.DatabaseName))}]");
            }

            return phases;
        }

        /// <summary>
        /// Loads a specific group of databases.
        /// </summary>
        /// <param name="databases">The databases to load.</param>
        /// <param name="batchId">Batch identifier for this group.</param>
        private void LoadDatabaseGroup(List<ILoadableDatabase> databases, string batchId)
        {
            if (databases.Count == 0)
                return;

            var builder = new WorkPackageBuilder()
                .WithName($"DatabaseBatch_{batchId}")
                .WithBatchId(batchId);

            foreach (var database in databases)
            {
                _batchIds[database.DatabaseName] = batchId;
                builder.AddStep($"Load_{database.DatabaseName}", () => database.LoadData());
            }

            var package = builder.Build();
            package.PackageCompleted += (name, resultCode) => OnBatchCompleted(name, resultCode, batchId);
            package.PackageFailed += (name, error) => OnBatchFailed(name, error, batchId);

            ThreadPooler.Instance?.EnqueuePackage(package);
            GD.Print($"DatabaseLoadManager: Enqueued batch '{batchId}' with {databases.Count} databases");
        }

        /// <summary>
        /// Handles database load started event.
        /// </summary>
        private void OnDatabaseLoadStarted(string databaseName)
        {
            GD.Print($"DatabaseLoadManager: Database '{databaseName}' load started");
            _loadingStatus[databaseName] = false;
            _loadProgress[databaseName] = 0f;
        }

        /// <summary>
        /// Handles database load progress changed event.
        /// </summary>
        private void OnDatabaseLoadProgressChanged(string databaseName, float progress)
        {
            _loadProgress[databaseName] = progress;
            // Emit progress event if needed
            // SignalBus.Instance?.EmitDatabaseLoadProgress(databaseName, progress);
        }

        /// <summary>
        /// Handles database load completed event.
        /// </summary>
        private void OnDatabaseLoadCompleted(string databaseName, bool success)
        {
            _loadingStatus[databaseName] = success;
            _loadProgress[databaseName] = success ? 1.0f : 0f;

            if (success)
            {
                GD.Print($"DatabaseLoadManager: Database '{databaseName}' loaded successfully");
            }
            else
            {
                GD.PrintErr($"DatabaseLoadManager: Database '{databaseName}' failed to load");
            }

            // Emit completion event if needed
            // SignalBus.Instance?.EmitDatabaseLoadComplete(databaseName, success);
        }

        /// <summary>
        /// Handles batch completion.
        /// </summary>
        private void OnBatchCompleted(string packageName, int resultCode, string batchId)
        {
            GD.Print($"DatabaseLoadManager: Batch '{batchId}' completed with result code {resultCode}");

            // Check if all batches for the current batch are complete
            bool allBatchesComplete = _batchIds.Values.All(bid =>
            {
                if (bid == null) return true;
                // Check if any database still has this batchId
                return !_batchIds.Any(kvp => kvp.Value == batchId);
            });

            if (allBatchesComplete && batchId.StartsWith(_currentBatchId ?? ""))
            {
                GD.Print($"DatabaseLoadManager: All batches for '{_currentBatchId}' are complete");
                // SignalBus.Instance?.EmitDatabaseBatchComplete(_currentBatchId, true);
                _currentBatchId = null;
            }
        }

        /// <summary>
        /// Handles batch failure.
        /// </summary>
        private void OnBatchFailed(string packageName, string error, string batchId)
        {
            GD.PrintErr($"DatabaseLoadManager: Batch '{batchId}' failed: {error}");
            // SignalBus.Instance?.EmitDatabaseBatchComplete(batchId, false);
        }

        /// <summary>
        /// Gets all registered database names.
        /// </summary>
        public IEnumerable<string> GetRegisteredDatabaseNames() => _registeredDatabases.Keys;

        /// <summary>
        /// Gets a registered database by name.
        /// </summary>
        public ILoadableDatabase? GetDatabase(string databaseName) =>
            _registeredDatabases.GetValueOrDefault(databaseName);

        /// <summary>
        /// Checks if a database is registered.
        /// </summary>
        public bool IsDatabaseRegistered(string databaseName) =>
            _registeredDatabases.ContainsKey(databaseName);
    }
}