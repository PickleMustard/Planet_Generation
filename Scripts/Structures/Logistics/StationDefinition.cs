using System.Collections.Generic;
using Structures.Resources;
using Structures.Transfers;

namespace Structures.Logistics;

/// <summary>
/// Represents a specific station definition from the configuration.
/// Used for loading station data from YAML configuration files.
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
    /// Whether this station can build ships.
    /// </summary>
    public bool CanBuildShips { get; set; }

    /// <summary>
    /// Maximum number of ships that can be built in parallel.
    /// Defaults to 1.
    /// </summary>
    public int MaxParallelShipBuilds { get; set; } = 1;

    /// <summary>
    /// Whether this station can construct buildings on celestial bodies.
    /// </summary>
    public bool CanBuildBuildings { get; set; }

    /// <summary>
    /// Per-slot work budget per tick. Each occupied slot (regular or overtime) advances
    /// its building's construction by this much work per tick.
    /// </summary>
    public float BuildingWorkBudgetPerTick { get; set; } = 1.0f;

    /// <summary>
    /// Number of regular construction slots. Constant for the life of the station.
    /// Regular slots consume the building's defined resource cost (no overtime multiplier).
    /// </summary>
    public int RegularSlots { get; set; } = 1;

    /// <summary>
    /// Initial number of overtime construction slots. Mutable at runtime via the
    /// architect behavior. Each occupied overtime slot raises the resource cost
    /// of every overtime build linearly with occupancy.
    /// </summary>
    public int OvertimeSlots { get; set; } = 0;

    /// <summary>
    /// Per-occupied-overtime-slot resource cost multiplier increment. With N overtime
    /// slots currently occupied, each overtime build's required resources are multiplied
    /// by (1 + N * OvertimeCostPercent). Regular slots are unaffected.
    /// </summary>
    public float OvertimeCostPercent { get; set; } = 0.10f;

    /// <summary>
    /// Dictionary of required resources for construction (resource name -> amount).
    /// </summary>
    public Dictionary<string, int> RequiredResources { get; set; } = new();

    /// <summary>Visual representation settings.</summary>
    public VisualDefinition Visual { get; set; } = new();

    /// <summary>Separate Icon property for 2D icons (distinct from 3D Visual).</summary>
    public IconDefinition Icon { get; set; } = new();

    /// <summary>
    /// Behavior class names or script paths parsed from the <c>behaviors:</c> YAML block.
    /// Station-specific counterpart of <see cref="BuildingDefinition.BehaviorEntries"/>.
    /// </summary>
    public Godot.Collections.Array<string> BehaviorRefs { get; set; } = new();

    /// <summary>
    /// Total number of bulk-storage slots this station exposes.
    /// Defaults to 0 (no storage).
    /// </summary>
    public int StorageCapacity { get; set; } = 0;

    /// <summary>
    /// Filter mix for the bulk-storage slots. The sum of <see cref="SlotFilterSpec.Count"/>
    /// values must be ≤ <see cref="StorageCapacity"/>; any remaining slots default to
    /// <see cref="SlotFilter.Any"/>.
    /// </summary>
    public List<SlotFilterSpec> SlotFilters { get; set; } = new();

    /// <summary>
    /// Transfer station parameters parsed from the <c>transfer_station:</c> YAML block.
    /// Null when the station has no transfer capability.
    /// </summary>
    public TransferStationDefinition? TransferStation { get; set; }
}
