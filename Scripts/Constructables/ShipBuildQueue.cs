using System.Collections.Generic;
using Constructables.ArtificialSatellites;
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

            if (ship.CheckRequiredResourcesAvailable())
            {
                ship.UpdateProgress(delta);
            }

            if (emitProgress)
            {
                ConstructionManager.Instance?.NotifyProgressUpdate(
                    ship.Name.ToString(), ship.GetProgress(), ship.GetStatus());
            }

            if (ship.GetStatus() == ConstructionStatus.Complete.ToString())
            {
                _activeBuilds.RemoveAt(i);
                ConstructionManager.Instance?.NotifyConstructionComplete(ship);
                GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Ship '{ship.Name}' construction complete");
            }
        }
    }

    private void PromoteFromQueue()
    {
        while (_activeBuilds.Count < _maxParallelBuilds && _queue.Count > 0)
        {
            var ship = _queue[0];
            _queue.RemoveAt(0);
            _activeBuilds.Add(ship);
            GameLogger.Info($"ShipBuildQueue [{_owner.Name}]: Promoted '{ship.Name}' to active build (active: {_activeBuilds.Count}/{_maxParallelBuilds})");
        }
    }
}
