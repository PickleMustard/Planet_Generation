using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Constructables.Power;
using Constructables.Tick;
using GdUnit4;
using Structures.Resources;
using static GdUnit4.Assertions;

namespace Tests.Constructables.Power;

/// <summary>
/// Drives a <see cref="PowerGrid"/> through synthetic ticks to validate supply/demand
/// resolution and brownout transitions. Uses pure Building objects with manually
/// attached behaviors — no Godot scene tree required.
/// </summary>
[TestSuite]
public partial class PowerGridTest
{
    [TestCase]
    public void Grid_NoMembers_TicksWithoutCrash()
    {
        var grid = new PowerGrid(0, body: null!);
        grid.OnManufactureTick(1f);
        AssertThat(grid.IsBrownedOut).IsFalse();
        AssertThat(grid.LastDraw).IsEqual(0f);
    }

    [TestCase]
    public void Grid_ProducerExceedsConsumer_BuildingPoweredOn()
    {
        var grid = new PowerGrid(0, body: null!);

        var producerBuilding = MakeBuildingWithProducer(output: 100f, manufacturing: true);
        var consumerBuilding = MakeBuildingWithConsumer(draw: 40f);

        grid.Contributors.Add(producerBuilding);
        grid.Consumers.Add(consumerBuilding);
        producerBuilding.PoweredOn = true;
        consumerBuilding.PoweredOn = true;

        grid.OnManufactureTick(1f);

        AssertThat(grid.IsBrownedOut).IsFalse();
        AssertThat(grid.LastGeneration).IsEqual(100f);
        AssertThat(grid.LastDraw).IsEqual(40f);
        AssertThat(consumerBuilding.PoweredOn).IsTrue();
    }

    [TestCase]
    public void Grid_DemandExceedsSupply_NoBattery_BrownsOut()
    {
        var grid = new PowerGrid(0, body: null!);

        var producerBuilding = MakeBuildingWithProducer(output: 30f, manufacturing: true);
        var consumerBuilding = MakeBuildingWithConsumer(draw: 100f);

        grid.Contributors.Add(producerBuilding);
        grid.Consumers.Add(consumerBuilding);
        producerBuilding.PoweredOn = true;
        consumerBuilding.PoweredOn = true;

        grid.OnManufactureTick(1f);

        AssertThat(grid.IsBrownedOut).IsTrue();
        AssertThat(producerBuilding.PoweredOn).IsFalse();
        AssertThat(consumerBuilding.PoweredOn).IsFalse();
    }

    [TestCase]
    public void Grid_BatteryCoversShortfall_StaysOnline()
    {
        var grid = new PowerGrid(0, body: null!);

        var producerBuilding = MakeBuildingWithProducer(output: 50f, manufacturing: true);
        var consumerBuilding = MakeBuildingWithConsumer(draw: 80f);
        var batteryBuilding = MakeBuildingWithBattery(capacity: 1000f, stored: 500f);

        grid.Contributors.Add(producerBuilding);
        grid.Contributors.Add(batteryBuilding);
        grid.Consumers.Add(consumerBuilding);
        producerBuilding.PoweredOn = true;
        consumerBuilding.PoweredOn = true;
        batteryBuilding.PoweredOn = true;

        grid.OnManufactureTick(1f);

        AssertThat(grid.IsBrownedOut).IsFalse();
        AssertThat(consumerBuilding.PoweredOn).IsTrue();

        var battery = batteryBuilding.GetBehavior<BatteryBehavior>()!;
        AssertThat(battery.Stored).IsLess(500f); // some stored energy was consumed
    }

    [TestCase]
    public void Grid_BatteryDrainsThenBrownsOut()
    {
        var grid = new PowerGrid(0, body: null!);

        var consumerBuilding = MakeBuildingWithConsumer(draw: 60f);
        var batteryBuilding = MakeBuildingWithBattery(capacity: 100f, stored: 50f);

        grid.Contributors.Add(batteryBuilding);
        grid.Consumers.Add(consumerBuilding);
        consumerBuilding.PoweredOn = true;
        batteryBuilding.PoweredOn = true;

        grid.OnManufactureTick(1f); // first tick draws 50 from battery, still short by 10
        AssertThat(grid.IsBrownedOut).IsTrue();
        AssertThat(consumerBuilding.PoweredOn).IsFalse();
    }

    [TestCase]
    public void Grid_RecoversFromBrownout_WhenSupplyReturns()
    {
        var grid = new PowerGrid(0, body: null!);

        var producerBuilding = MakeBuildingWithProducer(output: 30f, manufacturing: true);
        var consumerBuilding = MakeBuildingWithConsumer(draw: 100f);

        grid.Contributors.Add(producerBuilding);
        grid.Consumers.Add(consumerBuilding);
        producerBuilding.PoweredOn = true;
        consumerBuilding.PoweredOn = true;

        grid.OnManufactureTick(1f);
        AssertThat(grid.IsBrownedOut).IsTrue();

        // Bump output above demand, re-arm power, tick again.
        var producer = (StubPowerProducer)producerBuilding.GetBehavior<PowerProducerBehavior>()!;
        producer.StubOutput = 200f;
        producerBuilding.PoweredOn = true;
        StartProducing(producerBuilding);

        grid.OnManufactureTick(1f);

        AssertThat(grid.IsBrownedOut).IsFalse();
        AssertThat(consumerBuilding.PoweredOn).IsTrue();
    }

    [TestCase]
    public void Grid_AbsorbFrom_MovesMembersAndCells()
    {
        var keeper = new PowerGrid(0, body: null!);
        var victim = new PowerGrid(1, body: null!);

        var b = MakeBuildingWithProducer(output: 10f, manufacturing: true);
        victim.Contributors.Add(b);

        keeper.AbsorbFrom(victim);

        AssertThat(keeper.Contributors).Contains(b);
        AssertThat(victim.Contributors.Count).IsEqual(0);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Building MakeBuildingWithProducer(float output, bool manufacturing)
    {
        var building = MakeBuilding();
        var producer = new StubPowerProducer { StubOutput = output, Radius = 1 };
        building.Behaviors.Add(producer);
        producer.OnAttach(building);

        // Producer's IsProducing checks the ManufacturingBehavior state. Add one and
        // park it in the right state so SumGeneration counts the building.
        var mfg = new ManufacturingBehavior();
        building.Behaviors.Add(mfg);
        mfg.OnAttach(building);
        if (manufacturing)
            mfg.SetState(global::Structures.Enums.ManufacturingState.Manufacturing);

        return building;
    }

    private static void StartProducing(Building building)
    {
        var mfg = building.GetBehavior<ManufacturingBehavior>();
        mfg?.SetState(global::Structures.Enums.ManufacturingState.Manufacturing);
    }

    private static Building MakeBuildingWithConsumer(float draw)
    {
        var building = MakeBuilding();

        // Power draw is gated on the consumer's owner being in ManufacturingState.Manufacturing
        // (per spec: idle/asleep buildings draw 0). Add a ManufacturingBehavior parked in
        // Manufacturing so SumDraw counts BaseDraw for these tests.
        var mfg = new ManufacturingBehavior();
        building.Behaviors.Add(mfg);
        mfg.OnAttach(building);
        mfg.SetState(global::Structures.Enums.ManufacturingState.Manufacturing);

        var consumer = new PowerConsumerBehavior { BaseDraw = draw };
        building.Behaviors.Add(consumer);
        consumer.OnAttach(building);
        return building;
    }

    private static Building MakeBuildingWithBattery(float capacity, float stored)
    {
        var building = MakeBuilding();
        var battery = new BatteryBehavior { Capacity = capacity, Stored = stored, Radius = 1 };
        building.Behaviors.Add(battery);
        battery.OnAttach(building);
        return building;
    }

    private static Building MakeBuilding()
    {
        // Construct a Building with the minimum surface needed by the grid's tick.
        // ManufacturingBehavior config lives in BehaviorEntries; power flow ignores it.
        var def = new BuildingDefinition
        {
            IdName = "test_building",
            DisplayName = "Test",
        };
        var building = new Building();
        // Defer the full ApplyDefinition path (it pokes Storage); we only need behaviors.
        return building;
    }

    /// <summary>
    /// Test stub overriding Output/EffectiveOutput so tests can supply a magnitude directly
    /// without spinning up a RecipeDatabase + active recipe cycle.
    /// </summary>
    private sealed partial class StubPowerProducer : PowerProducerBehavior
    {
        public float StubOutput { get; set; }
        public override float Output => StubOutput;
        public override float EffectiveOutput => StubOutput;
    }
}
