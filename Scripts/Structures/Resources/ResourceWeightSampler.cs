using System.Collections.Generic;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Pure deterministic sampler: given a body subtype and a biome, returns a per-resource
/// preview weight produced by combining SubtypeResourceConfig.baseResourceWeight with the
/// biome's per-group <see cref="AvailabilityLevel"/>. Resources whose group is not offered
/// by the biome are omitted. Used by developer-tools previews so the editor cannot diverge
/// from runtime eligibility.
/// </summary>
public static class ResourceWeightSampler
{
    public static Dictionary<string, float> ForBiome(
        SubtypeResourceConfig? subtypeConfig,
        BiomeResourceEntry? biomeEntry,
        IReadOnlyDictionary<string, List<string>>? groupDefinitions)
    {
        var weights = new Dictionary<string, float>();
        if (subtypeConfig == null) return weights;

        float baseWeight = subtypeConfig.GetResourceWeight();
        foreach (var resourceId in subtypeConfig.ResolvedResources)
        {
            AvailabilityLevel? best = null;
            if (biomeEntry != null && groupDefinitions != null)
            {
                foreach (var group in subtypeConfig.ResourceGroups)
                {
                    if (!groupDefinitions.TryGetValue(group, out var members) || !members.Contains(resourceId))
                        continue;
                    var level = biomeEntry.GetGroupAvailability(group);
                    if (level != null && (best == null || LevelWeight(level.Value) > LevelWeight(best.Value)))
                        best = level;
                }
            }

            if (best == null) continue; // group not offered by this biome
            weights[resourceId] = baseWeight * LevelWeight(best.Value);
        }
        return weights;
    }

    /// <summary>Representative abundance weight for a level (parallels CellResourceGenerator's table).</summary>
    private static float LevelWeight(AvailabilityLevel level) => level switch
    {
        AvailabilityLevel.Abundant => 1.00f,
        AvailabilityLevel.Frequent => 0.85f,
        AvailabilityLevel.Normal => 0.70f,
        AvailabilityLevel.Scarce => 0.55f,
        AvailabilityLevel.Rare => 0.40f,
        _ => 0f,
    };
}
