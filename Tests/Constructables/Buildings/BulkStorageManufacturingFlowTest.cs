using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;

namespace Tests.Constructables.Buildings;

/// <summary>
/// Regression coverage for the HQ-style hybrid building: BulkStorageRoutingBehavior
/// + ManufacturingBehavior together must let a recipe with no inputs grow BulkStorage
/// every cycle, and the per-recipe OutputStorage slots must be created by StartCycle
/// so the Manufacturing UI tab can display them.
/// </summary>
[TestSuite]
public class BulkStorageManufacturingFlowTest
{
    [TestCase]
    public void StartCycle_AddsRecipeOutputSlot_ToOutputStorage()
    {
        var building = BuildHybrid();
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;

        AssertThat(building.OutputStorage.Slots.Count).IsEqual(0);

        mfg.StartCycle(NoInputIronRecipe(), productionSpeed: 10f);

        AssertThat(building.OutputStorage.Slots.Count).IsEqual(1);
        AssertThat(building.OutputStorage.Slots[0].Filter.Kind).IsEqual(SlotFilterKind.Resource);
        AssertThat(building.OutputStorage.Slots[0].Filter.ResourceId).IsEqual("iron");
    }

    [TestCase]
    public void HybridCycle_OutputDepositsToOutputStorageThenRoutesToBulk()
    {
        var building = BuildHybrid();
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var routing = building.GetBehavior<BulkStorageRoutingBehavior>()!;
        var recipe = NoInputIronRecipe();

        mfg.StartCycle(recipe, productionSpeed: 10f);
        mfg.OnManufactureTick(1f, building);

        AssertThat(building.OutputStorage.GetQuantity("iron")).IsEqual(1f);
        AssertThat(building.BulkStorage.GetQuantity("iron")).IsEqual(0f);

        routing.OnManufactureTick(1f, building);

        AssertThat(building.OutputStorage.GetQuantity("iron")).IsEqual(0f);
        AssertThat(building.BulkStorage.GetQuantity("iron")).IsEqual(1f);
    }

    [TestCase]
    public void HybridCycle_BulkStorageGrows_AcrossMultipleCycles()
    {
        var building = BuildHybrid();
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;
        var routing = building.GetBehavior<BulkStorageRoutingBehavior>()!;
        var recipe = NoInputIronRecipe();

        for (int cycle = 1; cycle <= 3; cycle++)
        {
            mfg.StartCycle(recipe, productionSpeed: 10f);
            mfg.OnManufactureTick(1f, building);
            routing.OnManufactureTick(1f, building);

            AssertThat(building.BulkStorage.GetQuantity("iron")).IsEqual((float)cycle);
            AssertThat(mfg.State).IsEqual(ManufacturingState.Idle);
        }
    }

    [TestCase]
    public void HybridCycle_ManufacturingPushOutputs_GatedWhenBulkPresent()
    {
        // ManufacturingBehavior.PushOutputs must be a no-op when BulkStorageRoutingBehavior
        // is present and bulk slots exist. Without the gate the cycle's outputs would race
        // straight into export links instead of flowing through BulkStorage as the user
        // expects.
        var building = BuildHybrid();
        var mfg = building.GetBehavior<ManufacturingBehavior>()!;

        mfg.StartCycle(NoInputIronRecipe(), productionSpeed: 10f);
        mfg.OnManufactureTick(1f, building);

        AssertThat(building.OutputStorage.GetQuantity("iron")).IsEqual(1f);
    }

    private static Building BuildHybrid()
    {
        var building = new Building();
        for (int i = 0; i < 5; i++)
            building.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));

        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(building);
        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);

        // Insertion order = tick order in tests that bypass Building.Register's priority sort.
        // Routing (priority -1) must run before Manufacturing (priority 5).
        building.Behaviors.Add(routing);
        building.Behaviors.Add(mfg);
        return building;
    }

    private static RecipeDefinition NoInputIronRecipe() => new RecipeDefinition
    {
        WorkRequired = 0.1f,
        InputResources = new Dictionary<string, float>(),
        OutputResources = new Dictionary<string, float> { ["iron"] = 1f },
    };
}
