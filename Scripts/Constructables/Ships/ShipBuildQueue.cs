using System.Collections.Generic;
using Structures.Enums;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Manages a queue of ships being constructed at a construction-yard station.
/// Ships are promoted from the queue to active builds based on available parallel slots.
/// The owning station ticks this each physics frame.
/// </summary>
public class ShipBuildQueue
{
    private readonly StationSatellite _owner;
    private readonly List<LogisticsUnit> _queue = new();
    private readonly List<LogisticsUnit> _activeBuilds = new();
    private readonly int _maxParallelBuilds;

    private float _progressTimer;
    private const float PROGRESS_SIGNAL_INTERVAL = 0.5f;

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

    /// <summary>
    /// Adds a ship to the build queue. The ship should already have its definition set
    /// and StartConstruction called (it will start as Blocked until promoted).
    /// </summary>
    public void Enqueue(LogisticsUnit ship)
    {
        _queue.Add(ship);
        GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Enqueued ship '{ship.Name}' (queue: {_queue.Count}, active: {_activeBuilds.Count}/{_maxParallelBuilds})");
    }

    /// <summary>
    /// Cancels a ship's construction, removing it from the queue or active builds.
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

        ConstructionManager.Instance?.NotifyConstructionCancelled(ship);
    }

    /// <summary>
    /// Toggles manual pause on a ship. When paused, an active ship is moved back to the
    /// front of the queue (releasing its slot); a queued ship simply gets the flag.
    /// Resume clears the flag so the ship is eligible for the next promotion tick.
    /// </summary>
    public void SetManualPause(LogisticsUnit ship, bool paused)
    {
        if (ship.IsManuallyPaused == paused)
            return;

        ship.SetManualPause(paused);

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
    /// Advances all active builds and promotes queued ships to active slots.
    /// Called each physics tick by the owning station.
    /// </summary>
    public void Tick(float delta)
    {
        PromoteFromQueue();

        _progressTimer += delta;
        bool emitProgress = _progressTimer >= PROGRESS_SIGNAL_INTERVAL;
        if (emitProgress)
            _progressTimer = 0f;

        for (int i = _activeBuilds.Count - 1; i >= 0; i--)
        {
            var ship = _activeBuilds[i];

            if (!ship.IsManuallyPaused && ship.CheckRequiredResourcesAvailable())
            {
                ship.UpdateProgress(delta);
            }

            if (emitProgress)
            {
                ConstructionManager.Instance?.NotifyProgressUpdate(
                    ship.Name.ToString(), ship.GetProgress(), ship.GetDisplayStatus());
            }

            if (ship.GetStatus() == ConstructionStatus.Complete.ToString())
            {
                _activeBuilds.RemoveAt(i);
                ConstructionManager.Instance?.NotifyConstructionComplete(ship);
                GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Ship '{ship.Name}' construction complete");
            }
        }

        if (emitProgress)
        {
            foreach (var ship in _queue)
            {
                ConstructionManager.Instance?.NotifyProgressUpdate(
                    ship.Name.ToString(), ship.GetProgress(), ship.GetDisplayStatus());
            }
        }
    }

    private void PromoteFromQueue()
    {
        while (_activeBuilds.Count < _maxParallelBuilds)
        {
            int nextIndex = -1;
            for (int i = 0; i < _queue.Count; i++)
            {
                if (!_queue[i].IsManuallyPaused)
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
