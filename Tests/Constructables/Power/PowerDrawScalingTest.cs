using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Constructables.Power;
using GdUnit4;
using Structures.Enums;
using static GdUnit4.Assertions;

namespace Tests.Constructables.Power;

/// <summary>
/// Verifies power draw is gated on manufacturing state per spec — idle, asleep
/// (waiting for inputs), and powered-off buildings draw zero.
/// </summary>
[TestSuite]
public class PowerDrawScalingTest
{
    private static (Building building, PowerConsumerBehavior consumer, ManufacturingBehavior mfg) Make(float draw)
    {
        var building = new Building();
        building.PoweredOn = true;

        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);

        var consumer = new PowerConsumerBehavior { BaseDraw = draw };
        consumer.OnAttach(building);
        building.Behaviors.Add(consumer);

        return (building, consumer, mfg);
    }

    [TestCase]
    public void IdleBuilding_DrawsZero()
    {
        var (_, consumer, _) = Make(50f);
        AssertThat(consumer.GetCurrentDraw()).IsEqual(0f);
    }

    [TestCase]
    public void Manufacturing_DrawsBaseDraw()
    {
        var (_, consumer, mfg) = Make(50f);
        mfg.SetState(ManufacturingState.Manufacturing);
        AssertThat(consumer.GetCurrentDraw()).IsEqual(50f);
    }

    [TestCase]
    public void WaitingForInputs_DrawsZero()
    {
        var (_, consumer, mfg) = Make(50f);
        mfg.SetState(ManufacturingState.WaitingForInputs);
        AssertThat(consumer.GetCurrentDraw()).IsEqual(0f);
    }

    [TestCase]
    public void Outputting_DrawsZero()
    {
        var (_, consumer, mfg) = Make(50f);
        mfg.SetState(ManufacturingState.Outputting);
        AssertThat(consumer.GetCurrentDraw()).IsEqual(0f);
    }

    [TestCase]
    public void PoweredOff_DrawsZero()
    {
        var (building, consumer, mfg) = Make(50f);
        mfg.SetState(ManufacturingState.Manufacturing);
        building.PoweredOn = false;
        AssertThat(consumer.GetCurrentDraw()).IsEqual(0f);
    }

    [TestCase]
    public void NoManufacturingBehavior_DrawsZero()
    {
        var building = new Building();
        building.PoweredOn = true;
        var consumer = new PowerConsumerBehavior { BaseDraw = 50f };
        consumer.OnAttach(building);
        building.Behaviors.Add(consumer);

        AssertThat(consumer.GetCurrentDraw()).IsEqual(0f);
    }

    [TestCase]
    public void NoOwner_DrawsZero()
    {
        var consumer = new PowerConsumerBehavior { BaseDraw = 50f };
        AssertThat(consumer.GetCurrentDraw()).IsEqual(0f);
    }
}
