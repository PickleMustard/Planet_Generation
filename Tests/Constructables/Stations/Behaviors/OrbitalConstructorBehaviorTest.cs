using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Stations;
using Constructables.Stations.Behaviors;

namespace Tests.Constructables.Stations.Behaviors;

/// <summary>
/// Verifies <see cref="OrbitalConstructorBehavior"/> slot/queue mechanics independent
/// of <see cref="BuildingConstructionManager"/>. Integration with the routing manager
/// is covered separately.
/// </summary>
[TestSuite]
public class OrbitalConstructorBehaviorTest
{
    [TestCase]
    public void WantsTick_EmptySlots_ReturnsFalse()
    {
        var behavior = new OrbitalConstructorBehavior { RegularSlotCount = 2 };
        AssertThat(behavior.WantsTick).IsFalse();
    }

    [TestCase]
    public void OnDetach_ClearsOwnerAndBody()
    {
        var behavior = new OrbitalConstructorBehavior();
        var station = new StationSatellite();
        behavior.OnAttach(station);

        AssertThat(behavior.Owner).IsEqual(station);

        behavior.OnDetach();

        AssertThat(behavior.Owner).IsNull();
    }

    [TestCase]
    public void Priority_Is50()
    {
        var behavior = new OrbitalConstructorBehavior();
        AssertThat(behavior.Priority).IsEqual(50);
    }

    [TestCase]
    public void SetOvertimeTarget_GrowsSlotList()
    {
        var behavior = new OrbitalConstructorBehavior { RegularSlotCount = 1 };
        behavior.SetOvertimeTarget(3);
        AssertThat(behavior.OvertimeSlotTarget).IsEqual(3);
        AssertThat(behavior.OvertimeSlotsConfigured).IsEqual(3);
    }

    [TestCase]
    public void SetOvertimeTarget_ShrinkBelowOccupied_MarksRetiring()
    {
        var behavior = new OrbitalConstructorBehavior
        {
            RegularSlotCount = 0,
            WorkBudgetPerTick = 0f,
        };
        behavior.SetOvertimeTarget(2);

        // Direct slot fill via reflection-free path: assign via API
        var b1 = MakeBuilding();
        var b2 = MakeBuilding();
        behavior.AssignBuilding(b1);
        behavior.AssignBuilding(b2);
        AssertThat(behavior.OccupiedOvertimeCount).IsEqual(2);

        behavior.SetOvertimeTarget(1);
        AssertThat(behavior.OvertimeSlotTarget).IsEqual(1);
        // Both stay occupied; trailing slot flagged retiring
        AssertThat(behavior.OvertimeSlotsConfigured).IsEqual(2);
        AssertThat(behavior.OvertimeSlots[1].IsRetiring).IsTrue();
    }

    [TestCase]
    public void AssignBuilding_FillsRegularBeforeOvertime()
    {
        var behavior = new OrbitalConstructorBehavior
        {
            RegularSlotCount = 1,
        };
        behavior.SetOvertimeTarget(1);

        var b1 = MakeBuilding();
        var b2 = MakeBuilding();
        behavior.AssignBuilding(b1);
        AssertThat(behavior.OccupiedRegularCount).IsEqual(1);
        AssertThat(behavior.OccupiedOvertimeCount).IsEqual(0);

        behavior.AssignBuilding(b2);
        AssertThat(behavior.OccupiedRegularCount).IsEqual(1);
        AssertThat(behavior.OccupiedOvertimeCount).IsEqual(1);
    }

    [TestCase]
    public void AssignBuilding_OverflowsToQueue()
    {
        var behavior = new OrbitalConstructorBehavior { RegularSlotCount = 1 };
        var b1 = MakeBuilding();
        var b2 = MakeBuilding();
        behavior.AssignBuilding(b1);
        behavior.AssignBuilding(b2);

        AssertThat(behavior.OccupiedSlotCount).IsEqual(1);
        AssertThat(behavior.QueueCount).IsEqual(1);
        AssertThat(behavior.Load).IsEqual(2);
    }

    [TestCase]
    public void RemoveBuilding_PromotesFromQueue()
    {
        var behavior = new OrbitalConstructorBehavior { RegularSlotCount = 1 };
        var b1 = MakeBuilding();
        var b2 = MakeBuilding();
        behavior.AssignBuilding(b1);
        behavior.AssignBuilding(b2);

        behavior.RemoveBuilding(b1);
        AssertThat(behavior.OccupiedRegularCount).IsEqual(1);
        AssertThat(behavior.QueueCount).IsEqual(0);
        AssertThat(behavior.RegularSlots[0].Building).IsEqual(b2);
    }

    private static Building MakeBuilding()
    {
        return new Building();
    }
}
