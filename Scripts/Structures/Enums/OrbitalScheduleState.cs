namespace Structures.Enums;

/// <summary>
/// Defines the state of an orbital transfer schedule containing multiple legs.
/// </summary>
public enum OrbitalScheduleState
{
    /// <summary>
    /// Schedule is assigned but not yet started.
    /// </summary>
    Idle,

    /// <summary>
    /// Schedule is actively executing its current leg.
    /// </summary>
    Running,

    /// <summary>
    /// Waiting for the configured delay period between legs.
    /// </summary>
    WaitingBetweenLegs,

    /// <summary>
    /// All legs have completed (non-repeating schedule).
    /// </summary>
    Complete,

    /// <summary>
    /// A leg exhausted its max trajectory retries. Unit is stranded.
    /// </summary>
    Stranded,

    /// <summary>
    /// Schedule was manually stopped by the player. Can be resumed.
    /// </summary>
    Stopped,

    /// <summary>
    /// The unit is held by a Market Station for the duration of a sell/purchase cycle. The
    /// executor idles; the station drives the release via <c>ResumeAfterMarketHold</c>.
    /// </summary>
    Held
}
