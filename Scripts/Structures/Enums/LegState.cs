namespace Structures.Enums;

/// <summary>
/// Defines the execution state of a single orbital transfer leg.
/// </summary>
public enum LegState
{
    /// <summary>
    /// Leg has not started execution.
    /// </summary>
    Pending,

    /// <summary>
    /// Unit is refueling at the origin station.
    /// </summary>
    RefuelingAtOrigin,

    /// <summary>
    /// Unit is loading cargo from the origin station.
    /// </summary>
    LoadingCargo,

    /// <summary>
    /// Unit is calculating trajectory options within departure constraints.
    /// </summary>
    PlanningTrajectory,

    /// <summary>
    /// Trajectory planning failed, waiting before next retry attempt.
    /// </summary>
    WaitingForRetry,

    /// <summary>
    /// Unit is executing the transfer along the planned trajectory.
    /// </summary>
    InTransit,

    /// <summary>
    /// Unit is refueling at the destination station after arrival.
    /// </summary>
    RefuelingAtDestination,

    /// <summary>
    /// Unit is unloading cargo at the destination station.
    /// </summary>
    UnloadingCargo,

    /// <summary>
    /// Leg has completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// Leg failed (e.g., exceeded max trajectory retries).
    /// </summary>
    Failed
}
