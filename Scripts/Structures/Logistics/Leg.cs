using System;
using Structures.Enums;
using Structures.Resources;

namespace Structures.Logistics;

/// <summary>
/// Defines a single orbital transfer between two endpoints.
/// Contains both the leg configuration (endpoints, cargo orders, constraints)
/// and runtime execution state managed by the OrbitalScheduleExecutor.
/// </summary>
public class Leg
{
    /// <summary>
    /// Unique identifier for this leg.
    /// </summary>
    public string LegId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The origin endpoint. If a station, cargo pickup and refueling are available.
    /// </summary>
    public LegEndpoint Origin { get; set; } = null!;

    /// <summary>
    /// The destination endpoint. If a station, cargo dropoff and refueling are available.
    /// </summary>
    public LegEndpoint Destination { get; set; } = null!;

    /// <summary>
    /// Resources to load from the origin station's economy.
    /// Null if no pickup is needed or origin is not a station.
    /// </summary>
    public CargoManifest? PickupOrder { get; set; }

    /// <summary>
    /// Resources to unload to the destination station's economy.
    /// Null if no dropoff is needed or destination is not a station.
    /// </summary>
    public CargoManifest? DropoffOrder { get; set; }

    /// <summary>
    /// Expenditure constraints for trajectory planning (TOF or DeltaV bounds).
    /// </summary>
    public DepartureConstraints DepartureConstraints { get; set; } = new();

    /// <summary>
    /// Refueling instructions for this leg.
    /// </summary>
    public RefuelInstructions RefuelInstructions { get; set; } = new();

    /// <summary>
    /// Maximum seconds the unit may wait at the origin before it must depart on
    /// this leg. Null means no forced-departure deadline.
    /// </summary>
    public float? MaxWaitSeconds { get; set; }

    /// <summary>
    /// True when this leg is the auto-generated closing leg that returns the unit
    /// from the final destination back to the schedule's starting origin so a
    /// repeating schedule can loop. Closing legs are managed by
    /// <see cref="OrbitalScheduleEditor"/> and are not directly editable.
    /// </summary>
    public bool IsClosingLeg { get; set; }

    // --- Runtime state (managed by OrbitalScheduleExecutor) ---

    /// <summary>
    /// Current execution state of this leg.
    /// </summary>
    public LegState State { get; set; } = LegState.Pending;

    /// <summary>
    /// The trajectory selected for this leg after planning. Null until planning succeeds.
    /// </summary>
    public TrajectorySolution? SelectedTrajectory { get; set; }

    /// <summary>
    /// Number of trajectory planning attempts made for this leg.
    /// </summary>
    public int TrajectoryAttempts { get; set; }

    /// <summary>
    /// Resets runtime state for re-execution (e.g., when a repeating schedule loops).
    /// </summary>
    public void ResetRuntimeState()
    {
        State = LegState.Pending;
        SelectedTrajectory = null;
        TrajectoryAttempts = 0;
    }
}
