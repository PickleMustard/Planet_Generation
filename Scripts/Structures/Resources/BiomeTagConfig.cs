using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Main configuration class for biome tag probabilities.
/// Contains a dictionary mapping biome types to their tag configurations.
/// </summary>
public class BiomeTagConfig
{
    /// <summary>
    /// Tag configurations for each biome type.
    /// </summary>
    public Dictionary<Biome.BiomeType, BiomeTagEntry> Biomes { get; set; } = new();

    /// <summary>
    /// Validates the entire configuration for consistency.
    /// Checks that all Biome.BiomeType enum values are represented and configurations are valid.
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
                GD.PrintErr($"BiomeTagConfig: Missing configuration for biome type {biomeType}");
                isValid = false;
            }
            else
            {
                var entry = Biomes[biomeType];
                if (entry == null)
                {
                    GD.PrintErr($"BiomeTagConfig: Null configuration for biome type {biomeType}");
                    isValid = false;
                }
                else if (!entry.Validate())
                {
                    GD.PrintErr($"BiomeTagConfig: Invalid configuration for biome type {biomeType}");
                    isValid = false;
                }
            }
        }

        if (isValid)
        {
            GD.Print("BiomeTagConfig: Configuration validation passed");
        }
        else
        {
            GD.PrintErr("BiomeTagConfig: Configuration validation failed");
        }

        return isValid;
    }

    /// <summary>
    /// Gets the tag configuration for a specific biome type.
    /// </summary>
    /// <param name="biomeType">The biome type to get configuration for</param>
    /// <returns>The biome tag entry, or null if not found</returns>
    public BiomeTagEntry? GetBiomeConfig(Biome.BiomeType biomeType)
    {
        return Biomes.TryGetValue(biomeType, out var config) ? config : null;
    }

    /// <summary>
    /// Gets all tags for a specific biome type.
    /// </summary>
    /// <param name="biomeType">The biome type to get tags for</param>
    /// <returns>Set of tags for the biome, or null if biome not found</returns>
    public HashSet<string>? GetBiomeTags(Biome.BiomeType biomeType)
    {
        return GetBiomeConfig(biomeType)?.Tags;
    }

    /// <summary>
    /// Checks if a specific biome type has a particular tag.
    /// </summary>
    /// <param name="biomeType">The biome type to check</param>
    /// <param name="tag">The tag to check for</param>
    /// <returns>True if the biome has the tag, false otherwise</returns>
    public bool BiomeHasTag(Biome.BiomeType biomeType, string tag)
    {
        return GetBiomeConfig(biomeType)?.HasTag(tag) ?? false;
    }

    /// <summary>
    /// Gets the probability modifier for a planetary type tag within a specific biome.
    /// </summary>
    /// <param name="biomeType">The biome type</param>
    /// <param name="planetaryTag">The planetary type tag</param>
    /// <returns>The probability modifier, or 1.0 if not defined</returns>
    public float GetProbabilityModifier(Biome.BiomeType biomeType, string planetaryTag)
    {
        return GetBiomeConfig(biomeType)?.GetProbabilityModifier(planetaryTag) ?? 1.0f;
    }

    /// <summary>
    /// Gets all biome types that have a specific tag.
    /// </summary>
    /// <param name="tag">The tag to search for</param>
    /// <returns>List of biome types that have the specified tag</returns>
    public List<Biome.BiomeType> GetBiomesWithTag(string tag)
    {
        var biomes = new List<Biome.BiomeType>();
        
        foreach (var kvp in Biomes)
        {
            if (kvp.Value?.HasTag(tag) ?? false)
            {
                biomes.Add(kvp.Key);
            }
        }
        
        return biomes;
    }
}