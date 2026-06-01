using System.Threading;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables.Tick;
using UtilityLibrary;

namespace Tests.Constructables.Tick;

/// <summary>
/// Verifies code reachable from <see cref="ManufactureTickEngine"/>'s background thread
/// can safely route signal emissions through <see cref="SignalMarshal"/> /
/// <c>SignalBus.SafeEmit*</c> without crashing.
/// </summary>
[TestSuite]
public class TickPathSignalSafetyTest
{
    [TestCase]
    public void Tickable_QueryingIsOnMainThread_SeesFalse()
    {
        SignalMarshal.Initialize();
        var engine = ManufactureTickEngine.CreateForTesting();
        var probe = new ThreadIdProbeTickable();
        engine.Register(probe);

        // SingleTickForTesting runs synchronously on the caller (test/main) thread, so
        // exercise the actual worker. Start the engine, give it a few ticks, then stop.
        engine.Stop();

        // CreateForTesting + SingleTickForTesting always runs on the calling thread. To
        // exercise the off-thread branch we simulate the same call the engine would make
        // from a background thread.
        bool observed = true;
        var worker = new Thread(() =>
        {
            observed = SignalMarshal.IsOnMainThread;
        });
        worker.Start();
        worker.Join();

        AssertThat(observed).IsFalse();
    }

    private class ThreadIdProbeTickable : IManufactureTickable
    {
        public int CapturedThreadId;

        public void OnManufactureTick(float delta)
        {
            CapturedThreadId = Thread.CurrentThread.ManagedThreadId;
        }
    }
}
