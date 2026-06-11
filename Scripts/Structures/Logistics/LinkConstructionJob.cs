using System.Collections.Generic;
using Constructables;
using Structures.Enums;
using UtilityLibrary;

namespace Structures.Logistics;

/// <summary>
/// Construction job that builds a single dormant <see cref="ResourceLink"/> through the shared
/// Orbital Architect pipeline. Wraps a <see cref="ConstructionState"/> (work + required resources)
/// and exposes the <see cref="IArchitectConstructable"/> surface the architect drives, so a link
/// competes for architect slots exactly like a building. When the architect finishes the work the
/// wrapped link is activated and begins transporting.
///
/// Delegation mirrors <see cref="Building"/>'s IConstructable wrappers. Unlike a building, a link
/// job is not routed through <see cref="ConstructionManager"/> on completion — it activates its own
/// link inside <see cref="UpdateProgress"/> (the architect skips the ConstructionManager notify for
/// non-IConstructable jobs).
/// </summary>
public sealed class LinkConstructionJob : IArchitectConstructable
{
    private readonly ResourceLink _link;
    private readonly ConstructionState _state;

    /// <summary>The link this job builds. Dormant until construction completes.</summary>
    public ResourceLink Link => _link;

    public LinkConstructionJob(
        ResourceLink link,
        float workRequired,
        Dictionary<string, int>? requiredResources)
    {
        _link = link;
        _state = new ConstructionState(workRequired, requiredResources);
        Name = BuildName(link);
        // Move out of Pending immediately (InProgress if no resources required, else Blocked),
        // matching Building.StartConstruction so the architect's delivery loop can drive it.
        _state.TryStart();
    }

    public string Name { get; }

    public StationSatellite? ConstructingStation { get; set; }

    public Dictionary<string, int> SnapshotRequiredResources()
    {
        var dict = new Dictionary<string, int>();
        foreach (var kvp in _state.RequiredResources)
            dict[kvp.Key] = kvp.Value;
        return dict;
    }

    public Dictionary<string, int> SnapshotAvailableResources()
    {
        var dict = new Dictionary<string, int>();
        foreach (var kvp in _state.AvailableResources)
            dict[kvp.Key] = kvp.Value;
        return dict;
    }

    public void ReplaceRequiredResources(Dictionary<string, int> values)
    {
        _state.RequiredResources.Clear();
        foreach (var kvp in values)
            _state.RequiredResources[kvp.Key] = kvp.Value;
    }

    public void DeliverResources(string resourceId, int amount) =>
        _state.DeliverResources(resourceId, amount);

    public bool CheckRequiredResourcesAvailable() => _state.CheckRequiredResourcesAvailable();

    public string GetStatus() => _state.Status.ToString();

    public float GetProgress() => _state.GetProgress();

    public void UpdateProgress(float delta)
    {
        _state.UpdateProgress(delta);

        if (_state.StatusChanged && _state.Status == ConstructionStatus.Complete)
        {
            _link.Activate();
            GameLogger.Info($"LinkConstructionJob: '{Name}' construction complete; link activated.");
        }
    }

    private static string BuildName(ResourceLink link)
    {
        string source = link.Source?.Owner?.Name ?? "?";
        string target = link.Target?.Owner?.Name ?? "?";
        return $"Link {source}->{target}";
    }
}
