using System;
using System.Collections.Generic;
using System.Threading;
using Constructables.Tick;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Tests.Constructables.Tick;

[TestSuite]
public class ManufactureTickEngineTests
{
    private sealed class CountingTickable : IManufactureTickable
    {
        public int Count;
        public int Priority;
        public Action<int>? OnTickHook;
        public int TickPriority => Priority;
        public void OnManufactureTick(float delta)
        {
            int n = Interlocked.Increment(ref Count);
            OnTickHook?.Invoke(n);
        }
    }

    private sealed class ThrowingTickable : IManufactureTickable
    {
        public int Count;
        public void OnManufactureTick(float delta)
        {
            Interlocked.Increment(ref Count);
            throw new InvalidOperationException("boom");
        }
    }

    private static void EnsureNoEngine()
    {
        ManufactureTickEngine.Instance?.Stop();
    }

    [TestCase]
    public void Start_ThenStop_JoinsCleanly()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.Start();
        Thread.Sleep(100);
        AssertThat(engine.IsRunning).IsTrue();
        engine.Stop();
        AssertThat(engine.IsRunning).IsFalse();
        AssertThat(ManufactureTickEngine.Instance).IsNull();
    }

    [TestCase]
    public void DoubleStart_Throws()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.Start();
        try
        {
            AssertThrown(() => ManufactureTickEngine.Start())
                .IsInstanceOf<InvalidOperationException>();
        }
        finally
        {
            engine.Stop();
        }
    }

    [TestCase]
    public void Cadence_60Hz_Within10Percent()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.Start();
        try
        {
            var t = new CountingTickable();
            engine.Register(t);
            Thread.Sleep(1000);
            int count = t.Count;
            AssertThat(count).IsGreater(53);
            AssertThat(count).IsLess(67);
        }
        finally
        {
            engine.Stop();
        }
    }

    [TestCase]
    public void Register_AfterStart_TickableReceivesTicks()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.Start();
        try
        {
            var t = new CountingTickable();
            engine.Register(t);
            Thread.Sleep(200);
            AssertThat(t.Count).IsGreater(5);
        }
        finally
        {
            engine.Stop();
        }
    }

    [TestCase]
    public void Unregister_StopsTickable()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.Start();
        try
        {
            var t = new CountingTickable();
            engine.Register(t);
            Thread.Sleep(150);
            int before = t.Count;
            engine.Unregister(t);
            Thread.Sleep(150);
            int after = t.Count;
            // Allow at most a couple extra ticks before drain
            AssertThat(after - before).IsLessEqual(3);
        }
        finally
        {
            engine.Stop();
        }
    }

    [TestCase]
    public void Pause_StopsTickAdvancing_Resume_Continues()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.Start();
        try
        {
            var t = new CountingTickable();
            engine.Register(t);
            Thread.Sleep(150);
            engine.Pause();
            Thread.Sleep(50); // settle into pause
            int paused = t.Count;
            Thread.Sleep(200);
            int afterPauseWindow = t.Count;
            AssertThat(afterPauseWindow - paused).IsLessEqual(2);

            engine.Resume();
            Thread.Sleep(200);
            AssertThat(t.Count).IsGreater(afterPauseWindow + 5);
        }
        finally
        {
            engine.Stop();
        }
    }

    [TestCase]
    public void RegisterBatch_AllReceiveTicks()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.Start();
        try
        {
            var ts = new List<IManufactureTickable>();
            for (int i = 0; i < 5; i++) ts.Add(new CountingTickable());
            engine.RegisterBatch(ts);
            Thread.Sleep(200);
            foreach (var t in ts)
                AssertThat(((CountingTickable)t).Count).IsGreater(3);
        }
        finally
        {
            engine.Stop();
        }
    }

    [TestCase]
    public void ExceptionInTickable_DoesNotKillThread()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.Start();
        try
        {
            var thrower = new ThrowingTickable();
            var counter = new CountingTickable();
            engine.Register(thrower);
            engine.Register(counter);
            Thread.Sleep(200);
            AssertThat(counter.Count).IsGreater(5);
            AssertThat(thrower.Count).IsGreater(5);
            AssertThat(engine.IsRunning).IsTrue();
        }
        finally
        {
            engine.Stop();
        }
    }

    [TestCase]
    public void Priority_LowerTicksFirst()
    {
        EnsureNoEngine();
        var engine = ManufactureTickEngine.CreateForTesting();

        var order = new List<int>();
        var lockObj = new object();
        var first = new CountingTickable
        {
            Priority = -10,
            OnTickHook = _ => { lock (lockObj) order.Add(-10); }
        };
        var second = new CountingTickable
        {
            Priority = 0,
            OnTickHook = _ => { lock (lockObj) order.Add(0); }
        };
        engine.Register(second);
        engine.Register(first);
        engine.SingleTickForTesting();

        AssertThat(order.Count).IsEqual(2);
        AssertThat(order[0]).IsEqual(-10);
        AssertThat(order[1]).IsEqual(0);
    }
}
