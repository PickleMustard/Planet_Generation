namespace Structures.Enums;

/// <summary>
/// Defines the type of orbital transfer for a trajectory solution.
/// </summary>
public enum TransferType
{
    /// <summary>
    /// Direct transfer - single-arc Lambert solution with no complete revolutions.
    /// </summary>
    Direct,

    /// <summary>
    /// Multi-revolution transfer - Lambert solution with one or more complete orbital revolutions.
    /// </summary>
    MultiRev,

    /// <summary>
    /// Gravity assist transfer - trajectory utilizing gravitational slingshot maneuvers.
    /// </summary>
    GravityAssist
}
