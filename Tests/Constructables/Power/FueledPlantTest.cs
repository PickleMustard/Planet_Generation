using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Constructables.Power;
using Structures.Enums;

namespace Tests.Constructables.Power;

/// <summary>
/// Verifies the fueled-vs-renewable distinction on PowerProducerBehavior — fueled plants
/// only produce while a manufacturing cycle is active; renewables produce continuously
/// while powered on.
/// </summary>
[TestSuite]
public class FueledPlantTest
{
    private static (Building b, PowerProducerBehavior prod, ManufacturingBehavior mfg) Make(bool renewable)
    {
        var building = new Building();
        building.PoweredOn = true;

        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);

        var prod = new PowerProducerBehavior { Output = 100f, Radius = 4, IsRenewable = renewable };
        prod.OnAttach(building);
        building.Behaviors.Add(prod);

        return (building, prod, mfg);
    }

    [TestCase]
    public void Fueled_NotManufacturing_DoesNotProduce()
    {
        var (_, prod, _) = Make(renewable: false);
        AssertThat(prod.IsProducing).IsFalse();
    }

    [TestCase]
    public void Fueled_Manufacturing_Produces()
    {
        var (_, prod, mfg) = Make(renewable: false);
        mfg.SetState(ManufacturingState.Manufacturing);
        AssertThat(prod.IsProducing).IsTrue();
    }

    [TestCase]
    public void Fueled_WaitingForInputs_DoesNotProduce()
    {
        var (_, prod, mfg) = Make(renewable: false);
        mfg.SetState(ManufacturingState.WaitingForInputs);
        AssertThat(prod.IsProducing).IsFalse();
    }

    [TestCase]
    public void Renewable_Idle_StillProduces()
    {
        var (_, prod, mfg) = Make(renewable: true);
        AssertThat(mfg.State).IsEqual(ManufacturingState.Idle);
        AssertThat(prod.IsProducing).IsTrue();
    }

    [TestCase]
    public void Renewable_PoweredOff_DoesNotProduce()
    {
        var (building, prod, _) = Make(renewable: true);
        building.PoweredOn = false;
        AssertThat(prod.IsProducing).IsFalse();
    }
}
