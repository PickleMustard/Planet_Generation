using System;
using Structures.Enums;
using Structures.Resources;

namespace Structures.Transfers;

/// <summary>
/// Represents a single resource transfer between an origin and destination.
/// Created for both one-time orders and schedule-dispatched transfers.
/// </summary>
public class TransferOrder
{
    /// <summary>
    /// Unique identifier for this transfer order.
    /// </summary>
    public string OrderId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Building id of the origin transfer-station building.
    /// </summary>
    public string OriginBuildingId { get; set; } = "";

    /// <summary>
    /// The destination for this transfer.
    /// </summary>
    public TransferDestination Destination { get; set; } = null!;

    /// <summary>
    /// The actual resources loaded onto the vehicle (after withdraw from origin).
    /// May be less than RequestedManifest if stockpile was insufficient.
    /// </summary>
    public CargoManifest Manifest { get; set; } = new();

    /// <summary>
    /// The resources originally requested for this transfer.
    /// </summary>
    public CargoManifest RequestedManifest { get; set; } = new();

    /// <summary>
    /// Current state of this transfer.
    /// </summary>
    public SurfaceTransferState State { get; set; } = SurfaceTransferState.Loading;

    /// <summary>
    /// Total travel time in seconds for this transfer.
    /// </summary>
    public float TravelTimeSeconds { get; set; }

    /// <summary>
    /// Time elapsed since the transfer began transit.
    /// </summary>
    public float ElapsedTimeSeconds { get; set; }

    /// <summary>
    /// Game time when the transfer was dispatched.
    /// </summary>
    public double DispatchedAtTime { get; set; }

    /// <summary>
    /// If this transfer was spawned by a schedule, the schedule's ID. Null for one-time orders.
    /// </summary>
    public string? SourceScheduleId { get; set; }

    /// <summary>
    /// Normalized progress (0 to 1) of the transfer.
    /// </summary>
    public float Progress => TravelTimeSeconds > 0f ? Math.Clamp(ElapsedTimeSeconds / TravelTimeSeconds, 0f, 1f) : 1f;

    /// <summary>
    /// Whether the transfer has completed its travel.
    /// </summary>
    public bool IsTransitComplete => ElapsedTimeSeconds >= TravelTimeSeconds;
}
