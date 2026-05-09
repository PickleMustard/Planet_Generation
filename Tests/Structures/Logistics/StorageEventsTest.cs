using System;
using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Logistics;

namespace Tests.Structures.Logistics;

[TestSuite]
public class StorageEventsTest
{
    private static (Storage storage, List<(string id, float delta)> events) Make(float capacity = 100f)
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        var events = new List<(string id, float delta)>();
        storage.StorageUpdated += (id, delta) => events.Add((id, delta));
        return (storage, events);
    }

    [TestCase]
    public void Deposit_NonZero_FiresEventWithPositiveDelta()
    {
        var (storage, events) = Make();

        storage.Deposit("iron_ore", 30f);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].id).IsEqual("iron_ore");
        AssertThat(events[0].delta).IsEqual(30f);
    }

    [TestCase]
    public void Deposit_ZeroAmount_DoesNotFire()
    {
        var (storage, events) = Make();

        storage.Deposit("iron_ore", 0f);

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void Deposit_FullStorage_DoesNotFire()
    {
        // Slot capacity comes from the resource's MaxStackSize; without a database
        // the fallback is StorageSlot.FallbackCapacity (100). Fill to that, then a
        // further deposit should be a no-op and not fire an event.
        var (storage, events) = Make();
        storage.Deposit("iron_ore", StorageSlot.FallbackCapacity);
        events.Clear();

        storage.Deposit("iron_ore", 10f);

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void Withdraw_NonZero_FiresEventWithNegativeDelta()
    {
        var (storage, events) = Make();
        storage.Deposit("iron_ore", 50f);
        events.Clear();

        storage.Withdraw("iron_ore", 20f);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].id).IsEqual("iron_ore");
        AssertThat(events[0].delta).IsEqual(-20f);
    }

    [TestCase]
    public void Withdraw_PartialFulfillment_DeltaMatchesActual()
    {
        var (storage, events) = Make();
        storage.Deposit("iron_ore", 10f);
        events.Clear();

        storage.Withdraw("iron_ore", 100f);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].delta).IsEqual(-10f);
    }

    [TestCase]
    public void Withdraw_EmptyStorage_DoesNotFire()
    {
        var (storage, events) = Make();

        storage.Withdraw("iron_ore", 50f);

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void AddSlot_PrePopulatedQuantity_FiresEvent()
    {
        var storage = new Storage();
        var events = new List<(string id, float delta)>();
        storage.StorageUpdated += (id, delta) => events.Add((id, delta));

        var slot = new StorageSlot(SlotFilter.ForResource("iron_ore"));
        slot.OccupiedResourceId = "iron_ore";
        slot.Quantity = 25f;
        storage.AddSlot(slot);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].id).IsEqual("iron_ore");
        AssertThat(events[0].delta).IsEqual(25f);
    }

    [TestCase]
    public void AddSlot_EmptyQuantity_DoesNotFire()
    {
        var storage = new Storage();
        var events = new List<(string id, float delta)>();
        storage.StorageUpdated += (id, delta) => events.Add((id, delta));

        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void RemoveSlot_NonEmpty_FiresNegativeDelta()
    {
        var (storage, events) = Make();
        storage.Deposit("iron_ore", 40f);
        var slot = storage.Slots[0];
        events.Clear();

        storage.RemoveSlot(slot);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].delta).IsEqual(-40f);
    }

    [TestCase]
    public void RemoveSlot_Empty_DoesNotFire()
    {
        var (storage, events) = Make();
        var slot = storage.Slots[0];

        storage.RemoveSlot(slot);

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void ThrowingSubscriber_DoesNotCorruptAccountingOrPropagate()
    {
        var (storage, events) = Make();
        storage.StorageUpdated += (_, _) => throw new InvalidOperationException("boom");

        float deposited = storage.Deposit("iron_ore", 30f);

        AssertThat(deposited).IsEqual(30f);
        AssertThat(storage.GetQuantity("iron_ore")).IsEqual(30f);
        AssertThat(events.Count).IsEqual(1);
    }
}
