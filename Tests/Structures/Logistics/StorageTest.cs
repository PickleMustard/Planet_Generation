using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Logistics;

namespace Tests.Structures.Logistics;

[TestSuite]
public class StorageTest
{
    [TestCase]
    public void Deposit_SingleSlot_FullDeposit()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        int deposited = storage.Deposit("_test_ore", 50);

        AssertThat(deposited).IsEqual(50);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(50);
    }

    [TestCase]
    public void Deposit_SingleSlot_PartialDeposit()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 80);

        int deposited = storage.Deposit("_test_ore", 30);

        AssertThat(deposited).IsEqual(20);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(100);
    }

    [TestCase]
    public void Deposit_ZeroAmount_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        int deposited = storage.Deposit("_test_ore", 0);

        AssertThat(deposited).IsEqual(0);
    }

    [TestCase]
    public void Deposit_NegativeAmount_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        int deposited = storage.Deposit("_test_ore", -10f);

        AssertThat(deposited).IsEqual(0);
    }

    [TestCase]
    public void Withdraw_SingleSlot_FullWithdraw()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 50);

        int withdrawn = storage.Withdraw("_test_ore", 30);

        AssertThat(withdrawn).IsEqual(30);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(20);
    }

    [TestCase]
    public void Withdraw_SingleSlot_PartialWithdraw()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 50);

        int withdrawn = storage.Withdraw("_test_ore", 70);

        AssertThat(withdrawn).IsEqual(50);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(0);
    }

    [TestCase]
    public void Withdraw_ZeroAmount_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 50);

        int withdrawn = storage.Withdraw("_test_ore", 0);

        AssertThat(withdrawn).IsEqual(0);
    }

    [TestCase]
    public void Withdraw_NegativeAmount_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 50);

        int withdrawn = storage.Withdraw("_test_ore", -10);

        AssertThat(withdrawn).IsEqual(0);
    }

    [TestCase]
    public void MultiSlot_SameResource_DepositAcrossSlots()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        int deposited = storage.Deposit("_test_ore", 75);

        AssertThat(deposited).IsEqual(75);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(75);
    }

    [TestCase]
    public void MultiSlot_SameResource_WithdrawAcrossSlots()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 80);

        int withdrawn = storage.Withdraw("_test_ore", 60);

        AssertThat(withdrawn).IsEqual(60);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(20);
    }

    [TestCase]
    public void GetCapacity_SingleSlot()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        AssertThat(storage.GetCapacity("_test_ore")).IsEqual(100);
    }

    [TestCase]
    public void GetCapacity_MultiSlot()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        // Two empty resource-locked slots, fallback capacity = 100 each.
        AssertThat(storage.GetCapacity("_test_ore")).IsEqual(StorageSlot.FallbackCapacity * 2);
    }

    [TestCase]
    public void GetFreeSpace_SingleSlot()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 30);

        AssertThat(storage.GetFreeSpace("_test_ore")).IsEqual(70);
    }

    [TestCase]
    public void HasSpace_True()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 50);

        AssertThat(storage.HasSpace("_test_ore", 30)).IsTrue();
    }

    [TestCase]
    public void HasSpace_False()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 80);

        AssertThat(storage.HasSpace("_test_ore", 30)).IsFalse();
    }

    [TestCase]
    public void GetAllQuantities_ReturnsCorrectTotals()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore2")));
        storage.Deposit("_test_ore", 30);
        storage.Deposit("_test_ore2", 20);

        var quantities = storage.GetAllQuantities();

        AssertThat(quantities["_test_ore"]).IsEqual(30);
        AssertThat(quantities["_test_ore2"]).IsEqual(20);
    }

    [TestCase]
    public void Slots_ListsAllAddedSlots()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore2")));

        AssertThat(storage.Slots.Count).IsEqual(3);
    }

    [TestCase]
    public void Deposit_UnknownResource_RoutesToAcceptingFilter_OrZero()
    {
        var storage = new Storage();
        // Only iron_ore-filtered slot exists; copper_ore is rejected by the filter.
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        int deposited = storage.Deposit("_test_ore2", 50);

        AssertThat(deposited).IsEqual(0);
    }

    [TestCase]
    public void Withdraw_UnknownResource_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        int withdrawn = storage.Withdraw("_test_ore2", 50);

        AssertThat(withdrawn).IsEqual(0);
    }

    [TestCase]
    public void AddSlot_NullSlot_IsIgnored()
    {
        var storage = new Storage();
        storage.AddSlot(null!);

        AssertThat(storage.Slots).HasSize(0);
    }

    // ── Fractional buffer invariants ───────────────────────────────────────

    [TestCase]
    public void Deposit_SubUnit_ReturnsZeroAndBuffers()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        int deposited = storage.Deposit("_test_ore", 0.4f);

        AssertThat(deposited).IsEqual(0);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(0);
    }

    [TestCase]
    public void Deposit_SubUnit_AccumulatesAcrossCalls()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));

        // 0.4 + 0.4 + 0.4 = 1.2 → one whole unit lands, 0.2 stays buffered.
        AssertThat(storage.Deposit("_test_ore", 0.4f)).IsEqual(0);
        AssertThat(storage.Deposit("_test_ore", 0.4f)).IsEqual(0);
        AssertThat(storage.Deposit("_test_ore", 0.4f)).IsEqual(1);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(1);

        // 0.2 + 0.9 = 1.1 → one more whole unit, 0.1 buffered.
        AssertThat(storage.Deposit("_test_ore", 0.9f)).IsEqual(1);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(2);
    }

    [TestCase]
    public void Deposit_Fractional_OnFullStorage_KeepsBufferAcrossCalls()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 100); // fill the single 100-cap slot

        // No room for whole units; buffer accumulates silently.
        AssertThat(storage.Deposit("_test_ore", 0.7f)).IsEqual(0);
        AssertThat(storage.Deposit("_test_ore", 0.7f)).IsEqual(0);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(100);

        // Withdraw a whole unit then deposit again — buffer has 1.4 + 0.6 = 2.0 → 2 units flow in.
        storage.Withdraw("_test_ore", 2);
        AssertThat(storage.Deposit("_test_ore", 0.6f)).IsEqual(2);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(100);
    }

    [TestCase]
    public void Withdraw_DoesNotTouchFractionalBuffer()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("_test_ore")));
        storage.Deposit("_test_ore", 5);
        storage.Deposit("_test_ore", 0.3f); // 0.3 buffered

        AssertThat(storage.Withdraw("_test_ore", 10)).IsEqual(5);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(0);

        // Buffer still 0.3 — repeated 0.7 deposit crosses 1.0 immediately.
        AssertThat(storage.Deposit("_test_ore", 0.7f)).IsEqual(1);
        AssertThat(storage.GetQuantity("_test_ore")).IsEqual(1);
    }
}
