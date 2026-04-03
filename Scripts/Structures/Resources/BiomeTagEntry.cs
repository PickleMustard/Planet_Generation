using System.Collections.Generic;
using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Configuration for tags associated with a specific biome type.
/// Defines the tags that this biome naturally has and probability modifiers
/// for planetary type tags within this biome.
/// </summary>
public class BiomeTagEntry
{
    /// <summary>
    /// The biome type this configuration applies to.
    /// </summary>
    public Biome.BiomeType Biome { get; set; }

    /// <summary>
    /// Set of descriptive tags that this biome naturally has.
    /// Tags describe the biome's characteristics (e.g., "mountain", "rocky", "temperate").
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Probability modifiers for planetary type tags within this biome.
    /// Key: Planetary type tag (e.g., "temperate", "arid", "habitable")
    /// Value: Multiplier to apply to tag probability (e.g., 1.2 = 20% increase, 0.8 = 20% decrease)
    /// </summary>
    public Dictionary<string, float> ProbabilityModifiers { get; set; } = new();

    /// <summary>
    /// Validates the configuration for consistency and reasonable values.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        // Check that Tags collection is not null
        if (Tags == null)
        {
            GD.PrintErr($"BiomeTagEntry '{Biome}': Tags collection is null");
            return false;
        }

        // Check that ProbabilityModifiers collection is not null
        if (ProbabilityModifiers == null)
        {
            GD.PrintErr($"BiomeTagEntry '{Biome}': ProbabilityModifiers collection is null");
            return false;
        }

        // Ensure at least one tag is defined for each biome
        if (Tags.Count == 0)
        {
            GD.PrintErr($"BiomeTagEntry '{Biome}': No tags defined for biome");
            return false;
        }

        // Validate each tag
        foreach (var tag in Tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                GD.PrintErr($"BiomeTagEntry '{Biome}': Contains empty or whitespace tag");
                return false;
            }
        }

        // Validate probability modifiers
        foreach (var kvp in ProbabilityModifiers)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                GD.PrintErr($"BiomeTagEntry '{Biome}': Contains empty or whitespace tag in probability modifiers");
                return false;
            }

            // Probability modifiers should be between 0.0 and 3.0 (as suggested in requirements)
            // This allows for significant adjustments while maintaining some sanity
            if (kvp.Value < 0.0f || kvp.Value > 3.0f)
            {
                GD.PrintErr($"BiomeTagEntry '{Biome}': Probability modifier for tag '{kvp.Key}' ({kvp.Value}) is outside valid range [0.0, 3.0]");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if this biome has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for</param>
    /// <returns>True if the tag is present, false otherwise</returns>
    public bool HasTag(string tag)
    {
        return Tags?.Contains(tag) ?? false;
    }

    /// <summary>
    /// Gets the probability modifier for a specific planetary type tag.
    /// If no modifier is defined for the tag, returns 1.0 (no effect).
    /// </summary>
    /// <param name="tag">The planetary type tag to get the modifier for</param>
    /// <returns>The probability modifier, or 1.0 if not defined</returns>
    public float GetProbabilityModifier(string tag)
    {
        if (ProbabilityModifiers?.TryGetValue(tag, out float modifier) ?? false)
        {
            return Mathf.Clamp(modifier, 0.0f, 3.0f);
        }
        return 1.0f;
    }
}