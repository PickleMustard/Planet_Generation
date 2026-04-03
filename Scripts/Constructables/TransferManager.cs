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
/// Singleton autoload that manages all surface resource transfers between continents
/// and between continents and orbital stations on the same celestial body.
/// Ticked each physics frame independently of EconomyManager.
/// </summary>
public partial class TransferManager : Node
{
    private static TransferManager? _instance;
    public static TransferManager? Instance => _instance;

    private readonly Dictionary<string, ActiveTransfer> _activeTransfers = new();
    private readonly Dictionary<int, List<TransferSchedule>> _schedulesByContinent = new();
    private readonly Dictionary<int, List<TransferStationRegistration>> _stationsByContinent = new();

    // Lookup for resolving endpoints
    private readonly Dictionary<int, IResourceEndpoint> _continentEndpoints = new();
    private readonly Dictionary<string, IResourceEndpoint> _stationEndpoints = new();

    private double _totalTime;

    public int ActiveTransferCount => _activeTransfers.Count;

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }

        _instance = this;
        GameLogger.Info("[TransferManager] Initialized");
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
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
    /// Registers a continent's economy as a transfer endpoint.
    /// </summary>
    public void RegisterContinentEndpoint(int continentIndex, IResourceEndpoint endpoint)
    {
        _continentEndpoints[continentIndex] = endpoint;
    }

    /// <summary>
    /// Unregisters a continent endpoint.
    /// </summary>
    public void UnregisterContinentEndpoint(int continentIndex)
    {
        _continentEndpoints.Remove(continentIndex);
    }

    /// <summary>
    /// Registers an orbital station's economy as a transfer endpoint.
    /// </summary>
    public void RegisterStationEndpoint(string stationId, IResourceEndpoint endpoint)
    {
        _stationEndpoints[stationId] = endpoint;
    }

    /// <summary>
    /// Unregisters an orbital station endpoint.
    /// </summary>
    public void UnregisterStationEndpoint(string stationId)
    {
        _stationEndpoints.Remove(stationId);
    }

    #endregion

    #region Transfer Station Tracking

    /// <summary>
    /// Called when a transfer station building completes construction on a continent.
    /// </summary>
    public void OnTransferStationBuilt(int continentIndex, BuildingConstruction building)
    {
        if (building.Definition?.TransferStation == null)
            return;

        if (!_stationsByContinent.TryGetValue(continentIndex, out var stations))
        {
            stations = new List<TransferStationRegistration>();
            _stationsByContinent[continentIndex] = stations;
        }

        stations.Add(new TransferStationRegistration
        {
            Building = building,
            Definition = building.Definition.TransferStation,
        });

        float newCapacity = GetTotalCapacity(continentIndex);
        SignalBus.Instance?.EmitContinentTransferCapacityChanged(continentIndex, newCapacity);

        GameLogger.Info(
            $"[TransferManager] Transfer station built on continent {continentIndex} " +
            $"(capacity: {newCapacity:F0})"
        );
    }

    /// <summary>
    /// Called when a transfer station building is destroyed on a continent.
    /// In-flight transfers are unaffected.
    /// </summary>
    public void OnTransferStationDestroyed(int continentIndex, BuildingConstruction building)
    {
        if (!_stationsByContinent.TryGetValue(continentIndex, out var stations))
            return;

        stations.RemoveAll(r => r.Building == building);

        float newCapacity = GetTotalCapacity(continentIndex);
        SignalBus.Instance?.EmitContinentTransferCapacityChanged(continentIndex, newCapacity);

        // If no stations remain, stop all schedules for this continent
        if (stations.Count == 0)
        {
            StopAllSchedulesForContinent(continentIndex);
        }

        GameLogger.Info(
            $"[TransferManager] Transfer station destroyed on continent {continentIndex} " +
            $"(remaining capacity: {newCapacity:F0})"
        );
    }

    /// <summary>
    /// Whether a continent has at least one transfer station.
    /// </summary>
    public bool HasTransferStation(int continentIndex)
    {
        return _stationsByContinent.TryGetValue(continentIndex, out var stations)
               && stations.Count > 0;
    }

    /// <summary>
    /// Gets the total cargo capacity available on a continent (sum of all stations).
    /// </summary>
    public float GetTotalCapacity(int continentIndex)
    {
        if (!_stationsByContinent.TryGetValue(continentIndex, out var stations))
            return 0f;

        float total = 0f;
        foreach (var station in stations)
        {
            total += station.Definition.CargoCapacity;
        }
        return total;
    }

    /// <summary>
    /// Gets the maximum concurrent transfers allowed from a continent (sum of all stations).
    /// </summary>
    public int GetMaxConcurrentTransfers(int continentIndex)
    {
        if (!_stationsByContinent.TryGetValue(continentIndex, out var stations))
            return 0;

        int total = 0;
        foreach (var station in stations)
        {
            total += station.Definition.MaxConcurrentTransfers;
        }
        return total;
    }

    /// <summary>
    /// Gets the best (fastest) vehicle speed from stations on a continent.
    /// </summary>
    public float GetVehicleSpeed(int continentIndex)
    {
        if (!_stationsByContinent.TryGetValue(continentIndex, out var stations) || stations.Count == 0)
            return 0f;

        float best = 0f;
        foreach (var station in stations)
        {
            if (station.Definition.VehicleSpeed > best)
                best = station.Definition.VehicleSpeed;
        }
        return best;
    }

    /// <summary>
    /// Gets the count of currently active transfers from a continent.
    /// </summary>
    public int GetActiveTransferCountForContinent(int continentIndex)
    {
        int count = 0;
        foreach (var kvp in _activeTransfers)
        {
            if (kvp.Value.Order.OriginContinentIndex == continentIndex)
                count++;
        }
        return count;
    }

    #endregion

    #region One-Time Transfers

    /// <summary>
    /// Dispatches a one-time transfer of resources from a continent to a destination.
    /// Resources are withdrawn from the origin immediately; deposited at destination on arrival.
    /// </summary>
    /// <returns>The order ID if successful, or null if validation failed.</returns>
    public string? DispatchOneTimeTransfer(
        int originContinentIndex,
        TransferDestination destination,
        Dictionary<string, float> requestedResources)
    {
        // Validate origin has a transfer station
        if (!HasTransferStation(originContinentIndex))
        {
            GameLogger.Warning(
                $"[TransferManager] Cannot dispatch: continent {originContinentIndex} has no transfer station"
            );
            return null;
        }

        // Validate concurrent transfer limit
        int activeCount = GetActiveTransferCountForContinent(originContinentIndex);
        int maxConcurrent = GetMaxConcurrentTransfers(originContinentIndex);
        if (activeCount >= maxConcurrent)
        {
            GameLogger.Warning(
                $"[TransferManager] Cannot dispatch: continent {originContinentIndex} " +
                $"at max concurrent transfers ({activeCount}/{maxConcurrent})"
            );
            return null;
        }

        // Validate origin endpoint exists
        if (!_continentEndpoints.TryGetValue(originContinentIndex, out var originEndpoint))
        {
            GameLogger.Warning(
                $"[TransferManager] Cannot dispatch: no economy for continent {originContinentIndex}"
            );
            return null;
        }

        // Validate destination endpoint exists
        IResourceEndpoint? destEndpoint = ResolveEndpoint(destination);
        if (destEndpoint == null)
        {
            GameLogger.Warning(
                $"[TransferManager] Cannot dispatch: destination {destination} not found"
            );
            return null;
        }

        // Compute travel time
        float travelTime = ComputeTravelTime(originContinentIndex, destination);
        if (travelTime <= 0f)
        {
            GameLogger.Warning("[TransferManager] Cannot dispatch: invalid travel time");
            return null;
        }

        // Build cargo manifest, capped by capacity
        float totalCapacity = GetTotalCapacity(originContinentIndex);
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

            // Get transport weight for this resource
            float weight = GetTransportWeight(resourceId);

            // How much can we fit?
            float remainingCapacity = totalCapacity - usedCapacity;
            float maxUnits = remainingCapacity / weight;
            float toLoad = Math.Min(requestedAmount, maxUnits);

            if (toLoad <= 0f)
                continue;

            // Withdraw from origin
            float actualWithdrawn = originEndpoint.WithdrawResource(resourceId, toLoad);
            if (actualWithdrawn > 0f)
            {
                manifest.LoadResource(resourceId, actualWithdrawn);
                usedCapacity += actualWithdrawn * weight;
            }
        }

        // If nothing was loaded, abort
        if (manifest.TotalUnits <= 0f)
        {
            GameLogger.Warning("[TransferManager] Cannot dispatch: no resources available to load");
            return null;
        }

        // Create the order
        var order = new TransferOrder
        {
            OriginContinentIndex = originContinentIndex,
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

        SignalBus.Instance?.EmitTransferDispatched(order.OrderId, originContinentIndex);

        GameLogger.Info(
            $"[TransferManager] Dispatched transfer {order.OrderId[..8]}... " +
            $"from continent {originContinentIndex} to {destination} " +
            $"({manifest.TotalUnits:F1} units, ETA {travelTime:F1}s)"
        );

        return order.OrderId;
    }

    #endregion

    #region Travel Time

    /// <summary>
    /// Computes travel time between an origin continent and a destination.
    /// </summary>
    public float ComputeTravelTime(int originContinentIndex, TransferDestination destination)
    {
        float speed = GetVehicleSpeed(originContinentIndex);
        if (speed <= 0f)
            return 0f;

        float distance = ComputeDistance(originContinentIndex, destination);
        if (distance <= 0f)
            return 0f;

        return distance / speed;
    }

    private float ComputeDistance(int originContinentIndex, TransferDestination destination)
    {
        // For continent-to-continent: use distance between averaged centers
        if (!destination.IsOrbitalStation && destination.ContinentIndex.HasValue)
        {
            var originContinent = FindContinent(originContinentIndex);
            var destContinent = FindContinent(destination.ContinentIndex.Value);

            if (originContinent != null && destContinent != null)
            {
                // Great-circle distance approximated by Euclidean distance between centers
                return originContinent.averagedCenter.DistanceTo(destContinent.averagedCenter);
            }
        }

        // For continent-to-station: use orbital radius as proxy for distance
        // This is a simplified model; in reality it would depend on launch geometry
        if (destination.IsOrbitalStation)
        {
            // Default distance for orbital transfers
            return 100f;
        }

        return 0f;
    }

    private Continent? FindContinent(int continentIndex)
    {
        // Try to find continent via the endpoint's economy
        if (_continentEndpoints.TryGetValue(continentIndex, out var endpoint)
            && endpoint is ContinentEconomy economy)
        {
            return economy.Continent;
        }
        return null;
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
            else if (order.State == SurfaceTransferState.Complete
                     || order.State == SurfaceTransferState.Reverting)
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
        IResourceEndpoint? originEndpoint = _continentEndpoints.TryGetValue(
            order.OriginContinentIndex, out var ep) ? ep : null;

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
                    // Revert to origin
                    if (originEndpoint != null)
                    {
                        float reverted = originEndpoint.DepositResource(resourceId, remainder);
                        float lost = remainder - reverted;
                        totalReverted += reverted;

                        if (lost > 0f)
                        {
                            GameLogger.Warning(
                                $"[TransferManager] {lost:F1} units of '{resourceId}' " +
                                $"lost (both destination and origin full)"
                            );
                        }
                    }
                    else
                    {
                        totalReverted += remainder;
                        GameLogger.Warning(
                            $"[TransferManager] {remainder:F1} units of '{resourceId}' " +
                            $"lost (origin endpoint gone)"
                        );
                    }
                }
            }
            else
            {
                // Destination gone; revert everything
                if (originEndpoint != null)
                {
                    float reverted = originEndpoint.DepositResource(resourceId, amount);
                    totalReverted += reverted;
                }
            }
        }

        bool fullyAccepted = totalReverted <= 0f;
        order.State = SurfaceTransferState.Complete;

        SignalBus.Instance?.EmitTransferArrived(order.OrderId, fullyAccepted);

        if (totalReverted > 0f)
        {
            SignalBus.Instance?.EmitTransferReverted(
                order.OrderId, order.OriginContinentIndex, totalReverted
            );
        }

        GameLogger.Info(
            $"[TransferManager] Transfer {order.OrderId[..8]}... completed. " +
            $"Accepted: {fullyAccepted}, Reverted: {totalReverted:F1}"
        );
    }

    private void TickSchedules(float delta)
    {
        foreach (var kvp in _schedulesByContinent)
        {
            int continentIndex = kvp.Key;
            var schedules = kvp.Value;

            for (int i = 0; i < schedules.Count; i++)
            {
                var schedule = schedules[i];
                TickSchedule(schedule, continentIndex);
            }
        }
    }

    private void TickSchedule(TransferSchedule schedule, int continentIndex)
    {
        switch (schedule.State)
        {
            case TransferScheduleState.Accumulating:
                TickScheduleAccumulating(schedule, continentIndex);
                break;

            case TransferScheduleState.Dispatched:
                TickScheduleDispatched(schedule);
                break;
        }
    }

    private void TickScheduleAccumulating(TransferSchedule schedule, int continentIndex)
    {
        if (!_continentEndpoints.TryGetValue(continentIndex, out var endpoint))
            return;

        float totalCapacity = GetTotalCapacity(continentIndex);
        if (totalCapacity <= 0f)
            return;

        // Resolve target quantities from proportions
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

        // Check departure condition
        float thresholdFraction = schedule.Threshold.ToFraction();
        bool shouldDepart;

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

        if (!shouldDepart)
            return;

        // Dispatch the transfer
        string? orderId = DispatchOneTimeTransfer(
            continentIndex,
            schedule.Destination,
            targetQuantities
        );

        if (orderId != null)
        {
            // Tag the order as belonging to this schedule
            if (_activeTransfers.TryGetValue(orderId, out var transfer))
            {
                transfer.Order.SourceScheduleId = schedule.ScheduleId;
            }

            schedule.ActiveTransferOrderId = orderId;
            schedule.State = TransferScheduleState.Dispatched;
            SignalBus.Instance?.EmitTransferScheduleStateChanged(
                schedule.ScheduleId, (int)TransferScheduleState.Dispatched
            );

            GameLogger.Info(
                $"[TransferManager] Schedule {schedule.ScheduleId[..8]}... dispatched transfer {orderId[..8]}..."
            );
        }
    }

    private void TickScheduleDispatched(TransferSchedule schedule)
    {
        if (schedule.ActiveTransferOrderId == null)
        {
            // No active transfer — go back to accumulating
            schedule.State = TransferScheduleState.Accumulating;
            SignalBus.Instance?.EmitTransferScheduleStateChanged(
                schedule.ScheduleId, (int)TransferScheduleState.Accumulating
            );
            return;
        }

        // Check if the transfer has completed
        if (!_activeTransfers.ContainsKey(schedule.ActiveTransferOrderId))
        {
            schedule.ActiveTransferOrderId = null;
            schedule.State = TransferScheduleState.Accumulating;
            SignalBus.Instance?.EmitTransferScheduleStateChanged(
                schedule.ScheduleId, (int)TransferScheduleState.Accumulating
            );

            GameLogger.Info(
                $"[TransferManager] Schedule {schedule.ScheduleId[..8]}... transfer completed, resuming accumulation"
            );
        }
    }

    #endregion

    #region Schedules (Phase 4 placeholder)

    /// <summary>
    /// Creates a new recurring transfer schedule.
    /// </summary>
    public string? CreateSchedule(
        int originContinentIndex,
        TransferDestination destination,
        Dictionary<string, float> resourceProportions,
        DepartureConditionMode departureMode,
        DepartureThreshold threshold)
    {
        if (!HasTransferStation(originContinentIndex))
        {
            GameLogger.Warning(
                $"[TransferManager] Cannot create schedule: continent {originContinentIndex} has no transfer station"
            );
            return null;
        }

        var schedule = new TransferSchedule
        {
            OriginContinentIndex = originContinentIndex,
            Destination = destination,
            ResourceProportions = new Dictionary<string, float>(resourceProportions),
            DepartureMode = departureMode,
            Threshold = threshold,
            State = TransferScheduleState.Idle,
        };

        if (!_schedulesByContinent.TryGetValue(originContinentIndex, out var schedules))
        {
            schedules = new List<TransferSchedule>();
            _schedulesByContinent[originContinentIndex] = schedules;
        }

        schedules.Add(schedule);

        GameLogger.Info(
            $"[TransferManager] Schedule {schedule.ScheduleId[..8]}... created " +
            $"for continent {originContinentIndex} to {destination}"
        );

        return schedule.ScheduleId;
    }

    /// <summary>
    /// Starts a schedule that is currently Idle.
    /// </summary>
    public bool StartSchedule(string scheduleId)
    {
        var schedule = FindSchedule(scheduleId);
        if (schedule == null)
            return false;

        if (schedule.State != TransferScheduleState.Idle
            && schedule.State != TransferScheduleState.Stopped)
        {
            GameLogger.Warning(
                $"[TransferManager] Cannot start schedule {scheduleId[..8]}...: state is {schedule.State}"
            );
            return false;
        }

        schedule.State = TransferScheduleState.Accumulating;
        SignalBus.Instance?.EmitTransferScheduleStateChanged(
            scheduleId, (int)TransferScheduleState.Accumulating
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
            scheduleId, (int)TransferScheduleState.Stopped
        );
        return true;
    }

    /// <summary>
    /// Removes a schedule entirely.
    /// </summary>
    public bool RemoveSchedule(string scheduleId)
    {
        foreach (var kvp in _schedulesByContinent)
        {
            int removed = kvp.Value.RemoveAll(s => s.ScheduleId == scheduleId);
            if (removed > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets all schedules for a continent.
    /// </summary>
    public IReadOnlyList<TransferSchedule> GetSchedulesForContinent(int continentIndex)
    {
        if (_schedulesByContinent.TryGetValue(continentIndex, out var schedules))
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
        foreach (var kvp in _schedulesByContinent)
        {
            foreach (var schedule in kvp.Value)
            {
                if (schedule.ScheduleId == scheduleId)
                    return schedule;
            }
        }
        return null;
    }

    private void StopAllSchedulesForContinent(int continentIndex)
    {
        if (!_schedulesByContinent.TryGetValue(continentIndex, out var schedules))
            return;

        foreach (var schedule in schedules)
        {
            if (schedule.State != TransferScheduleState.Stopped)
            {
                schedule.State = TransferScheduleState.Stopped;
                SignalBus.Instance?.EmitTransferScheduleStateChanged(
                    schedule.ScheduleId, (int)TransferScheduleState.Stopped
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
            _stationEndpoints.TryGetValue(destination.StationSatelliteId, out var ep);
            return ep;
        }

        if (destination.ContinentIndex.HasValue)
        {
            _continentEndpoints.TryGetValue(destination.ContinentIndex.Value, out var ep);
            return ep;
        }

        return null;
    }

    private static float GetTransportWeight(string resourceId)
    {
        var resourceDb = ResourceDatabase.Instance;
        if (resourceDb != null && resourceDb.IsLoaded
            && resourceDb.TryGetResource(resourceId, out var def) && def != null)
        {
            return def.TransportWeight;
        }
        return 1.0f;
    }

    #endregion

    /// <summary>
    /// Tracks a transfer station building on a continent.
    /// </summary>
    public class TransferStationRegistration
    {
        public BuildingConstruction Building { get; set; } = null!;
        public TransferStationDefinition Definition { get; set; } = null!;
    }

    /// <summary>
    /// Tracks an in-flight transfer.
    /// </summary>
    public class ActiveTransfer
    {
        public TransferOrder Order { get; set; } = null!;
    }
}
