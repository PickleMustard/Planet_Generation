using System.Collections.Generic;
using Godot;
using Constructables.Stations;
using UtilityLibrary;

namespace Constructables.Stations.Behaviors;

/// <summary>
/// Manages a ship build queue for the owning station. Directly corresponds to the
/// legacy <c>ConstructionYardStation</c> — ships are enqueued, ticked, and completed
/// through a <see cref="ShipBuildQueue"/>. <see cref="WantsTick"/> returns
/// <c>true</c> when builds are active or queued, enabling sleep/wake.
/// </summary>
public partial class ShipyardBehavior : RefCounted, IStationBehavior
{
    private StationSatellite? _owner;
    private ShipBuildQueue? _shipBuildQueue;

    /// <summary>Maximum parallel ship builds. Set from <see cref="Structures.Logistics.StationDefinition.MaxParallelShipBuilds"/>.</summary>
    public int MaxParallelShipBuilds { get; set; } = 1;

    public StationSatellite? Owner => _owner;

    public int ActiveShipBuildCount => _shipBuildQueue?.ActiveCount ?? 0;
    public int QueuedShipBuildCount => _shipBuildQueue?.QueuedCount ?? 0;
    public int MaxParallelBuilds => _shipBuildQueue?.MaxParallelBuilds ?? 0;

    public void OnAttach(StationSatellite owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null) return;
        _shipBuildQueue = new ShipBuildQueue(_owner, MaxParallelShipBuilds);
        GameLogger.Info(
            $"ShipyardBehavior {_owner.Name}: Initialized with "
            + $"{MaxParallelShipBuilds} parallel build slot(s)"
        );
    }

    public void OnUnregister() => _shipBuildQueue = null;

    public void OnDetach()
    {
        _owner = null;
        _shipBuildQueue = null;
    }

    public void OnManufactureTick(float delta, StationSatellite owner)
        => _shipBuildQueue?.Tick(delta);

    public bool WantsTick => _shipBuildQueue != null
        && (_shipBuildQueue.ActiveCount > 0 || _shipBuildQueue.QueuedCount > 0);

    public int Priority => 200;

    // --- Public API (mirrors ConstructionYardStation) ---

    /// <summary>
    /// Adds a ship to this station's build queue.
    /// The ship should already have its ShipDefinition set.
    /// </summary>
    public void EnqueueShipConstruction(LogisticsUnit ship)
    {
        if (_shipBuildQueue == null)
        {
            GameLogger.Warning(
                $"ShipyardBehavior {_owner?.Name}: Cannot enqueue ship — no build queue"
            );
            return;
        }
        ship.ConstructingStation = _owner;
        ship.StartConstruction(new Godot.Collections.Dictionary());
        _shipBuildQueue.Enqueue(ship);
    }

    /// <summary>
    /// Cancels construction of a ship, removing it from the queue or active builds.
    /// Refunds all delivered resources back to the station's economy stockpile.
    /// </summary>
    public void CancelShipConstruction(LogisticsUnit ship)
    {
        RefundDeliveredResources(ship);
        _shipBuildQueue?.Cancel(ship);
    }

    /// <summary>
    /// Toggles the manual-pause flag on an in-queue or active ship.
    /// Paused active ships release their slot and return to the front of the queue.
    /// </summary>
    public void SetShipPaused(LogisticsUnit ship, bool paused)
        => _shipBuildQueue?.SetManualPause(ship, paused);

    /// <summary>
    /// Moves <paramref name="ship"/> to appear immediately before <paramref name="before"/>
    /// in the queue. Pass null to move to the end. No-op for active builds.
    /// </summary>
    public void ReorderQueue(LogisticsUnit ship, LogisticsUnit? before)
        => _shipBuildQueue?.ReorderQueue(ship, before);

    /// <summary>Returns the list of ships waiting in the build queue.</summary>
    public IReadOnlyList<LogisticsUnit> GetShipBuildQueue()
        => _shipBuildQueue?.GetQueuedShips()
            ?? (IReadOnlyList<LogisticsUnit>)new List<LogisticsUnit>();

    /// <summary>Returns the list of ships currently being actively built.</summary>
    public IReadOnlyList<LogisticsUnit> GetActiveBuilds()
        => _shipBuildQueue?.GetActiveBuilds()
            ?? (IReadOnlyList<LogisticsUnit>)new List<LogisticsUnit>();

    private void RefundDeliveredResources(LogisticsUnit ship)
    {
        var delivered = ship.availableResources;
        if (delivered == null || delivered.Count == 0)
            return;
        ship.availableResources = new Godot.Collections.Dictionary<string, int>();
    }
}
