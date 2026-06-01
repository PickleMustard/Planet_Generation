using System.Collections.Generic;
using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace Structures.GameState;

/// <summary>
/// System-level owner of the galactic market: a price per resource, buy/sell orders that
/// move currency to/from <see cref="CompanyDataTracker"/>, and a periodic price-update loop.
/// A sibling of <see cref="SystemData"/> under system_container; session-scoped and accessed
/// through the static <see cref="Instance"/>.
///
/// This is a framework: prices, order plumbing, and a Timer-driven update hook to plug a real
/// supply/demand model into later. Prices update on a main-thread Godot <see cref="Timer"/>,
/// so all signal emission uses plain SignalBus emits (no SafeEmit needed).
/// </summary>
public partial class EconomyTracker : Node
{
    public static EconomyTracker? Instance { get; private set; }

    /// <summary>Seconds between price-update ticks.</summary>
    [Export]
    public float PriceUpdateIntervalSeconds { get; set; } = 5f;

    /// <summary>
    /// Per-resource market state. Supply/demand pressures accumulate between price ticks
    /// from player orders and are consumed (reset) each tick.
    /// </summary>
    public class MarketEntry
    {
        public double BasePrice;
        public double CurrentPrice;
        public double SupplyPressure;
        public double DemandPressure;
    }

    private readonly Dictionary<string, MarketEntry> _market = new();
    private Timer? _priceTimer;

    public override void _Ready()
    {
        Instance = this;
        SeedMarket();

        _priceTimer = new Timer
        {
            Name = "PriceUpdateTimer",
            WaitTime = PriceUpdateIntervalSeconds,
            Autostart = true,
            OneShot = false,
        };
        AddChild(_priceTimer);
        _priceTimer.Timeout += OnPriceUpdateTick;

        base._Ready();
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
        base._ExitTree();
    }

    /// <summary>Builds the price table from every resource in the database.</summary>
    private void SeedMarket()
    {
        _market.Clear();
        foreach (var kvp in ResourceDatabase.Instance.GetAllResources())
        {
            var def = kvp.Value;
            // Starting price from the resource definition, stored in cents (hundredths).
            double basePrice = def.BasePrice;
            _market[kvp.Key] = new MarketEntry
            {
                BasePrice = basePrice,
                CurrentPrice = basePrice,
            };
        }
    }

    /// <summary>Current market price for a resource, or 0 if unknown.</summary>
    public double GetPrice(string resourceId)
    {
        if (resourceId != null && _market.TryGetValue(resourceId, out var entry))
            return entry.CurrentPrice;
        return 0.0;
    }

    public IReadOnlyDictionary<string, MarketEntry> GetMarket() => _market;

    /// <summary>
    /// Overwrites market prices from a loaded save. Must be called AFTER _Ready/SeedMarket so the
    /// loaded prices replace the freshly seeded defaults. Resources missing from the save keep their
    /// seeded entry; supply/demand pressures are left at zero (they are transient). Emits a single
    /// MarketPricesUpdated so listeners refresh.
    /// </summary>
    public void LoadState(
        float priceUpdateIntervalSeconds,
        IReadOnlyList<(string Id, double BasePrice, double CurrentPrice)> entries)
    {
        PriceUpdateIntervalSeconds = priceUpdateIntervalSeconds;
        if (_priceTimer != null)
            _priceTimer.WaitTime = priceUpdateIntervalSeconds;

        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.Id))
                continue;
            if (!_market.TryGetValue(e.Id, out var entry))
            {
                entry = new MarketEntry();
                _market[e.Id] = entry;
            }
            entry.BasePrice = e.BasePrice;
            entry.CurrentPrice = e.CurrentPrice;
            entry.SupplyPressure = 0;
            entry.DemandPressure = 0;
        }

        SignalBus.Instance?.EmitMarketPricesUpdated();
    }

    /// <summary>
    /// Attempts to buy <paramref name="quantity"/> units at the current price, withdrawing the
    /// cost from the company budget. Returns false (and emits MarketOrderRejected) on an unknown
    /// resource, non-positive quantity, or insufficient funds.
    /// </summary>
    public bool RequestBuy(string resourceId, int quantity)
    {
        if (string.IsNullOrEmpty(resourceId) || !_market.TryGetValue(resourceId, out var entry))
        {
            SignalBus.Instance?.EmitMarketOrderRejected(resourceId, quantity, "unknown_resource");
            return false;
        }
        if (quantity <= 0)
        {
            SignalBus.Instance?.EmitMarketOrderRejected(resourceId, quantity, "invalid_quantity");
            return false;
        }

        double cost = entry.CurrentPrice * quantity;
        if (CompanyDataTracker.Instance?.TryWithdraw(cost) != true)
        {
            SignalBus.Instance?.EmitMarketOrderRejected(resourceId, quantity, "insufficient_funds");
            return false;
        }

        entry.DemandPressure += quantity;
        SignalBus.Instance?.EmitMarketOrderFilled(resourceId, quantity, cost, true);
        return true;
    }

    /// <summary>
    /// Sells <paramref name="quantity"/> units at the current price, depositing the proceeds into
    /// the company budget. Returns false (and emits MarketOrderRejected) on an unknown resource or
    /// non-positive quantity.
    /// </summary>
    public bool RequestSell(string resourceId, int quantity)
    {
        if (string.IsNullOrEmpty(resourceId) || !_market.TryGetValue(resourceId, out var entry))
        {
            SignalBus.Instance?.EmitMarketOrderRejected(resourceId, quantity, "unknown_resource");
            return false;
        }
        if (quantity <= 0)
        {
            SignalBus.Instance?.EmitMarketOrderRejected(resourceId, quantity, "invalid_quantity");
            return false;
        }

        double revenue = entry.CurrentPrice * quantity;
        CompanyDataTracker.Instance?.Deposit(revenue);

        entry.SupplyPressure += quantity;
        SignalBus.Instance?.EmitMarketOrderFilled(resourceId, quantity, revenue, false);
        return true;
    }

    /// <summary>
    /// Periodic price recalculation. PLACEHOLDER model: nudges each price toward equilibrium by a
    /// small proportional step driven by net demand (demand - supply) accumulated since the last
    /// tick, then resets the pressures. Replace with the real galactic supply/demand model.
    /// </summary>
    private void OnPriceUpdateTick()
    {
        const double adjustmentRate = 0.01;
        const double minPrice = 0.01;

        bool anyChanged = false;
        foreach (var kvp in _market)
        {
            var entry = kvp.Value;
            double net = entry.DemandPressure - entry.SupplyPressure;
            entry.DemandPressure = 0;
            entry.SupplyPressure = 0;

            if (net == 0)
                continue;

            double newPrice = entry.CurrentPrice + entry.BasePrice * adjustmentRate * net;
            newPrice = newPrice < minPrice ? minPrice : newPrice;
            if (newPrice == entry.CurrentPrice)
                continue;

            entry.CurrentPrice = newPrice;
            SignalBus.Instance?.EmitMarketPriceChanged(kvp.Key, newPrice);
            anyChanged = true;
        }

        if (anyChanged)
            SignalBus.Instance?.EmitMarketPricesUpdated();
    }
}
