using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.Resources;
using Structures.Transfers;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace Constructables.Stations.Behaviors;

/// <summary>
/// Registers the owning station as a transfer-hub endpoint with its parent body's
/// <see cref="IOrbitalBody"/> endpoint registry, and manages all transfers and schedules
/// that originate from this station. Mirrors
/// <see cref="Buildings.Behaviors.TransferStationBehavior"/> but uses
/// <see cref="StationResourceEndpoint"/> instead of <see cref="Buildings.BuildingResourceEndpoint"/>.
/// </summary>
public partial class TransferHubBehavior : RefCounted, IStationBehavior, IStationBehaviorConfigurable
{
    private StationSatellite? _owner;
    private IOrbitalBody? _body;
    private StationResourceEndpoint? _endpoint;
    private TransferStationDefinition? _endpointDef;
    private readonly Dictionary<string, ActiveTransfer> _activeTransfers = new();
    private readonly Dictionary<string, List<TransferSchedule>> _schedulesByOrigin = new();
    private double _totalTime;

    /// <summary>Transfer station definition parsed from YAML. Set before OnRegister.</summary>
    public TransferStationDefinition? EndpointDef { get; set; }

    /// <summary>
    /// Applies inline config from the behaviors: YAML block.
    /// Reads <c>transfer_station</c> sub-dict to populate <see cref="EndpointDef"/>.
    /// </summary>
    public void Configure(Dictionary<string, object> config)
    {
        if (config.TryGetValue("transfer_station", out var ts) && ts is Dictionary<object, object> tsDict)
        {
            EndpointDef = new TransferStationDefinition
            {
                CargoCapacity = BaseConfigLoader.ReadFloat(tsDict, "cargo_capacity", 500.0f),
                VehicleSpeed = BaseConfigLoader.ReadFloat(tsDict, "vehicle_speed", 50.0f),
                MaxConcurrentTransfers = BaseConfigLoader.ReadInt(tsDict, "max_concurrent_transfers", 2),
            };
        }
    }

    public StationSatellite? Owner => _owner;

    /// <summary>
    /// The endpoint adapter exposed by this behavior. Other behaviors / the tick logic
    /// use this to withdraw from and deposit to the owning station's bulk storage.
    /// </summary>
    public IResourceEndpoint? ResourceEndpoint => _endpoint;

    public bool WantsTick => _activeTransfers.Count > 0 || AnyScheduleRunning();

    // Run after StorageHubBehavior (0) and OrbitalConstructorBehavior (50).
    public int Priority => 100;

    public void OnAttach(StationSatellite owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null) return;

        // Walk scene tree upward to find IOrbitalBody parent.
        Node? cursor = _owner.GetParent();
        while (cursor != null)
        {
            if (cursor is IOrbitalBody body)
            {
                _body = body;
                break;
            }
            cursor = cursor.GetParent();
        }

        if (_body == null)
        {
            GameLogger.Warning(
                $"TransferHubBehavior {_owner.Name}: "
                + "No IOrbitalBody found in parent tree; skipping registration"
            );
            return;
        }

        _endpointDef ??= EndpointDef;
        if (_endpointDef == null)
        {
            GameLogger.Warning(
                $"TransferHubBehavior {_owner.Name}: "
                + "No TransferStationDefinition provided; skipping registration"
            );
            return;
        }

        if (string.IsNullOrEmpty(_owner.Id))
        {
            GameLogger.Warning(
                $"TransferHubBehavior {_owner.Name}: "
                + "Owner has no Id; skipping registration"
            );
            return;
        }

        _endpoint = (StationResourceEndpoint)_owner.ResourceEndpoint;
        _body.RegisterTransferEndpoint(_owner.Id, _endpointDef, _owner);

        if (!_schedulesByOrigin.ContainsKey(_owner.Id))
            _schedulesByOrigin[_owner.Id] = new List<TransferSchedule>();
    }

    public void OnUnregister()
    {
        if (_owner == null || _body == null || string.IsNullOrEmpty(_owner.Id))
            return;

        StopAllSchedulesForOrigin(_owner.Id);
        _body.UnregisterTransferEndpoint(_owner.Id);
        _endpoint = null;
        _endpointDef = null;
    }

    public void OnDetach()
    {
        _owner = null;
        _body = null;
        _endpoint = null;
        _endpointDef = null;
        _activeTransfers.Clear();
        _schedulesByOrigin.Clear();
        _totalTime = 0;
    }

    public void OnManufactureTick(float delta, StationSatellite owner)
    {
        _totalTime += delta;
        TickActiveTransfers(delta);
        TickSchedules(delta);
    }

    #region Endpoint Queries

    public bool HasEndpoint(string endpointId)
    {
        return _owner != null && _owner.Id == endpointId && _endpointDef != null;
    }

    public float GetCapacity(string endpointId)
    {
        if (_owner != null && _owner.Id == endpointId && _endpointDef != null)
            return _endpointDef.CargoCapacity;
        return 0f;
    }

    public int GetMaxConcurrentTransfers(string endpointId)
    {
        if (_owner != null && _owner.Id == endpointId && _endpointDef != null)
            return _endpointDef.MaxConcurrentTransfers;
        return 0;
    }

    public float GetVehicleSpeed(string endpointId)
    {
        if (_owner != null && _owner.Id == endpointId && _endpointDef != null)
            return _endpointDef.VehicleSpeed;
        return 0f;
    }

    public int GetActiveTransferCountForOrigin(string endpointId)
    {
        if (_owner == null || _owner.Id != endpointId)
            return 0;
        int count = 0;
        foreach (var kvp in _activeTransfers)
        {
            if (kvp.Value.Order.OriginBuildingId == endpointId)
                count++;
        }
        return count;
    }

    public IReadOnlyList<string> GetEndpointsOnContinent(int continentIndex)
    {
        return _body?.GetTransferEndpointsOnContinent(continentIndex)
            ?? Array.Empty<string>();
    }

    public float GetTotalCapacityOnContinent(int continentIndex)
    {
        return _body?.GetTotalTransferCapacityOnContinent(continentIndex) ?? 0f;
    }

    #endregion

    #region One-Time Transfers

    public string? DispatchOneTimeTransfer(
        string originBuildingId,
        TransferDestination destination,
        Dictionary<string, float> requestedResources
    )
    {
        if (_owner == null || _owner.Id != originBuildingId || _endpoint == null)
        {
            GameLogger.Warning(
                $"[TransferHubBehavior] Cannot dispatch: origin '{originBuildingId}' "
                + "is not this behavior's owner"
            );
            return null;
        }

        if (!HasEndpoint(originBuildingId))
        {
            GameLogger.Warning(
                $"[TransferHubBehavior] Cannot dispatch: origin '{originBuildingId}' "
                + "has no registered endpoint"
            );
            return null;
        }

        int activeCount = GetActiveTransferCountForOrigin(originBuildingId);
        int maxConcurrent = GetMaxConcurrentTransfers(originBuildingId);
        if (activeCount >= maxConcurrent)
        {
            GameLogger.Warning(
                $"[TransferHubBehavior] Cannot dispatch: origin '{originBuildingId}' "
                + $"at max concurrent transfers ({activeCount}/{maxConcurrent})"
            );
            return null;
        }

        IResourceEndpoint? destEndpoint = ResolveEndpoint(destination);
        if (destEndpoint == null)
        {
            GameLogger.Warning(
                $"[TransferHubBehavior] Cannot dispatch: destination {destination} not found"
            );
            return null;
        }

        float travelTime = ComputeTravelTime(originBuildingId, destination);
        if (travelTime <= 0f)
        {
            GameLogger.Warning("[TransferHubBehavior] Cannot dispatch: invalid travel time");
            return null;
        }

        float totalCapacity = GetCapacity(originBuildingId);
        var manifest = new CargoManifest();
        var requestedManifest = new CargoManifest();
        float usedCapacity = 0f;

        foreach (var kvp in requestedResources)
        {
            string resourceId = kvp.Key;
            int requestedAmount = Mathf.FloorToInt(kvp.Value);
            if (requestedAmount <= 0)
                continue;

            requestedManifest.LoadResource(resourceId, requestedAmount);

            float weight = GetTransportWeight(resourceId);
            if (weight <= 0f)
                continue;

            float remainingCapacity = totalCapacity - usedCapacity;
            int maxUnits = (int)Mathf.Floor(remainingCapacity / weight);
            int toLoad = System.Math.Min(requestedAmount, maxUnits);

            if (toLoad <= 0)
                continue;

            int actualWithdrawn = _endpoint.WithdrawResource(resourceId, toLoad);
            if (actualWithdrawn > 0)
            {
                manifest.LoadResource(resourceId, actualWithdrawn);
                usedCapacity += actualWithdrawn * weight;
            }
        }

        if (manifest.TotalUnits <= 0)
        {
            GameLogger.Warning(
                "[TransferHubBehavior] Cannot dispatch: no resources available to load"
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

        GameLogger.Info(
            $"[TransferHubBehavior] Dispatched transfer {order.OrderId[..8]}... "
            + $"from '{originBuildingId[..Math.Min(8, originBuildingId.Length)]}' to {destination} "
            + $"({manifest.TotalUnits} units, ETA {travelTime:F1}s)"
        );

        return order.OrderId;
    }

    #endregion

    #region Travel Time

    public float ComputeTravelTime(string originBuildingId, TransferDestination destination)
    {
        float speed = GetVehicleSpeed(originBuildingId);
        if (speed <= 0f)
            return 0f;

        float distance = ComputeDistance(destination);
        if (distance <= 0f)
            return 0f;

        return distance / speed;
    }

    private static float ComputeDistance(TransferDestination destination)
    {
        if (destination.IsOrbitalStation)
            return 100f;

        if (!string.IsNullOrEmpty(destination.BuildingId))
            return 100f;

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
        IResourceEndpoint? originEndpoint = _endpoint;

        int totalReverted = 0;

        foreach (var kvp in order.Manifest.Resources)
        {
            string resourceId = kvp.Key;
            int amount = kvp.Value;

            if (destEndpoint != null)
            {
                int deposited = destEndpoint.DepositResource(resourceId, amount);
                int remainder = amount - deposited;

                if (remainder > 0)
                {
                    if (originEndpoint != null)
                    {
                        int reverted = originEndpoint.DepositResource(resourceId, remainder);
                        int lost = remainder - reverted;
                        totalReverted += reverted;

                        if (lost > 0)
                        {
                            GameLogger.Warning(
                                $"[TransferHubBehavior] {lost} units of '{resourceId}' "
                                + "lost (both destination and origin full)"
                            );
                        }
                    }
                    else
                    {
                        GameLogger.Warning(
                            $"[TransferHubBehavior] {remainder} units of '{resourceId}' "
                            + "lost (origin endpoint gone)"
                        );
                    }
                }
            }
            else
            {
                if (originEndpoint != null)
                {
                    int reverted = originEndpoint.DepositResource(resourceId, amount);
                    totalReverted += reverted;
                }
                else
                {
                    GameLogger.Warning(
                        $"[TransferHubBehavior] {amount} units of '{resourceId}' "
                        + "lost (destination and origin endpoints both gone)"
                    );
                }
            }
        }

        bool fullyAccepted = totalReverted <= 0;
        order.State = SurfaceTransferState.Complete;

        GameLogger.Info(
            $"[TransferHubBehavior] Transfer {order.OrderId[..8]}... completed. "
            + $"Accepted: {fullyAccepted}, Reverted: {totalReverted}"
        );
    }

    private void TickSchedules(float delta)
    {
        if (_owner == null)
            return;

        string originId = _owner.Id;
        if (!_schedulesByOrigin.TryGetValue(originId, out var schedules))
            return;

        for (int i = 0; i < schedules.Count; i++)
        {
            var schedule = schedules[i];
            TickSchedule(schedule, originId);
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
        if (_endpoint == null)
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
            if (weight <= 0f) continue;
            float capacityForResource = totalCapacity * proportion;
            int targetUnits = (int)Mathf.Floor(capacityForResource / weight);
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
                    int stockpile = _endpoint.GetStockpile(kvp.Key);
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
                    int stockpile = _endpoint.GetStockpile(kvp.Key);
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

            GameLogger.Info(
                $"[TransferHubBehavior] Schedule {schedule.ScheduleId[..8]}... "
                + $"dispatched transfer {orderId[..8]}..."
            );
        }
    }

    private void TickScheduleDispatched(TransferSchedule schedule)
    {
        if (schedule.ActiveTransferOrderId == null)
        {
            schedule.State = TransferScheduleState.Accumulating;
            return;
        }

        if (!_activeTransfers.ContainsKey(schedule.ActiveTransferOrderId))
        {
            schedule.ActiveTransferOrderId = null;
            schedule.State = TransferScheduleState.Accumulating;

            GameLogger.Info(
                $"[TransferHubBehavior] Schedule {schedule.ScheduleId[..8]}... "
                + "transfer completed, resuming accumulation"
            );
        }
    }

    #endregion

    #region Schedules

    public string? CreateSchedule(
        string originBuildingId,
        TransferDestination destination,
        Dictionary<string, float> resourceProportions,
        DepartureConditionMode departureMode,
        DepartureThreshold threshold,
        float? waitSeconds = null
    )
    {
        if (_owner == null || _owner.Id != originBuildingId)
        {
            GameLogger.Warning(
                $"[TransferHubBehavior] Cannot create schedule: origin '{originBuildingId}' "
                + "is not this behavior's owner"
            );
            return null;
        }

        if (!HasEndpoint(originBuildingId))
        {
            GameLogger.Warning(
                $"[TransferHubBehavior] Cannot create schedule: origin '{originBuildingId}' "
                + "has no endpoint"
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
            $"[TransferHubBehavior] Schedule {schedule.ScheduleId[..8]}... created "
            + $"for origin '{originBuildingId[..Math.Min(8, originBuildingId.Length)]}' to {destination}"
        );

        return schedule.ScheduleId;
    }

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
                $"[TransferHubBehavior] Cannot start schedule {scheduleId[..8]}...: "
                + $"state is {schedule.State}"
            );
            return false;
        }

        schedule.State = TransferScheduleState.Accumulating;
        return true;
    }

    public bool StopSchedule(string scheduleId)
    {
        var schedule = FindSchedule(scheduleId);
        if (schedule == null)
            return false;

        schedule.State = TransferScheduleState.Stopped;
        return true;
    }

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

    public IReadOnlyList<TransferSchedule> GetSchedulesForOrigin(string originBuildingId)
    {
        if (_schedulesByOrigin.TryGetValue(originBuildingId, out var schedules))
            return schedules;
        return Array.Empty<TransferSchedule>();
    }

    public IReadOnlyList<TransferSchedule> GetSchedulesForDestination(TransferDestination dest)
    {
        var matches = new List<TransferSchedule>();
        if (_owner == null)
            return matches;

        if (!_schedulesByOrigin.TryGetValue(_owner.Id, out var schedules))
            return matches;

        foreach (var s in schedules)
        {
            if (DestinationsEqual(s.Destination, dest))
                matches.Add(s);
        }
        return matches;
    }

    public bool IsTransferActive(string orderId) => _activeTransfers.ContainsKey(orderId);

    public IReadOnlyDictionary<string, ActiveTransfer> GetActiveTransfers() => _activeTransfers;

    /// <summary>Accumulated game-time of this behavior's tick. Exposed for save/load.</summary>
    public double TotalTime => _totalTime;

    /// <summary>
    /// Rehydrates transfer state from a save: restores the accumulated game-time clock and seeds the
    /// active-transfer and schedule collections. Call after <see cref="OnRegister"/> so the endpoint
    /// and origin schedule list already exist. Schedules are keyed by their stored origin id.
    /// </summary>
    public void RestoreState(
        double totalTime,
        IEnumerable<TransferOrder> activeOrders,
        IEnumerable<TransferSchedule> schedules)
    {
        _totalTime = totalTime;

        foreach (var order in activeOrders)
            _activeTransfers[order.OrderId] = new ActiveTransfer { Order = order };

        foreach (var schedule in schedules)
        {
            if (!_schedulesByOrigin.TryGetValue(schedule.OriginBuildingId, out var list))
            {
                list = new List<TransferSchedule>();
                _schedulesByOrigin[schedule.OriginBuildingId] = list;
            }
            list.Add(schedule);
        }
    }

    public IReadOnlyList<TransferSchedule> GetAllSchedules()
    {
        var all = new List<TransferSchedule>();
        foreach (var kvp in _schedulesByOrigin)
            all.AddRange(kvp.Value);
        return all;
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
                schedule.State = TransferScheduleState.Stopped;
        }
    }

    private bool AnyScheduleRunning()
    {
        foreach (var kvp in _schedulesByOrigin)
        {
            foreach (var s in kvp.Value)
            {
                if (s.State == TransferScheduleState.Accumulating
                    || s.State == TransferScheduleState.Dispatched)
                    return true;
            }
        }
        return false;
    }

    private static bool DestinationsEqual(TransferDestination a, TransferDestination b)
    {
        if (a.IsOrbitalStation != b.IsOrbitalStation)
            return false;
        if (a.IsOrbitalStation)
            return a.StationSatelliteId == b.StationSatelliteId;
        return a.BuildingId == b.BuildingId;
    }

    #endregion

    #region Helpers

    private IResourceEndpoint? ResolveEndpoint(TransferDestination destination)
    {
        if (_body == null)
            return null;

        if (destination.IsOrbitalStation && destination.StationSatelliteId != null)
        {
            var owner = _body.GetTransferEndpointOwner(destination.StationSatelliteId);
            if (owner is Building building)
            {
                var behavior = building.GetBehavior<Buildings.Behaviors.TransferStationBehavior>();
                return behavior?.ResourceEndpoint;
            }
            if (owner is StationSatellite station)
            {
                return ResolveStationEndpoint(station, destination.StationSatelliteId);
            }
            if (owner != null)
            {
                GameLogger.Warning(
                    $"[TransferHubBehavior] Unknown owner type '{owner.GetType().Name}' "
                    + $"for endpoint '{destination.StationSatelliteId}'"
                );
            }
            return null;
        }

        if (!string.IsNullOrEmpty(destination.BuildingId))
        {
            var owner = _body.GetTransferEndpointOwner(destination.BuildingId);
            if (owner is Building building)
            {
                var behavior = building.GetBehavior<Buildings.Behaviors.TransferStationBehavior>();
                return behavior?.ResourceEndpoint;
            }
            if (owner is StationSatellite station)
            {
                return ResolveStationEndpoint(station, destination.BuildingId);
            }
            if (owner != null)
            {
                GameLogger.Warning(
                    $"[TransferHubBehavior] Unknown owner type '{owner.GetType().Name}' "
                    + $"for endpoint '{destination.BuildingId}'"
                );
            }
            return null;
        }

        return null;
    }

    /// <summary>
    /// Resolves the <see cref="IResourceEndpoint"/> exposed directly by a
    /// <see cref="StationSatellite"/>. Every station carries one regardless of attached behaviors.
    /// </summary>
    private static IResourceEndpoint? ResolveStationEndpoint(StationSatellite station, string endpointId)
        => station.ResourceEndpoint;

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
