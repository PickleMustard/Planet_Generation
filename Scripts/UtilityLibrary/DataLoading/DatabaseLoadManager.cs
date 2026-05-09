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

        // Phase-gating state using TaskCompletionSource
        private readonly ConcurrentDictionary<string, TaskCompletionSource<int>> _phaseCompletionSources = new();
        private readonly object _phaseLock = new();
        private List<List<ILoadableDatabase>>? _currentPhases;

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

            // Validate dependencies: warn if a dependency references an unregistered database
            foreach (var dependency in database.Dependencies)
            {
                if (!_registeredDatabases.ContainsKey(dependency))
                {
                    GameLogger.Warning(
                        $"DatabaseLoadManager: Database '{dbName}' declares dependency on '{dependency}' " +
                        $"which is not yet registered. If '{dependency}' is external, this warning can be ignored."
                    );
                }
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
        /// Phases execute sequentially: a phase only starts after every database
        /// in the prior phase has completed loading (success or failure).
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
            _phaseCompletionSources.Clear();

            var phases = TopologicalSort(_registeredDatabases.Values.ToList());
            _currentPhases = phases;

            if (phases.Count == 0)
            {
                GD.Print("DatabaseLoadManager: No phases to load after topological sort");
                return true;
            }

            // Prepare TaskCompletionSources for every phase so that later phases
            // can logically await earlier ones.
            int totalPhases = phases.Count;
            for (int i = 0; i < totalPhases; i++)
            {
                string phaseId = $"{_currentBatchId}_phase{i}";
                _phaseCompletionSources[phaseId] = new TaskCompletionSource<int>();
            }

            // Begin sequential execution by submitting Phase 0.
            // Subsequent phases are submitted inside OnBatchCompleted only when
            // the preceding phase finishes.
            SubmitPhase(0);
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
        /// Loads a specific phase of databases via the ThreadPooler.
        /// </summary>
        private void SubmitPhase(int phaseIndex)
        {
            if (_currentPhases == null || phaseIndex >= _currentPhases.Count)
                return;

            var phase = _currentPhases[phaseIndex];
            if (phase.Count == 0)
            {
                AdvanceToNextPhase(phaseIndex);
                return;
            }

            // Use 1 package per database within a phase so they execute in
            // parallel across the thread pool.
            string phaseBatchId = $"{_currentBatchId}_phase{phaseIndex}";
            int packagesInPhase = phase.Count;
            var completedPackages = new ConcurrentBag<int>();
            bool failureDetected = false;
            var failureLock = new object();

            foreach (var database in phase)
            {
                _batchIds[database.DatabaseName] = phaseBatchId;

                var builder = new WorkPackageBuilder()
                    .WithName($"DatabaseBatch_{phaseBatchId}_{database.DatabaseName}")
                    .WithBatchId(phaseBatchId)
                    .AddStep($"Load_{database.DatabaseName}", () => database.LoadData());

                var package = builder.Build();
                package.PackageCompleted += (_, resultCode) =>
                {
                    completedPackages.Add(resultCode);
                    CheckPhaseCompletion(phaseIndex, packagesInPhase, completedPackages, ref failureDetected, failureLock);
                };
                package.PackageFailed += (_, error) =>
                {
                    lock (failureLock)
                    {
                        failureDetected = true;
                    }
                    completedPackages.Add(1); // treat failure as a completed attempt
                    CheckPhaseCompletion(phaseIndex, packagesInPhase, completedPackages, ref failureDetected, failureLock);
                };

                ThreadPooler.Instance?.EnqueuePackage(package);
            }

            GD.Print($"DatabaseLoadManager: Enqueued phase '{phaseBatchId}' with {phase.Count} database(s)");
        }

        /// <summary>
        /// Checks whether every package in the given phase has finished.
        /// If so, the associated TaskCompletionSource is resolved and the
        /// next phase is submitted (or all phases complete).
        /// </summary>
        private void CheckPhaseCompletion(
            int phaseIndex,
            int expectedPackages,
            ConcurrentBag<int> completedPackages,
            ref bool failureDetected,
            object failureLock)
        {
            int completedCount = completedPackages.Count;
            if (completedCount < expectedPackages)
                return;

            string phaseBatchId = $"{_currentBatchId}_phase{phaseIndex}";
            int phaseResult;
            lock (failureLock)
            {
                phaseResult = failureDetected ? 1 : 0;
            }

            if (_phaseCompletionSources.TryGetValue(phaseBatchId, out var tcs))
            {
                tcs.TrySetResult(phaseResult);
            }

            if (phaseResult == 0)
            {
                AdvanceToNextPhase(phaseIndex);
            }
            else
            {
                GD.PrintErr($"DatabaseLoadManager: Phase '{phaseBatchId}' failed. Halting downstream phase execution.");
                SignalBatchCompletionIfAllResolved();
            }
        }

        /// <summary>
        /// Advances to the next phase, if any, by submitting it to the thread pool.
        /// </summary>
        private void AdvanceToNextPhase(int currentPhaseIndex)
        {
            int nextPhaseIndex = currentPhaseIndex + 1;
            if (_currentPhases != null && nextPhaseIndex < _currentPhases.Count)
            {
                SubmitPhase(nextPhaseIndex);
            }
            else
            {
                // All phases complete
                SignalBatchCompletionIfAllResolved();
            }
        }

        /// <summary>
        /// Signals overall completion when all phase TCS have been resolved.
        /// </summary>
        private void SignalBatchCompletionIfAllResolved()
        {
            bool allResolved = _phaseCompletionSources.Values.All(tcs => tcs.Task.IsCompleted);
            if (allResolved)
            {
                int overallResult = _phaseCompletionSources.Values.Any(tcs => tcs.Task.Result != 0) ? 1 : 0;
                OnAllPhasesCompleted(overallResult);
            }
        }

        /// <summary>
        /// Called once all database loading phases have resolved.
        /// </summary>
        private void OnAllPhasesCompleted(int overallResultCode)
        {
            if (overallResultCode == 0)
            {
                GD.Print($"DatabaseLoadManager: Batch '{_currentBatchId}' completed successfully");
                // SignalBus.Instance?.EmitDatabaseBatchComplete(_currentBatchId, true);
            }
            else
            {
                GD.PrintErr($"DatabaseLoadManager: Batch '{_currentBatchId}' completed with errors");
                // SignalBus.Instance?.EmitDatabaseBatchComplete(_currentBatchId, false);
            }
            _currentBatchId = null;
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

        /// <summary>
        /// Checks if a database is currently waiting for its dependencies to be satisfied
        /// before it can begin loading.
        /// </summary>
        public bool IsDatabaseWaitingForDependencies(string databaseName)
        {
            if (_currentPhases == null || string.IsNullOrEmpty(_currentBatchId))
                return false;

            var db = GetDatabase(databaseName);
            if (db == null)
                return false;

            // If already loaded or has progress, it's not waiting
            if (db.IsLoaded || GetDatabaseProgress(databaseName) > 0f)
                return false;

            // Find which phase this database is in
            int dbPhaseIndex = -1;
            for (int i = 0; i < _currentPhases.Count; i++)
            {
                if (_currentPhases[i].Any(d => d.DatabaseName == databaseName))
                {
                    dbPhaseIndex = i;
                    break;
                }
            }

            if (dbPhaseIndex <= 0)
                return false; // Phase 0 databases don't wait for registered deps

            // Check if all prior phases have been resolved
            for (int priorPhase = 0; priorPhase < dbPhaseIndex; priorPhase++)
            {
                string priorPhaseId = $"{_currentBatchId}_phase{priorPhase}";
                if (_phaseCompletionSources.TryGetValue(priorPhaseId, out var tcs))
                {
                    if (!tcs.Task.IsCompleted)
                        return true; // A prior phase is still running
                }
                else
                {
                    // TCS doesn't exist - phase hasn't even been started yet
                    return true;
                }
            }

            return false;
        }
    }
}