using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Constructables.Stations;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace Constructables.Stations.Behaviors;

/// <summary>
/// "Market Station" behavior — a wormhole sink that lets the player sell cargo to the galactic market.
/// A logistics unit routed here is accepted (subject to a ship-level limit), held out of the system for
/// a cargo-dependent number of manufacture ticks, has its hull sold at the "to market" segment boundary,
/// optionally back-filled by standing purchase orders, then released so its schedule continues.
///
/// Hold duration (ticks):
///   sellSegment = (BaseHoldTime / 2) * sellUnits     * GetToMarketSpeed()
///   buySegment  = (BaseHoldTime / 2) * plannedBuy    * GetFromMarketSpeed()
///   total       = sellSegment + buySegment
/// so a ship that arrives empty but leaves with purchases is still held.
///
/// Threading: <see cref="OnManufactureTick"/> runs on the ManufactureTickEngine background thread and
/// only advances per-unit tick counters; all economy/scene side effects are marshalled to the main
/// thread via <c>Callable.From(...).CallDeferred()</c>. <see cref="TryAcceptUnit"/> is called from the
/// executor's _PhysicsProcess (main thread). A single <see cref="_lock"/> guards the held/queue/counter
/// collections.
/// </summary>
public partial class MarketStationBehavior : RefCounted, IStationBehavior, IStationBehaviorConfigurable, IStationBehaviorDisplay
{
    /// <summary>A unit currently held out of the system by this station.</summary>
    internal sealed class HeldUnit
    {
        public string UnitId = "";
        public LogisticsUnit Unit = null!;
        public long SellSegmentTicks;
        public long TotalHoldTicks;
        public long ElapsedTicks;
        public bool HasSold;
        public bool Releasing;
        public Dictionary<string, int> PlannedPurchase = new();
    }

    private StationSatellite? _owner;
    private readonly object _lock = new();

    // --- Config (set by Configure before OnAttach; read-only thereafter) ---
    public int ShipLevelLimit { get; private set; } = 1;
    public int MaxHeldShips { get; private set; } = 1;
    public float BaseHoldTime { get; private set; } = 600f; // ticks
    public float BaseToMarketSpeed { get; private set; } = 1f;
    public float BaseFromMarketSpeed { get; private set; } = 1f;

    private readonly List<MarketStationModifier> _modifiers = new();

    // --- Runtime/persisted state (guarded by _lock) ---
    private readonly List<MarketPurchaseOrder> _purchaseOrders = new();
    private readonly List<HeldUnit> _held = new();
    private readonly List<LogisticsUnit> _queue = new();
    private readonly Dictionary<string, int> _purchasedThisMonth = new();

    // Last absolute month index (TimeKeeper.AbsoluteMonth) the counters were aligned to. Persisted via
    // StationDto.MarketState.MonthAnchorTick; counters reset when the live clock advances past it.
    private long _monthAnchorTick;

    public StationSatellite? Owner => _owner;

    // =====================================================================
    // Configuration
    // =====================================================================

    public void Configure(Dictionary<string, object> config)
    {
        ShipLevelLimit = CfgInt(config, "ship_level_limit", ShipLevelLimit);
        MaxHeldShips = CfgInt(config, "max_held_ships", MaxHeldShips);
        BaseHoldTime = CfgFloat(config, "base_hold_time", BaseHoldTime);
        BaseToMarketSpeed = CfgFloat(config, "to_market_speed_modifier", BaseToMarketSpeed);
        BaseFromMarketSpeed = CfgFloat(config, "from_market_speed_modifier", BaseFromMarketSpeed);

        if (config.TryGetValue("modifiers", out var modsObj) && modsObj is List<object> modList)
        {
            foreach (var mo in modList)
            {
                if (mo is Dictionary<object, object> md)
                    _modifiers.Add(ParseModifier(md));
            }
        }
    }

    private static MarketStationModifier ParseModifier(Dictionary<object, object> md)
    {
        string typeStr = BaseConfigLoader.ReadString(md, "type", "multiplicative");
        bool additive = string.Equals(typeStr, "additive", StringComparison.OrdinalIgnoreCase);
        string source = BaseConfigLoader.ReadString(md, "source", "config");
        float neutral = additive ? 0f : 1f;
        float toS = BaseConfigLoader.ReadFloat(md, "to_market_speed", neutral);
        float fromS = BaseConfigLoader.ReadFloat(md, "from_market_speed", neutral);
        float sell = BaseConfigLoader.ReadFloat(md, "sell_price", neutral);
        float buy = BaseConfigLoader.ReadFloat(md, "buy_price", neutral);
        return additive
            ? MarketStationModifier.Additive(source, toS, fromS, sell, buy)
            : MarketStationModifier.Multiplicative(source, toS, fromS, sell, buy);
    }

    // =====================================================================
    // Lifecycle
    // =====================================================================

    public void OnAttach(StationSatellite owner) => _owner = owner;
    public void OnRegister() { }
    public void OnUnregister() { }
    public void OnDetach() => _owner = null;

    public bool WantsTick => true;
    public int Priority => 0;

    public void OnManufactureTick(float delta, StationSatellite owner)
    {
        List<HeldUnit>? toSell = null;
        List<HeldUnit>? toRelease = null;

        lock (_lock)
        {
            ResetMonthlyLimitsIfNeeded();

            foreach (var h in _held)
            {
                if (h.Releasing)
                    continue;

                h.ElapsedTicks++;

                if (!h.HasSold && h.ElapsedTicks >= h.SellSegmentTicks)
                {
                    h.HasSold = true;
                    (toSell ??= new List<HeldUnit>()).Add(h);
                }

                if (h.ElapsedTicks >= h.TotalHoldTicks)
                {
                    h.Releasing = true;
                    (toRelease ??= new List<HeldUnit>()).Add(h);
                }
            }
        }

        if (toSell != null)
            foreach (var h in toSell)
            {
                var captured = h;
                Callable.From(() => DoSellAndPurchase(captured)).CallDeferred();
            }

        if (toRelease != null)
            foreach (var h in toRelease)
            {
                var captured = h;
                Callable.From(() => DoRelease(captured)).CallDeferred();
            }
    }

    // =====================================================================
    // Accept / hold (main thread)
    // =====================================================================

    /// <summary>
    /// Called by <see cref="OrbitalScheduleExecutor"/> when a unit arrives at this station. Returns true
    /// if the station took responsibility for the unit (held or queued) — the executor then pauses the
    /// schedule. Returns false only when the ship level exceeds <see cref="ShipLevelLimit"/>, in which
    /// case the executor completes the leg normally with no sale. Main-thread only.
    /// </summary>
    public bool TryAcceptUnit(LogisticsUnit unit)
    {
        if (unit == null)
            return false;

        int level = unit.ShipDef?.ShipLevel ?? 1;
        if (level > ShipLevelLimit)
        {
            GameLogger.Info(
                $"[MarketStation {_owner?.Name}] Rejecting {unit.Name}: ship level {level} > limit {ShipLevelLimit}");
            return false;
        }

        bool hasSlot;
        lock (_lock)
            hasSlot = _held.Count < MaxHeldShips;

        if (hasSlot)
        {
            BeginHold(unit);
        }
        else
        {
            lock (_lock)
                _queue.Add(unit);
            GameLogger.Info($"[MarketStation {_owner?.Name}] Queued {unit.Name} (capacity {MaxHeldShips})");
            SignalBus.Instance?.SafeEmitMarketStationUnitAccepted(_owner?.Id ?? "", unit.Id);
        }

        return true;
    }

    /// <summary>Begins holding a unit: sizes the hold, hides the unit, registers the record. Main thread.</summary>
    private void BeginHold(LogisticsUnit unit)
    {
        int sellAmt = unit.Cargo?.TotalUnits ?? 0;
        double budget = CompanyDataTracker.Instance?.Budget ?? 0.0;
        int plannableUnits = (int)Math.Floor(unit.ShipDef?.CargoCapacity ?? 0f); // hull empties after the sale

        Dictionary<string, int> plan;
        lock (_lock)
            plan = PlanPurchases(budget, plannableUnits);

        int buyAmt = plan.Values.Sum();
        var (sellSeg, totalSeg) = ComputeHoldTicks(
            BaseHoldTime, sellAmt, buyAmt, GetToMarketSpeed(), GetFromMarketSpeed());

        var record = new HeldUnit
        {
            UnitId = unit.Id,
            Unit = unit,
            SellSegmentTicks = sellSeg,
            TotalHoldTicks = totalSeg,
            ElapsedTicks = 0,
            HasSold = false,
            PlannedPurchase = plan,
        };

        unit.EnterMarketHold();
        lock (_lock)
            _held.Add(record);

        GameLogger.Info(
            $"[MarketStation {_owner?.Name}] Holding {unit.Name}: sell {sellAmt}u, buy {buyAmt}u, {record.TotalHoldTicks} ticks");
        SignalBus.Instance?.SafeEmitMarketStationUnitAccepted(_owner?.Id ?? "", unit.Id);
    }

    /// <summary>
    /// Greedily plans the buy-back across purchase orders against a budget and free cargo units. Reads
    /// the running monthly counters; call under <see cref="_lock"/>.
    /// </summary>
    private Dictionary<string, int> PlanPurchases(double budget, int availableUnits)
    {
        var plan = new Dictionary<string, int>();
        var economy = EconomyTracker.Instance;
        if (economy == null)
            return plan;

        double buyMult = GetBuyMultiplier();
        foreach (var order in _purchaseOrders)
        {
            if (availableUnits <= 0 || budget <= 0)
                break;
            if (order.MonthlyLimit <= 0 || string.IsNullOrEmpty(order.ResourceId))
                continue;

            double unitPrice = economy.GetPrice(order.ResourceId) * buyMult;
            if (unitPrice <= 0)
                continue;

            _purchasedThisMonth.TryGetValue(order.ResourceId, out int already);
            int monthlyRemaining = Math.Max(0, order.MonthlyLimit - already);
            if (monthlyRemaining <= 0)
                continue;

            int affordable = (int)Math.Floor(budget / unitPrice);
            int qty = Math.Min(Math.Min(monthlyRemaining, affordable), availableUnits);
            if (qty <= 0)
                continue;

            plan[order.ResourceId] = qty;
            budget -= qty * unitPrice;
            availableUnits -= qty;
        }

        return plan;
    }

    // =====================================================================
    // Sell + purchase + release (main thread, deferred from tick)
    // =====================================================================

    private void DoSellAndPurchase(HeldUnit h)
    {
        var economy = EconomyTracker.Instance;
        var unit = h.Unit;
        double totalRevenue = 0.0;
        double sellMult = GetSellMultiplier();

        if (unit.Cargo != null && economy != null)
        {
            var snapshot = new Dictionary<string, int>(unit.Cargo.Resources);
            foreach (var kv in snapshot)
            {
                double unitPrice = economy.GetPrice(kv.Key) * sellMult;
                if (economy.RequestSell(kv.Key, kv.Value, sellMult))
                {
                    totalRevenue += unitPrice * kv.Value;
                    unit.UnloadCargo(kv.Key, kv.Value);
                }
            }
        }

        FulfillPurchases(h);

        GameLogger.Info(
            $"[MarketStation {_owner?.Name}] Sold {unit.Name}'s cargo for {totalRevenue:F0}");
        SignalBus.Instance?.SafeEmitMarketStationSaleCompleted(_owner?.Id ?? "", h.UnitId, totalRevenue);
    }

    private void FulfillPurchases(HeldUnit h)
    {
        var economy = EconomyTracker.Instance;
        if (economy == null || h.PlannedPurchase.Count == 0)
            return;

        var unit = h.Unit;
        double buyMult = GetBuyMultiplier();

        lock (_lock)
        {
            double budget = CompanyDataTracker.Instance?.Budget ?? 0.0;
            int availableUnits = GetRemainingCargoUnits(unit);

            foreach (var kv in h.PlannedPurchase)
            {
                if (availableUnits <= 0 || budget <= 0)
                    break;

                string id = kv.Key;
                int planned = kv.Value;
                if (planned <= 0)
                    continue;

                double unitPrice = economy.GetPrice(id) * buyMult;
                if (unitPrice <= 0)
                    continue;

                _purchasedThisMonth.TryGetValue(id, out int already);
                int monthlyRemaining = Math.Max(0, GetMonthlyLimit(id) - already);
                int affordable = (int)Math.Floor(budget / unitPrice);
                int qty = Math.Min(Math.Min(planned, monthlyRemaining), Math.Min(affordable, availableUnits));
                if (qty <= 0)
                    continue;

                if (economy.RequestBuy(id, qty, buyMult))
                {
                    unit.LoadCargo(id, qty);
                    _purchasedThisMonth[id] = already + qty;
                    budget -= qty * unitPrice;
                    availableUnits -= qty;
                }
            }
        }
    }

    private void DoRelease(HeldUnit h)
    {
        var unit = h.Unit;
        unit.ExitMarketHold();
        unit.ScheduleExecutor?.ResumeAfterMarketHold();

        LogisticsUnit? promote = null;
        lock (_lock)
        {
            _held.Remove(h);
            if (_queue.Count > 0 && _held.Count < MaxHeldShips)
            {
                promote = _queue[0];
                _queue.RemoveAt(0);
            }
        }

        GameLogger.Info($"[MarketStation {_owner?.Name}] Released {unit.Name}");
        SignalBus.Instance?.SafeEmitMarketStationUnitReleased(_owner?.Id ?? "", h.UnitId);

        if (promote != null)
            BeginHold(promote);
    }

    // =====================================================================
    // Modifiers
    // =====================================================================

    public float GetToMarketSpeed() => FoldSpeed(BaseToMarketSpeed, m => m.ToMarketSpeed);
    public float GetFromMarketSpeed() => FoldSpeed(BaseFromMarketSpeed, m => m.FromMarketSpeed);
    public double GetSellMultiplier() => FoldMultiplier(m => m.SellPriceMultiplier);
    public double GetBuyMultiplier() => FoldMultiplier(m => m.BuyPriceMultiplier);

    private float FoldSpeed(float baseValue, Func<MarketStationModifier, float> select)
    {
        float v = baseValue;
        foreach (var m in _modifiers)
            if (m.Type == ModifierType.Additive)
                v += select(m);
        foreach (var m in _modifiers)
            if (m.Type == ModifierType.Multiplicative)
                v *= select(m);
        return Math.Max(0f, v);
    }

    private double FoldMultiplier(Func<MarketStationModifier, float> select)
    {
        double v = 1.0;
        foreach (var m in _modifiers)
            if (m.Type == ModifierType.Additive)
                v += select(m);
        foreach (var m in _modifiers)
            if (m.Type == ModifierType.Multiplicative)
                v *= select(m);
        return Math.Max(0.0, v);
    }

    public void AddModifier(MarketStationModifier modifier) => _modifiers.Add(modifier);

    // =====================================================================
    // Purchase orders (player-edited, persisted)
    // =====================================================================

    public IReadOnlyList<MarketPurchaseOrder> GetPurchaseOrders()
    {
        lock (_lock)
            return _purchaseOrders.ToList();
    }

    public void SetPurchaseOrder(string resourceId, int monthlyLimit)
    {
        if (string.IsNullOrEmpty(resourceId))
            return;
        lock (_lock)
        {
            var existing = _purchaseOrders.FirstOrDefault(o => o.ResourceId == resourceId);
            if (existing != null)
                existing.MonthlyLimit = monthlyLimit;
            else
                _purchaseOrders.Add(new MarketPurchaseOrder(resourceId, monthlyLimit));
        }
    }

    public void RemovePurchaseOrder(string resourceId)
    {
        lock (_lock)
            _purchaseOrders.RemoveAll(o => o.ResourceId == resourceId);
    }

    private int GetMonthlyLimit(string resourceId)
    {
        var order = _purchaseOrders.FirstOrDefault(o => o.ResourceId == resourceId);
        return order?.MonthlyLimit ?? 0;
    }

    /// <summary>
    /// Resets the per-month purchase counters when the global clock (<see cref="TimeKeeper"/>) advances
    /// into a new month. <see cref="_monthAnchorTick"/> holds the last absolute month the counters were
    /// aligned to. No-op until the clock exists. Call under <see cref="_lock"/>.
    /// </summary>
    private void ResetMonthlyLimitsIfNeeded()
    {
        var tk = TimeKeeper.Instance;
        if (tk == null)
            return;

        long month = tk.AbsoluteMonth;
        if (month != _monthAnchorTick)
        {
            _monthAnchorTick = month;
            _purchasedThisMonth.Clear();
        }
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    public int HeldCount { get { lock (_lock) return _held.Count; } }
    public int QueuedCount { get { lock (_lock) return _queue.Count; } }

    /// <summary>
    /// Pure hold-duration formula (ticks). The hold is split into a "to market" segment sized by the
    /// units being sold and a "from market" segment sized by the units bought back, so an empty ship
    /// that leaves with purchases is still held. Returns (sellSegment, total).
    /// </summary>
    public static (long SellSegment, long Total) ComputeHoldTicks(
        float baseHoldTime, int sellAmount, int buyAmount, float toMarketSpeed, float fromMarketSpeed)
    {
        long sellSeg = (long)(baseHoldTime / 2.0 * sellAmount * toMarketSpeed);
        long buySeg = (long)(baseHoldTime / 2.0 * buyAmount * fromMarketSpeed);
        return (sellSeg, sellSeg + buySeg);
    }

    private static int GetRemainingCargoUnits(LogisticsUnit unit)
    {
        float capacity = unit.ShipDef?.CargoCapacity ?? 0f;
        float used = unit.GetCargoMass();
        return Math.Max(0, (int)Math.Floor(capacity - used));
    }

    private static int CfgInt(Dictionary<string, object> c, string key, int fallback) =>
        c.TryGetValue(key, out var v) && v != null ? SafeInt(v, fallback) : fallback;

    private static float CfgFloat(Dictionary<string, object> c, string key, float fallback) =>
        c.TryGetValue(key, out var v) && v != null ? SafeFloat(v, fallback) : fallback;

    private static int SafeInt(object v, int fallback)
    {
        try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
        catch { return fallback; }
    }

    private static float SafeFloat(object v, float fallback)
    {
        try { return Convert.ToSingle(v, CultureInfo.InvariantCulture); }
        catch { return fallback; }
    }

    // =====================================================================
    // IStationBehaviorDisplay
    // =====================================================================

    public string TabLabel => "Market";

    public IEnumerable<(string Key, string Value)> GetDisplayRows()
    {
        int held, queued, orders;
        lock (_lock)
        {
            held = _held.Count;
            queued = _queue.Count;
            orders = _purchaseOrders.Count;
        }
        yield return ("Ship Level Limit", ShipLevelLimit.ToString());
        yield return ("Hold Capacity", MaxHeldShips.ToString());
        yield return ("Held", held.ToString());
        yield return ("Queued", queued.ToString());
        yield return ("Purchase Orders", orders.ToString());
        yield return ("Base Hold (ticks)", BaseHoldTime.ToString("F0", CultureInfo.InvariantCulture));
    }
}
