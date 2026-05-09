using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using Constructables;
using Constructables.Buildings;
using Constructables.Tick;

namespace Tests.Constructables.Buildings;

[TestSuite]
public class BuildingTest
{
    // ========================================================================
    // ON DELIVERY
    // ========================================================================

    [TestCase]
    public void OnDelivery_HasSpace_ReturnsTrueAndDeposits()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));

        var package = new ResourcePackage
        {
            ResourceId = "iron",
            Quantity = 25f
        };

        bool result = building.OnDelivery(package);

        AssertThat(result).IsTrue();
        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(25f);
    }

    [TestCase]
    public void OnDelivery_NoSpace_ReturnsFalse()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        building.InputStorage.Deposit("iron", 20f);

        var package = new ResourcePackage
        {
            ResourceId = "iron",
            Quantity = 25f
        };

        bool result = building.OnDelivery(package);

        AssertThat(result).IsFalse();
        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(20f);
    }

    [TestCase]
    public void OnDelivery_NullPackage_ReturnsFalse()
    {
        var building = new Building();
        bool result = building.OnDelivery(null!);
        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void OnDelivery_MultipleResources_DepositCorrectly()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("copper")));

        var pkg1 = new ResourcePackage { ResourceId = "iron", Quantity = 10f };
        var pkg2 = new ResourcePackage { ResourceId = "copper", Quantity = 20f };
        var pkg3 = new ResourcePackage { ResourceId = "iron", Quantity = 5f };

        AssertThat(building.OnDelivery(pkg1)).IsTrue();
        AssertThat(building.OnDelivery(pkg2)).IsTrue();
        AssertThat(building.OnDelivery(pkg3)).IsTrue();

        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(15f);
        AssertThat(building.InputStorage.GetQuantity("copper")).IsEqual(20f);
    }

    [TestCase]
    public void OnDelivery_PartialSpaceAvailable_ReturnsFalse()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        building.InputStorage.Deposit("iron", 20f);

        var package = new ResourcePackage
        {
            ResourceId = "iron",
            Quantity = 15f
        };

        bool result = building.OnDelivery(package);

        AssertThat(result).IsFalse();
        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(20f);
    }

    // ========================================================================
    // STORAGE INTEGRATION
    // ========================================================================

    [TestCase]
    public void Storage_InputAndOutput_AreIndependent()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        building.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));

        building.InputStorage.Deposit("iron", 50f);
        building.OutputStorage.Deposit("iron", 30f);

        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(50f);
        AssertThat(building.OutputStorage.GetQuantity("iron")).IsEqual(30f);
    }

    [TestCase]
    public void Storage_StartsEmpty_RecipeAndStorageHubPopulate()
    {
        // After the slot/storage refactor, ApplyDefinition no longer pre-allocates
        // generic input/output slots or per-resource starting slots. InputStorage and
        // OutputStorage are populated by ManufacturingBehavior on recipe set; BulkStorage
        // is populated by StorageHubBehavior at OnRegister.
        var definition = new BuildingDefinition
        {
            IdName = "test_building",
            DisplayName = "Test Building",
            StorageCapacity = 0,
        };

        var building = new Building();
        building.ApplyDefinition(definition);

        AssertThat(building.InputStorage.Slots.Count).IsEqual(0);
        AssertThat(building.OutputStorage.Slots.Count).IsEqual(0);
        AssertThat(building.BulkStorage.Slots.Count).IsEqual(0);
    }

    [TestCase]
    public void Storage_EmptyByDefault_HasNoSpaceForAnyResource()
    {
        var building = new Building();
        AssertThat(building.InputStorage.HasSpace("iron", 1f)).IsFalse();
        AssertThat(building.OutputStorage.HasSpace("copper", 1f)).IsFalse();
    }

    // ========================================================================
    // ON MANUFACTURE TICK
    // ========================================================================

    [TestCase]
    public void OnManufactureTick_PoweredOff_DoesNotTickBehaviors()
    {
        var building = new Building();
        building.PoweredOn = false;

        var mockBehavior = new MockBehavior();
        building.Behaviors.Add(mockBehavior);

        building.OnManufactureTick(0.1f);

        AssertThat(mockBehavior.TickCount).IsEqual(0);
    }

    [TestCase]
    public void OnManufactureTick_PoweredOn_TicksAllBehaviors()
    {
        var building = new Building();
        building.PoweredOn = true;

        var behaviorA = new MockBehavior();
        var behaviorB = new MockBehavior();
        building.Behaviors.Add(behaviorA);
        building.Behaviors.Add(behaviorB);

        building.OnManufactureTick(0.1f);
        building.OnManufactureTick(0.1f);

        AssertThat(behaviorA.TickCount).IsEqual(2);
        AssertThat(behaviorB.TickCount).IsEqual(2);
    }

    [TestCase]
    public void OnManufactureTick_PassesDeltaToBehaviors()
    {
        var building = new Building();
        building.PoweredOn = true;

        var mockBehavior = new MockBehavior();
        building.Behaviors.Add(mockBehavior);

        building.OnManufactureTick(0.05f);

        AssertThat(mockBehavior.LastDelta).IsEqual(0.05f);
    }

    [TestCase]
    public void OnManufactureTick_PassesBuildingAsOwner()
    {
        var building = new Building();
        building.PoweredOn = true;

        var mockBehavior = new MockBehavior();
        building.Behaviors.Add(mockBehavior);

        building.OnManufactureTick(0.1f);

        AssertThat(mockBehavior.LastOwner).IsEqual(building);
    }

    [TestCase]
    public void OnManufactureTick_BehaviorThrows_OthersStillTick()
    {
        var building = new Building();
        building.PoweredOn = true;

        var goodBehavior = new MockBehavior();
        var throwingBehavior = new ThrowingBehavior();
        building.Behaviors.Add(throwingBehavior);
        building.Behaviors.Add(goodBehavior);

        building.OnManufactureTick(0.1f);

        AssertThat(goodBehavior.TickCount).IsEqual(1);
    }

    // ========================================================================
    // CONSTRUCTION COMPLETION REGISTRATION
    // ========================================================================

    [TestCase]
    public void ConstructionComplete_RegistersWithManufactureTickEngine()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var definition = new BuildingDefinition
        {
            WorkRequired = 10f,
            RequiredResources = new Dictionary<string, int>()
        };
        var building = new Building();
        building.ApplyDefinition(definition);
        building.StartConstruction(new Godot.Collections.Dictionary());

        building.UpdateProgress(10f);
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(1);

        engine.Stop();
    }

    [TestCase]
    public void Shutdown_UnregistersFromManufactureTickEngine()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var definition = new BuildingDefinition
        {
            WorkRequired = 10f,
            RequiredResources = new Dictionary<string, int>()
        };
        var building = new Building();
        building.ApplyDefinition(definition);
        building.StartConstruction(new Godot.Collections.Dictionary());
        building.UpdateProgress(10f);
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(1);

        building.Shutdown();
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(0);
        engine.Stop();
    }

    // ========================================================================
    // MOCK BEHAVIORS
    // ========================================================================

    private class MockBehavior : IBuildingBehavior
    {
        public Building? Owner => null;
        public int TickCount { get; private set; }
        public float LastDelta { get; private set; }
        public Building? LastOwner { get; private set; }

        public void OnAttach(Building owner) { }
        public void OnRegister() { }
        public void OnUnregister() { }
        public void OnDetach() { }

        public void OnManufactureTick(float delta, Building owner)
        {
            TickCount++;
            LastDelta = delta;
            LastOwner = owner;
        }
    }

    private class ThrowingBehavior : IBuildingBehavior
    {
        public Building? Owner => null;

        public void OnAttach(Building owner) { }
        public void OnRegister() { }
        public void OnUnregister() { }
        public void OnDetach() { }

        public void OnManufactureTick(float delta, Building owner)
        {
            throw new System.InvalidOperationException("Simulated failure");
        }
    }
}
