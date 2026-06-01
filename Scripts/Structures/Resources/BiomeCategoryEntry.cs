using System;
using System.Collections.Generic;

namespace Structures.Resources;

/// <summary>
/// Defines a biome category containing multiple biomes (referenced by stable ID).
/// Biome categories allow buildings to specify placement requirements using high-level
/// category names instead of individual biome IDs.
/// </summary>
public class BiomeCategoryEntry
{
    /// <summary>
    /// The unique identifier for this category (e.g., "mountain", "arable", "ocean").
    /// Used in YAML configuration with the prefix "category:".
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The set of biome IDs that belong to this category.
    /// </summary>
    public HashSet<string> Biomes { get; set; } = new(StringComparer.Ordinal);

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(CategoryId))
            return false;
        if (Biomes == null || Biomes.Count == 0)
            return false;
        return true;
    }

    public bool ContainsBiome(string biomeId) =>
        Biomes?.Contains(biomeId) ?? false;
}
