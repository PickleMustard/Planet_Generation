using System;
using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Logistics;

namespace Tests.Structures.Logistics;

[TestSuite]
public class StorageEventsTest
{
    private static (Storage storage, List<(string id, int delta)> events) Make()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        var events = new List<(string id, int delta)>();
        storage.StorageUpdated += (id, delta) => events.Add((id, delta));
        return (storage, events);
    }

    [TestCase]
    public void Deposit_NonZero_FiresEventWithPositiveDelta()
    {
        var (storage, events) = Make();

        storage.Deposit("_test_ore", 30);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].id).IsEqual("_test_ore");
        AssertThat(events[0].delta).IsEqual(30);
    }

    [TestCase]
    public void Deposit_ZeroAmount_DoesNotFire()
    {
        var (storage, events) = Make();

        storage.Deposit("_test_ore", 0);

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void Deposit_FullStorage_DoesNotFire()
    {
        // Slot capacity comes from the resource's MaxStackSize; without a database
        // the fallback is StorageSlot.FallbackCapacity (100). Fill to that, then a
        // further deposit should be a no-op and not fire an event.
        var (storage, events) = Make();
        storage.Deposit("_test_ore", StorageSlot.FallbackCapacity);
        events.Clear();

        storage.Deposit("_test_ore", 10);

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void Deposit_SubUnit_DoesNotFire()
    {
        var (storage, events) = Make();

        storage.Deposit("_test_ore", 0.4f);

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void Withdraw_NonZero_FiresEventWithNegativeDelta()
    {
        var (storage, events) = Make();
        storage.Deposit("_test_ore", 50);
        events.Clear();

        storage.Withdraw("_test_ore", 20);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].id).IsEqual("_test_ore");
        AssertThat(events[0].delta).IsEqual(-20);
    }

    [TestCase]
    public void Withdraw_PartialFulfillment_DeltaMatchesActual()
    {
        var (storage, events) = Make();
        storage.Deposit("_test_ore", 10);
        events.Clear();

        storage.Withdraw("_test_ore", 100);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].delta).IsEqual(-10);
    }

    [TestCase]
    public void Withdraw_EmptyStorage_DoesNotFire()
    {
        var (storage, events) = Make();

        storage.Withdraw("_test_ore", 50);

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void AddSlot_PrePopulatedQuantity_FiresEvent()
    {
        var storage = new Storage();
        var events = new List<(string id, int delta)>();
        storage.StorageUpdated += (id, delta) => events.Add((id, delta));

        var slot = new StorageSlot(SlotFilter.ForResource("_test_ore"));
        slot.OccupiedResourceId = "_test_ore";
        slot.Quantity = 25;
        storage.AddSlot(slot);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].id).IsEqual("_test_ore");
        AssertThat(events[0].delta).IsEqual(25);
    }

    [TestCase]
    public void AddSlot_EmptyQuantity_DoesNotFire()
    {
        var storage = new Storage();
        var events = new List<(string id, int delta)>();
        storage.StorageUpdated += (id, delta) => events.Add((id, delta));

        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public void RemoveSlot_NonEmpty_FiresNegativeDelta()
    {
        var (storage, events) = Make();
        storage.Deposit("_test_ore", 40);
        var slot = storage.Slots[0];
        events.Clear();

        storage.RemoveSlot(slot);

        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].delta).IsEqual(-40);
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

        int deposited = storage.Deposit("_test_ore", 30);

        AssertThat(deposited).IsEqual(30);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(30);
        AssertThat(events.Count).IsEqual(1);
    }
}
