using System.Collections.Generic;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using GdUnit4;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using Structures.Transfers;
using static GdUnit4.Assertions;

namespace Tests.Constructables;

[TestSuite]
public class BodyTransferManagerTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void RegisterEndpoint_ExposesItForDispatch()
    {
        var mgr = new BodyTransferManager();
        var (building, endpoint) = MakeHub("hub-A");

        mgr.RegisterEndpoint(building.Id, endpoint, MakeStationDef(), building);

        AssertThat(mgr.HasEndpoint(building.Id)).IsTrue();
        AssertThat(mgr.GetEndpointBuilding(building.Id)).IsEqual(building);
        AssertThat(mgr.GetCapacity(building.Id)).IsEqual(500f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DispatchOneTimeTransfer_DeliversToDestinationAfterTravelTime()
    {
        var mgr = new BodyTransferManager();
        var (origin, originEp) = MakeHub("hub-origin");
        var (dest, destEp) = MakeHub("hub-dest");

        mgr.RegisterEndpoint(origin.Id, originEp, MakeStationDef(), origin);
        mgr.RegisterEndpoint(dest.Id, destEp, MakeStationDef(), dest);

        // Seed origin storage.
        originEp.DepositResource("iron", 80f);

        var orderId = mgr.DispatchOneTimeTransfer(
            origin.Id,
            TransferDestination.ForBuilding(dest.Id),
            new Dictionary<string, float> { { "iron", 50f } }
        );
        AssertThat(orderId).IsNotNull();
        AssertThat(mgr.IsTransferActive(orderId!)).IsTrue();

        // Travel time = distance(100) / speed(50) = 2 seconds.
        // Tick a single physics step long enough to complete it.
        mgr._PhysicsProcess(2.5);

        AssertThat(mgr.IsTransferActive(orderId!)).IsFalse();
        AssertThat(destEp.GetStockpile("iron")).IsGreater(0f);
        AssertThat(originEp.GetStockpile("iron")).IsLess(80f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void UnregisterEndpoint_StopsSchedulesFromThatOrigin()
    {
        var mgr = new BodyTransferManager();
        var (origin, originEp) = MakeHub("hub-origin");
        var (dest, destEp) = MakeHub("hub-dest");

        mgr.RegisterEndpoint(origin.Id, originEp, MakeStationDef(), origin);
        mgr.RegisterEndpoint(dest.Id, destEp, MakeStationDef(), dest);

        var scheduleId = mgr.CreateSchedule(
            origin.Id,
            TransferDestination.ForBuilding(dest.Id),
            new Dictionary<string, float> { { "iron", 1.0f } },
            DepartureConditionMode.AnyResource,
            DepartureThreshold.Half
        );
        AssertThat(scheduleId).IsNotNull();
        mgr.StartSchedule(scheduleId!);

        mgr.UnregisterEndpoint(origin.Id);

        var schedules = mgr.GetSchedulesForOrigin(origin.Id);
        AssertThat(schedules.Count).IsEqual(1);
        AssertThat(schedules[0].State).IsEqual(TransferScheduleState.Stopped);
        AssertThat(mgr.HasEndpoint(origin.Id)).IsFalse();
    }

    private static (Building building, BuildingResourceEndpoint endpoint) MakeHub(string idHint)
    {
        var building = new Building { Id = idHint };
        var hub = new StorageHubBehavior
        {
            StorageCapacity = 8,
            SlotFilters = new List<SlotFilterSpec>
            {
                new(SlotFilter.Any(), 8),
            },
        };
        hub.OnAttach(building);
        hub.OnRegister();
        return (building, new BuildingResourceEndpoint(building));
    }

    private static TransferStationDefinition MakeStationDef() => new()
    {
        CargoCapacity = 500f,
        VehicleSpeed = 50f,
        MaxConcurrentTransfers = 2,
    };
}
