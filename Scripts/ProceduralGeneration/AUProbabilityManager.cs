using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using ProceduralGeneration.ColorSystem;
using ProceduralGeneration.Data;
using Structures;
using Structures.Enums;
using UtilityLibrary.DataLoading;

namespace ProceduralGeneration;

public class AUProbabilityManager
{
    private readonly RandomNumberGenerator _rng;

    public AUProbabilityManager(RandomNumberGenerator rng)
    {
        _rng = rng;
    }

    /// <summary>
    /// Phase 7: roll a subtype id from a SystemTemplate <c>subtype_weights</c> map.
    /// Caller already has the family-typed weights; returns the chosen id, or
    /// <paramref name="fallback"/> when the map is empty / all-zero.
    /// </summary>
    public string SelectFromWeights(IReadOnlyDictionary<string, float> weights, string fallback = "")
    {
        if (weights == null || weights.Count == 0) return fallback;

        float total = 0f;
        foreach (var kvp in weights)
        {
            if (kvp.Value > 0) total += kvp.Value;
        }
        if (total <= 0f) return fallback;

        float roll = _rng.Randf() * total;
        float cumulative = 0f;
        string? last = null;
        foreach (var kvp in weights)
        {
            if (kvp.Value <= 0) continue;
            cumulative += kvp.Value;
            last = kvp.Key;
            if (roll <= cumulative) return kvp.Key;
        }
        return last ?? fallback;
    }

    /// <summary>
    /// Selects a <see cref="BodyClassification"/> for any <see cref="OrbitalBodyType"/> from one 1D
    /// AU range table keyed on <paramref name="effectiveAU"/> (the body's cumulative distance from
    /// the system center). When the body is a satellite, <paramref name="immediateParentSubtypeId"/>
    /// (its parent's subtype id) selects an optional per-parent-subtype weight modifier set that
    /// scales the matched range's base weights.
    /// </summary>
    public BodyClassification SelectClassification(
        OrbitalBodyType bodyType,
        float effectiveAU,
        string? immediateParentSubtypeId = null,
        BodyClassification? manualOverride = null
    )
    {
        if (manualOverride != null)
        {
            return manualOverride;
        }

        var config = AUProbabilityLoader.LoadForType(bodyType);
        if (config.AURanges.Count == 0)
        {
            GD.PrintErr($"No subtype found for {bodyType}");
            var defaultSubtype = config.DefaultSubtype ?? AUProbabilityLoader.GetDefaultSubtype(bodyType);
            return BodyClassification.FromType(bodyType, defaultSubtype);
        }

        var range = FindMatchingRange(config, effectiveAU);
        if (range == null || range.SubtypeDistribution.Count == 0)
        {
            var defaultSubtype = config.DefaultSubtype ?? AUProbabilityLoader.GetDefaultSubtype(bodyType);
            return BodyClassification.FromType(bodyType, defaultSubtype);
        }

        IReadOnlyDictionary<string, float>? modifiers = null;
        if (!string.IsNullOrEmpty(immediateParentSubtypeId))
        {
            config.ParentSubtypeModifiers.TryGetValue(immediateParentSubtypeId, out var m);
            modifiers = m;
        }

        var subtype = WeightedRandomSelection(range.SubtypeDistribution, modifiers);
        return BodyClassification.FromType(bodyType, subtype);
    }

    public object? SelectBeltSubtype(
        SatelliteGroupTypes beltType,
        float distanceFromStarAU,
        object? manualOverride = null
    )
    {
        if (manualOverride != null)
        {
            return manualOverride;
        }

        var config = AUProbabilityLoader.LoadBeltConfig();
        return SelectSubtypeFromConfig(config, distanceFromStarAU);
    }

    private object SelectSubtypeFromConfig(AUProbabilityConfig config, float distanceAU)
    {
        var range = FindMatchingRange(config, distanceAU);

        if (range == null || range.SubtypeDistribution.Count == 0)
        {
            return config.DefaultSubtype ?? AUProbabilityLoader.GetDefaultSubtype(config.BodyType);
        }

        return WeightedRandomSelection(range.SubtypeDistribution);
    }

    private static AUProbabilityRange? FindMatchingRange(
        AUProbabilityConfig config,
        float distanceAU
    ) => FindMatchingRange(config.AURanges, distanceAU);

    private static AUProbabilityRange? FindMatchingRange(
        Array<AUProbabilityRange> ranges,
        float distanceAU
    )
    {
        foreach (var range in ranges)
        {
            bool inRange =
                distanceAU >= range.MinAU
                && (distanceAU < range.MaxAU || (distanceAU == range.MaxAU && !range.ExclusiveMax));

            if (inRange)
            {
                return range;
            }
        }
        return null;
    }

    private object WeightedRandomSelection(Array<SubtypeProbability> probabilities) =>
        WeightedRandomSelection(probabilities, null);

    /// <summary>
    /// Weighted random pick where each candidate's weight is scaled by an optional multiplier keyed
    /// on the candidate's stable subtype id string (used for parent-subtype bias, e.g. an ice giant
    /// parent favoring volcanic moons). Unlisted ids default to a 1.0 multiplier.
    /// </summary>
    private object WeightedRandomSelection(
        Array<SubtypeProbability> probabilities,
        IReadOnlyDictionary<string, float>? modifiers
    )
    {
        float EffectiveWeight(SubtypeProbability p)
        {
            float multiplier = 1.0f;
            if (modifiers != null)
            {
                string? id = BiomeIdMapper.SubtypeObjectToId(p.Subtype);
                if (id != null && modifiers.TryGetValue(id, out var m))
                {
                    multiplier = m;
                }
            }
            return p.Weight * multiplier;
        }

        float total = probabilities.Sum(EffectiveWeight);
        float random = _rng.Randf() * total;

        float cumulative = 0;
        foreach (var prob in probabilities)
        {
            cumulative += EffectiveWeight(prob);
            if (random <= cumulative)
            {
                return prob.Subtype;
            }
        }

        return probabilities.Last().Subtype;
    }
}
