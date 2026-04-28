using System.Collections.Generic;
using Structures.Resources;

namespace Structures.Logistics;

/// <summary>
/// Represents a specific ship definition from the configuration.
/// Used for loading ship data from YAML configuration files.
/// </summary>
public class ShipDefinition
{
    /// <summary>
    /// The unique name of the ship.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The dry mass of the ship without fuel or cargo (in kg).
    /// </summary>
    public float DryMass { get; set; }

    /// <summary>
    /// Maximum cargo capacity (in kg or volume units).
    /// </summary>
    public float CargoCapacity { get; set; }

    /// <summary>
    /// Maximum fuel capacity (in kg or volume units).
    /// </summary>
    public float FuelCapacity { get; set; }

    /// <summary>
    /// The engine category used by this ship (e.g., "Chemical", "Nuclear", "Fusion").
    /// </summary>
    public string EngineCategory { get; set; } = string.Empty;

    /// <summary>
    /// Time required to construct the ship.
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
}
