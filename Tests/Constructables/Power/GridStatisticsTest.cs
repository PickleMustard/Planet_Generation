using Constructables.Power;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Tests.Constructables.Power;

/// <summary>
/// Verifies GridStatistics aggregate accumulation, 1Hz downsampling (60-tick interval),
/// and ring-buffer wrap-around semantics.
/// </summary>
[TestSuite]
public class GridStatisticsTest
{
    [TestCase]
    public void RecordTick_AccumulatesAggregates()
    {
        var stats = new GridStatistics();
        stats.RecordTick(100f, 50f, 200f, false);
        stats.RecordTick(120f, 80f, 200f, false);

        AssertThat(stats.TotalGenerated).IsEqualApprox(220f, 0.001f);
        AssertThat(stats.TotalConsumed).IsEqualApprox(130f, 0.001f);
        AssertThat(stats.PeakGeneration).IsEqualApprox(120f, 0.001f);
        AssertThat(stats.PeakDraw).IsEqualApprox(80f, 0.001f);
        AssertThat(stats.TickCount).IsEqual(2L);
    }

    [TestCase]
    public void History_DownsamplesEvery60Ticks()
    {
        var stats = new GridStatistics();
        // 59 ticks → no sample yet.
        for (int i = 0; i < 59; i++)
            stats.RecordTick(10f, 5f, 100f, false);
        AssertThat(stats.GetHistorySnapshot().Count).IsEqual(0);

        // 60th tick → first sample.
        stats.RecordTick(10f, 5f, 100f, false);
        AssertThat(stats.GetHistorySnapshot().Count).IsEqual(1);

        // 119 more → still 1 sample (counter rolled to 59).
        for (int i = 0; i < 59; i++)
            stats.RecordTick(10f, 5f, 100f, false);
        AssertThat(stats.GetHistorySnapshot().Count).IsEqual(1);

        // One more → second sample.
        stats.RecordTick(10f, 5f, 100f, false);
        AssertThat(stats.GetHistorySnapshot().Count).IsEqual(2);
    }

    [TestCase]
    public void History_WrapsAroundAtCapacity()
    {
        var stats = new GridStatistics();
        // Fill ring buffer with samples 0..299 (each via 60 ticks at different generation values).
        for (int s = 0; s < GridStatistics.HistoryCapacity; s++)
        {
            for (int t = 0; t < 60; t++)
                stats.RecordTick(s, 0f, 0f, false);
        }
        var snap1 = stats.GetHistorySnapshot();
        AssertThat(snap1.Count).IsEqual(GridStatistics.HistoryCapacity);
        AssertThat(snap1[0].Generation).IsEqualApprox(0f, 0.001f);
        AssertThat(snap1[GridStatistics.HistoryCapacity - 1].Generation).IsEqualApprox(GridStatistics.HistoryCapacity - 1, 0.001f);

        // Add 5 more samples → first 5 evicted, newest at the end.
        for (int s = 0; s < 5; s++)
            for (int t = 0; t < 60; t++)
                stats.RecordTick(1000f + s, 0f, 0f, false);
        var snap2 = stats.GetHistorySnapshot();
        AssertThat(snap2.Count).IsEqual(GridStatistics.HistoryCapacity);
        AssertThat(snap2[0].Generation).IsEqualApprox(5f, 0.001f);
        AssertThat(snap2[GridStatistics.HistoryCapacity - 1].Generation).IsEqualApprox(1004f, 0.001f);
    }

    [TestCase]
    public void History_ReturnsCopy_NotReference()
    {
        var stats = new GridStatistics();
        for (int t = 0; t < 60; t++)
            stats.RecordTick(50f, 25f, 100f, false);
        var snap1 = stats.GetHistorySnapshot();
        // Continue recording — snap1 must not grow.
        for (int t = 0; t < 60; t++)
            stats.RecordTick(50f, 25f, 100f, false);
        AssertThat(snap1.Count).IsEqual(1);
        AssertThat(stats.GetHistorySnapshot().Count).IsEqual(2);
    }

    [TestCase]
    public void BrownoutSample_RecordedInHistory()
    {
        var stats = new GridStatistics();
        for (int t = 0; t < 60; t++)
            stats.RecordTick(10f, 100f, 0f, true);
        var snap = stats.GetHistorySnapshot();
        AssertThat(snap.Count).IsEqual(1);
        AssertThat(snap[0].BrownedOut).IsTrue();
    }
}
