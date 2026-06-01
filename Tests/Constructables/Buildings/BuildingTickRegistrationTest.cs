using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Constructables.Tick;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;

namespace Tests.Constructables.Buildings;

[TestSuite]
public class BuildingTickRegistrationTest
{
    private static (Building building, ManufacturingBehavior mfg, RecipeDefinition recipe) MakeBuilding()
    {
        var def = new BuildingDefinition
        {
            IdName = "test_factory",
            DisplayName = "Test Factory",
            WorkRequired = 0f,
        };
        def.RequiredResources["concrete"] = 0;

        var building = new Building();
        building.ApplyDefinition(def);
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);

        var recipe = new RecipeDefinition
        {
            WorkRequired = 1f,
            InputResources = new Dictionary<string, float> { ["iron_ore"] = 5f },
            OutputResources = new Dictionary<string, float> { ["iron"] = 1f },
        };

        return (building, mfg, recipe);
    }

    [TestCase]
    public void WaitingForInputs_UnregistersAfterTick()
    {
        var engine = ManufactureTickEngine.CreateForTesting();
        try
        {
            var (building, mfg, recipe) = MakeBuilding();
            engine.Register(building);

            mfg.StartCycle(recipe, productionSpeed: 1f);
            AssertThat(mfg.State).IsEqual(ManufacturingState.WaitingForInputs);
            AssertThat(mfg.WantsTick).IsFalse();

            engine.SingleTickForTesting(); // drain Register, tick → enqueues Unregister
            engine.SingleTickForTesting(); // drain Unregister

            AssertThat(engine.TickableCount).IsEqual(0);
        }
        finally { engine.Stop(); }
    }

    [TestCase]
    public void DepositMissingInput_ReRegistersBuilding()
    {
        var engine = ManufactureTickEngine.CreateForTesting();
        try
        {
            var (building, mfg, recipe) = MakeBuilding();
            engine.Register(building);

            mfg.StartCycle(recipe, productionSpeed: 1f);
            engine.SingleTickForTesting();
            engine.SingleTickForTesting();
            AssertThat(engine.TickableCount).IsEqual(0);

            building.InputStorage.Deposit("iron_ore", 5f);

            AssertThat(engine.PendingOpsCount).IsEqual(1);

            engine.SingleTickForTesting(); // drain Register, tick → fulfill inputs → state=Manufacturing

            AssertThat(mfg.State).IsEqual(ManufacturingState.Manufacturing);
        }
        finally { engine.Stop(); }
    }

    [TestCase]
    public void IdleBuildingWithDefaultBehaviors_StaysRegistered()
    {
        var engine = ManufactureTickEngine.CreateForTesting();
        try
        {
            var (building, mfg, _) = MakeBuilding();
            engine.Register(building);

            engine.SingleTickForTesting();

            AssertThat(mfg.State).IsEqual(ManufacturingState.Idle);
            AssertThat(mfg.WantsTick).IsTrue();
            AssertThat(engine.TickableCount).IsEqual(1);
        }
        finally { engine.Stop(); }
    }

    [TestCase]
    public void TogglePower_Off_UnregistersAndStorageEventDoesNotReWake()
    {
        var engine = ManufactureTickEngine.CreateForTesting();
        try
        {
            var (building, _, _) = MakeBuilding();
            engine.Register(building);
            engine.SingleTickForTesting();
            AssertThat(engine.TickableCount).IsEqual(1);

            building.TogglePower();
            engine.SingleTickForTesting();
            AssertThat(engine.TickableCount).IsEqual(0);

            building.InputStorage.Deposit("iron_ore", 5f);
            engine.SingleTickForTesting();

            AssertThat(engine.TickableCount).IsEqual(0);
        }
        finally { engine.Stop(); }
    }
}
