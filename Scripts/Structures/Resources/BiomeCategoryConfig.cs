using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Configuration class for biome categories.
/// Maps category names (e.g., "mountain", "arable") to sets of biome types.
/// Used for building placement requirements to allow specifying groups of biomes
/// instead of individual biome types.
/// </summary>
public class BiomeCategoryConfig
{
    /// <summary>
    /// Dictionary mapping category IDs to their entries.
    /// </summary>
    public Dictionary<string, BiomeCategoryEntry> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the entire configuration.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        bool isValid = true;

        if (Categories == null || Categories.Count == 0)
        {
            GD.PrintErr("BiomeCategoryConfig: No categories defined");
            return false;
        }

        foreach (var kvp in Categories)
        {
            if (kvp.Value == null)
            {
                GD.PrintErr($"BiomeCategoryConfig: Null entry for category '{kvp.Key}'");
                isValid = false;
                continue;
            }

            if (!kvp.Value.Validate())
            {
                GD.PrintErr($"BiomeCategoryConfig: Invalid configuration for category '{kvp.Key}'");
                isValid = false;
            }
        }

        return isValid;
    }

    /// <summary>
    /// Gets all biome types for a specific category.
    /// </summary>
    /// <param name="categoryId">The category ID (case-insensitive)</param>
    /// <returns>Set of biome types in the category, or null if category not found</returns>
    public HashSet<Biome.BiomeType>? GetBiomesForCategory(string categoryId)
    {
        if (Categories.TryGetValue(categoryId, out var entry))
        {
            return entry.Biomes;
        }
        return null;
    }

    /// <summary>
    /// Checks if a category exists.
    /// </summary>
    /// <param name="categoryId">The category ID (case-insensitive)</param>
    /// <returns>True if the category exists</returns>
    public bool HasCategory(string categoryId)
    {
        return Categories.ContainsKey(categoryId);
    }

    /// <summary>
    /// Gets all categories that contain a specific biome type.
    /// </summary>
    /// <param name="biomeType">The biome type to search for</param>
    /// <returns>List of category IDs containing the biome</returns>
    public List<string> GetCategoriesForBiome(Biome.BiomeType biomeType)
    {
        var categories = new List<string>();

        foreach (var kvp in Categories)
        {
            if (kvp.Value?.ContainsBiome(biomeType) ?? false)
            {
                categories.Add(kvp.Key);
            }
        }

        return categories;
    }

    /// <summary>
    /// Resolves a list of biome entries (which can be individual biome names or category: prefixes)
    /// to a unique set of BiomeType values.
    /// </summary>
    /// <param name="entries">List of entries (e.g., "Grassland", "category:mountain")</param>
    /// <param name="wildcardPresent">Output parameter indicating if wildcard "*" was found</param>
    /// <returns>Set of resolved biome types (empty if wildcard is present)</returns>
    public HashSet<Biome.BiomeType> ResolveBiomeEntries(
        IEnumerable<string> entries,
        out bool wildcardPresent
    )
    {
        var result = new HashSet<Biome.BiomeType>();
        wildcardPresent = false;

        foreach (var entry in entries)
        {
            string trimmed = entry?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Check for wildcard
            if (trimmed == "*")
            {
                wildcardPresent = true;
                return new HashSet<Biome.BiomeType>(); // Return empty set for wildcard
            }

            // Check for category prefix
            if (trimmed.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
            {
                string categoryId = trimmed.Substring(9).Trim(); // Remove "category:" prefix

                var categoryBiomes = GetBiomesForCategory(categoryId);
                if (categoryBiomes != null)
                {
                    foreach (var biome in categoryBiomes)
                    {
                        result.Add(biome);
                    }
                }
                else
                {
                    GD.PrintErr($"BiomeCategoryConfig: Unknown category '{categoryId}'");
                }
            }
            else
            {
                // Parse as individual biome name
                if (TryParseBiomeType(trimmed, out var biomeType))
                {
                    result.Add(biomeType);
                }
                else
                {
                    GD.PrintErr($"BiomeCategoryConfig: Unknown biome type '{trimmed}'");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets all biome types defined in the BiomeType enum.
    /// Used when wildcard "*" is specified.
    /// </summary>
    /// <returns>Set of all biome types</returns>
    public static HashSet<Biome.BiomeType> GetAllBiomes()
    {
        return Enum.GetValues<Biome.BiomeType>().ToHashSet();
    }

    /// <summary>
    /// Tries to parse a biome name string to a BiomeType enum value.
    /// Handles various formats (e.g., "Grassland", "grassland", "GrassLand").
    /// </summary>
    /// <param name="name">The biome name to parse</param>
    /// <param name="biomeType">Output biome type if successful</param>
    /// <returns>True if parsing succeeded</returns>
    public static bool TryParseBiomeType(string name, out Biome.BiomeType biomeType)
    {
        biomeType = Biome.BiomeType.Tundra;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        string normalized = name.Replace("_", "").Replace(" ", "").Trim();

        foreach (Biome.BiomeType type in Enum.GetValues(typeof(Biome.BiomeType)))
        {
            string enumName = type.ToString().Replace("_", "");
            if (string.Equals(normalized, enumName, StringComparison.OrdinalIgnoreCase))
            {
                biomeType = type;
                return true;
            }
        }

        return false;
    }
}
