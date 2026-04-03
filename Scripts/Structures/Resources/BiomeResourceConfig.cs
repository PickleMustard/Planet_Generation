using System;
using System.Collections.Generic;
using Structures.Enums;
using UtilityLibrary;
using YamlDotNet.Serialization;

namespace Structures.Resources;

/// <summary>
/// Main configuration class for biome resource weight modifiers.
/// Contains a dictionary mapping biome types to their resource weight entries.
/// </summary>
public class BiomeResourceConfig
{
    /// <summary>
    /// YAML deserialization target (list-of-objects format).
    /// </summary>
    [YamlMember(Alias = "biomes")]
    public List<BiomeResourceEntry> BiomesRaw { get; set; } = new();

    /// <summary>
    /// Runtime dictionary built from BiomesRaw after deserialization.
    /// </summary>
    [YamlIgnore]
    public Dictionary<Biome.BiomeType, BiomeResourceEntry> Biomes { get; set; } = new();

    /// <summary>
    /// Converts the deserialized BiomesRaw list into the Biomes dictionary.
    /// Must be called after YAML deserialization and before Validate().
    /// </summary>
    /// <returns>True if all entries were successfully converted</returns>
    public bool BuildDictionary()
    {
        Biomes.Clear();

        if (BiomesRaw == null || BiomesRaw.Count == 0)
        {
            GameLogger.Error("BiomeResourceConfig: Empty or null BiomesRaw list");
            return false;
        }

        bool allParsed = true;
        foreach (var entry in BiomesRaw)
        {
            var biomeType = entry.Biome;

            if (Biomes.ContainsKey(biomeType))
            {
                GameLogger.Error($"BiomeResourceConfig: Duplicate biome entry '{biomeType}'");
                allParsed = false;
                continue;
            }

            Biomes[biomeType] = entry;
        }

        if (allParsed)
        {
            GameLogger.Info($"BiomeResourceConfig: Built dictionary with {Biomes.Count} biomes");
        }

        return allParsed;
    }

    /// <summary>
    /// Validates the entire configuration for consistency.
    /// Checks that all 19 BiomeType enum values are represented and configurations are valid.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        bool isValid = true;

        // Get all biome type enum values
        var allBiomeTypes = Enum.GetValues<Biome.BiomeType>();

        // Check that all biome types are represented
        foreach (var biomeType in allBiomeTypes)
        {
            if (!Biomes.ContainsKey(biomeType))
            {
                GameLogger.Error($"BiomeResourceConfig: Missing configuration for biome type {biomeType}");
                isValid = false;
            }
            else
            {
                var entry = Biomes[biomeType];
                if (entry == null)
                {
                    GameLogger.Error($"BiomeResourceConfig: Null configuration for biome type {biomeType}");
                    isValid = false;
                }
                else if (!entry.Validate())
                {
                    GameLogger.Error($"BiomeResourceConfig: Invalid configuration for biome type {biomeType}");
                    isValid = false;
                }
            }
        }

        if (isValid)
        {
            GameLogger.Info("BiomeResourceConfig: Configuration validation passed");
        }
        else
        {
            GameLogger.Error("BiomeResourceConfig: Configuration validation failed");
        }

        return isValid;
    }

    /// <summary>
    /// Gets the resource weight entry for a specific biome type.
    /// </summary>
    /// <param name="biomeType">The biome type to get configuration for</param>
    /// <returns>The biome resource entry, or null if not found</returns>
    public BiomeResourceEntry? GetBiomeConfig(Biome.BiomeType biomeType)
    {
        return Biomes.TryGetValue(biomeType, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the weight modifier for a resource ID within a specific biome.
    /// Convenience method that delegates to GetBiomeConfig.
    /// </summary>
    /// <param name="biomeType">The biome type</param>
    /// <param name="resourceId">The resource ID</param>
    /// <returns>The weight modifier, or 1.0 if biome not found or resource not specified</returns>
    public float GetWeightModifier(Biome.BiomeType biomeType, string resourceId)
    {
        return GetBiomeConfig(biomeType)?.GetWeightModifier(resourceId) ?? 1.0f;
    }
}
