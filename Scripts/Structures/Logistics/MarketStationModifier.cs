using Structures.Enums;

namespace Structures.Logistics;

/// <summary>
/// A stackable modifier applied to a Market Station's behavior. Mirrors the shape of
/// <see cref="EngineModifier"/>: an <see cref="ModifierType"/> determines how the four factors
/// fold together (additive deltas are summed onto the base, multiplicative factors are multiplied),
/// and a <see cref="Source"/> string tags where the modifier came from (research, partnership, etc.).
///
/// The four factors:
///  - <see cref="ToMarketSpeed"/> / <see cref="FromMarketSpeed"/> scale the sell / purchase hold
///    segments. Lower is faster. (Additive: delta on 1.0; Multiplicative: factor.)
///  - <see cref="SellPriceMultiplier"/> scales sale revenue (&gt; 1 sells for more).
///  - <see cref="BuyPriceMultiplier"/> scales purchase cost (&lt; 1 buys for less).
/// </summary>
public struct MarketStationModifier
{
    public ModifierType Type { get; private set; }
    public string Source { get; private set; }

    public float ToMarketSpeed { get; private set; }
    public float FromMarketSpeed { get; private set; }
    public float SellPriceMultiplier { get; private set; }
    public float BuyPriceMultiplier { get; private set; }

    public MarketStationModifier(
        ModifierType type,
        string source,
        float toMarketSpeed,
        float fromMarketSpeed,
        float sellPriceMultiplier,
        float buyPriceMultiplier)
    {
        Type = type;
        Source = source;
        ToMarketSpeed = toMarketSpeed;
        FromMarketSpeed = fromMarketSpeed;
        SellPriceMultiplier = sellPriceMultiplier;
        BuyPriceMultiplier = buyPriceMultiplier;
    }

    /// <summary>
    /// Creates an additive modifier. Each value is a delta folded onto the corresponding base
    /// (e.g. ToMarketSpeed -0.1 makes the sell segment 10% of base faster). Identity = all zeros.
    /// </summary>
    public static MarketStationModifier Additive(
        string source,
        float toMarketSpeedDelta = 0f,
        float fromMarketSpeedDelta = 0f,
        float sellPriceDelta = 0f,
        float buyPriceDelta = 0f) =>
        new(ModifierType.Additive, source, toMarketSpeedDelta, fromMarketSpeedDelta, sellPriceDelta, buyPriceDelta);

    /// <summary>
    /// Creates a multiplicative modifier. Each value is a factor (1.0 = no change). Identity = all ones.
    /// </summary>
    public static MarketStationModifier Multiplicative(
        string source,
        float toMarketSpeedFactor = 1f,
        float fromMarketSpeedFactor = 1f,
        float sellPriceFactor = 1f,
        float buyPriceFactor = 1f) =>
        new(ModifierType.Multiplicative, source, toMarketSpeedFactor, fromMarketSpeedFactor, sellPriceFactor, buyPriceFactor);
}
