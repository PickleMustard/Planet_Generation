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
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        float deposited = storage.Deposit("iron_ore", 50f);

        AssertThat(deposited).IsEqual(50f);
        AssertThat(storage.GetQuantity("iron_ore")).IsEqual(50f);
    }

    [TestCase]
    public void Deposit_SingleSlot_PartialDeposit()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 80f);

        float deposited = storage.Deposit("iron_ore", 30f);

        AssertThat(deposited).IsEqual(20f);
        AssertThat(storage.GetQuantity("iron_ore")).IsEqual(100f);
    }

    [TestCase]
    public void Deposit_ZeroAmount_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        float deposited = storage.Deposit("iron_ore", 0f);

        AssertThat(deposited).IsEqual(0f);
    }

    [TestCase]
    public void Deposit_NegativeAmount_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        float deposited = storage.Deposit("iron_ore", -10f);

        AssertThat(deposited).IsEqual(0f);
    }

    [TestCase]
    public void Withdraw_SingleSlot_FullWithdraw()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 50f);

        float withdrawn = storage.Withdraw("iron_ore", 30f);

        AssertThat(withdrawn).IsEqual(30f);
        AssertThat(storage.GetQuantity("iron_ore")).IsEqual(20f);
    }

    [TestCase]
    public void Withdraw_SingleSlot_PartialWithdraw()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 50f);

        float withdrawn = storage.Withdraw("iron_ore", 70f);

        AssertThat(withdrawn).IsEqual(50f);
        AssertThat(storage.GetQuantity("iron_ore")).IsEqual(0f);
    }

    [TestCase]
    public void Withdraw_ZeroAmount_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 50f);

        float withdrawn = storage.Withdraw("iron_ore", 0f);

        AssertThat(withdrawn).IsEqual(0f);
    }

    [TestCase]
    public void Withdraw_NegativeAmount_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 50f);

        float withdrawn = storage.Withdraw("iron_ore", -10f);

        AssertThat(withdrawn).IsEqual(0f);
    }

    [TestCase]
    public void MultiSlot_SameResource_DepositAcrossSlots()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        float deposited = storage.Deposit("iron_ore", 75f);

        AssertThat(deposited).IsEqual(75f);
        AssertThat(storage.GetQuantity("iron_ore")).IsEqual(75f);
    }

    [TestCase]
    public void MultiSlot_SameResource_WithdrawAcrossSlots()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 80f);

        float withdrawn = storage.Withdraw("iron_ore", 60f);

        AssertThat(withdrawn).IsEqual(60f);
        AssertThat(storage.GetQuantity("iron_ore")).IsEqual(20f);
    }

    [TestCase]
    public void GetCapacity_SingleSlot()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        AssertThat(storage.GetCapacity("iron_ore")).IsEqual(100f);
    }

    [TestCase]
    public void GetCapacity_MultiSlot()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        // Two empty resource-locked slots, fallback capacity = 100 each.
        AssertThat(storage.GetCapacity("iron_ore")).IsEqual(StorageSlot.FallbackCapacity * 2);
    }

    [TestCase]
    public void GetFreeSpace_SingleSlot()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 30f);

        AssertThat(storage.GetFreeSpace("iron_ore")).IsEqual(70f);
    }

    [TestCase]
    public void HasSpace_True()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 50f);

        AssertThat(storage.HasSpace("iron_ore", 30f)).IsTrue();
    }

    [TestCase]
    public void HasSpace_False()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.Deposit("iron_ore", 80f);

        AssertThat(storage.HasSpace("iron_ore", 30f)).IsFalse();
    }

    [TestCase]
    public void GetAllQuantities_ReturnsCorrectTotals()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("copper_ore")));
        storage.Deposit("iron_ore", 30f);
        storage.Deposit("copper_ore", 20f);

        var quantities = storage.GetAllQuantities();

        AssertThat(quantities["iron_ore"]).IsEqual(30f);
        AssertThat(quantities["copper_ore"]).IsEqual(20f);
    }

    [TestCase]
    public void Slots_ListsAllAddedSlots()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("copper_ore")));

        AssertThat(storage.Slots.Count).IsEqual(3);
    }

    [TestCase]
    public void Deposit_UnknownResource_RoutesToAcceptingFilter_OrZero()
    {
        var storage = new Storage();
        // Only iron_ore-filtered slot exists; copper_ore is rejected by the filter.
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        float deposited = storage.Deposit("copper_ore", 50f);

        AssertThat(deposited).IsEqual(0f);
    }

    [TestCase]
    public void Withdraw_UnknownResource_ReturnsZero()
    {
        var storage = new Storage();
        storage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        float withdrawn = storage.Withdraw("copper_ore", 50f);

        AssertThat(withdrawn).IsEqual(0f);
    }

    [TestCase]
    public void AddSlot_NullSlot_IsIgnored()
    {
        var storage = new Storage();
        storage.AddSlot(null!);

        AssertThat(storage.Slots).HasSize(0);
    }
}
