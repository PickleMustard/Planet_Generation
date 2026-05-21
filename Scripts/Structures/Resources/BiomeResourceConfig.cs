using System;
using System.Collections.Generic;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;
using UtilityLibrary;
using YamlDotNet.Serialization;

namespace Structures.Resources;

/// <summary>
/// Main configuration class for biome resource weight modifiers.
/// Keyed by stable biome ID (e.g. <c>biome_mountain</c>). YAML still allows enum names
/// for backwards compatibility — the loader normalizes them to IDs.
/// </summary>
public class BiomeResourceConfig
{
    /// <summary>
    /// YAML deserialization target (list-of-objects format).
    /// </summary>
    [YamlMember(Alias = "biomes")]
    public List<BiomeResourceEntry> BiomesRaw { get; set; } = new();

    /// <summary>
    /// Runtime dictionary built from BiomesRaw after deserialization. Keys are biome IDs.
    /// </summary>
    [YamlIgnore]
    public Dictionary<string, BiomeResourceEntry> Biomes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Converts the deserialized BiomesRaw list into the Biomes dictionary.
    /// Must be called after YAML deserialization and before Validate().
    /// </summary>
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
            string biomeId = BiomeIdMapper.BiomeTypeToId(entry.Biome);

            if (Biomes.ContainsKey(biomeId))
            {
                GameLogger.Error($"BiomeResourceConfig: Duplicate biome entry '{biomeId}'");
                allParsed = false;
                continue;
            }

            Biomes[biomeId] = entry;
        }

        if (allParsed)
        {
            GameLogger.Info($"BiomeResourceConfig: Built dictionary with {Biomes.Count} biomes");
        }

        return allParsed;
    }

    /// <summary>
    /// Validates that every <see cref="Biome.BiomeType"/> enum value has an entry.
    /// </summary>
    public bool Validate()
    {
        bool isValid = true;

        foreach (Biome.BiomeType biomeType in Enum.GetValues<Biome.BiomeType>())
        {
            string id = BiomeIdMapper.BiomeTypeToId(biomeType);
            if (!Biomes.ContainsKey(id))
            {
                GameLogger.Error($"BiomeResourceConfig: Missing configuration for biome '{id}'");
                isValid = false;
                continue;
            }

            var entry = Biomes[id];
            if (entry == null)
            {
                GameLogger.Error($"BiomeResourceConfig: Null configuration for biome '{id}'");
                isValid = false;
            }
            else if (!entry.Validate())
            {
                GameLogger.Error($"BiomeResourceConfig: Invalid configuration for biome '{id}'");
                isValid = false;
            }
        }

        if (isValid)
            GameLogger.Info("BiomeResourceConfig: Configuration validation passed");
        else
            GameLogger.Error("BiomeResourceConfig: Configuration validation failed");

        return isValid;
    }

    public BiomeResourceEntry? GetBiomeConfig(string biomeId) =>
        Biomes.TryGetValue(biomeId, out var c) ? c : null;

    public BiomeResourceEntry? GetBiomeConfig(Biome.BiomeType biomeType) =>
        GetBiomeConfig(BiomeIdMapper.BiomeTypeToId(biomeType));

    public float GetWeightModifier(string biomeId, string resourceId) =>
        GetBiomeConfig(biomeId)?.GetWeightModifier(resourceId) ?? 1.0f;

    public float GetWeightModifier(Biome.BiomeType biomeType, string resourceId) =>
        GetWeightModifier(BiomeIdMapper.BiomeTypeToId(biomeType), resourceId);
}
