using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables.Stations.Behaviors;

namespace Tests.Stations;

/// <summary>
/// Pure unit tests for <see cref="MarketStationBehavior"/> — the parts that don't require Godot
/// singletons (EconomyTracker / CompanyDataTracker / scene tree): config parsing, modifier folding,
/// and the hold-duration formula. The sell/purchase/release path and save round-trip need a running
/// engine and are exercised via the in-engine smoke test described in the plan.
/// </summary>
[TestSuite]
public class MarketStationBehaviorTest
{
    private static MarketStationBehavior Configured(Dictionary<string, object> config)
    {
        var b = new MarketStationBehavior();
        b.Configure(config);
        return b;
    }

    [TestCase]
    public void Configure_ReadsScalarSettings()
    {
        var b = Configured(new Dictionary<string, object>
        {
            ["ship_level_limit"] = "3",
            ["max_held_ships"] = "4",
            ["base_hold_time"] = "600",
            ["to_market_speed_modifier"] = "0.85",
            ["from_market_speed_modifier"] = "1.2",
        });

        AssertThat(b.ShipLevelLimit).IsEqual(3);
        AssertThat(b.MaxHeldShips).IsEqual(4);
        AssertThat(b.BaseHoldTime).IsEqual(600f);
        AssertThat(b.BaseToMarketSpeed).IsEqual(0.85f);
        AssertThat(b.BaseFromMarketSpeed).IsEqual(1.2f);
    }

    [TestCase]
    public void HoldFormula_SplitsSellAndBuySegments()
    {
        // base=600, sell=10, buy=4, speeds=1.0
        // sell = 300 * 10 = 3000 ; buy = 300 * 4 = 1200 ; total = 4200
        var (sell, total) = MarketStationBehavior.ComputeHoldTicks(600f, 10, 4, 1f, 1f);
        AssertThat(sell).IsEqual(3000L);
        AssertThat(total).IsEqual(4200L);
    }

    [TestCase]
    public void HoldFormula_EmptyShipBuyingStillHeld()
    {
        // No cargo to sell, but buys 5 units back → only the buy segment contributes.
        var (sell, total) = MarketStationBehavior.ComputeHoldTicks(600f, 0, 5, 1f, 1f);
        AssertThat(sell).IsEqual(0L);
        AssertThat(total).IsEqual(1500L);
    }

    [TestCase]
    public void HoldFormula_SpeedModifiersScaleSegments()
    {
        // toSpeed 0.5 halves the sell segment; fromSpeed 2.0 doubles the buy segment.
        var (sell, total) = MarketStationBehavior.ComputeHoldTicks(600f, 10, 4, 0.5f, 2f);
        AssertThat(sell).IsEqual(1500L);       // 300 * 10 * 0.5
        AssertThat(total).IsEqual(1500L + 2400L); // buy = 300 * 4 * 2.0
    }

    [TestCase]
    public void Modifiers_MultiplicativeFoldOntoPrices()
    {
        var b = Configured(new Dictionary<string, object>
        {
            ["modifiers"] = new List<object>
            {
                new Dictionary<object, object>
                {
                    ["type"] = "multiplicative",
                    ["source"] = "Broker",
                    ["sell_price"] = "1.1",
                    ["buy_price"] = "0.9",
                },
            },
        });

        AssertThat(b.GetSellMultiplier()).IsEqualApprox(1.1, 0.0001);
        AssertThat(b.GetBuyMultiplier()).IsEqualApprox(0.9, 0.0001);
    }

    [TestCase]
    public void Modifiers_SpeedFoldsAdditiveThenMultiplicative()
    {
        var b = Configured(new Dictionary<string, object>
        {
            ["to_market_speed_modifier"] = "1.0",
            ["modifiers"] = new List<object>
            {
                new Dictionary<object, object>
                {
                    ["type"] = "additive",
                    ["source"] = "Tuning",
                    ["to_market_speed"] = "-0.2", // 1.0 + (-0.2) = 0.8
                },
                new Dictionary<object, object>
                {
                    ["type"] = "multiplicative",
                    ["source"] = "Overdrive",
                    ["to_market_speed"] = "0.5", // 0.8 * 0.5 = 0.4
                },
            },
        });

        AssertThat(b.GetToMarketSpeed()).IsEqualApprox(0.4f, 0.0001f);
    }

    [TestCase]
    public void PurchaseOrders_SetUpdateRemove()
    {
        var b = new MarketStationBehavior();
        b.SetPurchaseOrder("iron", 100);
        b.SetPurchaseOrder("iron", 250); // update existing
        b.SetPurchaseOrder("steel", 50);

        var orders = b.GetPurchaseOrders();
        AssertThat(orders.Count).IsEqual(2);

        b.RemovePurchaseOrder("iron");
        AssertThat(b.GetPurchaseOrders().Count).IsEqual(1);
        AssertThat(b.GetPurchaseOrders()[0].ResourceId).IsEqual("steel");
        AssertThat(b.GetPurchaseOrders()[0].MonthlyLimit).IsEqual(50);
    }
}
