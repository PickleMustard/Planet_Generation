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
