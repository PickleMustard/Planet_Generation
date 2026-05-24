using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.GameState;
using Structures.Resources;
using Structures.Transfers;
using UtilityLibrary;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Registers the owning building as a transfer-station endpoint with its parent body's
/// <see cref="IOrbitalBody"/> endpoint registry, and manages all transfers and schedules
/// that originate from this building. Tick is driven via <see cref="OnManufactureTick"/>.
/// </summary>
public partial class TransferStationBehavior : RefCounted, IBuildingBehavior, IBehaviorConfigurable
{
    private Building? _owner;
    private BuildingResourceEndpoint? _endpoint;
    private TransferStationDefinition? _endpointDef;
    private IOrbitalBody? _body;
    private readonly Dictionary<string, ActiveTransfer> _activeTransfers = new();
    private readonly Dictionary<string, List<TransferSchedule>> _schedulesByOrigin = new();
    private readonly Dictionary<string, double> _lastProgressTimeBySchedule = new();
    private readonly Dictionary<string, float> _lastProgressSignalBySchedule = new();
    private double _totalTime;

    // Config stored locally — no longer read from Definition.TransferStation
    private float _cargoCapacity;
    private float _vehicleSpeed;
    private int _maxConcurrentTransfers;

    /// <summary>Cargo capacity read from YAML behavior config.</summary>
    public float CargoCapacity => _cargoCapacity;

    /// <summary>Vehicle speed read from YAML behavior config.</summary>
    public float VehicleSpeed => _vehicleSpeed;

    /// <summary>Max concurrent transfers read from YAML behavior config.</summary>
    public int MaxConcurrentTransfers => _maxConcurrentTransfers;

    public Building? Owner => _owner;

    /// <summary>
    /// The endpoint adapter exposed by this behavior. Other behaviors / the tick logic
    /// use this to withdraw from and deposit to the owning building's bulk storage.
    /// </summary>
    public IResourceEndpoint? ResourceEndpoint => _endpoint;

    public bool WantsTick => true;

    // Run after StorageHubBehavior so BulkStorage slots exist before the endpoint is exposed.
    public int Priority => 100;

    public void OnAttach(Building owner)
    {
        _owner = owner;
    }

    public void Configure(Dictionary<string, object> config)
    {
        _cargoCapacity = BehaviorConfigHelper.ReadFloat(config, "cargo_capacity", 500.0f);
        _vehicleSpeed = BehaviorConfigHelper.ReadFloat(config, "vehicle_speed", 50.0f);
        _maxConcurrentTransfers = BehaviorConfigHelper.ReadInt(config, "max_concurrent_transfers", 2);
    }

    public void OnRegister()
    {
        if (_owner == null)
            return;
        if (_cargoCapacity <= 0f)
        {
            GameLogger.Warning(
                $"TransferStationBehavior on '{_owner.Name}' has no cargo capacity; skipping registration."
            );
            return;
        }
        if (string.IsNullOrEmpty(_owner.Id))
        {
            GameLogger.Warning(
                $"TransferStationBehavior on '{_owner.Name}' has no Building.Id; skipping registration."
            );
            return;
        }

        // Discover parent body at registration time, when VisualNode is set.
        if (_body == null && _owner.VisualNode != null)
        {
            Node? cursor = _owner.VisualNode;
            while (cursor != null)
            {
                if (cursor is IOrbitalBody body)
                {
                    _body = body;
                    break;
                }
                cursor = cursor.GetParent();
            }
        }

        if (_body == null)
        {
            GameLogger.Warning(
                $"TransferStationBehavior: could not find IOrbitalBody for '{_owner.Name}'."
            );
            return;
        }

        _endpoint = new BuildingResourceEndpoint(_owner);
        _endpointDef = new TransferStationDefinition
        {
            CargoCapacity = _cargoCapacity,
            VehicleSpeed = _vehicleSpeed,
            MaxConcurrentTransfers = _maxConcurrentTransfers,
        };
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
        _lastProgressTimeBySchedule.Clear();
        _lastProgressSignalBySchedule.Clear();
        _totalTime = 0;
    }

    public void OnManufactureTick(float delta, Building owner)
    {
        float dt = delta;
        _totalTime += delta;

        TickActiveTransfers(dt);
        TickSchedules(dt);
    }

    #region Endpoint Queries

    public bool HasEndpoint(string endpointId)
    {
        return _owner != null && _owner.Id == endpointId && _endpointDef != null;
    }

    public Building? GetEndpointBuilding(string endpointId)
    {
        if (_owner != null && _owner.Id == endpointId)
            return _owner;
        return null;
    }

    public float GetCapacity(string endpointId)
    {
        if (_owner != null && _owner.Id == endpointId && _endpointDef != null)
            return _cargoCapacity;
        return 0f;
    }

    public int GetMaxConcurrentTransfers(string endpointId)
    {
        if (_owner != null && _owner.Id == endpointId && _endpointDef != null)
            return _maxConcurrentTransfers;
        return 0;
    }

    public float GetVehicleSpeed(string endpointId)
    {
        if (_owner != null && _owner.Id == endpointId && _endpointDef != null)
            return _vehicleSpeed;
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
                $"[TransferStationBehavior] Cannot dispatch: origin '{originBuildingId}' is not this behavior's owner"
            );
            return null;
        }

        if (!HasEndpoint(originBuildingId))
        {
            GameLogger.Warning(
                $"[TransferStationBehavior] Cannot dispatch: origin '{originBuildingId}' has no registered endpoint"
            );
            return null;
        }

        int activeCount = GetActiveTransferCountForOrigin(originBuildingId);
        int maxConcurrent = GetMaxConcurrentTransfers(originBuildingId);
        if (activeCount >= maxConcurrent)
        {
            GameLogger.Warning(
                $"[TransferStationBehavior] Cannot dispatch: origin '{originBuildingId}' "
                    + $"at max concurrent transfers ({activeCount}/{maxConcurrent})"
            );
            return null;
        }

        IResourceEndpoint? destEndpoint = ResolveEndpoint(destination);
        if (destEndpoint == null)
        {
            GameLogger.Warning(
                $"[TransferStationBehavior] Cannot dispatch: destination {destination} not found on this body"
            );
            return null;
        }

        float travelTime = ComputeTravelTime(originBuildingId, destination);
        if (travelTime <= 0f)
        {
            GameLogger.Warning("[TransferStationBehavior] Cannot dispatch: invalid travel time");
            return null;
        }

        float totalCapacity = GetCapacity(originBuildingId);
        var manifest = new CargoManifest();
        var requestedManifest = new CargoManifest();
        float usedCapacity = 0f;

        foreach (var kvp in requestedResources)
        {
            string resourceId = kvp.Key;
            // Floor the caller's requested amount to whole units — resources transport as integers.
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
                "[TransferStationBehavior] Cannot dispatch: no resources available to load"
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

        int continentIdx = _owner.PrimaryCell?.ContinentIndex ?? -1;
        SignalBus.Instance?.SafeEmitTransferDispatched(order.OrderId, continentIdx);

        GameLogger.Info(
            $"[TransferStationBehavior] Dispatched transfer {order.OrderId[..8]}... "
                + $"from '{originBuildingId[..System.Math.Min(8, originBuildingId.Length)]}' to {destination} "
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

        float distance = ComputeDistance(originBuildingId, destination);
        if (distance <= 0f)
            return 0f;

        return distance / speed;
    }

    private float ComputeDistance(string originBuildingId, TransferDestination destination)
    {
        if (destination.IsOrbitalStation)
        {
            return 100f;
        }

        if (!string.IsNullOrEmpty(destination.BuildingId))
        {
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
                                $"[TransferStationBehavior] {lost} units of '{resourceId}' "
                                    + $"lost (both destination and origin full)"
                            );
                        }
                    }
                    else
                    {
                        GameLogger.Warning(
                            $"[TransferStationBehavior] {remainder} units of '{resourceId}' "
                                + $"lost (origin endpoint gone)"
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
                        $"[TransferStationBehavior] {amount} units of '{resourceId}' "
                            + $"lost (destination and origin endpoints both gone)"
                    );
                }
            }
        }

        bool fullyAccepted = totalReverted <= 0;
        order.State = SurfaceTransferState.Complete;

        SignalBus.Instance?.SafeEmitTransferArrived(order.OrderId, fullyAccepted);

        if (totalReverted > 0)
        {
            int continentIdx = _owner?.PrimaryCell?.ContinentIndex ?? -1;
            SignalBus.Instance?.SafeEmitTransferReverted(order.OrderId, continentIdx, totalReverted);
        }

        GameLogger.Info(
            $"[TransferStationBehavior] Transfer {order.OrderId[..8]}... completed. "
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

        // Target quantities derived from capacity/weight, floored to whole units.
        // Downstream DispatchOneTimeTransfer floors again defensively.
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
        float progressSignal;

        if (schedule.WaitSeconds.HasValue)
        {
            float waitFor = schedule.WaitSeconds.Value;
            shouldDepart = (_totalTime - schedule.LastDispatchTime) >= waitFor;
            progressSignal = waitFor > 0f
                ? (float)System.Math.Clamp((_totalTime - schedule.LastDispatchTime) / waitFor, 0.0, 1.0)
                : 1f;
        }
        else
        {
            float thresholdFraction = schedule.Threshold.ToFraction();
            float bestRatio = schedule.DepartureMode == DepartureConditionMode.AnyResource ? 0f : 1f;
            if (schedule.DepartureMode == DepartureConditionMode.AnyResource)
            {
                shouldDepart = false;
                foreach (var kvp in targetQuantities)
                {
                    int stockpile = _endpoint.GetStockpile(kvp.Key);
                    float required = kvp.Value * thresholdFraction;
                    if (required > 0f)
                    {
                        float ratio = Math.Clamp(stockpile / required, 0f, 1f);
                        if (ratio > bestRatio) bestRatio = ratio;
                        if (stockpile >= required)
                        {
                            shouldDepart = true;
                            break;
                        }
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
                    if (required <= 0f)
                    {
                        shouldDepart = false;
                        bestRatio = 0f;
                        continue;
                    }
                    float ratio = Math.Clamp(stockpile / required, 0f, 1f);
                    if (ratio < bestRatio) bestRatio = ratio;
                    if (stockpile < required)
                        shouldDepart = false;
                }
            }
            progressSignal = bestRatio;
        }

        _lastProgressSignalBySchedule.TryGetValue(schedule.ScheduleId, out float prevSignal);
        if (progressSignal > prevSignal + 1e-4f)
        {
            _lastProgressSignalBySchedule[schedule.ScheduleId] = progressSignal;
            _lastProgressTimeBySchedule[schedule.ScheduleId] = _totalTime;
        }
        else if (!_lastProgressTimeBySchedule.ContainsKey(schedule.ScheduleId))
        {
            _lastProgressTimeBySchedule[schedule.ScheduleId] = _totalTime;
            _lastProgressSignalBySchedule[schedule.ScheduleId] = progressSignal;
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
            _lastProgressSignalBySchedule[schedule.ScheduleId] = 0f;
            _lastProgressTimeBySchedule[schedule.ScheduleId] = _totalTime;
            SignalBus.Instance?.SafeEmitTransferScheduleStateChanged(
                schedule.ScheduleId,
                (int)TransferScheduleState.Dispatched
            );

            GameLogger.Info(
                $"[TransferStationBehavior] Schedule {schedule.ScheduleId[..8]}... dispatched transfer {orderId[..8]}..."
            );
        }
    }

    private void TickScheduleDispatched(TransferSchedule schedule)
    {
        if (schedule.ActiveTransferOrderId == null)
        {
            schedule.State = TransferScheduleState.Accumulating;
            _lastProgressSignalBySchedule[schedule.ScheduleId] = 0f;
            _lastProgressTimeBySchedule[schedule.ScheduleId] = _totalTime;
            SignalBus.Instance?.SafeEmitTransferScheduleStateChanged(
                schedule.ScheduleId,
                (int)TransferScheduleState.Accumulating
            );
            return;
        }

        if (!_activeTransfers.ContainsKey(schedule.ActiveTransferOrderId))
        {
            schedule.ActiveTransferOrderId = null;
            schedule.State = TransferScheduleState.Accumulating;
            _lastProgressSignalBySchedule[schedule.ScheduleId] = 0f;
            _lastProgressTimeBySchedule[schedule.ScheduleId] = _totalTime;
            SignalBus.Instance?.SafeEmitTransferScheduleStateChanged(
                schedule.ScheduleId,
                (int)TransferScheduleState.Accumulating
            );

            GameLogger.Info(
                $"[TransferStationBehavior] Schedule {schedule.ScheduleId[..8]}... transfer completed, resuming accumulation"
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
                $"[TransferStationBehavior] Cannot create schedule: origin '{originBuildingId}' is not this behavior's owner"
            );
            return null;
        }

        if (!HasEndpoint(originBuildingId))
        {
            GameLogger.Warning(
                $"[TransferStationBehavior] Cannot create schedule: origin '{originBuildingId}' has no endpoint"
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
        _lastProgressTimeBySchedule[schedule.ScheduleId] = _totalTime;
        _lastProgressSignalBySchedule[schedule.ScheduleId] = 0f;

        GameLogger.Info(
            $"[TransferStationBehavior] Schedule {schedule.ScheduleId[..8]}... created "
                + $"for origin '{originBuildingId[..System.Math.Min(8, originBuildingId.Length)]}' to {destination}"
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

    private static bool DestinationsEqual(TransferDestination a, TransferDestination b)
    {
        if (a.IsOrbitalStation != b.IsOrbitalStation)
            return false;
        if (a.IsOrbitalStation)
            return a.StationSatelliteId == b.StationSatelliteId;
        return a.BuildingId == b.BuildingId;
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
                $"[TransferStationBehavior] Cannot start schedule {scheduleId[..8]}...: state is {schedule.State}"
            );
            return false;
        }

        schedule.State = TransferScheduleState.Accumulating;
        _lastProgressSignalBySchedule[scheduleId] = 0f;
        _lastProgressTimeBySchedule[scheduleId] = _totalTime;
        SignalBus.Instance?.SafeEmitTransferScheduleStateChanged(
            scheduleId,
            (int)TransferScheduleState.Accumulating
        );
        return true;
    }

    public bool StopSchedule(string scheduleId)
    {
        var schedule = FindSchedule(scheduleId);
        if (schedule == null)
            return false;

        schedule.State = TransferScheduleState.Stopped;
        SignalBus.Instance?.SafeEmitTransferScheduleStateChanged(
            scheduleId,
            (int)TransferScheduleState.Stopped
        );
        return true;
    }

    public bool RemoveSchedule(string scheduleId)
    {
        foreach (var kvp in _schedulesByOrigin)
        {
            int removed = kvp.Value.RemoveAll(s => s.ScheduleId == scheduleId);
            if (removed > 0)
            {
                _lastProgressTimeBySchedule.Remove(scheduleId);
                _lastProgressSignalBySchedule.Remove(scheduleId);
                return true;
            }
        }
        return false;
    }

    public IReadOnlyList<TransferSchedule> GetSchedulesForOrigin(string originBuildingId)
    {
        if (_schedulesByOrigin.TryGetValue(originBuildingId, out var schedules))
            return schedules;
        return Array.Empty<TransferSchedule>();
    }

    public bool IsTransferActive(string orderId)
    {
        return _activeTransfers.ContainsKey(orderId);
    }

    public IReadOnlyCollection<ActiveTransfer> GetActiveTransfers() => _activeTransfers.Values;

    /// <summary>Game-time accumulated by this behavior's tick. Used by UI for derived progress calculations.</summary>
    public double TotalTime => _totalTime;

    /// <summary>Returns the active in-flight order for the given schedule, or null if none.</summary>
    public TransferOrder? GetActiveOrder(string scheduleId)
    {
        var schedule = FindSchedule(scheduleId);
        if (schedule?.ActiveTransferOrderId == null)
            return null;
        return _activeTransfers.TryGetValue(schedule.ActiveTransferOrderId, out var active)
            ? active.Order
            : null;
    }

    /// <summary>Current stockpile of a resource at this station's endpoint. Returns 0 if no endpoint.</summary>
    public int GetCurrentStockpile(string resourceId)
        => _endpoint?.GetStockpile(resourceId) ?? 0;

    /// <summary>
    /// Game-time of the last observed accumulation progress for the schedule. Returns
    /// <see cref="double.NegativeInfinity"/> if the schedule is unknown to this behavior.
    /// </summary>
    public double GetLastProgressTime(string scheduleId)
        => _lastProgressTimeBySchedule.TryGetValue(scheduleId, out var t) ? t : double.NegativeInfinity;

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
            {
                schedule.State = TransferScheduleState.Stopped;
                SignalBus.Instance?.SafeEmitTransferScheduleStateChanged(
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
        if (_body == null)
            return null;

        if (destination.IsOrbitalStation && destination.StationSatelliteId != null)
        {
            var owner = _body.GetTransferEndpointOwner(destination.StationSatelliteId);
            if (owner is Building building)
            {
                var behavior = building.GetBehavior<TransferStationBehavior>();
                return behavior?.ResourceEndpoint;
            }
            if (owner is StationSatellite station)
            {
                return ResolveStationEndpoint(station, destination.StationSatelliteId);
            }
            if (owner != null)
            {
                GameLogger.Warning(
                    $"[TransferStationBehavior] Unknown owner type '{owner.GetType().Name}' for endpoint '{destination.StationSatelliteId}'"
                );
            }
            return null;
        }

        if (!string.IsNullOrEmpty(destination.BuildingId))
        {
            var owner = _body.GetTransferEndpointOwner(destination.BuildingId);
            if (owner is Building building)
            {
                var behavior = building.GetBehavior<TransferStationBehavior>();
                return behavior?.ResourceEndpoint;
            }
            if (owner is StationSatellite station)
            {
                return ResolveStationEndpoint(station, destination.BuildingId);
            }
            if (owner != null)
            {
                GameLogger.Warning(
                    $"[TransferStationBehavior] Unknown owner type '{owner.GetType().Name}' for endpoint '{destination.BuildingId}'"
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
