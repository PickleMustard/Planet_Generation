using System;
using System.Collections.Generic;
using Structures.Enums;

namespace Structures.Transfers;

/// <summary>
/// Defines a recurring transfer schedule that automatically dispatches transfers
/// when departure conditions are met.
/// </summary>
public class TransferSchedule
{
    /// <summary>
    /// Unique identifier for this schedule.
    /// </summary>
    public string ScheduleId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Index of the origin continent.
    /// </summary>
    public int OriginContinentIndex { get; set; }

    /// <summary>
    /// The destination for transfers created by this schedule.
    /// </summary>
    public TransferDestination Destination { get; set; } = null!;

    /// <summary>
    /// Resource proportions defining how cargo capacity is allocated.
    /// Maps resource ID to proportion of total capacity (0.0 to 1.0).
    /// Sum of all proportions must be &lt;= 1.0.
    /// </summary>
    public Dictionary<string, float> ResourceProportions { get; set; } = new();

    /// <summary>
    /// Whether departure is triggered when any single resource or all resources meet the threshold.
    /// </summary>
    public DepartureConditionMode DepartureMode { get; set; } = DepartureConditionMode.AllResources;

    /// <summary>
    /// The fill threshold that triggers departure.
    /// </summary>
    public DepartureThreshold Threshold { get; set; } = DepartureThreshold.Full;

    /// <summary>
    /// Current state of this schedule.
    /// </summary>
    public TransferScheduleState State { get; set; } = TransferScheduleState.Idle;

    /// <summary>
    /// The order ID of the currently in-flight transfer, if any.
    /// Null when no transfer is active.
    /// </summary>
    public string? ActiveTransferOrderId { get; set; }
}
