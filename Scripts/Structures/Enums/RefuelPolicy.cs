namespace Structures.Enums;

/// <summary>
/// Defines when a logistics unit should refuel during a leg.
/// </summary>
public enum RefuelPolicy
{
    /// <summary>
    /// No refueling at either endpoint.
    /// </summary>
    None,

    /// <summary>
    /// Refuel at the origin station before departure.
    /// </summary>
    AtOrigin,

    /// <summary>
    /// Refuel at the destination station after arrival.
    /// </summary>
    AtDestination,

    /// <summary>
    /// Refuel at both origin and destination stations.
    /// </summary>
    AtBoth
}
