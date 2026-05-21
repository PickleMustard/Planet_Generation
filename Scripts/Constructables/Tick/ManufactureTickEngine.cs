using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UtilityLibrary;

namespace Constructables.Tick;

/// <summary>
/// Dedicated-thread fixed-rate (60Hz) tick driver for manufacturing simulation.
/// Decouples per-building / per-economy work from Godot's physics frame so expansive
/// factories can't starve rendering or logistics updates.
///
/// Lifecycle: created by SystemData._Ready; torn down by SystemData.EndGame.
/// Not an autoload — only alive during an active game session.
///
/// Threading model: a single dedicated worker thread ticks all registered tickables
/// sequentially. Register/Unregister enqueue ops onto a thread-safe queue; ops are
/// drained atomically at the start of each tick before any tickable runs.
///
/// IMPORTANT — SceneTree thread-safety: any code reachable from
/// <see cref="IManufactureTickable.OnManufactureTick"/> MUST emit signals via
/// <c>SignalBus.SafeEmit*</c> (for bus signals) or
/// <c>UtilityLibrary.SignalMarshal.EmitNode</c> (for per-Node signals). Direct
/// <c>EmitSignal</c> / <c>SignalBus.EmitX</c> from a tickable will crash Godot. SceneTree
/// mutation (AddChild / RemoveChild / QueueFree / Reparent) must be marshaled with
/// <c>CallDeferredThreadGroup("add_child", ...)</c> or wrapped in
/// <c>Callable.From(() => ...).CallDeferred()</c>. New tick-reachable signals must ship
/// with a <c>SafeEmit*</c> companion in <c>SignalBus</c>.
/// </summary>
public sealed class ManufactureTickEngine
{
    public const float TickHz = 60f;
    public const float TickDelta = 1f / TickHz;

    private static ManufactureTickEngine? _instance;
    public static ManufactureTickEngine? Instance => _instance;

    private enum OpKind { Add, Remove }

    private readonly struct Op
    {
        public readonly OpKind Kind;
        public readonly IManufactureTickable Target;
        public Op(OpKind kind, IManufactureTickable target) { Kind = kind; Target = target; }
    }

    private const int RollingWindow = 60;

    private readonly ConcurrentQueue<Op> _ops = new();
    private readonly List<IManufactureTickable> _tickables = new();
    private readonly ManualResetEventSlim _pauseGate = new(initialState: true);
    private readonly CancellationTokenSource _cts = new();
    private Thread? _worker;
    private long _tickCount;

    // Telemetry: written only by the worker thread (Interlocked-friendly), read by UI / debug.
    private long _lastTickElapsedTicks;
    private long _maxTickElapsedTicks;
    private long _behindByTicks;
    private readonly long[] _ringDurations = new long[RollingWindow];
    private long _ringIndex;

    private long _exceptionCount;
    private string? _lastExceptionType;
    private string? _lastExceptionMessage;
    private string? _lastExceptionTickable;
    private DateTime _lastExceptionUtc;
    private readonly object _exceptionLock = new();

    // Snapshot of the active tickables, republished when DrainOps mutates the list.
    private volatile IManufactureTickable[] _publishedTickables = Array.Empty<IManufactureTickable>();

    public bool IsRunning => _worker != null && _worker.IsAlive && !_cts.IsCancellationRequested;
    public bool IsPaused => !_pauseGate.IsSet;
    public long TickCount => Interlocked.Read(ref _tickCount);
    public int TickableCount => _publishedTickables.Length;
    public int PendingOpsCount => _ops.Count;

    public long LastTickElapsedTicks => Interlocked.Read(ref _lastTickElapsedTicks);
    public long MaxTickElapsedTicks => Interlocked.Read(ref _maxTickElapsedTicks);
    public long BehindByTicks => Interlocked.Read(ref _behindByTicks);
    public long ExceptionCount => Interlocked.Read(ref _exceptionCount);

    public double LastTickMs => LastTickElapsedTicks * 1000.0 / Stopwatch.Frequency;
    public double TargetTickMs => 1000.0 / TickHz;
    public double BehindByMs => -BehindByTicks * 1000.0 / Stopwatch.Frequency;

    public double AverageTickMs()
    {
        long sum = 0;
        int n = 0;
        for (int i = 0; i < RollingWindow; i++)
        {
            long v = Interlocked.Read(ref _ringDurations[i]);
            if (v > 0) { sum += v; n++; }
        }
        return n == 0 ? 0.0 : sum / (double)n * 1000.0 / Stopwatch.Frequency;
    }

    public void ResetTimingStats()
    {
        Interlocked.Exchange(ref _maxTickElapsedTicks, 0);
        for (int i = 0; i < RollingWindow; i++)
            Interlocked.Exchange(ref _ringDurations[i], 0);
    }

    public (string? type, string? message, string? tickable, DateTime utc) GetLastException()
    {
        lock (_exceptionLock)
            return (_lastExceptionType, _lastExceptionMessage, _lastExceptionTickable, _lastExceptionUtc);
    }

    public IReadOnlyList<IManufactureTickable> SnapshotTickables() => _publishedTickables;

    private ManufactureTickEngine() { }

    /// <summary>
    /// Starts the engine on a fresh background thread. Throws if an instance is already running.
    /// </summary>
    public static ManufactureTickEngine Start()
    {
        if (_instance != null)
            throw new InvalidOperationException(
                "ManufactureTickEngine already running. Call Stop() before starting a new instance.");

        var engine = new ManufactureTickEngine();
        _instance = engine;
        engine._worker = new Thread(engine.WorkerLoop)
        {
            IsBackground = true,
            Name = "ManufactureTickEngine",
        };
        engine._worker.Start();
        GameLogger.Info("[ManufactureTickEngine] Started");
        return engine;
    }

    /// <summary>
    /// Signals the worker to stop, joins it (with timeout), and clears the singleton instance.
    /// In-flight tick completes; queued ops are dropped.
    /// </summary>
    public void Stop()
    {
        _cts.Cancel();
        _pauseGate.Set(); // Unblock if paused
        if (_worker != null && _worker.IsAlive)
        {
            if (!_worker.Join(2000))
                GameLogger.Warning("[ManufactureTickEngine] Worker did not exit within 2s");
        }
        _tickables.Clear();
        _publishedTickables = Array.Empty<IManufactureTickable>();
        while (_ops.TryDequeue(out _)) { }
        if (ReferenceEquals(_instance, this))
            _instance = null;
        GameLogger.Info("[ManufactureTickEngine] Stopped");
    }

    /// <summary>
    /// Pauses the tick. The current in-flight tick (if any) completes; the next iteration blocks
    /// until Resume.
    /// </summary>
    public void Pause() => _pauseGate.Reset();

    public void Resume() => _pauseGate.Set();

    public void Register(IManufactureTickable t)
    {
        if (t == null) return;
        _ops.Enqueue(new Op(OpKind.Add, t));
    }

    public void Unregister(IManufactureTickable t)
    {
        if (t == null) return;
        _ops.Enqueue(new Op(OpKind.Remove, t));
    }

    public void RegisterBatch(IEnumerable<IManufactureTickable> ts)
    {
        if (ts == null) return;
        foreach (var t in ts)
            if (t != null) _ops.Enqueue(new Op(OpKind.Add, t));
    }

    private void WorkerLoop()
    {
        var sw = Stopwatch.StartNew();
        long startTicks = sw.ElapsedTicks;
        long ticksPerInterval = Stopwatch.Frequency / (long)TickHz;
        var token = _cts.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                _pauseGate.Wait(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            DrainOps();

            long tickStart = sw.ElapsedTicks;
            for (int i = 0; i < _tickables.Count; i++)
            {
                try
                {
                    _tickables[i].OnManufactureTick(TickDelta);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _exceptionCount);
                    var tickableTypeName = _tickables[i].GetType().Name;
                    lock (_exceptionLock)
                    {
                        _lastExceptionType = ex.GetType().Name;
                        _lastExceptionMessage = ex.Message;
                        _lastExceptionTickable = tickableTypeName;
                        _lastExceptionUtc = DateTime.UtcNow;
                    }
                    GameLogger.Error(
                        $"[ManufactureTickEngine] Tickable threw: {ex.GetType().Name}: {ex.Message}");
                }
            }
            long tickElapsed = sw.ElapsedTicks - tickStart;
            Interlocked.Exchange(ref _lastTickElapsedTicks, tickElapsed);
            int slot = (int)(_ringIndex % RollingWindow);
            Interlocked.Exchange(ref _ringDurations[slot], tickElapsed);
            _ringIndex++;
            if (tickElapsed > Interlocked.Read(ref _maxTickElapsedTicks))
                Interlocked.Exchange(ref _maxTickElapsedTicks, tickElapsed);

            Interlocked.Increment(ref _tickCount);

            // Pace to the next tick boundary
            long currentTicks = TickCount; // Already incremented
            long nextDeadline = startTicks + currentTicks * ticksPerInterval;
            long elapsed = sw.ElapsedTicks;
            long remaining = nextDeadline - elapsed;
            Interlocked.Exchange(ref _behindByTicks, remaining);

            if (remaining < -2 * ticksPerInterval)
            {
                // Falling far behind — log + resync without compounding
                GameLogger.Warning(
                    $"[ManufactureTickEngine] Behind by {-remaining / (double)Stopwatch.Frequency * 1000:F1}ms; resyncing");
                startTicks = sw.ElapsedTicks - currentTicks * ticksPerInterval;
            }
            else if (remaining > 0)
            {
                // Coarse sleep while >1ms remains; spin-wait the tail
                long oneMsTicks = Stopwatch.Frequency / 1000;
                while (sw.ElapsedTicks < nextDeadline - oneMsTicks)
                {
                    if (token.IsCancellationRequested) break;
                    Thread.Sleep(1);
                }
                while (sw.ElapsedTicks < nextDeadline)
                {
                    if (token.IsCancellationRequested) break;
                    Thread.SpinWait(64);
                }
            }
        }
    }

    private void DrainOps()
    {
        bool changed = false;

        while (_ops.TryDequeue(out var op))
        {
            switch (op.Kind)
            {
                case OpKind.Add:
                    if (!_tickables.Contains(op.Target))
                    {
                        _tickables.Add(op.Target);
                        changed = true;
                    }
                    break;
                case OpKind.Remove:
                    changed |= _tickables.Remove(op.Target);
                    break;
            }
        }

        if (changed)
        {
            _tickables.Sort((a, b) => a.TickPriority.CompareTo(b.TickPriority));
            _publishedTickables = _tickables.ToArray();
        }
    }

    /// <summary>
    /// Test-only hook — runs one tick worth of work synchronously on the calling thread.
    /// Engine MUST NOT be Started when this is called.
    /// </summary>
    internal void SingleTickForTesting()
    {
        if (IsRunning)
            throw new InvalidOperationException("Cannot single-tick a running engine");
        DrainOps();
        for (int i = 0; i < _tickables.Count; i++)
        {
            try { _tickables[i].OnManufactureTick(TickDelta); }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _exceptionCount);
                var tickableTypeName = _tickables[i].GetType().Name;
                lock (_exceptionLock)
                {
                    _lastExceptionType = ex.GetType().Name;
                    _lastExceptionMessage = ex.Message;
                    _lastExceptionTickable = tickableTypeName;
                    _lastExceptionUtc = DateTime.UtcNow;
                }
                GameLogger.Error($"[ManufactureTickEngine] {ex.Message}");
            }
        }
        Interlocked.Increment(ref _tickCount);
    }

    /// <summary>
    /// Test-only constructor for harnesses that want to drive ticks manually via SingleTickForTesting.
    /// Sets the singleton instance so that <see cref="Instance"/>-based registrations work.
    /// </summary>
    internal static ManufactureTickEngine CreateForTesting()
    {
        var engine = new ManufactureTickEngine();
        _instance = engine;
        return engine;
    }
}
