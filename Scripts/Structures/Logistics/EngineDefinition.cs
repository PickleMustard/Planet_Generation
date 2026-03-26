using System;
using System.Collections.Generic;
using System.Linq;
using Structures.Enums;
using UtilityLibrary.GameMath.Orbital;

namespace Structures.Logistics;

/// <summary>
/// Defines engine performance characteristics with support for modifiers.
/// Implements the stacking formula: Final = (Base + ΣAdditive) × ΠMultiplicative
/// </summary>
public class EngineDefinition
{
    /// <summary>
    /// Base specific impulse (Isp) in seconds without any modifiers.
    /// </summary>
    public float BaseSpecificImpulse { get; private set; }

    /// <summary>
    /// Base thrust output in Newtons without any modifiers.
    /// </summary>
    public float BaseThrust { get; private set; }

    /// <summary>
    /// Effective specific impulse after applying all modifiers.
    /// Calculated as: (BaseSpecificImpulse + ΣAdditiveIsp) × ΠMultiplicativeIsp
    /// </summary>
    public float EffectiveSpecificImpulse { get; private set; }

    /// <summary>
    /// Effective thrust output after applying all modifiers.
    /// Calculated as: (BaseThrust + ΣAdditiveThrust) × ΠMultiplicativeThrust
    /// </summary>
    public float EffectiveThrust { get; private set; }

    /// <summary>
    /// Exhaust velocity in m/s calculated from effective Isp.
    /// Calculated as: EffectiveSpecificImpulse × g₀
    /// </summary>
    public float ExhaustVelocity => EffectiveSpecificImpulse * OrbitalMath.STANDARD_GRAVITY;

    /// <summary>
    /// Dictionary of currently active modifiers, keyed by source identifier.
    /// </summary>
    private readonly Dictionary<string, EngineModifier> _activeModifiers = new();

    /// <summary>
    /// History of all applied modifiers for audit and debugging.
    /// </summary>
    private readonly List<AppliedModifier> _modifierHistory = new();

    /// <summary>
    /// Creates a new EngineDefinition with the specified base values.
    /// </summary>
    /// <param name="baseSpecificImpulse">Base specific impulse in seconds.</param>
    /// <param name="baseThrust">Base thrust in Newtons.</param>
    public EngineDefinition(float baseSpecificImpulse, float baseThrust)
    {
        BaseSpecificImpulse = baseSpecificImpulse;
        BaseThrust = baseThrust;
        EffectiveSpecificImpulse = baseSpecificImpulse;
        EffectiveThrust = baseThrust;
    }

    /// <summary>
    /// Applies a modifier to the engine definition.
    /// </summary>
    /// <param name="modifier">The engine modifier to apply.</param>
    /// <returns>True if the modifier was applied successfully, false if a modifier with the same source already exists.</returns>
    public bool ApplyModifier(EngineModifier modifier)
    {
        if (_activeModifiers.ContainsKey(modifier.Source))
        {
            return false;
        }

        // Store the values before modification for history tracking
        float ispBefore = EffectiveSpecificImpulse;
        float thrustBefore = EffectiveThrust;

        // Add the modifier
        _activeModifiers[modifier.Source] = modifier;

        // Recalculate effective values
        RecalculateEffectiveValues();

        // Record the change in history
        _modifierHistory.Add(
            new AppliedModifier(modifier.Source, modifier.Type, ispBefore, EffectiveSpecificImpulse)
        );

        _modifierHistory.Add(
            new AppliedModifier(modifier.Source, modifier.Type, thrustBefore, EffectiveThrust)
        );

        return true;
    }

    /// <summary>
    /// Removes a modifier from the engine definition by its source identifier.
    /// </summary>
    /// <param name="sourceId">The source identifier of the modifier to remove.</param>
    /// <returns>True if the modifier was removed, false if no modifier with that source existed.</returns>
    public bool RemoveModifier(string sourceId)
    {
        if (!_activeModifiers.ContainsKey(sourceId))
        {
            return false;
        }

        // Store the values before modification for history tracking
        float ispBefore = EffectiveSpecificImpulse;
        float thrustBefore = EffectiveThrust;

        // Remove the modifier
        _activeModifiers.Remove(sourceId);

        // Recalculate effective values
        RecalculateEffectiveValues();

        // Record the removal in history
        var removedModifier = _activeModifiers.Values.FirstOrDefault(m => m.Source == sourceId);
        _modifierHistory.Add(
            new AppliedModifier(sourceId, removedModifier.Type, ispBefore, EffectiveSpecificImpulse)
        );

        _modifierHistory.Add(
            new AppliedModifier(sourceId, removedModifier.Type, thrustBefore, EffectiveThrust)
        );

        return true;
    }

    /// <summary>
    /// Recalculates the effective values based on all active modifiers.
    /// Uses formula: Final = (Base + ΣAdditive) × ΠMultiplicative
    /// </summary>
    private void RecalculateEffectiveValues()
    {
        float additiveIsp = 0f;
        float additiveThrust = 0f;
        float multiplicativeIsp = 1f;
        float multiplicativeThrust = 1f;

        foreach (var modifier in _activeModifiers.Values)
        {
            if (modifier.Type == ModifierType.Additive)
            {
                additiveIsp += modifier.IspValue;
                additiveThrust += modifier.ThrustValue;
            }
            else if (modifier.Type == ModifierType.Multiplicative)
            {
                multiplicativeIsp *= modifier.IspValue;
                multiplicativeThrust *= modifier.ThrustValue;
            }
        }

        EffectiveSpecificImpulse = (BaseSpecificImpulse + additiveIsp) * multiplicativeIsp;
        EffectiveThrust = (BaseThrust + additiveThrust) * multiplicativeThrust;

        // Ensure values don't go negative
        EffectiveSpecificImpulse = Math.Max(0f, EffectiveSpecificImpulse);
        EffectiveThrust = Math.Max(0f, EffectiveThrust);
    }

    /// <summary>
    /// Gets a detailed breakdown of how the effective values are calculated.
    /// </summary>
    /// <returns>A dictionary containing detailed breakdown information.</returns>
    public Dictionary<string, object> GetDetailedBreakdown()
    {
        float additiveIsp = 0f;
        float additiveThrust = 0f;
        float multiplicativeIsp = 1f;
        float multiplicativeThrust = 1f;

        var additiveModifiers = new List<EngineModifier>();
        var multiplicativeModifiers = new List<EngineModifier>();

        foreach (var modifier in _activeModifiers.Values)
        {
            if (modifier.Type == ModifierType.Additive)
            {
                additiveIsp += modifier.IspValue;
                additiveThrust += modifier.ThrustValue;
                additiveModifiers.Add(modifier);
            }
            else if (modifier.Type == ModifierType.Multiplicative)
            {
                multiplicativeIsp *= modifier.IspValue;
                multiplicativeThrust *= modifier.ThrustValue;
                multiplicativeModifiers.Add(modifier);
            }
        }

        return new Dictionary<string, object>
        {
            ["BaseSpecificImpulse"] = BaseSpecificImpulse,
            ["BaseThrust"] = BaseThrust,
            ["AdditiveIsp"] = additiveIsp,
            ["AdditiveThrust"] = additiveThrust,
            ["MultiplicativeIsp"] = multiplicativeIsp,
            ["MultiplicativeThrust"] = multiplicativeThrust,
            ["EffectiveSpecificImpulse"] = EffectiveSpecificImpulse,
            ["EffectiveThrust"] = EffectiveThrust,
            ["ExhaustVelocity"] = ExhaustVelocity,
            ["AdditiveModifiers"] = additiveModifiers,
            ["MultiplicativeModifiers"] = multiplicativeModifiers,
            ["TotalModifierCount"] = _activeModifiers.Count,
            ["CalculationIsp"] =
                $"({BaseSpecificImpulse} + {additiveIsp}) × {multiplicativeIsp} = {EffectiveSpecificImpulse}",
            ["CalculationThrust"] =
                $"({BaseThrust} + {additiveThrust}) × {multiplicativeThrust} = {EffectiveThrust}",
        };
    }

    /// <summary>
    /// Gets the complete history of all applied and removed modifiers.
    /// </summary>
    /// <returns>A list of AppliedModifier records in chronological order.</returns>
    public List<AppliedModifier> GetModifierHistory()
    {
        return new List<AppliedModifier>(_modifierHistory);
    }

    /// <summary>
    /// Gets the count of currently active modifiers.
    /// </summary>
    public int ActiveModifierCount => _activeModifiers.Count;

    /// <summary>
    /// Checks if a modifier with the specified source exists.
    /// </summary>
    /// <param name="sourceId">The source identifier to check.</param>
    /// <returns>True if a modifier with that source exists.</returns>
    public bool HasModifier(string sourceId)
    {
        return _activeModifiers.ContainsKey(sourceId);
    }

    /// <summary>
    /// Clears all modifiers and resets to base values.
    /// </summary>
    public void ClearAllModifiers()
    {
        _activeModifiers.Clear();
        EffectiveSpecificImpulse = BaseSpecificImpulse;
        EffectiveThrust = BaseThrust;
    }
}
