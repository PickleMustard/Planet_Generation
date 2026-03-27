#if DEBUG
using System;
using System.Collections.Generic;
using UI.Debug.DatabaseViewer;

namespace UtilityLibrary.TaskSystem
{
    public partial class ThreadPooler : IDebugDataProvider
    {
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
    }
}
#endif
