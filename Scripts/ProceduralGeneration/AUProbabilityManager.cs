using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
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
    /// Selects a BodyClassification for a CelestialBodyType based on AU distance.
    /// </summary>
    public BodyClassification SelectClassification(
        CelestialBodyType bodyType,
        float distanceAU,
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
            return BodyClassification.FromLegacy(bodyType, defaultSubtype);
        }

        var subtype = SelectSubtypeFromConfig(config, distanceAU);
        return BodyClassification.FromLegacy(bodyType, subtype);
    }

    public object? SelectSatelliteSubtype(
        SatelliteBodyType satType,
        CelestialBodyType parentType,
        float distanceFromParentAU,
        object? manualOverride = null
    )
    {
        if (manualOverride != null)
        {
            return manualOverride;
        }

        var config = AUProbabilityLoader.LoadSatelliteConfig();

        if (config.ParentBodyInfluence.TryGetValue(parentType, out var parentConfig))
        {
            return SelectSubtypeFromConfig(parentConfig, distanceFromParentAU);
        }

        if (config.DefaultConfig != null)
        {
            return SelectSubtypeFromConfig(config.DefaultConfig, distanceFromParentAU);
        }

        return SatelliteSubtype.RockyMoon;
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
    )
    {
        foreach (var range in config.AURanges)
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

    private object WeightedRandomSelection(Array<SubtypeProbability> probabilities)
    {
        float total = probabilities.Sum(p => p.Weight);
        float random = _rng.Randf() * total;

        float cumulative = 0;
        foreach (var prob in probabilities)
        {
            cumulative += prob.Weight;
            if (random <= cumulative)
            {
                return prob.Subtype;
            }
        }

        return probabilities.Last().Subtype;
    }
}
