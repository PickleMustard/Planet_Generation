using Structures.Enums;

namespace Structures.Logistics;

/// <summary>
/// Defines refueling behavior for a leg, including when to refuel,
/// what fuel type to use, and how much to load.
/// </summary>
public class RefuelInstructions
{
    /// <summary>
    /// When to refuel: at origin, destination, both, or not at all.
    /// </summary>
    public RefuelPolicy Policy { get; set; } = RefuelPolicy.None;

    /// <summary>
    /// Resource ID of the fuel to withdraw from station economy.
    /// </summary>
    public string FuelResourceId { get; set; } = "Fuel";

    /// <summary>
    /// Amount of fuel to load. Use float.MaxValue to fill to capacity.
    /// </summary>
    public float Amount { get; set; } = float.MaxValue;

    /// <summary>
    /// Whether refueling should occur at the origin station.
    /// </summary>
    public bool RefuelAtOrigin => Policy == RefuelPolicy.AtOrigin || Policy == RefuelPolicy.AtBoth;

    /// <summary>
    /// Whether refueling should occur at the destination station.
    /// </summary>
    public bool RefuelAtDestination => Policy == RefuelPolicy.AtDestination || Policy == RefuelPolicy.AtBoth;
}
