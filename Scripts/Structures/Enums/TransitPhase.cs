namespace Structures.Enums;

/// <summary>
/// Defines the current burn phase of a logistics unit during an orbital transfer.
/// Tracked as a sub-state by the movement controller while the unit is InTransit.
/// </summary>
public enum TransitPhase
{
    /// <summary>
    /// Departure burn active — engine is firing to accelerate onto the transfer orbit.
    /// Fuel is being consumed at the acceleration fuel rate.
    /// </summary>
    Accelerating,

    /// <summary>
    /// Engine off — the unit is following a ballistic arc (Kepler propagation).
    /// No fuel is consumed during this phase.
    /// </summary>
    Coasting,

    /// <summary>
    /// Arrival burn active — engine is firing to decelerate into the destination orbit.
    /// Fuel is being consumed at the deceleration fuel rate.
    /// </summary>
    Decelerating
}
