using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Stations;
using Constructables.Stations.Behaviors;
using Structures.Logistics;

namespace Tests.Constructables.Stations.Behaviors;

/// <summary>
/// Verifies <see cref="StorageHubBehavior"/> allocates and removes
/// bulk-storage slots correctly.
/// </summary>
[TestSuite]
public class StorageHubBehaviorTest
{
    private static StationSatellite MakeStation(int slotCount)
    {
        var station = new StationSatellite();
        // Ensure BulkStorage is accessible
        return station;
    }

    [TestCase]
    public void OnRegister_AddsStorageCapacitySlotsToBulkStorage()
    {
        var station = MakeStation(0);
        var hub = new StorageHubBehavior { StorageCapacity = 3 };
        hub.OnAttach(station);
        hub.OnRegister();

        AssertThat(station.BulkStorage.Slots.Count).IsEqual(3);

        hub.OnUnregister();
        hub.OnDetach();
    }

    [TestCase]
    public void OnUnregister_RemovesAllAddedSlots()
    {
        var station = MakeStation(0);
        var hub = new StorageHubBehavior { StorageCapacity = 2 };
        hub.OnAttach(station);
        hub.OnRegister();

        AssertThat(station.BulkStorage.Slots.Count).IsEqual(2);

        hub.OnUnregister();

        AssertThat(station.BulkStorage.Slots.Count).IsEqual(0);
        hub.OnDetach();
    }

    [TestCase]
    public void OnRegister_SpecificFiltersAppliedFirst_RemainderAsAny()
    {
        var station = MakeStation(0);
        var hub = new StorageHubBehavior
        {
            StorageCapacity = 4,
            SlotFilters = new List<SlotFilterSpec>
            {
                new(SlotFilter.ForResource("iron"), 2),
            },
        };
        hub.OnAttach(station);
        hub.OnRegister();

        // First 2 slots have resource filter "iron", next 2 are Any
        AssertThat(station.BulkStorage.Slots.Count).IsEqual(4);
        AssertThat(station.BulkStorage.Slots[0].Filter.Kind).IsEqual(SlotFilterKind.Resource);
        AssertThat(station.BulkStorage.Slots[0].Filter.ResourceId).IsEqual("iron");
        AssertThat(station.BulkStorage.Slots[1].Filter.Kind).IsEqual(SlotFilterKind.Resource);
        AssertThat(station.BulkStorage.Slots[1].Filter.ResourceId).IsEqual("iron");
        AssertThat(station.BulkStorage.Slots[2].Filter.Kind).IsEqual(SlotFilterKind.Any);
        AssertThat(station.BulkStorage.Slots[3].Filter.Kind).IsEqual(SlotFilterKind.Any);

        hub.OnUnregister();
        hub.OnDetach();
    }

    [TestCase]
    public void OnRegister_StorageCapacityZero_NoSlotsAdded()
    {
        var station = MakeStation(0);
        var hub = new StorageHubBehavior { StorageCapacity = 0 };
        hub.OnAttach(station);
        hub.OnRegister();

        AssertThat(station.BulkStorage.Slots.Count).IsEqual(0);

        hub.OnUnregister();
        hub.OnDetach();
    }

    [TestCase]
    public void WantsTick_ReturnsFalse()
    {
        var hub = new StorageHubBehavior();
        AssertThat(hub.WantsTick).IsFalse();
    }

    [TestCase]
    public void OnManufactureTick_IsNoOp()
    {
        var station = MakeStation(0);
        var hub = new StorageHubBehavior();
        hub.OnAttach(station);
        // Should not throw
        hub.OnManufactureTick(0.016f, station);
        hub.OnDetach();
    }

    [TestCase]
    public void OnRegister_NullOwner_NoSlotsAdded()
    {
        var hub = new StorageHubBehavior { StorageCapacity = 3 };
        // OnAttach not called — Owner is null
        hub.OnRegister();

        // No crash, no side effects
        hub.OnUnregister();
        hub.OnDetach();
    }
}
