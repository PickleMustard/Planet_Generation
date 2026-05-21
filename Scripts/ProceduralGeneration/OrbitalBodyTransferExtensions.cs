using System;
using System.Collections.Generic;
using System.Linq;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Godot;
using Structures.GameState;
using Structures.Transfers;

namespace ProceduralGeneration;

/// <summary>
/// Body-level aggregation helpers for transfer data. Since transfer state is now
/// fragmented across per-building <see cref="TransferStationBehavior"/> instances and
/// per-station TransferHubBehavior instances, these extension methods iterate the body's
/// endpoint registry and sum results on demand.
/// </summary>
public static class OrbitalBodyTransferExtensions
{
    /// <summary>
    /// Resolves an <see cref="IResourceEndpoint"/> from a registered endpoint owner.
    /// For Building owners, uses TransferStationBehavior. For StationSatellite owners,
    /// uses duck-typing to find a behavior with a ResourceEndpoint property (e.g. TransferHubBehavior).
    /// </summary>
    private static IResourceEndpoint? ResolveEndpoint(GodotObject owner, string id)
    {
        if (owner is Building building)
        {
            var behavior = building.GetBehavior<TransferStationBehavior>();
            return behavior?.ResourceEndpoint;
        }
        if (owner is StationSatellite station)
        {
            // Duck-type: find any behavior with a ResourceEndpoint property
            foreach (var behavior in station.Behaviors)
            {
                var prop = behavior.GetType().GetProperty("ResourceEndpoint");
                if (prop?.PropertyType == typeof(IResourceEndpoint))
                {
                    var endpoint = prop.GetValue(behavior) as IResourceEndpoint;
                    if (endpoint != null)
                        return endpoint;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the active transfer count for a registered endpoint, regardless of owner type.
    /// </summary>
    private static int GetActiveTransferCountForEndpoint(GodotObject owner, string id)
    {
        if (owner is Building building)
        {
            var behavior = building.GetBehavior<TransferStationBehavior>();
            return behavior?.GetActiveTransferCountForOrigin(id) ?? 0;
        }
        if (owner is StationSatellite station)
        {
            foreach (var behavior in station.Behaviors)
            {
                var method = behavior.GetType().GetMethod("GetActiveTransferCountForOrigin");
                if (method != null && method.ReturnType == typeof(int))
                {
                    return (int?)method.Invoke(behavior, new object[] { id }) ?? 0;
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// Gets schedules for a registered endpoint, regardless of owner type.
    /// </summary>
    private static IReadOnlyList<TransferSchedule> GetSchedulesForEndpoint(GodotObject owner, string id)
    {
        if (owner is Building building)
        {
            var behavior = building.GetBehavior<TransferStationBehavior>();
            return behavior?.GetSchedulesForOrigin(id) ?? Array.Empty<TransferSchedule>();
        }
        if (owner is StationSatellite station)
        {
            foreach (var behavior in station.Behaviors)
            {
                var method = behavior.GetType().GetMethod("GetSchedulesForOrigin");
                if (method != null)
                {
                    var result = method.Invoke(behavior, new object[] { id });
                    if (result is IReadOnlyList<TransferSchedule> schedules)
                        return schedules;
                }
            }
        }
        return Array.Empty<TransferSchedule>();
    }

    /// <summary>
    /// Total number of active (in-flight) transfers across all endpoints on the body.
    /// </summary>
    public static int GetTotalActiveTransfers(this IOrbitalBody body)
    {
        int total = 0;
        foreach (var id in body.GetAllTransferEndpoints())
        {
            var owner = body.GetTransferEndpointOwner(id);
            if (owner != null)
                total += GetActiveTransferCountForEndpoint(owner, id);
        }
        return total;
    }

    /// <summary>
    /// Total number of schedules across all endpoints on the body.
    /// </summary>
    public static int GetTotalScheduleCount(this IOrbitalBody body)
    {
        int total = 0;
        foreach (var id in body.GetAllTransferEndpoints())
        {
            var owner = body.GetTransferEndpointOwner(id);
            if (owner != null)
                total += GetSchedulesForEndpoint(owner, id).Count;
        }
        return total;
    }

    /// <summary>
    /// All active transfers originating from endpoints on the given continent.
    /// </summary>
    public static int GetActiveTransferCountOnContinent(this IOrbitalBody body, int continentIndex)
    {
        int total = 0;
        foreach (var id in body.GetTransferEndpointsOnContinent(continentIndex))
        {
            var owner = body.GetTransferEndpointOwner(id);
            if (owner != null)
                total += GetActiveTransferCountForEndpoint(owner, id);
        }
        return total;
    }

    /// <summary>
    /// All schedules originating from endpoints on the given continent.
    /// </summary>
    public static IReadOnlyList<TransferSchedule> GetSchedulesOnContinent(this IOrbitalBody body, int continentIndex)
    {
        var all = new List<TransferSchedule>();
        foreach (var id in body.GetTransferEndpointsOnContinent(continentIndex))
        {
            var owner = body.GetTransferEndpointOwner(id);
            if (owner != null)
                all.AddRange(GetSchedulesForEndpoint(owner, id));
        }
        return all;
    }

    /// <summary>
    /// Collects all schedules across every endpoint on the body.
    /// </summary>
    public static IReadOnlyList<TransferSchedule> GetAllSchedulesAcrossBody(this IOrbitalBody body)
    {
        var all = new List<TransferSchedule>();
        foreach (var id in body.GetAllTransferEndpoints())
        {
            var owner = body.GetTransferEndpointOwner(id);
            if (owner != null)
                all.AddRange(GetSchedulesForEndpoint(owner, id));
        }
        return all;
    }

    private static IReadOnlyCollection<TransferStationBehavior.ActiveTransfer> GetActiveTransfersForEndpoint(GodotObject owner, string id)
    {
        if (owner is Building building)
        {
            var behavior = building.GetBehavior<TransferStationBehavior>();
            return behavior?.GetActiveTransfers() ?? Array.Empty<TransferStationBehavior.ActiveTransfer>();
        }
        if (owner is StationSatellite station)
        {
            foreach (var behavior in station.Behaviors)
            {
                var method = behavior.GetType().GetMethod("GetActiveTransfers");
                if (method != null)
                {
                    var result = method.Invoke(behavior, Array.Empty<object>());
                    if (result is IReadOnlyCollection<TransferStationBehavior.ActiveTransfer> active)
                        return active;
                }
            }
        }
        return Array.Empty<TransferStationBehavior.ActiveTransfer>();
    }

    private static bool DestinationTargets(TransferDestination dest, string targetId)
    {
        if (dest.IsOrbitalStation)
            return dest.StationSatelliteId == targetId;
        return dest.BuildingId == targetId;
    }

    /// <summary>
    /// Aggregates schedules across the body whose destination is the given building/station id.
    /// Excludes schedules where origin equals destination (defensive — should not happen in practice).
    /// </summary>
    public static IReadOnlyList<TransferSchedule> GetInboundSchedulesForBuilding(this IOrbitalBody body, string buildingId)
    {
        var inbound = new List<TransferSchedule>();
        if (string.IsNullOrEmpty(buildingId))
            return inbound;

        foreach (var id in body.GetAllTransferEndpoints())
        {
            if (id == buildingId)
                continue;
            var owner = body.GetTransferEndpointOwner(id);
            if (owner == null)
                continue;
            foreach (var schedule in GetSchedulesForEndpoint(owner, id))
            {
                if (schedule.OriginBuildingId == buildingId)
                    continue;
                if (DestinationTargets(schedule.Destination, buildingId))
                    inbound.Add(schedule);
            }
        }
        return inbound;
    }

    /// <summary>
    /// Aggregates active transfers across the body whose destination is the given building/station id.
    /// </summary>
    public static IReadOnlyList<TransferStationBehavior.ActiveTransfer> GetInboundActiveTransfersForBuilding(this IOrbitalBody body, string buildingId)
    {
        var inbound = new List<TransferStationBehavior.ActiveTransfer>();
        if (string.IsNullOrEmpty(buildingId))
            return inbound;

        foreach (var id in body.GetAllTransferEndpoints())
        {
            if (id == buildingId)
                continue;
            var owner = body.GetTransferEndpointOwner(id);
            if (owner == null)
                continue;
            foreach (var transfer in GetActiveTransfersForEndpoint(owner, id))
            {
                var order = transfer.Order;
                if (order == null)
                    continue;
                if (order.OriginBuildingId == buildingId)
                    continue;
                if (DestinationTargets(order.Destination, buildingId))
                    inbound.Add(transfer);
            }
        }
        return inbound;
    }
}
