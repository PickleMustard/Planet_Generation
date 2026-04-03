using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace Constructables.ArtificialSatellites;

/// <summary>
/// A station capable of constructing ships via a queued build system.
/// Ships are built from the station's internal stockpile with configurable parallel build slots.
/// </summary>
public partial class ConstructionYardStation : StationSatellite
{
    private ShipBuildQueue? _shipBuildQueue;

    /// <summary>
    /// The maximum number of ships that can be built simultaneously.
    /// </summary>
    public int MaxParallelShipBuilds => _shipBuildQueue != null
        ? _shipBuildQueue.GetActiveBuilds().Count + _shipBuildQueue.GetQueuedShips().Count
        : 0;

    public override void SetStationDefinition(StationDefinition definition)
    {
        base.SetStationDefinition(definition);
        _shipBuildQueue = new ShipBuildQueue(this, definition.MaxParallelShipBuilds);
        GameLogger.Info($"ConstructionYardStation {Name}: Initialized with {definition.MaxParallelShipBuilds} parallel build slot(s)");
    }

    protected override void TickOperational(float delta)
    {
        base.TickOperational(delta);
        _shipBuildQueue?.Tick(delta);
    }

    /// <summary>
    /// Adds a ship to this station's build queue.
    /// The ship should already have its ShipDefinition set.
    /// </summary>
    public void EnqueueShipConstruction(LogisticsUnit ship)
    {
        if (_shipBuildQueue == null)
        {
            GameLogger.Warning($"ConstructionYardStation {Name}: Cannot enqueue ship - no build queue initialized");
            return;
        }

        ship.StartConstruction(new Godot.Collections.Dictionary());
        _shipBuildQueue.Enqueue(ship);
    }

    /// <summary>
    /// Cancels construction of a ship, removing it from the queue or active builds.
    /// </summary>
    public void CancelShipConstruction(LogisticsUnit ship)
    {
        _shipBuildQueue?.Cancel(ship);
    }

    /// <summary>
    /// Returns the list of ships waiting in the build queue.
    /// </summary>
    public IReadOnlyList<LogisticsUnit> GetShipBuildQueue()
    {
        return _shipBuildQueue?.GetQueuedShips() ?? (IReadOnlyList<LogisticsUnit>)new List<LogisticsUnit>();
    }

    /// <summary>
    /// Returns the list of ships currently being actively built.
    /// </summary>
    public IReadOnlyList<LogisticsUnit> GetActiveBuilds()
    {
        return _shipBuildQueue?.GetActiveBuilds() ?? (IReadOnlyList<LogisticsUnit>)new List<LogisticsUnit>();
    }
}
