using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.Resources;
using Structures.Transfers;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Per-body manager that handles all surface resource transfers between transfer-station
/// buildings on the same orbital body. Endpoints are buildings: each transfer-station
/// registers itself via <see cref="RegisterEndpoint"/> from <c>TransferStationBehavior</c>
/// and exposes its <see cref="Building.BulkStorage"/> through an <see cref="IResourceEndpoint"/>
/// adapter. All transfers are intra-body only; inter-body movement uses the logistics
/// (orbital) system.
/// </summary>
public partial class BodyTransferManager : Node
{
    private readonly Dictionary<string, ActiveTransfer> _activeTransfers = new();

    // Single endpoint registry keyed by Building.Id (or StationSatellite.Id when an
    // orbital station starts registering — both are GUIDs so collisions are not a
    // concern in practice).
    private readonly Dictionary<string, IResourceEndpoint> _endpoints = new();
    private readonly Dictionary<string, TransferStationDefinition> _endpointDefs = new();
    private readonly Dictionary<string, Building> _endpointBuildings = new();

    // Schedules grouped by their origin endpoint id.
    private readonly Dictionary<string, List<TransferSchedule>> _schedulesByOrigin = new();

    private double _totalTime;

    public int ActiveTransferCount => _activeTransfers.Count;

    /// <summary>
    /// Returns all currently active (in-flight) transfers.
    /// </summary>
    public IReadOnlyCollection<ActiveTransfer> GetActiveTransfers() => _activeTransfers.Values;

    /// <summary>
    /// Returns all transfer schedules across all origins on this body.
    /// </summary>
    public IReadOnlyList<TransferSchedule> GetAllSchedules()
    {
        var all = new List<TransferSchedule>();
        foreach (var kvp in _schedulesByOrigin)
            all.AddRange(kvp.Value);
        return all;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _totalTime += delta;

        TickActiveTransfers(dt);
        TickSchedules(dt);
    }

    #region Endpoint Registration

    /// <summary>
    /// Registers a transfer-station building as a transfer endpoint. Called from
    /// <see cref="Buildings.Behaviors.TransferStationBehavior.OnRegister"/>.
    /// </summary>
    public void RegisterEndpoint(
        string endpointId,
        IResourceEndpoint endpoint,
        TransferStationDefinition? definition,
        Building? sourceBuilding
    )
    {
        if (string.IsNullOrEmpty(endpointId))
            return;

        _endpoints[endpointId] = endpoint;
        if (definition != null)
            _endpointDefs[endpointId] = definition;
        if (sourceBuilding != null)
            _endpointBuildings[endpointId] = sourceBuilding;

        SignalBus.Instance?.EmitContinentTransferCapacityChanged(
            sourceBuilding?.PrimaryCell?.ContinentIndex ?? -1,
            GetTotalCapacityOnContinent(sourceBuilding?.PrimaryCell?.ContinentIndex ?? -1)
        );

        GameLogger.Info(
            $"[BodyTransferManager] Endpoint '{endpointId[..System.Math.Min(8, endpointId.Length)]}' "
                + $"registered (capacity: {definition?.CargoCapacity ?? 0f:F0})"
        );
    }

    /// <summary>
    /// Unregisters an endpoint. Schedules originating at this endpoint are stopped;
    /// in-flight orders remain alive — their origin lookup at completion will return
    /// null and the existing fallback in <see cref="CompleteTransfer"/> handles that
    /// (logs lost cargo).
    /// </summary>
    public void UnregisterEndpoint(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return;

        int? continentIdx = _endpointBuildings.TryGetValue(endpointId, out var b)
            ? b.PrimaryCell?.ContinentIndex
            : null;

        StopAllSchedulesForOrigin(endpointId);

        _endpoints.Remove(endpointId);
        _endpointDefs.Remove(endpointId);
        _endpointBuildings.Remove(endpointId);

        if (continentIdx.HasValue)
            SignalBus.Instance?.EmitContinentTransferCapacityChanged(
                continentIdx.Value,
                GetTotalCapacityOnContinent(continentIdx.Value)
            );
    }

    #endregion

    #region Endpoint Queries

    /// <summary>
    /// Whether the given building id has a registered transfer-station endpoint.
    /// </summary>
    public bool HasEndpoint(string endpointId)
    {
        return !string.IsNullOrEmpty(endpointId) && _endpoints.ContainsKey(endpointId);
    }

    /// <summary>
    /// Returns the transfer-station building associated with an endpoint id, or null.
    /// </summary>
    public Building? GetEndpointBuilding(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return null;
        _endpointBuildings.TryGetValue(endpointId, out var b);
        return b;
    }

    /// <summary>
    /// Cargo capacity of a single endpoint (a single hub is the dispatch unit).
    /// </summary>
    public float GetCapacity(string endpointId)
    {
        if (_endpointDefs.TryGetValue(endpointId, out var def))
            return def.CargoCapacity;
        return 0f;
    }

    /// <summary>
    /// Max simultaneous in-flight transfers from a single endpoint.
    /// </summary>
    public int GetMaxConcurrentTransfers(string endpointId)
    {
        if (_endpointDefs.TryGetValue(endpointId, out var def))
            return def.MaxConcurrentTransfers;
        return 0;
    }

    /// <summary>
    /// Vehicle speed for a single endpoint.
    /// </summary>
    public float GetVehicleSpeed(string endpointId)
    {
        if (_endpointDefs.TryGetValue(endpointId, out var def))
            return def.VehicleSpeed;
        return 0f;
    }

    /// <summary>
    /// Counts in-flight transfers originating from a single endpoint.
    /// </summary>
    public int GetActiveTransferCountForOrigin(string endpointId)
    {
        if (string.IsNullOrEmpty(endpointId))
            return 0;
        int count = 0;
        foreach (var kvp in _activeTransfers)
        {
            if (kvp.Value.Order.OriginBuildingId == endpointId)
                count++;
        }
        return count;
    }

    /// <summary>
    /// All registered endpoint ids whose backing building lives on the given continent.
    /// </summary>
    public IReadOnlyList<string> GetEndpointsOnContinent(int continentIndex)
    {
        var list = new List<string>();
        foreach (var kvp in _endpointBuildings)
        {
            if (kvp.Value.PrimaryCell?.ContinentIndex == continentIndex)
                list.Add(kvp.Key);
        }
        return list;
    }

    /// <summary>
    /// Sums the cargo capacity across every endpoint on a continent.
    /// </summary>
    public float GetTotalCapacityOnContinent(int continentIndex)
    {
        if (continentIndex < 0)
            return 0f;
        float total = 0f;
        foreach (var id in GetEndpointsOnContinent(continentIndex))
            total += GetCapacity(id);
        return total;
    }

    #endregion

    #region One-Time Transfers

    /// <summary>
    /// Dispatches a one-time transfer from the given origin endpoint to a destination.
    /// Resources are withdrawn from the origin's bulk storage immediately and deposited
    /// at the destination on arrival.
    /// </summary>
    /// <returns>The order ID if successful, or null if validation failed.</returns>
    public string? DispatchOneTimeTransfer(
        string originBuildingId,
        TransferDestination destination,
        Dictionary<string, float> requestedResources
    )
    {
        if (!HasEndpoint(originBuildingId))
        {
            GameLogger.Warning(
                $"[BodyTransferManager] Cannot dispatch: origin '{originBuildingId}' has no registered endpoint"
            );
            return null;
        }

        int activeCount = GetActiveTransferCountForOrigin(originBuildingId);
        int maxConcurrent = GetMaxConcurrentTransfers(originBuildingId);
        if (activeCount >= maxConcurrent)
        {
            GameLogger.Warning(
                $"[BodyTransferManager] Cannot dispatch: origin '{originBuildingId}' "
                    + $"at max concurrent transfers ({activeCount}/{maxConcurrent})"
            );
            return null;
        }

        if (!_endpoints.TryGetValue(originBuildingId, out var originEndpoint))
        {
            GameLogger.Warning(
                $"[BodyTransferManager] Cannot dispatch: no endpoint for '{originBuildingId}'"
            );
            return null;
        }

        IResourceEndpoint? destEndpoint = ResolveEndpoint(destination);
        if (destEndpoint == null)
        {
            GameLogger.Warning(
                $"[BodyTransferManager] Cannot dispatch: destination {destination} not found on this body"
            );
            return null;
        }

        float travelTime = ComputeTravelTime(originBuildingId, destination);
        if (travelTime <= 0f)
        {
            GameLogger.Warning("[BodyTransferManager] Cannot dispatch: invalid travel time");
            return null;
        }

        float totalCapacity = GetCapacity(originBuildingId);
        var manifest = new CargoManifest();
        var requestedManifest = new CargoManifest();
        float usedCapacity = 0f;

        foreach (var kvp in requestedResources)
        {
            string resourceId = kvp.Key;
            float requestedAmount = kvp.Value;
            if (requestedAmount <= 0f)
                continue;

            requestedManifest.LoadResource(resourceId, requestedAmount);

            float weight = GetTransportWeight(resourceId);

            float remainingCapacity = totalCapacity - usedCapacity;
            float maxUnits = remainingCapacity / weight;
            float toLoad = Math.Min(requestedAmount, maxUnits);

            if (toLoad <= 0f)
                continue;

            float actualWithdrawn = originEndpoint.WithdrawResource(resourceId, toLoad);
            if (actualWithdrawn > 0f)
            {
                manifest.LoadResource(resourceId, actualWithdrawn);
                usedCapacity += actualWithdrawn * weight;
            }
        }

        if (manifest.TotalUnits <= 0f)
        {
            GameLogger.Warning(
                "[BodyTransferManager] Cannot dispatch: no resources available to load"
            );
            return null;
        }

        var order = new TransferOrder
        {
            OriginBuildingId = originBuildingId,
            Destination = destination,
            Manifest = manifest,
            RequestedManifest = requestedManifest,
            State = SurfaceTransferState.InTransit,
            TravelTimeSeconds = travelTime,
            ElapsedTimeSeconds = 0f,
            DispatchedAtTime = _totalTime,
        };

        var activeTransfer = new ActiveTransfer { Order = order };
        _activeTransfers[order.OrderId] = activeTransfer;

        // Continent index emitted with the dispatch signal for legacy UI listeners.
        int continentIdx =
            _endpointBuildings.TryGetValue(originBuildingId, out var ob)
                ? ob.PrimaryCell?.ContinentIndex ?? -1
                : -1;
        SignalBus.Instance?.EmitTransferDispatched(order.OrderId, continentIdx);

        GameLogger.Info(
            $"[BodyTransferManager] Dispatched transfer {order.OrderId[..8]}... "
                + $"from '{originBuildingId[..System.Math.Min(8, originBuildingId.Length)]}' to {destination} "
                + $"({manifest.TotalUnits:F1} units, ETA {travelTime:F1}s)"
        );

        return order.OrderId;
    }

    #endregion

    #region Travel Time

    /// <summary>
    /// Computes travel time between an origin endpoint and a destination.
    /// </summary>
    public float ComputeTravelTime(string originBuildingId, TransferDestination destination)
    {
        float speed = GetVehicleSpeed(originBuildingId);
        if (speed <= 0f)
            return 0f;

        float distance = ComputeDistance(originBuildingId, destination);
        if (distance <= 0f)
            return 0f;

        return distance / speed;
    }

    private float ComputeDistance(string originBuildingId, TransferDestination destination)
    {
        // TODO: great-circle distance between origin and destination primary cells.
        if (destination.IsOrbitalStation)
        {
            return 100f;
        }

        if (!string.IsNullOrEmpty(destination.BuildingId))
        {
            // Surface-to-surface: stub distance until great-circle is implemented.
            return 100f;
        }

        return 0f;
    }

    #endregion

    #region Tick Logic

    private void TickActiveTransfers(float delta)
    {
        if (_activeTransfers.Count == 0)
            return;

        var completedIds = new List<string>();

        foreach (var kvp in _activeTransfers)
        {
            var transfer = kvp.Value;
            var order = transfer.Order;

            if (order.State == SurfaceTransferState.InTransit)
            {
                order.ElapsedTimeSeconds += delta;

                if (order.IsTransitComplete)
                {
                    CompleteTransfer(transfer);
                    completedIds.Add(kvp.Key);
                }
            }
            else if (
                order.State == SurfaceTransferState.Complete
                || order.State == SurfaceTransferState.Reverting
            )
            {
                completedIds.Add(kvp.Key);
            }
        }

        foreach (var id in completedIds)
        {
            _activeTransfers.Remove(id);
        }
    }

    private void CompleteTransfer(ActiveTransfer transfer)
    {
        var order = transfer.Order;
        order.State = SurfaceTransferState.Unloading;

        IResourceEndpoint? destEndpoint = ResolveEndpoint(order.Destination);
        IResourceEndpoint? originEndpoint = _endpoints.TryGetValue(
            order.OriginBuildingId,
            out var ep
        )
            ? ep
            : null;

        float totalReverted = 0f;

        foreach (var kvp in order.Manifest.Resources)
        {
            string resourceId = kvp.Key;
            float amount = kvp.Value;

            if (destEndpoint != null)
            {
                float deposited = destEndpoint.DepositResource(resourceId, amount);
                float remainder = amount - deposited;

                if (remainder > 0f)
                {
                    if (originEndpoint != null)
                    {
                        float reverted = originEndpoint.DepositResource(resourceId, remainder);
                        float lost = remainder - reverted;
                        totalReverted += reverted;

                        if (lost > 0f)
                        {
                            GameLogger.Warning(
                                $"[BodyTransferManager] {lost:F1} units of '{resourceId}' "
                                    + $"lost (both destination and origin full)"
                            );
                        }
                    }
                    else
                    {
                        GameLogger.Warning(
                            $"[BodyTransferManager] {remainder:F1} units of '{resourceId}' "
                                + $"lost (origin endpoint gone)"
                        );
                    }
                }
            }
            else
            {
                if (originEndpoint != null)
                {
                    float reverted = originEndpoint.DepositResource(resourceId, amount);
                    totalReverted += reverted;
                }
                else
                {
                    GameLogger.Warning(
                        $"[BodyTransferManager] {amount:F1} units of '{resourceId}' "
                            + $"lost (destination and origin endpoints both gone)"
                    );
                }
            }
        }

        bool fullyAccepted = totalReverted <= 0f;
        order.State = SurfaceTransferState.Complete;

        SignalBus.Instance?.EmitTransferArrived(order.OrderId, fullyAccepted);

        if (totalReverted > 0f)
        {
            int continentIdx =
                _endpointBuildings.TryGetValue(order.OriginBuildingId, out var ob)
                    ? ob.PrimaryCell?.ContinentIndex ?? -1
                    : -1;
            SignalBus.Instance?.EmitTransferReverted(order.OrderId, continentIdx, totalReverted);
        }

        GameLogger.Info(
            $"[BodyTransferManager] Transfer {order.OrderId[..8]}... completed. "
                + $"Accepted: {fullyAccepted}, Reverted: {totalReverted:F1}"
        );
    }

    private void TickSchedules(float delta)
    {
        foreach (var kvp in _schedulesByOrigin)
        {
            string originId = kvp.Key;
            var schedules = kvp.Value;

            for (int i = 0; i < schedules.Count; i++)
            {
                var schedule = schedules[i];
                TickSchedule(schedule, originId);
            }
        }
    }

    private void TickSchedule(TransferSchedule schedule, string originId)
    {
        switch (schedule.State)
        {
            case TransferScheduleState.Accumulating:
                TickScheduleAccumulating(schedule, originId);
                break;

            case TransferScheduleState.Dispatched:
                TickScheduleDispatched(schedule);
                break;
        }
    }

    private void TickScheduleAccumulating(TransferSchedule schedule, string originId)
    {
        if (!_endpoints.TryGetValue(originId, out var endpoint))
            return;

        float totalCapacity = GetCapacity(originId);
        if (totalCapacity <= 0f)
            return;

        var targetQuantities = new Dictionary<string, float>();
        foreach (var kvp in schedule.ResourceProportions)
        {
            string resourceId = kvp.Key;
            float proportion = kvp.Value;
            float weight = GetTransportWeight(resourceId);
            float capacityForResource = totalCapacity * proportion;
            float targetUnits = capacityForResource / weight;
            targetQuantities[resourceId] = targetUnits;
        }

        if (targetQuantities.Count == 0)
            return;

        bool shouldDepart;

        if (schedule.WaitSeconds.HasValue)
        {
            float waitFor = schedule.WaitSeconds.Value;
            shouldDepart = (_totalTime - schedule.LastDispatchTime) >= waitFor;
        }
        else
        {
            float thresholdFraction = schedule.Threshold.ToFraction();
            if (schedule.DepartureMode == DepartureConditionMode.AnyResource)
            {
                shouldDepart = false;
                foreach (var kvp in targetQuantities)
                {
                    float stockpile = endpoint.GetStockpile(kvp.Key);
                    float required = kvp.Value * thresholdFraction;
                    if (stockpile >= required && required > 0f)
                    {
                        shouldDepart = true;
                        break;
                    }
                }
            }
            else // AllResources
            {
                shouldDepart = true;
                foreach (var kvp in targetQuantities)
                {
                    float stockpile = endpoint.GetStockpile(kvp.Key);
                    float required = kvp.Value * thresholdFraction;
                    if (stockpile < required || required <= 0f)
                    {
                        shouldDepart = false;
                        break;
                    }
                }
            }
        }

        if (!shouldDepart)
            return;

        string? orderId = DispatchOneTimeTransfer(
            originId,
            schedule.Destination,
            targetQuantities
        );

        if (orderId != null)
        {
            if (_activeTransfers.TryGetValue(orderId, out var transfer))
            {
                transfer.Order.SourceScheduleId = schedule.ScheduleId;
            }

            schedule.ActiveTransferOrderId = orderId;
            schedule.LastDispatchTime = _totalTime;
            schedule.State = TransferScheduleState.Dispatched;
            SignalBus.Instance?.EmitTransferScheduleStateChanged(
                schedule.ScheduleId,
                (int)TransferScheduleState.Dispatched
            );

            GameLogger.Info(
                $"[BodyTransferManager] Schedule {schedule.ScheduleId[..8]}... dispatched transfer {orderId[..8]}..."
            );
        }
    }

    private void TickScheduleDispatched(TransferSchedule schedule)
    {
        if (schedule.ActiveTransferOrderId == null)
        {
            schedule.State = TransferScheduleState.Accumulating;
            SignalBus.Instance?.EmitTransferScheduleStateChanged(
                schedule.ScheduleId,
                (int)TransferScheduleState.Accumulating
            );
            return;
        }

        if (!_activeTransfers.ContainsKey(schedule.ActiveTransferOrderId))
        {
            schedule.ActiveTransferOrderId = null;
            schedule.State = TransferScheduleState.Accumulating;
            SignalBus.Instance?.EmitTransferScheduleStateChanged(
                schedule.ScheduleId,
                (int)TransferScheduleState.Accumulating
            );

            GameLogger.Info(
                $"[BodyTransferManager] Schedule {schedule.ScheduleId[..8]}... transfer completed, resuming accumulation"
            );
        }
    }

    #endregion

    #region Schedules

    /// <summary>
    /// Creates a new recurring transfer schedule originating from the given endpoint.
    /// </summary>
    public string? CreateSchedule(
        string originBuildingId,
        TransferDestination destination,
        Dictionary<string, float> resourceProportions,
        DepartureConditionMode departureMode,
        DepartureThreshold threshold,
        float? waitSeconds = null
    )
    {
        if (!HasEndpoint(originBuildingId))
        {
            GameLogger.Warning(
                $"[BodyTransferManager] Cannot create schedule: origin '{originBuildingId}' has no endpoint"
            );
            return null;
        }

        if (!_schedulesByOrigin.TryGetValue(originBuildingId, out var schedules))
        {
            schedules = new List<TransferSchedule>();
            _schedulesByOrigin[originBuildingId] = schedules;
        }

        var schedule = new TransferSchedule
        {
            OriginBuildingId = originBuildingId,
            Destination = destination,
            ResourceProportions = new Dictionary<string, float>(resourceProportions),
            DepartureMode = departureMode,
            Threshold = threshold,
            State = TransferScheduleState.Idle,
            WaitSeconds = waitSeconds,
            Priority = schedules.Count + 1,
            LastDispatchTime = _totalTime,
        };

        schedules.Add(schedule);

        GameLogger.Info(
            $"[BodyTransferManager] Schedule {schedule.ScheduleId[..8]}... created "
                + $"for origin '{originBuildingId[..System.Math.Min(8, originBuildingId.Length)]}' to {destination}"
        );

        return schedule.ScheduleId;
    }

    /// <summary>
    /// Reorders the schedules for a single origin endpoint according to the supplied id list.
    /// </summary>
    public bool ReorderSchedules(string originBuildingId, IList<string> orderedIds)
    {
        if (!_schedulesByOrigin.TryGetValue(originBuildingId, out var schedules))
            return false;
        if (schedules.Count == 0)
            return false;

        var byId = new Dictionary<string, TransferSchedule>(schedules.Count);
        foreach (var s in schedules)
            byId[s.ScheduleId] = s;

        var reordered = new List<TransferSchedule>(schedules.Count);
        foreach (var id in orderedIds)
        {
            if (byId.TryGetValue(id, out var s))
            {
                reordered.Add(s);
                byId.Remove(id);
            }
        }
        foreach (var leftover in schedules)
        {
            if (byId.ContainsKey(leftover.ScheduleId))
            {
                reordered.Add(leftover);
                byId.Remove(leftover.ScheduleId);
            }
        }

        for (int i = 0; i < reordered.Count; i++)
            reordered[i].Priority = i + 1;

        schedules.Clear();
        schedules.AddRange(reordered);
        return true;
    }

    /// <summary>
    /// Returns all schedules whose destination matches the given target.
    /// </summary>
    public IReadOnlyList<TransferSchedule> GetSchedulesForDestination(TransferDestination dest)
    {
        var matches = new List<TransferSchedule>();
        foreach (var kvp in _schedulesByOrigin)
        {
            foreach (var s in kvp.Value)
            {
                if (DestinationsEqual(s.Destination, dest))
                    matches.Add(s);
            }
        }
        return matches;
    }

    private static bool DestinationsEqual(TransferDestination a, TransferDestination b)
    {
        if (a.IsOrbitalStation != b.IsOrbitalStation)
            return false;
        if (a.IsOrbitalStation)
            return a.StationSatelliteId == b.StationSatelliteId;
        return a.BuildingId == b.BuildingId;
    }

    /// <summary>
    /// Starts a schedule that is currently Idle or Stopped.
    /// </summary>
    public bool StartSchedule(string scheduleId)
    {
        var schedule = FindSchedule(scheduleId);
        if (schedule == null)
            return false;

        if (
            schedule.State != TransferScheduleState.Idle
            && schedule.State != TransferScheduleState.Stopped
        )
        {
            GameLogger.Warning(
                $"[BodyTransferManager] Cannot start schedule {scheduleId[..8]}...: state is {schedule.State}"
            );
            return false;
        }

        schedule.State = TransferScheduleState.Accumulating;
        SignalBus.Instance?.EmitTransferScheduleStateChanged(
            scheduleId,
            (int)TransferScheduleState.Accumulating
        );
        return true;
    }

    /// <summary>
    /// Stops a running schedule.
    /// </summary>
    public bool StopSchedule(string scheduleId)
    {
        var schedule = FindSchedule(scheduleId);
        if (schedule == null)
            return false;

        schedule.State = TransferScheduleState.Stopped;
        SignalBus.Instance?.EmitTransferScheduleStateChanged(
            scheduleId,
            (int)TransferScheduleState.Stopped
        );
        return true;
    }

    /// <summary>
    /// Removes a schedule entirely.
    /// </summary>
    public bool RemoveSchedule(string scheduleId)
    {
        foreach (var kvp in _schedulesByOrigin)
        {
            int removed = kvp.Value.RemoveAll(s => s.ScheduleId == scheduleId);
            if (removed > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all schedules originating at a specific endpoint.
    /// </summary>
    public IReadOnlyList<TransferSchedule> GetSchedulesForOrigin(string originBuildingId)
    {
        if (_schedulesByOrigin.TryGetValue(originBuildingId, out var schedules))
            return schedules;
        return Array.Empty<TransferSchedule>();
    }

    /// <summary>
    /// Checks whether a specific transfer order is still active (in-flight).
    /// </summary>
    public bool IsTransferActive(string orderId)
    {
        return _activeTransfers.ContainsKey(orderId);
    }

    private TransferSchedule? FindSchedule(string scheduleId)
    {
        foreach (var kvp in _schedulesByOrigin)
        {
            foreach (var schedule in kvp.Value)
            {
                if (schedule.ScheduleId == scheduleId)
                    return schedule;
            }
        }
        return null;
    }

    private void StopAllSchedulesForOrigin(string originBuildingId)
    {
        if (!_schedulesByOrigin.TryGetValue(originBuildingId, out var schedules))
            return;

        foreach (var schedule in schedules)
        {
            if (schedule.State != TransferScheduleState.Stopped)
            {
                schedule.State = TransferScheduleState.Stopped;
                SignalBus.Instance?.EmitTransferScheduleStateChanged(
                    schedule.ScheduleId,
                    (int)TransferScheduleState.Stopped
                );
            }
        }
    }

    #endregion

    #region Helpers

    private IResourceEndpoint? ResolveEndpoint(TransferDestination destination)
    {
        if (destination.IsOrbitalStation && destination.StationSatelliteId != null)
        {
            _endpoints.TryGetValue(destination.StationSatelliteId, out var ep);
            return ep;
        }

        if (!string.IsNullOrEmpty(destination.BuildingId))
        {
            _endpoints.TryGetValue(destination.BuildingId, out var ep);
            return ep;
        }

        return null;
    }

    private static float GetTransportWeight(string resourceId)
    {
        var resourceDb = ResourceDatabase.Instance;
        if (
            resourceDb != null
            && resourceDb.IsLoaded
            && resourceDb.TryGetResource(resourceId, out var def)
            && def != null
        )
        {
            return def.TransportWeight;
        }
        return 1.0f;
    }

    #endregion

    /// <summary>
    /// Tracks an in-flight transfer.
    /// </summary>
    public class ActiveTransfer
    {
        public TransferOrder Order { get; set; } = null!;
    }
}
