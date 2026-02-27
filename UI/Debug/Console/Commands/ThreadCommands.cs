#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using UI.Debug;
using PlanetGeneration;

namespace UI.Debug.Console;

public static class ThreadCommands
{
    [DebugCommand("thread_status", "Show thread pool status and active tasks", "thread_status", Category = "Threading")]
    public static int ThreadStatus(CommandContext ctx, string[] args)
    {
        var threadPool = MeshGenerationThreadPool.Instance;
        if (threadPool == null)
        {
            ctx.WriteError("MeshGenerationThreadPool not initialized");
            ctx.WriteLine("Thread pool may not be enabled or the scene hasn't started");
            return 1;
        }

        try
        {
            var activeCount = threadPool.GetActiveTaskCount();
            var queuedCount = threadPool.GetQueuedTaskCount();
            var resourceStatus = threadPool.GetResourceStatus();

            ctx.WriteLine("[color=yellow]Thread Pool Status:[/color]");
            ctx.WriteLine($"  Active Tasks: {activeCount}");
            ctx.WriteLine($"  Queued Tasks: {queuedCount}");
            ctx.WriteLine($"  Available Memory: {resourceStatus.AvailableMemoryMB} MB");

            if (activeCount > 0 || queuedCount > 0)
            {
                ctx.WriteLine("\n[color=cyan]Task Queue:[/color]");
                
                var taskRegistryField = typeof(MeshGenerationThreadPool)
                    .GetField("taskRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (taskRegistryField != null)
                {
                    var taskRegistry = taskRegistryField.GetValue(threadPool);
                    if (taskRegistry is Dictionary<string, MeshGenerationTask> registry)
                    {
                        foreach (var kvp in registry)
                        {
                            var task = kvp.Value;
                            ctx.WriteLine($"  [{task.Priority}] {task.Id} - {task.BodyName}");
                            ctx.WriteLine($"      Started: {task.StartTime:HH:mm:ss}");
                            ctx.WriteLine($"      Progress: {task.Progress * 100:F1}%");
                            if (task.Exception != null)
                            {
                                ctx.WriteLine($"      [color=red]Error: {task.Exception.Message}[/color]");
                            }
                        }
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to get thread status: {ex.Message}");
            return 1;
        }
    }

    [DebugCommand("thread_cancel", "Cancel a specific task by ID", "thread_cancel <task_id>", Category = "Threading")]
    public static int ThreadCancel(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: thread_cancel <task_id>");
            ctx.WriteLine("Use 'thread_status' to see available task IDs");
            return 1;
        }

        var taskId = args[0];
        var threadPool = MeshGenerationThreadPool.Instance;
        
        if (threadPool == null)
        {
            ctx.WriteError("MeshGenerationThreadPool not initialized");
            return 1;
        }

        try
        {
            threadPool.CancelTask(taskId);
            ctx.WriteLine($"[color=green]Cancelled task: {taskId}[/color]");
            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to cancel task: {ex.Message}");
            return 1;
        }
    }

    [DebugCommand("watch", "Watch a property for changes", "watch <namespace>.<property> [interval]", Category = "Threading")]
    public static int Watch(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: watch <namespace>.<property> [interval_ms]");
            ctx.WriteLine("Example: watch CelestialBody.Earth.Position 1000");
            ctx.WriteLine("\nUse 'watch_stop' to stop watching");
            return 1;
        }

        var path = args[0];
        var interval = 1000;
        
        if (args.Length > 1 && int.TryParse(args[1], out var customInterval))
        {
            interval = Math.Max(100, customInterval);
        }

        var parts = path.Split('.', 2);
        if (parts.Length < 2)
        {
            ctx.WriteError("Invalid path format. Use: <namespace>.<property>");
            return 1;
        }

        var ns = parts[0];
        var propertyPath = parts[1];

        if (!InstanceRegistry.TryGetInstance(ns, out var instance))
        {
            ctx.WriteError($"Instance not found: {ns}");
            return 1;
        }

        var watchId = $"{ns}.{propertyPath}";
        if (WatchManager.IsWatching(watchId))
        {
            ctx.WriteWarning($"Already watching: {watchId}");
            return 0;
        }

        WatchManager.StartWatch(watchId, instance, propertyPath, interval);
        ctx.WriteLine($"[color=green]Started watching: {watchId}[/color]");
        ctx.WriteLine($"  Interval: {interval}ms");
        ctx.WriteLine("  Use 'watch_stop' to stop");

        return 0;
    }

    [DebugCommand("watch_stop", "Stop watching a property", "watch_stop [namespace.property]", Category = "Threading")]
    public static int WatchStop(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            var watching = WatchManager.GetAllWatches();
            if (watching.Count == 0)
            {
                ctx.WriteLine("No active watches");
                return 0;
            }

            ctx.WriteLine("[color=yellow]Active watches:[/color]");
            foreach (var watch in watching)
            {
                ctx.WriteLine($"  {watch}");
            }
            ctx.WriteLine("\nUse 'watch_stop <namespace.property>' to stop a specific watch");
            ctx.WriteLine("Use 'watch_stop all' to stop all watches");
            return 0;
        }

        var watchId = args[0];
        
        if (watchId.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var count = WatchManager.StopAll();
            ctx.WriteLine($"[color=green]Stopped {count} watch(es)[/color]");
            return 0;
        }

        if (WatchManager.StopWatch(watchId))
        {
            ctx.WriteLine($"[color=green]Stopped watching: {watchId}[/color]");
            return 0;
        }
        
        ctx.WriteError($"Watch not found: {watchId}");
        return 1;
    }

    [DebugCommand("watch_list", "List all active watches", "watch_list", Category = "Threading")]
    public static int WatchList(CommandContext ctx, string[] args)
    {
        var watching = WatchManager.GetAllWatches();
        
        if (watching.Count == 0)
        {
            ctx.WriteLine("No active watches");
            return 0;
        }

        ctx.WriteLine($"[color=yellow]Active Watches ({watching.Count}):[/color]");
        foreach (var watch in watching)
        {
            var info = WatchManager.GetWatchInfo(watch);
            if (info != null)
            {
                ctx.WriteLine($"  {watch}");
                ctx.WriteLine($"    Last value: {info.LastValue}");
                ctx.WriteLine($"    Changes: {info.ChangeCount}");
            }
        }
        
        return 0;
    }
}

internal static class WatchManager
{
    private static readonly Dictionary<string, WatchInfo> _watches = new();
    private static readonly object _lock = new();

    public class WatchInfo
    {
        public string WatchId { get; set; }
        public object Instance { get; set; }
        public string PropertyPath { get; set; }
        public int IntervalMs { get; set; }
        public string LastValue { get; set; }
        public int ChangeCount { get; set; }
        public DateTime StartTime { get; set; }
    }

    public static bool IsWatching(string watchId)
    {
        lock (_lock)
        {
            return _watches.ContainsKey(watchId);
        }
    }

    public static void StartWatch(string watchId, object instance, string propertyPath, int intervalMs)
    {
        lock (_lock)
        {
            if (_watches.ContainsKey(watchId))
                return;

            var watch = new WatchInfo
            {
                WatchId = watchId,
                Instance = instance,
                PropertyPath = propertyPath,
                IntervalMs = intervalMs,
                StartTime = DateTime.Now
            };

            _watches[watchId] = watch;
        }
    }

    public static bool StopWatch(string watchId)
    {
        lock (_lock)
        {
            return _watches.Remove(watchId);
        }
    }

    public static int StopAll()
    {
        lock (_lock)
        {
            var count = _watches.Count;
            _watches.Clear();
            return count;
        }
    }

    public static List<string> GetAllWatches()
    {
        lock (_lock)
        {
            return new List<string>(_watches.Keys);
        }
    }

    public static WatchInfo GetWatchInfo(string watchId)
    {
        lock (_lock)
        {
            return _watches.TryGetValue(watchId, out var info) ? info : null;
        }
    }
}
#endif
