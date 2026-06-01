using System.Collections.Generic;

namespace Constructables.Power;

/// <summary>
/// A single downsampled snapshot of grid telemetry. One sample per second.
/// </summary>
public readonly record struct GridSample(float Generation, float Draw, float BatteryStored, bool BrownedOut);

/// <summary>
/// Per-grid telemetry. Only the largest grid keeps its stats across a merge or split;
/// smaller participants' stats are discarded per the design spec.
///
/// Maintains running aggregates plus a 1Hz ring buffer of the last <see cref="HistoryCapacity"/>
/// samples (5 minutes at the default capacity). <see cref="RecordTick"/> is called from the
/// manufacture worker thread; UI consumers read via <see cref="GetHistorySnapshot"/>, which
/// returns a copy under a lock.
/// </summary>
public sealed class GridStatistics
{
    public const int HistoryCapacity = 300;
    private const int SamplesPerTick = 60;

    public float TotalGenerated { get; private set; }
    public float TotalConsumed { get; private set; }
    public float PeakDraw { get; private set; }
    public float PeakGeneration { get; private set; }
    public int BrownoutCount { get; private set; }
    public long TickCount { get; private set; }

    private readonly object _historyLock = new();
    private readonly GridSample[] _history = new GridSample[HistoryCapacity];
    private int _writeIndex;
    private int _filled;
    private int _downsampleCounter;

    public void RecordTick(float generated, float drawn, float batteryStored, bool brownedOut)
    {
        TotalGenerated += generated;
        TotalConsumed += drawn;
        if (drawn > PeakDraw)
            PeakDraw = drawn;
        if (generated > PeakGeneration)
            PeakGeneration = generated;
        TickCount++;

        _downsampleCounter++;
        if (_downsampleCounter < SamplesPerTick)
            return;
        _downsampleCounter = 0;

        var sample = new GridSample(generated, drawn, batteryStored, brownedOut);
        lock (_historyLock)
        {
            _history[_writeIndex] = sample;
            _writeIndex = (_writeIndex + 1) % HistoryCapacity;
            if (_filled < HistoryCapacity)
                _filled++;
        }
    }

    public void RecordBrownoutEntered() => BrownoutCount++;

    /// <summary>
    /// Returns a copy of the history in oldest-to-newest order. Safe to call from any thread.
    /// </summary>
    public List<GridSample> GetHistorySnapshot()
    {
        var result = new List<GridSample>(HistoryCapacity);
        lock (_historyLock)
        {
            if (_filled < HistoryCapacity)
            {
                for (int i = 0; i < _filled; i++)
                    result.Add(_history[i]);
            }
            else
            {
                for (int i = 0; i < HistoryCapacity; i++)
                {
                    int idx = (_writeIndex + i) % HistoryCapacity;
                    result.Add(_history[idx]);
                }
            }
        }
        return result;
    }
}
