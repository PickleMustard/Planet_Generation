using System.Collections.Generic;
using Godot;
using Structures.Enums;
using UtilityLibrary;

namespace Structures.Resources;

/// <summary>
/// Configuration for resource weight modifiers for a specific biome type.
/// Defines per-resource-ID weight modifiers rather than tag-based modifiers.
/// Biomes only modify probability — they cannot add or remove resources.
/// </summary>
public class BiomeResourceEntry
{
    /// <summary>
    /// The biome type this configuration applies to.
    /// </summary>
    public Biome.BiomeType Biome { get; set; }

    /// <summary>
    /// Key: resource ID (e.g., "iron_ore"), Value: weight modifier [0.0, 3.0]
    /// Unmentioned resources default to 1.0 (no modification).
    /// </summary>
    public Dictionary<string, float> ResourceWeightModifiers { get; set; } = new();

    /// <summary>
    /// Gets the weight modifier for a given resource ID.
    /// Returns the modifier if specified, or 1.0f if not found.
    /// Result is clamped to [0.0, 3.0].
    /// </summary>
    /// <param name="resourceId">The resource ID to get the modifier for</param>
    /// <returns>The weight modifier, or 1.0 if not specified</returns>
    public float GetWeightModifier(string resourceId)
    {
        if (ResourceWeightModifiers != null && ResourceWeightModifiers.TryGetValue(resourceId, out float modifier))
        {
            return Mathf.Clamp(modifier, 0.0f, 3.0f);
        }
        return 1.0f;
    }

    /// <summary>
    /// Validates the configuration for consistency and reasonable values.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        // Check that ResourceWeightModifiers collection is not null
        if (ResourceWeightModifiers == null)
        {
            GameLogger.Error($"BiomeResourceEntry '{Biome}': ResourceWeightModifiers collection is null");
            return false;
        }

        // Validate each modifier value is in range [0.0, 3.0]
        foreach (var kvp in ResourceWeightModifiers)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                GameLogger.Error($"BiomeResourceEntry '{Biome}': Contains empty or whitespace resource ID");
                return false;
            }

            if (kvp.Value < 0.0f || kvp.Value > 3.0f)
            {
                GameLogger.Error($"BiomeResourceEntry '{Biome}': Weight modifier for resource '{kvp.Key}' ({kvp.Value}) is outside valid range [0.0, 3.0]");
                return false;
            }
        }

        return true;
    }
}