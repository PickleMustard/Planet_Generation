using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Stations;
using Constructables.Stations.Behaviors;

namespace Tests.Constructables.Stations.Behaviors;

/// <summary>
/// Verifies <see cref="ShipyardBehavior"/> creates a ShipBuildQueue
/// and delegates ship construction API correctly.
/// </summary>
[TestSuite]
public class ShipyardBehaviorTest
{
    [TestCase]
    public void OnRegister_CreatesShipBuildQueueWithCorrectMaxParallelBuilds()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 3 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        AssertThat(shipyard.MaxParallelBuilds).IsEqual(3);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void WantsTick_ReturnsFalseWhenQueueEmpty()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        AssertThat(shipyard.WantsTick).IsFalse();

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void WantsTick_ReturnsTrueWhenBuildsActiveOrQueued()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        // After enqueuing, WantsTick should be true
        var ship = new LogisticsUnit();
        shipyard.EnqueueShipConstruction(ship);

        AssertThat(shipyard.WantsTick).IsTrue();

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void OnManufactureTick_DelegatesToQueue()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        // Should not throw
        shipyard.OnManufactureTick(0.016f, station);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void Priority_Is200()
    {
        var shipyard = new ShipyardBehavior();
        AssertThat(shipyard.Priority).IsEqual(200);
    }

    [TestCase]
    public void OnUnregister_ClearsQueue()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        shipyard.OnUnregister();

        AssertThat(shipyard.ActiveShipBuildCount).IsEqual(0);
        AssertThat(shipyard.QueuedShipBuildCount).IsEqual(0);

        shipyard.OnDetach();
    }

    [TestCase]
    public void GetShipBuildQueue_ReturnsEmptyListBeforeRegister()
    {
        var shipyard = new ShipyardBehavior();
        var queue = shipyard.GetShipBuildQueue();
        AssertThat(queue.Count).IsEqual(0);
    }

    [TestCase]
    public void GetActiveBuilds_ReturnsEmptyListBeforeRegister()
    {
        var shipyard = new ShipyardBehavior();
        var active = shipyard.GetActiveBuilds();
        AssertThat(active.Count).IsEqual(0);
    }
}
