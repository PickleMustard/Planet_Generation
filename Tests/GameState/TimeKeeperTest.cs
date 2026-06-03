using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Enums;
using Structures.GameState;
using UtilityLibrary.SaveLoad.Dto;

namespace Tests.GameState;

/// <summary>
/// Verifies TimeKeeper's tick → quarter/year derivation, the month-rollover advance driven by
/// OnManufactureTick, and the save round-trip through TimeDto.
/// </summary>
[TestSuite]
public class TimeKeeperTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void Derivation_FromRestoredTicks()
    {
        var tk = new TimeKeeper();

        // One month in → Year 0, Q2.
        tk.Restore(new TimeDto { TotalTicks = TimeKeeper.TicksPerMonth });
        AssertThat(tk.AbsoluteMonth).IsEqual(1L);
        AssertThat(tk.CurrentYear).IsEqual(0);
        AssertThat((int)tk.CurrentQuarter).IsEqual((int)Quarter.Q2);

        // Four months in → Year 1, Q1.
        tk.Restore(new TimeDto { TotalTicks = 4 * TimeKeeper.TicksPerMonth });
        AssertThat(tk.CurrentYear).IsEqual(1);
        AssertThat((int)tk.CurrentQuarter).IsEqual((int)Quarter.Q1);

        tk.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void OnManufactureTick_AdvancesAndRollsOver()
    {
        var tk = new TimeKeeper();

        // Start one tick short of the first rollover.
        tk.Restore(new TimeDto { TotalTicks = TimeKeeper.TicksPerMonth - 1 });
        AssertThat(tk.AbsoluteMonth).IsEqual(0L);
        AssertThat((int)tk.CurrentQuarter).IsEqual((int)Quarter.Q1);

        // The tick that crosses the boundary.
        tk.OnManufactureTick(1f / 60f);
        AssertThat(tk.TotalTicks).IsEqual(TimeKeeper.TicksPerMonth);
        AssertThat(tk.AbsoluteMonth).IsEqual(1L);
        AssertThat((int)tk.CurrentQuarter).IsEqual((int)Quarter.Q2);

        tk.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SaveRoundTrip_PreservesTicks()
    {
        var tk = new TimeKeeper();
        tk.Restore(new TimeDto { TotalTicks = 5 * TimeKeeper.TicksPerMonth + 123 });

        var dto = (TimeDto)tk.Serialize();
        AssertThat(dto.TotalTicks).IsEqual(5 * TimeKeeper.TicksPerMonth + 123);

        var restored = new TimeKeeper();
        restored.Restore(dto);
        AssertThat(restored.TotalTicks).IsEqual(tk.TotalTicks);
        AssertThat(restored.CurrentYear).IsEqual(tk.CurrentYear);
        AssertThat((int)restored.CurrentQuarter).IsEqual((int)tk.CurrentQuarter);

        tk.Free();
        restored.Free();
    }
}
