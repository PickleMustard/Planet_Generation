using System;
using System.Collections.Generic;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Bookkeeping for ships being constructed at a station: enqueue, cancel, pause, reorder, and
/// promotion of queued ships into the available parallel build slots. It does NOT tick or check
/// resources — the owning <see cref="Stations.Behaviors.ShipyardBehavior"/> owns construction
/// progress and resource securing and calls <see cref="PromoteFromQueue"/> each tick.
/// </summary>
public class ShipBuildQueue
{
    private readonly StationSatellite _owner;
    private readonly List<LogisticsUnit> _queue = new();
    private readonly List<LogisticsUnit> _activeBuilds = new();
    private readonly HashSet<LogisticsUnit> _pausedShips = new();
    private readonly int _maxParallelBuilds;

    /// <summary>
    /// Fired when a ship is cancelled from the queue or an active build (main thread). The
    /// <see cref="Stations.Behaviors.ShipyardBehavior"/> handles refund + node cleanup.
    /// </summary>
    public event Action<LogisticsUnit>? ShipCancelled;

    public ShipBuildQueue(StationSatellite owner, int maxParallelBuilds)
    {
        _owner = owner;
        _maxParallelBuilds = maxParallelBuilds;
    }

    public int ActiveCount => _activeBuilds.Count;
    public int QueuedCount => _queue.Count;
    public int MaxParallelBuilds => _maxParallelBuilds;
    public IReadOnlyList<LogisticsUnit> GetQueuedShips() => _queue;
    public IReadOnlyList<LogisticsUnit> GetActiveBuilds() => _activeBuilds;

    /// <summary>Whether the user has manually paused this ship's build.</summary>
    public bool IsPaused(LogisticsUnit ship) => _pausedShips.Contains(ship);

    /// <summary>
    /// Adds a ship to the build queue. The station should already have created its construction state.
    /// </summary>
    public void Enqueue(LogisticsUnit ship)
    {
        _queue.Add(ship);
        GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Enqueued ship '{ship.Name}' (queue: {_queue.Count}, active: {_activeBuilds.Count}/{_maxParallelBuilds})");
    }

    /// <summary>
    /// Cancels a ship's construction, removing it from the queue or active builds,
    /// then invokes the <see cref="ShipCancelled"/> callback.
    /// </summary>
    public void Cancel(LogisticsUnit ship)
    {
        if (_queue.Remove(ship))
        {
            GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Removed '{ship.Name}' from queue");
        }
        else if (_activeBuilds.Remove(ship))
        {
            GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Removed '{ship.Name}' from active builds");
        }
        _pausedShips.Remove(ship);

        ShipCancelled?.Invoke(ship);
    }

    /// <summary>
    /// Removes a ship from the active builds list (used by the behavior when a build completes).
    /// </summary>
    public void RemoveActive(LogisticsUnit ship)
    {
        _activeBuilds.Remove(ship);
        _pausedShips.Remove(ship);
    }

    /// <summary>
    /// Toggles manual pause on a ship. When paused, an active ship is moved back to the
    /// front of the queue (releasing its slot); a queued ship simply gets the flag.
    /// Resume clears the flag so the ship is eligible for the next promotion.
    /// </summary>
    public void SetManualPause(LogisticsUnit ship, bool paused)
    {
        if (_pausedShips.Contains(ship) == paused)
            return;

        if (paused)
            _pausedShips.Add(ship);
        else
            _pausedShips.Remove(ship);

        if (paused && _activeBuilds.Remove(ship))
        {
            _queue.Insert(0, ship);
            GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Paused active '{ship.Name}', returned to queue front");
        }
        else
        {
            GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: {(paused ? "Paused" : "Resumed")} '{ship.Name}'");
        }
    }

    /// <summary>
    /// Moves <paramref name="ship"/> in the queue so it appears immediately before
    /// <paramref name="before"/>. Pass null to move to the end. No-op for active builds
    /// or when the ship is not queued. Returns true if the order changed.
    /// </summary>
    public bool ReorderQueue(LogisticsUnit ship, LogisticsUnit? before)
    {
        int fromIndex = _queue.IndexOf(ship);
        if (fromIndex < 0)
            return false;

        int targetIndex;
        if (before == null)
        {
            targetIndex = _queue.Count - 1;
        }
        else
        {
            int beforeIndex = _queue.IndexOf(before);
            if (beforeIndex < 0)
                return false;
            targetIndex = beforeIndex;
            if (fromIndex < beforeIndex)
                targetIndex -= 1;
        }

        if (targetIndex == fromIndex)
            return false;

        _queue.RemoveAt(fromIndex);
        _queue.Insert(targetIndex, ship);
        GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Reordered '{ship.Name}' ({fromIndex} → {targetIndex})");
        return true;
    }

    /// <summary>
    /// Fills free build slots with the first non-paused queued ships. Called each tick by the
    /// owning <see cref="Stations.Behaviors.ShipyardBehavior"/>.
    /// </summary>
    public void PromoteFromQueue()
    {
        while (_activeBuilds.Count < _maxParallelBuilds)
        {
            int nextIndex = -1;
            for (int i = 0; i < _queue.Count; i++)
            {
                if (!IsPaused(_queue[i]))
                {
                    nextIndex = i;
                    break;
                }
            }

            if (nextIndex < 0)
                return;

            var ship = _queue[nextIndex];
            _queue.RemoveAt(nextIndex);
            _activeBuilds.Add(ship);
            GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Promoted '{ship.Name}' to active build (active: {_activeBuilds.Count}/{_maxParallelBuilds})");
        }
    }
}
