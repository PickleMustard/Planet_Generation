#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UI.Debug;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Data provider for performance metrics: FPS, memory, frame time.
/// Auto-refreshes for live updates.
/// </summary>
[DebugData("Performance", Category = "Performance", AutoRefresh = true)]
public class PerformanceProvider : IDataProvider
{
    private DebugDataNode _cachedData;
    private double _lastRefreshTime;
    private readonly double _refreshInterval = 0.1;

    public string Name => "Performance";
    public string Category => "Performance";
    public bool NeedsRefresh => true;

    public PerformanceProvider()
    {
        _lastRefreshTime = 0;
    }

    public DebugDataNode GetData()
    {
        var now = Time.GetTicksMsec() / 1000.0;
        if (_cachedData == null || (now - _lastRefreshTime) >= _refreshInterval)
        {
            _cachedData = BuildPerformanceData();
            _lastRefreshTime = now;
        }
        return _cachedData;
    }

    public void Refresh()
    {
        _cachedData = null;
    }

    public IEnumerable<string> Search(string pattern)
    {
        var data = GetData();
        var results = new List<string>();
        SearchRecursive(data, "", pattern.ToLower(), results);
        return results;
    }

    private void SearchRecursive(DebugDataNode node, string path, string pattern, List<string> results)
    {
        var currentPath = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";

        if (node.Name.ToLower().Contains(pattern) ||
            (node.HasValue && node.Value?.ToString()?.ToLower()?.Contains(pattern) == true))
        {
            results.Add(currentPath);
        }

        foreach (var prop in node.Properties.Values)
        {
            var propPath = $"{currentPath}.{prop.Name}";
            if (prop.Name.ToLower().Contains(pattern) ||
                (prop.HasValue && prop.Value?.ToString()?.ToLower()?.Contains(pattern) == true))
            {
                results.Add(propPath);
            }
        }

        foreach (var child in node.Children)
        {
            SearchRecursive(child, currentPath, pattern, results);
        }
    }

    private DebugDataNode BuildPerformanceData()
    {
        var root = new DebugDataNode("Performance");

        var fps = root.AddChild("FPS");
        fps.AddProperty("Current", Performance.GetMonitor(Performance.Monitor.TimeFps));
        fps.AddProperty("Average Process", Performance.GetMonitor(Performance.Monitor.TimeProcess));
        fps.AddProperty("Average Physics Process", Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess));

        var frameTime = root.AddChild("Frame Time");
        frameTime.AddProperty("Process Time (ms)", Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000);
        frameTime.AddProperty("Physics Time (ms)", Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000);
        frameTime.AddProperty("Navigation Time (ms)", Performance.GetMonitor(Performance.Monitor.TimeNavigationProcess) * 1000);

        var memory = root.AddChild("Memory");
        memory.AddProperty("Static (MB)", Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024 * 1024));
        memory.AddProperty("Static Max (MB)", Performance.GetMonitor(Performance.Monitor.MemoryStaticMax) / (1024 * 1024));

        var objectCount = root.AddChild("Object Count");
        objectCount.AddProperty("Objects", Performance.GetMonitor(Performance.Monitor.ObjectCount));
        objectCount.AddProperty("Nodes", Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));

        var renderStats = root.AddChild("Render Statistics");
        renderStats.AddProperty("Video Mem (MB)", Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024 * 1024));
        renderStats.AddProperty("Texture Mem (MB)", Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / (1024 * 1024));
        renderStats.AddProperty("Buffer Mem (MB)", Performance.GetMonitor(Performance.Monitor.RenderBufferMemUsed) / (1024 * 1024));

        var audio = root.AddChild("Audio");
        audio.AddProperty("Output Latency", Performance.GetMonitor(Performance.Monitor.AudioOutputLatency));

        return root;
    }
}
#endif
