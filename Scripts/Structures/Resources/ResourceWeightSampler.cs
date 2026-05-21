using System.Collections.Generic;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Pure deterministic sampler: given a body subtype and a biome, returns the per-resource
/// weight produced by combining SubtypeResourceConfig.baseResourceWeight with
/// BiomeResourceConfig per-resource modifiers. Used by both the runtime continent generator
/// preview and the developer-tools Resource Heatmap tab so the editor cannot diverge from
/// runtime behaviour.
/// </summary>
public static class ResourceWeightSampler
{
    public static Dictionary<string, float> ForBiome(
        SubtypeResourceConfig? subtypeConfig,
        BiomeResourceEntry? biomeEntry)
    {
        var weights = new Dictionary<string, float>();
        if (subtypeConfig == null) return weights;

        float baseWeight = subtypeConfig.GetResourceWeight();
        foreach (var resourceId in subtypeConfig.ResolvedResources)
        {
            float biomeMod = biomeEntry?.GetWeightModifier(resourceId) ?? 1.0f;
            weights[resourceId] = baseWeight * biomeMod;
        }
        return weights;
    }
}
