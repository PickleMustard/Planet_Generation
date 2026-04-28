using System.Collections.Generic;
using Structures.Resources;

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
    /// Amount of work budget available per tick for building construction.
    /// Defaults to 1.0.
    /// </summary>
    public float BuildingWorkBudgetPerTick { get; set; } = 1.0f;

    /// <summary>
    /// Penalty factor for building multiple buildings simultaneously.
    /// Defaults to 0.05 (5% penalty per additional building).
    /// </summary>
    public float BuildingScalingPenalty { get; set; } = 0.05f;

    /// <summary>
    /// Dictionary of required resources for construction (resource name -> amount).
    /// </summary>
    public Dictionary<string, int> RequiredResources { get; set; } = new();

    /// <summary>Visual representation settings.</summary>
    public VisualDefinition Visual { get; set; } = new();

    /// <summary>Separate Icon property for 2D icons (distinct from 3D Visual).</summary>
    public IconDefinition Icon { get; set; } = new();
}
