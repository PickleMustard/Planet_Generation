using Structures.Enums;

namespace Structures.Logistics;

/// <summary>
/// Defines an engine modifier that can be applied to modify engine performance.
/// Supports both additive (flat value) and multiplicative (percentage) modifiers.
/// </summary>
public struct EngineModifier
{
    /// <summary>
    /// The type of modifier - determines how it stacks with other modifiers.
    /// </summary>
    public ModifierType Type { get; private set; }

    /// <summary>
    /// Unique identifier for the source of this modifier (e.g., "Upgrade", "Damage").
    /// </summary>
    public string Source { get; private set; }

    /// <summary>
    /// Isp bonus (for additive) or efficiency percentage (for multiplicative).
    /// For multiplicative: values > 1.0 increase efficiency, values &lt; 1.0 decrease it.
    /// </summary>
    public float IspValue { get; private set; }

    /// <summary>
    /// Thrust bonus (for additive) or thrust percentage (for multiplicative).
    /// For multiplicative: values > 1.0 increase thrust, values &lt; 1.0 decrease it.
    /// </summary>
    public float ThrustValue { get; private set; }

    private EngineModifier(ModifierType type, string source, float ispValue, float thrustValue)
    {
        Type = type;
        Source = source;
        IspValue = ispValue;
        ThrustValue = thrustValue;
    }

    /// <summary>
    /// Creates an additive modifier with both Isp and thrust bonuses.
    /// </summary>
    /// <param name="source">Unique identifier for the modifier source.</param>
    /// <param name="ispBonus">Flat Isp bonus in seconds.</param>
    /// <param name="thrustBonus">Flat thrust bonus in Newtons.</param>
    /// <returns>A new additive EngineModifier.</returns>
    public static EngineModifier Additive(string source, float ispBonus, float thrustBonus)
    {
        return new EngineModifier(ModifierType.Additive, source, ispBonus, thrustBonus);
    }

    /// <summary>
    /// Creates an additive modifier that affects only Isp.
    /// </summary>
    /// <param name="source">Unique identifier for the modifier source.</param>
    /// <param name="ispBonus">Flat Isp bonus in seconds.</param>
    /// <returns>A new additive Isp-only EngineModifier.</returns>
    public static EngineModifier AdditiveIsp(string source, float ispBonus)
    {
        return new EngineModifier(ModifierType.Additive, source, ispBonus, 0f);
    }

    /// <summary>
    /// Creates an additive modifier that affects only thrust.
    /// </summary>
    /// <param name="source">Unique identifier for the modifier source.</param>
    /// <param name="thrustBonus">Flat thrust bonus in Newtons.</param>
    /// <returns>A new additive thrust-only EngineModifier.</returns>
    public static EngineModifier AdditiveThrust(string source, float thrustBonus)
    {
        return new EngineModifier(ModifierType.Additive, source, 0f, thrustBonus);
    }

    /// <summary>
    /// Creates a multiplicative modifier with both Isp and thrust percentages.
    /// </summary>
    /// <param name="source">Unique identifier for the modifier source.</param>
    /// <param name="efficiencyPercent">Isp multiplier (e.g., 1.1 = +10%, 0.95 = -5%).</param>
    /// <param name="thrustPercent">Thrust multiplier (e.g., 1.1 = +10%, 0.95 = -5%).</param>
    /// <returns>A new multiplicative EngineModifier.</returns>
    public static EngineModifier Multiplicative(string source, float efficiencyPercent, float thrustPercent)
    {
        return new EngineModifier(ModifierType.Multiplicative, source, efficiencyPercent, thrustPercent);
    }

    /// <summary>
    /// Creates a multiplicative modifier that affects only Isp.
    /// </summary>
    /// <param name="source">Unique identifier for the modifier source.</param>
    /// <param name="efficiencyPercent">Isp multiplier (e.g., 1.1 = +10%, 0.95 = -5%).</param>
    /// <returns>A new multiplicative Isp-only EngineModifier.</returns>
    public static EngineModifier MultiplicativeIsp(string source, float efficiencyPercent)
    {
        return new EngineModifier(ModifierType.Multiplicative, source, efficiencyPercent, 1f);
    }

    /// <summary>
    /// Creates a multiplicative modifier that affects only thrust.
    /// </summary>
    /// <param name="source">Unique identifier for the modifier source.</param>
    /// <param name="thrustPercent">Thrust multiplier (e.g., 1.1 = +10%, 0.95 = -5%).</param>
    /// <returns>A new multiplicative thrust-only EngineModifier.</returns>
    public static EngineModifier MultiplicativeThrust(string source, float thrustPercent)
    {
        return new EngineModifier(ModifierType.Multiplicative, source, 1f, thrustPercent);
    }

    /// <summary>
    /// Creates an upgrade modifier that improves Isp (convenience method).
    /// Uses additive Isp bonus.
    /// </summary>
    /// <param name="source">Unique identifier for the upgrade.</param>
    /// <param name="ispBonus">Isp improvement in seconds.</param>
    /// <returns>A new upgrade EngineModifier.</returns>
    public static EngineModifier Upgrade(string source, float ispBonus)
    {
        return AdditiveIsp(source, ispBonus);
    }

    /// <summary>
    /// Creates a damage modifier that reduces both Isp and thrust.
    /// Uses multiplicative reduction.
    /// </summary>
    /// <param name="source">Unique identifier for the damage source.</param>
    /// <param name="percentReduction">Percentage reduction (e.g., 0.9 = 10% damage = 90% effectiveness).</param>
    /// <returns>A new damage EngineModifier.</returns>
    public static EngineModifier Damage(string source, float percentReduction)
    {
        return Multiplicative(source, percentReduction, percentReduction);
    }

    /// <summary>
    /// Creates a thrust boost modifier that increases thrust output.
    /// Uses multiplicative thrust bonus.
    /// </summary>
    /// <param name="source">Unique identifier for the boost source.</param>
    /// <param name="percentIncrease">Thrust multiplier (e.g., 1.25 = +25% thrust).</param>
    /// <returns>A new thrust boost EngineModifier.</returns>
    public static EngineModifier ThrustBoost(string source, float percentIncrease)
    {
        return MultiplicativeThrust(source, percentIncrease);
    }

    /// <summary>
    /// Creates a wear modifier that degrades both Isp efficiency and thrust.
    /// Uses multiplicative reduction for both values.
    /// </summary>
    /// <param name="source">Unique identifier for the wear source.</param>
    /// <param name="efficiencyLoss">Isp multiplier (e.g., 0.98 = 2% efficiency loss).</param>
    /// <param name="thrustLoss">Thrust multiplier (e.g., 0.95 = 5% thrust loss).</param>
    /// <returns>A new wear EngineModifier.</returns>
    public static EngineModifier Wear(string source, float efficiencyLoss, float thrustLoss)
    {
        return new EngineModifier(ModifierType.Multiplicative, source, efficiencyLoss, thrustLoss);
    }
}
