using System.Threading;
using Constructables.Tick;
using Godot;
using Structures.Enums;
using UtilityLibrary;
using UtilityLibrary.SaveLoad;
using UtilityLibrary.SaveLoad.Dto;

namespace Structures.GameState;

/// <summary>
/// Game-instance clock built on top of (but separate from) the <see cref="ManufactureTickEngine"/>.
/// Registers as the highest-priority tickable so its tick count advances before any other simulation
/// each tick. Converts ticks → months via the constant <see cref="TicksPerMonth"/>; a month is a
/// <see cref="Quarter"/> (Q1..Q4) and a year is all four quarters. When enough ticks pass to roll the
/// month over it fires <c>SignalBus.MonthElapsed</c> so monthly systems (stats, limits) can refresh.
///
/// State is just the tick count — quarter/year are derived from it, so save/load persists a single
/// number and there is no drift. A sibling of <see cref="SystemData"/> under system_container;
/// session-scoped and accessed through the static <see cref="Instance"/>.
///
/// Threading: <see cref="OnManufactureTick"/> runs on the ManufactureTickEngine background thread.
/// The tick count is read/written via <see cref="Interlocked"/>, and the rollover signal is emitted
/// through <c>SignalBus.SafeEmitMonthElapsed</c> (off-main-thread safe).
/// </summary>
public partial class TimeKeeper : Node, IManufactureTickable, ISaveSerializable, ISaveRestorable
{
    public static TimeKeeper? Instance { get; private set; }

    /// <summary>Ticks per in-game month (quarter). At 60Hz this is 10 real minutes.</summary>
    public const long TicksPerMonth = 36000;

    /// <summary>Quarters per year (Q1..Q4).</summary>
    public const int QuartersPerYear = 4;

    /// <summary>Section discriminator routing this clock's DTO within the save file.</summary>
    public string SaveKey => "time";

    // Only writer is the manufacture tick thread; reads use Interlocked.Read.
    private long _tickCount;

    // Last absolute month for which MonthElapsed was fired. Tick-thread only, except Restore.
    private long _lastEmittedMonth;

    /// <summary>Total ticks elapsed since the game instance began.</summary>
    public long TotalTicks => Interlocked.Read(ref _tickCount);

    /// <summary>Monotonic month index since game start (0-based). Determines quarter + year.</summary>
    public long AbsoluteMonth => TotalTicks / TicksPerMonth;

    /// <summary>Current year (0-based) — four quarters per year.</summary>
    public int CurrentYear => (int)(AbsoluteMonth / QuartersPerYear);

    /// <summary>Current quarter within the year.</summary>
    public Quarter CurrentQuarter => (Quarter)(AbsoluteMonth % QuartersPerYear);

    // Tick first each frame so every other tickable observes the up-to-date clock.
    public int TickPriority => int.MinValue;

    public override void _Ready()
    {
        Instance = this;
        AddToGroup("save_serializable");
        // SystemData (added before TimeKeeper in GameScene) starts the engine in its _Ready, so the
        // engine is live here. Register so the clock advances each tick.
        ManufactureTickEngine.Instance?.Register(this);
        base._Ready();
    }

    public override void _ExitTree()
    {
        ManufactureTickEngine.Instance?.Unregister(this);
        if (ReferenceEquals(Instance, this))
            Instance = null;
        base._ExitTree();
    }

    public void OnManufactureTick(float delta)
    {
        // Advance the tick count first — this is the authoritative time base for the game instance.
        Interlocked.Increment(ref _tickCount);

        long month = Interlocked.Read(ref _tickCount) / TicksPerMonth;
        if (month != _lastEmittedMonth)
        {
            _lastEmittedMonth = month;
            SignalBus.Instance?.SafeEmitMonthElapsed(CurrentYear, (int)CurrentQuarter);
        }
    }

    /// <summary>Snapshots the clock into a <see cref="TimeDto"/>. See <see cref="ISaveSerializable"/>.</summary>
    public object Serialize() => new TimeDto { TotalTicks = TotalTicks };

    /// <summary>Restores the clock from a <see cref="TimeDto"/>. See <see cref="ISaveRestorable"/>.</summary>
    public void Restore(object dto)
    {
        if (dto is TimeDto t)
        {
            Interlocked.Exchange(ref _tickCount, t.TotalTicks);
            _lastEmittedMonth = AbsoluteMonth; // resume without re-firing past rollovers
        }
    }
}
