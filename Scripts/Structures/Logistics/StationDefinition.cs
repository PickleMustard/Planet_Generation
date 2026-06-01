using System.Collections.Generic;
using System.Linq;
using Structures.Resources;
using Structures.Transfers;

namespace Structures.Logistics;

/// <summary>
/// Represents a specific station definition from the configuration.
/// Used for loading station data from YAML configuration files.
/// Behavior-specific configuration lives inside <see cref="BehaviorEntries"/>
/// as inline config dicts, mirroring <see cref="BuildingDefinition.BehaviorEntries"/>.
/// </summary>
public class StationDefinition
{
    /// <summary>
    /// The unique name of the station.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type/category of the station (e.g., "Shipyard", "Industrial", "Research").
    /// </summary>
    public string StationType { get; set; } = string.Empty;

    /// <summary>
    /// Time required to construct the station (in game ticks or seconds).
    /// </summary>
    public float ConstructionTime { get; set; }

    /// <summary>
    /// Dictionary of required resources for construction (resource name -> amount).
    /// </summary>
    public Dictionary<string, int> RequiredResources { get; set; } = new();

    /// <summary>Visual representation settings.</summary>
    public VisualDefinition Visual { get; set; } = new();

    /// <summary>Separate Icon property for 2D icons (distinct from 3D Visual).</summary>
    public IconDefinition Icon { get; set; } = new();

    /// <summary>
    /// Inline behavior entries parsed from YAML. Each entry pairs a behavior ID
    /// with an optional config dict. The factory calls IStationBehaviorConfigurable.Configure()
    /// when config is non-empty.
    /// </summary>
    public List<StationBehaviorConfigEntry> BehaviorEntries { get; set; } = new();

    /// <summary>
    /// Returns true if this definition includes a behavior with the given ID.
    /// </summary>
    public bool HasBehavior(string behaviorId) =>
        BehaviorEntries.Any(e => e.BehaviorId == behaviorId);

    /// <summary>
    /// Pairs a behavior class name with an inline config dict parsed from the
    /// behaviors: section of the station YAML. When config is non-empty, the
    /// factory will call IStationBehaviorConfigurable.Configure() before OnAttach().
    /// </summary>
    public class StationBehaviorConfigEntry
    {
        public string BehaviorId { get; set; } = "";
        public Dictionary<string, object> Config { get; set; } = new();
    }
}
