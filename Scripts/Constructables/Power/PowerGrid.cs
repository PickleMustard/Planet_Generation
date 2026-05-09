using System.Collections.Generic;
using Constructables.Buildings.Behaviors;
using Constructables.Tick;
using Godot;
using ProceduralGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables.Power;

/// <summary>
/// A connected region of cells served by one or more <see cref="IGridContributor"/>
/// buildings. Owns its own member sets and ticks once per manufacture tick to decide
/// supply vs. demand and gate <see cref="Building.PoweredOn"/> on every member.
///
/// Created, merged, split, and destroyed by <see cref="BodyPowerGridManager"/> in
/// response to building placement / removal events.
/// </summary>
public sealed class PowerGrid : IManufactureTickable
{
    public int Id { get; }
    public CelestialBody Body { get; }

    public HashSet<Building> Contributors { get; } = new();
    public HashSet<Building> Consumers { get; } = new();
    public HashSet<VoronoiCell> CoveredCells { get; } = new();

    public float LastGeneration { get; private set; }
    public float LastDraw { get; private set; }
    public float BatteryStored { get; private set; }
    public float BatteryCapacity { get; private set; }
    public bool IsBrownedOut { get; private set; }

    public GridStatistics Stats { get; private set; } = new();

    public int TickPriority => -100;

    public PowerGrid(int id, CelestialBody body)
    {
        Id = id;
        Body = body;
    }

    /// <summary>
    /// Replaces this grid's stats with the supplied instance. Used on merge to retain
    /// the larger participant's history.
    /// </summary>
    public void AdoptStats(GridStatistics stats) => Stats = stats;

    public void OnManufactureTick(float delta)
    {
        float generation = SumGeneration();
        float draw = SumDraw();
        float capacity = SumBatteryCapacity();
        float stored = SumBatteryStored();

        BatteryCapacity = capacity;
        LastGeneration = generation;
        LastDraw = draw;

        float net = generation - draw;
        bool wasBrownedOut = IsBrownedOut;

        if (net >= 0f)
        {
            // Surplus: charge batteries proportionally up to capacity.
            float room = capacity - stored;
            float charge = Mathf.Min(net, room);
            DistributeBatteryDelta(charge);
            BatteryStored = SumBatteryStored();
            IsBrownedOut = false;
        }
        else
        {
            float shortfall = -net;
            float drawn = Mathf.Min(shortfall, stored);
            DistributeBatteryDelta(-drawn);
            BatteryStored = SumBatteryStored();
            float remaining = shortfall - drawn;
            IsBrownedOut = remaining > 0f;
        }

        if (IsBrownedOut && !wasBrownedOut)
        {
            Stats.RecordBrownoutEntered();
            ApplyPowerStateToMembers(false);
        }
        else if (!IsBrownedOut && wasBrownedOut)
        {
            ApplyPowerStateToMembers(true);
        }

        Stats.RecordTick(generation, draw, IsBrownedOut);
    }

    /// <summary>
    /// Merges <paramref name="other"/> into this grid. The caller chooses which grid is
    /// kept; statistics on the surviving grid are preserved per the design spec.
    /// The other grid is left empty so its owner can unregister and dispose it.
    /// </summary>
    public void AbsorbFrom(PowerGrid other)
    {
        foreach (var c in other.Contributors)
            Contributors.Add(c);
        foreach (var c in other.Consumers)
            Consumers.Add(c);
        foreach (var cell in other.CoveredCells)
            CoveredCells.Add(cell);

        BatteryStored += other.BatteryStored;

        other.Contributors.Clear();
        other.Consumers.Clear();
        other.CoveredCells.Clear();
        other.BatteryStored = 0f;
        other.BatteryCapacity = 0f;
    }

    /// <summary>
    /// Sets <see cref="Building.PoweredOn"/> on every member. The engine continues ticking
    /// shut-off buildings; their <c>OnManufactureTick</c> early-returns on the
    /// <c>PoweredOn</c> gate so manufacturing progress is preserved.
    /// </summary>
    public void ApplyPowerStateToMembers(bool poweredOn)
    {
        foreach (var b in Contributors)
            b.PoweredOn = poweredOn;
        foreach (var b in Consumers)
            b.PoweredOn = poweredOn;
    }

    private float SumGeneration()
    {
        float total = 0f;
        foreach (var b in Contributors)
        {
            var producer = b.GetBehavior<PowerProducerBehavior>();
            if (producer != null && producer.IsProducing)
                total += producer.Output;
        }
        return total;
    }

    private float SumDraw()
    {
        float total = 0f;
        foreach (var b in Consumers)
        {
            if (b.IsUnderConstruction)
                continue;
            var consumer = b.GetBehavior<PowerConsumerBehavior>();
            if (consumer != null)
                total += consumer.GetCurrentDraw();
        }
        return total;
    }

    private float SumBatteryCapacity()
    {
        float total = 0f;
        foreach (var b in Contributors)
        {
            var battery = b.GetBehavior<BatteryBehavior>();
            if (battery != null)
                total += battery.Capacity;
        }
        return total;
    }

    private float SumBatteryStored()
    {
        float total = 0f;
        foreach (var b in Contributors)
        {
            var battery = b.GetBehavior<BatteryBehavior>();
            if (battery != null)
                total += battery.Stored;
        }
        return total;
    }

    private void DistributeBatteryDelta(float delta)
    {
        if (Mathf.IsZeroApprox(delta))
            return;

        var batteries = new List<BatteryBehavior>();
        foreach (var b in Contributors)
        {
            var bat = b.GetBehavior<BatteryBehavior>();
            if (bat != null)
                batteries.Add(bat);
        }
        if (batteries.Count == 0)
            return;

        if (delta > 0f)
        {
            float remaining = delta;
            foreach (var bat in batteries)
            {
                if (remaining <= 0f)
                    break;
                float room = bat.Capacity - bat.Stored;
                if (room <= 0f)
                    continue;
                float add = Mathf.Min(room, remaining);
                bat.Stored += add;
                remaining -= add;
            }
        }
        else
        {
            float remaining = -delta;
            foreach (var bat in batteries)
            {
                if (remaining <= 0f)
                    break;
                if (bat.Stored <= 0f)
                    continue;
                float take = Mathf.Min(bat.Stored, remaining);
                bat.Stored -= take;
                remaining -= take;
            }
        }
    }
}
