using System.Collections.Generic;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Defines a biome category containing multiple biome types.
/// Biome categories allow buildings to specify placement requirements
/// using high-level category names instead of individual biome types.
/// </summary>
public class BiomeCategoryEntry
{
    /// <summary>
    /// The unique identifier for this category (e.g., "mountain", "arable", "ocean").
    /// This is used in YAML configuration with the prefix "category:".
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the category.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what biomes are included in this category.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The set of biome types that belong to this category.
    /// </summary>
    public HashSet<Biome.BiomeType> Biomes { get; set; } = new();

    /// <summary>
    /// Validates the category entry.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(CategoryId))
            return false;

        if (Biomes == null || Biomes.Count == 0)
            return false;

        return true;
    }

    /// <summary>
    /// Checks if a specific biome type is in this category.
    /// </summary>
    /// <param name="biomeType">The biome type to check</param>
    /// <returns>True if the biome is in this category</returns>
    public bool ContainsBiome(Biome.BiomeType biomeType)
    {
        return Biomes?.Contains(biomeType) ?? false;
    }
}
