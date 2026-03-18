namespace Structures.Enums;

/// <summary>
/// Defines the possible states of a logistics unit.
/// </summary>
public enum LogisticsUnitState
{
    /// <summary>
    /// No active trajectory - the unit is stationary with no planned movement.
    /// </summary>
    Idle,

    /// <summary>
    /// Calculating route options - the unit is determining possible paths to its destination.
    /// </summary>
    Planning,

    /// <summary>
    /// Executing transfer - the unit is actively moving along its planned trajectory.
    /// </summary>
    InTransit,

    /// <summary>
    /// Approaching destination - the unit is nearing its final destination.
    /// </summary>
    Arriving,

    /// <summary>
    /// Cannot move - the unit is disabled and unable to move.
    /// </summary>
    Disabled
}
