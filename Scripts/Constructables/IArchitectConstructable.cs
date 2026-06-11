using System.Collections.Generic;

namespace Constructables;

/// <summary>
/// The construction surface an <see cref="Constructables.Stations.Behaviors.OrbitalConstructorBehavior"/>
/// architect drives, independent of what is being built. Both <see cref="Building"/> and
/// <see cref="Structures.Logistics.LinkConstructionJob"/> implement it so they can share the same
/// per-body architect slots/queue via <see cref="Buildings.BuildingConstructionManager"/>.
///
/// All members already existed on <see cref="Building"/>; this interface only names the subset the
/// architect tick loop touches. The thread-safe Snapshot/Replace variants exist because the
/// architect ticks on a background thread and must avoid the Godot Variant round-trip.
/// </summary>
public interface IArchitectConstructable
{
    /// <summary>Display name, used in logs and progress updates.</summary>
    string Name { get; }

    /// <summary>The architect station currently constructing this, or null.</summary>
    StationSatellite? ConstructingStation { get; set; }

    /// <summary>Plain-dictionary snapshot of remaining required resources (thread-safe read).</summary>
    Dictionary<string, int> SnapshotRequiredResources();

    /// <summary>Plain-dictionary snapshot of already-delivered resources (thread-safe read).</summary>
    Dictionary<string, int> SnapshotAvailableResources();

    /// <summary>Replaces the required-resource set without the Godot.Collections bridge.</summary>
    void ReplaceRequiredResources(Dictionary<string, int> values);

    /// <summary>Delivers resources to the construction site.</summary>
    void DeliverResources(string resourceId, int amount);

    /// <summary>True once every required resource has been delivered.</summary>
    bool CheckRequiredResourcesAvailable();

    /// <summary>Advances construction by the given work delta.</summary>
    void UpdateProgress(float delta);

    /// <summary>Construction status name (matches <see cref="Structures.Enums.ConstructionStatus"/>).</summary>
    string GetStatus();

    /// <summary>Normalized construction progress [0, 1].</summary>
    float GetProgress();
}
