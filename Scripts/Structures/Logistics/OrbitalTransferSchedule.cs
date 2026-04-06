using System;
using System.Collections.Generic;
using Structures.Enums;

namespace Structures.Logistics;

/// <summary>
/// Defines a sequence of orbital transfer legs to be executed by a logistics unit.
/// Supports configurable wait periods between legs, trajectory retry behavior,
/// and optional repeating execution.
/// </summary>
public class OrbitalTransferSchedule
{
    /// <summary>
    /// Unique identifier for this schedule.
    /// </summary>
    public string ScheduleId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Ordered list of legs to execute.
    /// </summary>
    public List<Leg> Legs { get; set; } = new();

    /// <summary>
    /// Seconds to wait between completing one leg and starting the next.
    /// </summary>
    public float WaitPeriodBetweenLegs { get; set; } = 0f;

    /// <summary>
    /// Seconds to wait before retrying trajectory planning after a failure.
    /// </summary>
    public float RetryPeriod { get; set; } = 60f;

    /// <summary>
    /// Maximum number of trajectory planning retries per leg before stranding.
    /// </summary>
    public int MaxRetries { get; set; } = 10;

    /// <summary>
    /// Whether the schedule loops back to the first leg after completing all legs.
    /// </summary>
    public bool IsRepeating { get; set; }

    // --- Runtime state (managed by OrbitalScheduleExecutor) ---

    /// <summary>
    /// Current execution state of the schedule.
    /// </summary>
    public OrbitalScheduleState State { get; set; } = OrbitalScheduleState.Idle;

    /// <summary>
    /// Index of the leg currently being executed.
    /// </summary>
    public int CurrentLegIndex { get; set; }

    /// <summary>
    /// The leg currently being executed, or null if index is out of range.
    /// </summary>
    public Leg? CurrentLeg => CurrentLegIndex >= 0 && CurrentLegIndex < Legs.Count
        ? Legs[CurrentLegIndex]
        : null;

    /// <summary>
    /// Resets all legs' runtime state and sets the schedule back to the first leg.
    /// Used when a repeating schedule loops.
    /// </summary>
    public void ResetForRepeat()
    {
        CurrentLegIndex = 0;
        foreach (var leg in Legs)
        {
            leg.ResetRuntimeState();
        }
    }
}
