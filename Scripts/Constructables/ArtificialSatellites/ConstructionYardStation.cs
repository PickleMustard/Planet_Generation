using System.Collections.Generic;
using Structures.Logistics;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// A station capable of constructing ships via a queued build system.
/// Ships are built from the station's internal stockpile with configurable parallel build slots.
/// </summary>
public partial class ConstructionYardStation : StationSatellite
{
    private ShipBuildQueue? _shipBuildQueue;

    /// <summary>
    /// Maximum number of ships that can be built in parallel at this yard.
    /// </summary>
    public int MaxParallelShipBuilds => _shipBuildQueue?.MaxParallelBuilds ?? 0;

    /// <summary>
    /// Number of ships currently occupying parallel build slots.
    /// </summary>
    public int ActiveShipBuildCount => _shipBuildQueue?.ActiveCount ?? 0;

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

        ship.ConstructingStation = this;
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
    {
        _shipBuildQueue?.SetManualPause(ship, paused);
    }

    /// <summary>
    /// Moves <paramref name="ship"/> to appear immediately before <paramref name="before"/>
    /// in the queue. Pass null to move to the end. No-op for active builds.
    /// </summary>
    public void ReorderQueue(LogisticsUnit ship, LogisticsUnit? before)
    {
        _shipBuildQueue?.ReorderQueue(ship, before);
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

    private void RefundDeliveredResources(LogisticsUnit ship)
    {
        var delivered = ship.availableResources;
        if (delivered == null || delivered.Count == 0)
            return;

        var economy = Economy ?? InitializeEconomy();

        foreach (var kvp in delivered)
        {
            string resourceId = kvp.Key;
            int amount = kvp.Value;
            if (amount <= 0)
                continue;

            float deposited = economy.DepositResource(resourceId, amount);
            if (deposited < amount)
            {
                GameLogger.Warning(
                    $"ConstructionYardStation {Name}: Refund partial for '{ship.Name}' — " +
                    $"{resourceId} {deposited}/{amount} (stockpile at capacity)");
            }
        }

        ship.availableResources = new Godot.Collections.Dictionary<string, int>();
    }
}
