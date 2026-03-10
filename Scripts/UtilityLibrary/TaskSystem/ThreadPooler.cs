using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using UtilityLibrary;
#if DEBUG
using UI.Debug;
using UI.Debug.DatabaseViewer;
#endif

namespace UtilityLibrary.TaskSystem
{
#if DEBUG
    [DebugData("Thread Pooler", Category = "System")]
#endif
    public partial class ThreadPooler : Node
#if DEBUG
            , IDebugDataProvider
#endif
        , IConfigurable
    {
        private static ThreadPooler? _instance;
        public static ThreadPooler? Instance => _instance;

        private Thread[]? _workers;
        private BlockingCollection<WorkPackage>[]? _priorityQueues;
        private ConcurrentDictionary<string, WorkPackage>? _activePackages;
        private ConcurrentDictionary<string, WorkPackage>? _pendingPackages;
        private ConcurrentDictionary<string, BatchStats>? _batchStats;
        private CancellationTokenSource? _cancellationToken;
        private ThreadAllocationInfo? _allocationInfo;
        private bool _isInitialized;
        private float _allocationPercentage = 0.75f;
        private int _manualThreadCount = 0;
        private readonly object _lock = new();

        public int ActivePackageCount => _activePackages?.Count ?? 0;
        public int PendingPackageCount => _pendingPackages?.Count ?? 0;
        public int WorkerCount => _workers?.Length ?? 0;
        public ThreadAllocationInfo? AllocationInfo => _allocationInfo;

        public string SettingsCategory => "threading";

        public IEnumerable<ConfigEntry> GetConfigEntries() => new[]
        {
            new ConfigEntry
            {
                Key = "allocation_percentage",
                ValueType = typeof(float),
                DefaultValue = 0.75f,
                MinValue = 0.1f,
                MaxValue = 1.0f,
                Description = "Percentage of CPU cores to allocate for thread pool",
                RequiresRestart = true
            },
            new ConfigEntry
            {
                Key = "manual_thread_count",
                ValueType = typeof(int),
                DefaultValue = 0,
                MinValue = 0,
                MaxValue = 64,
                Description = "Override thread count (0 = auto-calculated)",
                RequiresRestart = true
            }
        };

        public void ApplySetting(string key, object value)
        {
            // Settings require restart, cache the values for use during initialization
            switch (key)
            {
                case "allocation_percentage":
                    _allocationPercentage = Convert.ToSingle(value);
                    break;
                case "manual_thread_count":
                    _manualThreadCount = Convert.ToInt32(value);
                    break;
            }
        }

        public object? GetSettingDefault(string key) => key switch
        {
            "allocation_percentage" => 0.75f,
            "manual_thread_count" => 0,
            _ => null
        };

        public override void _Ready()
        {
            if (_instance == null)
            {
                _instance = this;
                RuntimeSettings.Instance?.RegisterConfigurable(this);
                Initialize();
                SignalBus.Instance?.AutoConnect(this);
            }
            else
            {
                GD.PrintErr("ThreadPooler already exists. Duplicate instance detected.");
            }
        }

        private void Initialize()
        {
            if (_isInitialized)
                return;

            lock (_lock)
            {
                if (_isInitialized)
                    return;

                int totalCores = System.Environment.ProcessorCount;
                int threadCount;
                float allocationPercentage;

                if (_manualThreadCount > 0)
                {
                    threadCount = Math.Min(_manualThreadCount, totalCores);
                    allocationPercentage = (float)threadCount / totalCores;
                }
                else
                {
                    allocationPercentage = _allocationPercentage;
                    threadCount = Math.Max(1, (int)(totalCores * allocationPercentage));
                }

                _allocationInfo = new ThreadAllocationInfo
                {
                    TotalCores = totalCores,
                    AllocatedThreads = threadCount,
                    AllocationPercentage = allocationPercentage
                };

                GD.Print(
                    $"ThreadPooler initializing with {threadCount} workers ({_allocationInfo.AllocationPercentage * 100:F0}% of {_allocationInfo.TotalCores} cores)"
                );

                _cancellationToken = new CancellationTokenSource();
                _activePackages = new ConcurrentDictionary<string, WorkPackage>();
                _pendingPackages = new ConcurrentDictionary<string, WorkPackage>();
                _batchStats = new ConcurrentDictionary<string, BatchStats>();

                int priorityCount = Enum.GetValues(typeof(TaskPriority)).Length;
                _priorityQueues = new BlockingCollection<WorkPackage>[priorityCount];
                for (int i = 0; i < priorityCount; i++)
                {
                    _priorityQueues[i] = new BlockingCollection<WorkPackage>();
                }

                _workers = new Thread[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    _workers[i] = new Thread(WorkerLoop)
                    {
                        Name = $"ThreadPooler-Worker-{i}",
                        IsBackground = true,
                    };
                    _workers[i].Start(i);
                }

                _isInitialized = true;
                GD.Print("ThreadPooler initialized successfully");
            }
        }

        private void WorkerLoop(object? threadIndex)
        {
            int index = (int)threadIndex!;
            GD.Print($"Worker {index} started");

            while (!_cancellationToken!.Token.IsCancellationRequested)
            {
                WorkPackage? package = null;

                try
                {
                    int queueIndex = BlockingCollection<WorkPackage>.TryTakeFromAny(
                        _priorityQueues!,
                        out package,
                        100,
                        _cancellationToken.Token
                    );

                    if (queueIndex == -1 || package == null)
                    {
                        continue;
                    }

                    _pendingPackages!.TryRemove(package.Name, out _);
                    _activePackages![package.Name] = package;

                    NotifyTimerStarted(package);

                    while (!package.IsComplete && !_cancellationToken.Token.IsCancellationRequested)
                    {
                        package.ExecuteNextStep();
                        NotifyTimerStepIncremented(package.Name);
                    }

                    OnPackageCompleted(package);
                    _activePackages.TryRemove(package.Name, out _);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (PackageFailedException ex)
                {
                    GD.PrintErr(
                        $"Worker {index} package failed after retries: '{ex.PackageName}' at step '{ex.FailedStepName}': {ex.ErrorMessage}"
                    );
                    if (package != null)
                    {
                        OnPackageFailed(package);
                        _activePackages!.TryRemove(package.Name, out _);
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr(
                        $"Worker {index} error processing package '{package?.Name}': {ex.Message}\n{ex.StackTrace}"
                    );
                    if (package != null)
                    {
                        OnPackageFailed(package);
                        _activePackages!.TryRemove(package.Name, out _);
                    }
                }
            }

            GD.Print($"Worker {index} stopped");
        }

        private void OnPackageCompleted(WorkPackage package)
        {
            if (!string.IsNullOrEmpty(package.BatchId))
            {
                var stats = _batchStats!.GetOrAdd(package.BatchId, _ => new BatchStats());
                stats.Total++;
                stats.Completed++;
                CheckBatchCompletion(package.BatchId);
            }
        }

        private void OnPackageFailed(WorkPackage package)
        {
            if (!string.IsNullOrEmpty(package.BatchId))
            {
                var stats = _batchStats!.GetOrAdd(package.BatchId, _ => new BatchStats());
                stats.Total++;
                stats.Failed++;
                CheckBatchCompletion(package.BatchId);
            }
        }

        private void CheckBatchCompletion(string batchId)
        {
            if (_batchStats!.TryGetValue(batchId, out var stats))
            {
                if (stats.Completed + stats.Failed >= stats.Expected)
                {
                    CallDeferred(
                        nameof(EmitBatchCompleteSignal),
                        batchId,
                        stats.Total,
                        stats.Completed
                    );
                    _batchStats.TryRemove(batchId, out _);
                }
            }
        }

        private void EmitBatchCompleteSignal(string batchId, int totalBodies, int successfulBodies)
        {
            SignalBus.Instance?.EmitSystemGenerationComplete(batchId, totalBodies, successfulBodies);
        }

        private void NotifyTimerStarted(WorkPackage package)
        {
            CallDeferred(
                nameof(EmitTimerStartSignal),
                package.Name,
                package.TotalSteps,
                0,
                package.StepNames
            );
        }

        private void EmitTimerStartSignal(
            string name,
            int totalSteps,
            int startingStep,
            string[] stepNames
        )
        {
            SignalBus.Instance?.EmitStartTimer(name, totalSteps, startingStep, stepNames);
        }

        private void NotifyTimerStepIncremented(string packageName)
        {
            CallDeferred(nameof(EmitTimerIncrementSignal), packageName);
        }

        private void EmitTimerIncrementSignal(string name)
        {
            SignalBus.Instance?.EmitIncrementTimerStep(name);
        }

        [SignalHandler("QueuePackage")]
        private void OnQueuePackage(WorkPackage package)
        {
            GD.Print($"Queueing Package: {package.Name}");
            EnqueuePackage(package);
        }

        public void RegisterBatch(string batchId, int expectedPackages)
        {
            var stats = _batchStats!.GetOrAdd(batchId, _ => new BatchStats());
            stats.Expected = expectedPackages;
        }

        public void EnqueuePackage(WorkPackage package)
        {
            GD.Print("Queueing Package");
            if (!_isInitialized)
            {
                throw new InvalidOperationException("ThreadPooler not initialized");
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            _pendingPackages![package.Name] = package;
            int queueIndex = (int)package.Priority;

            if (queueIndex >= 0 && queueIndex < _priorityQueues!.Length)
            {
                _priorityQueues[queueIndex].Add(package);
                GD.Print($"Package '{package.Name}' queued with priority {package.Priority}");
            }
            else
            {
                _priorityQueues![(int)TaskPriority.Normal].Add(package);
                GD.Print(
                    $"Package '{package.Name}' queued with default priority (invalid priority specified)"
                );
            }
        }

        public WorkPackageBuilder CreatePackage(string name)
        {
            return new WorkPackageBuilder().WithName(name);
        }

        public void CancelPackage(string packageName)
        {
            _pendingPackages!.TryRemove(packageName, out _);

            foreach (var queue in _priorityQueues!)
            {
                var items = queue.ToArray();
                foreach (var item in items)
                {
                    if (item.Name == packageName && queue.TryTake(out _))
                    {
                        GD.Print($"Package '{packageName}' cancelled from queue");
                        return;
                    }
                }
            }

            if (_activePackages!.TryGetValue(packageName, out _))
            {
                GD.Print($"Package '{packageName}' is currently active and cannot be cancelled");
            }
        }

        public void CancelAllPackages()
        {
            foreach (var queue in _priorityQueues!)
            {
                while (queue.TryTake(out _)) { }
            }
            _pendingPackages!.Clear();
            GD.Print("All pending packages cancelled");
        }

        public bool IsPackageActive(string packageName)
        {
            return _activePackages!.ContainsKey(packageName);
        }

        public bool IsPackagePending(string packageName)
        {
            return _pendingPackages!.ContainsKey(packageName);
        }

        public WorkPackage? GetActivePackage(string packageName)
        {
            _activePackages!.TryGetValue(packageName, out var package);
            return package;
        }

        public int GetQueueLength(TaskPriority priority)
        {
            int index = (int)priority;
            if (index >= 0 && index < _priorityQueues!.Length)
            {
                return _priorityQueues[index].Count;
            }
            return 0;
        }

        public int GetTotalQueueLength()
        {
            return _priorityQueues!.Sum(q => q.Count);
        }

        public void Shutdown()
        {
            if (!_isInitialized)
                return;

            lock (_lock)
            {
                if (!_isInitialized)
                    return;

                GD.Print("ThreadPooler shutting down...");
                _cancellationToken!.Cancel();

                foreach (var queue in _priorityQueues!)
                {
                    queue.CompleteAdding();
                }

                if (_workers != null)
                {
                    foreach (var worker in _workers)
                    {
                        if (worker != null && worker.IsAlive)
                        {
                            worker.Join(1000);
                        }
                    }
                }

                foreach (var queue in _priorityQueues)
                {
                    queue.Dispose();
                }

                _cancellationToken.Dispose();
                _isInitialized = false;
                GD.Print("ThreadPooler shutdown complete");
            }
        }

        public override void _ExitTree()
        {
            SignalBus.Instance?.AutoDisconnect(this);
            Shutdown();
            base._ExitTree();
        }

#if DEBUG
        string IDataProvider.Name => "Thread Pooler";
        string IDataProvider.Category => "System";
        bool IDataProvider.NeedsRefresh => true;
        object IDebugDataProvider.SourceObject => this;
        string IDebugDataProvider.InstanceNamespace => "ThreadPooler";
        bool IDebugDataProvider.IsSourceValid => _isInitialized && IsInstanceValid(this);

        DebugDataNode IDataProvider.GetData()
        {
            var node = new DebugDataNode("Thread Pooler")
                .AddProperty("Worker Count", WorkerCount)
                .AddProperty("Active Packages", ActivePackageCount)
                .AddProperty("Pending Packages", PendingPackageCount)
                .AddProperty("Total Cores", _allocationInfo?.TotalCores ?? 0)
                .AddProperty(
                    "Allocation %",
                    _allocationInfo != null
                        ? $"{_allocationInfo.AllocationPercentage * 100:F0}%"
                        : "N/A"
                );

            var activeNode = node.AddChild("Active Packages");
            foreach (var kvp in _activePackages!)
            {
                var pkg = kvp.Value;
                activeNode
                    .AddChild(pkg.Name)
                    .AddProperty("Current Step", $"{pkg.CurrentStepIndex + 1}/{pkg.TotalSteps}")
                    .AddProperty("Priority", pkg.Priority.ToString())
                    .AddProperty("Progress", $"{pkg.Progress * 100:F1}%");
            }

            var queueNode = node.AddChild("Queues by Priority");
            foreach (TaskPriority priority in Enum.GetValues(typeof(TaskPriority)))
            {
                int count = GetQueueLength(priority);
                if (count > 0)
                {
                    queueNode.AddChild($"{priority}: {count} packages");
                }
            }

            return node;
        }

        void IDataProvider.Refresh() { }

        IEnumerable<string> IDataProvider.Search(string pattern)
        {
            var results = new List<string>();
            foreach (var kvp in _activePackages!)
            {
                if (kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add($"Active/{kvp.Key}");
                }
            }
            foreach (var kvp in _pendingPackages!)
            {
                if (kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add($"Pending/{kvp.Key}");
                }
            }
            return results;
        }
#endif
    }

    internal class BatchStats
    {
        public int Expected;
        public int Total;
        public int Completed;
        public int Failed;
    }
}
