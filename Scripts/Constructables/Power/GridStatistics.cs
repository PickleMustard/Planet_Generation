namespace Constructables.Power;

/// <summary>
/// Per-grid telemetry. Only the largest grid keeps its stats across a merge or split;
/// smaller participants' stats are discarded per the design spec.
/// </summary>
public sealed class GridStatistics
{
    public float TotalGenerated { get; private set; }
    public float TotalConsumed { get; private set; }
    public float PeakDraw { get; private set; }
    public float PeakGeneration { get; private set; }
    public int BrownoutCount { get; private set; }
    public long TickCount { get; private set; }

    public void RecordTick(float generated, float drawn, bool brownedOut)
    {
        TotalGenerated += generated;
        TotalConsumed += drawn;
        if (drawn > PeakDraw)
            PeakDraw = drawn;
        if (generated > PeakGeneration)
            PeakGeneration = generated;
        TickCount++;
    }

    public void RecordBrownoutEntered() => BrownoutCount++;
}
