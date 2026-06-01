using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ProceduralGeneration.BiomeSystem;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Configuration class for biome categories.
/// Maps category names (e.g., "mountain", "arable") to sets of biome IDs.
/// Used for building placement requirements to allow specifying groups of biomes
/// instead of individual biome IDs.
/// </summary>
public class BiomeCategoryConfig
{
    public Dictionary<string, BiomeCategoryEntry> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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

    public HashSet<string>? GetBiomesForCategory(string categoryId)
    {
        if (Categories.TryGetValue(categoryId, out var entry))
            return entry.Biomes;
        return null;
    }

    public bool HasCategory(string categoryId) => Categories.ContainsKey(categoryId);

    public List<string> GetCategoriesForBiome(string biomeId)
    {
        var categories = new List<string>();
        foreach (var kvp in Categories)
            if (kvp.Value?.ContainsBiome(biomeId) ?? false)
                categories.Add(kvp.Key);
        return categories;
    }

    /// <summary>
    /// Resolves a list of biome entries (individual biome names/IDs or category:prefixes)
    /// to a set of stable biome IDs.
    /// </summary>
    public HashSet<string> ResolveBiomeEntries(
        IEnumerable<string> entries,
        out bool wildcardPresent)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        wildcardPresent = false;

        foreach (var entry in entries)
        {
            string trimmed = entry?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed == "*")
            {
                wildcardPresent = true;
                return new HashSet<string>();
            }

            if (trimmed.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
            {
                string categoryId = trimmed.Substring(9).Trim();
                var categoryBiomes = GetBiomesForCategory(categoryId);
                if (categoryBiomes != null)
                    foreach (var biomeId in categoryBiomes)
                        result.Add(biomeId);
                else
                    GD.PrintErr($"BiomeCategoryConfig: Unknown category '{categoryId}'");
            }
            else if (TryNormalizeBiomeId(trimmed, out var biomeId))
            {
                result.Add(biomeId);
            }
            else
            {
                GD.PrintErr($"BiomeCategoryConfig: Unknown biome '{trimmed}'");
            }
        }

        return result;
    }

    /// <summary>
    /// Returns every registered biome ID (from <see cref="BiomeDatabase"/> if loaded;
    /// otherwise enumerates the legacy enum). Used for wildcard "*" expansion.
    /// </summary>
    public static HashSet<string> GetAllBiomes()
    {
        if (BiomeDatabase.Instance.IsLoaded)
            return BiomeDatabase.Instance.All.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        return Enum.GetValues<Biome.BiomeType>()
            .Select(BiomeIdMapper.BiomeTypeToId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Accepts either a stable ID (<c>biome_grassland</c>) or a legacy enum name
    /// (<c>Grassland</c>, <c>grass land</c>) and returns the stable ID.
    /// </summary>
    public static bool TryNormalizeBiomeId(string name, out string biomeId)
    {
        biomeId = string.Empty;
        if (string.IsNullOrWhiteSpace(name)) return false;

        string trimmed = name.Trim();
        if (trimmed.StartsWith("biome_"))
        {
            biomeId = trimmed;
            return true;
        }

        string normalized = trimmed.Replace("_", "").Replace(" ", "");
        foreach (var type in Enum.GetValues<Biome.BiomeType>())
        {
            if (string.Equals(normalized, type.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                biomeId = BiomeIdMapper.BiomeTypeToId(type);
                return true;
            }
        }

        return false;
    }
}
