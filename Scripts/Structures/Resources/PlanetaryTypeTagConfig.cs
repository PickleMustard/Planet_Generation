using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Main configuration class for planetary type tags.
/// Contains dictionaries mapping celestial body subtypes to their tag configurations.
/// </summary>
public class PlanetaryTypeTagConfig
{
    /// <summary>
    /// Tag configurations for rocky planet subtypes
    /// </summary>
    public Dictionary<RockyPlanetSubtype, SubtypeTagConfig> RockyPlanetSubtypes { get; set; } = new();

    /// <summary>
    /// Tag configurations for gas giant subtypes
    /// </summary>
    public Dictionary<GasGiantSubtype, SubtypeTagConfig> GasGiantSubtypes { get; set; } = new();

    /// <summary>
    /// Tag configurations for ice giant subtypes
    /// </summary>
    public Dictionary<IceGiantSubtype, SubtypeTagConfig> IceGiantSubtypes { get; set; } = new();

    /// <summary>
    /// Tag configurations for dwarf planet subtypes
    /// </summary>
    public Dictionary<DwarfPlanetSubtype, SubtypeTagConfig> DwarfPlanetSubtypes { get; set; } = new();

    /// <summary>
    /// Tag configurations for black hole subtypes
    /// </summary>
    public Dictionary<BlackHoleSubtype, SubtypeTagConfig> BlackHoleSubtypes { get; set; } = new();

    /// <summary>
    /// Tag configurations for neutron star subtypes
    /// </summary>
    public Dictionary<NeutronStarSubtype, SubtypeTagConfig> NeutronStarSubtypes { get; set; } = new();

    /// <summary>
    /// Tag configurations for star subtypes
    /// </summary>
    public Dictionary<StarSubtype, SubtypeTagConfig> StarSubtypes { get; set; } = new();

    /// <summary>
    /// Tag configurations for satellite subtypes
    /// </summary>
    public Dictionary<SatelliteSubtype, SubtypeTagConfig> SatelliteSubtypes { get; set; } = new();

    /// <summary>
    /// Tag configurations for belt subtypes
    /// </summary>
    public Dictionary<BeltSubtype, SubtypeTagConfig> BeltSubtypes { get; set; } = new();

    /// <summary>
    /// Validates the entire configuration for consistency.
    /// Checks that all enum values are represented and configurations are valid.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        bool isValid = true;

        // Validate RockyPlanetSubtypes
        if (!ValidateEnumCoverage(RockyPlanetSubtypes, "RockyPlanetSubtype"))
        {
            isValid = false;
        }

        // Validate GasGiantSubtypes
        if (!ValidateEnumCoverage(GasGiantSubtypes, "GasGiantSubtype"))
        {
            isValid = false;
        }

        // Validate IceGiantSubtypes
        if (!ValidateEnumCoverage(IceGiantSubtypes, "IceGiantSubtype"))
        {
            isValid = false;
        }

        // Validate DwarfPlanetSubtypes
        if (!ValidateEnumCoverage(DwarfPlanetSubtypes, "DwarfPlanetSubtype"))
        {
            isValid = false;
        }

        // Validate BlackHoleSubtypes
        if (!ValidateEnumCoverage(BlackHoleSubtypes, "BlackHoleSubtype"))
        {
            isValid = false;
        }

        // Validate NeutronStarSubtypes
        if (!ValidateEnumCoverage(NeutronStarSubtypes, "NeutronStarSubtype"))
        {
            isValid = false;
        }

        // Validate StarSubtypes
        if (!ValidateEnumCoverage(StarSubtypes, "StarSubtype"))
        {
            isValid = false;
        }

        // Validate SatelliteSubtypes
        if (!ValidateEnumCoverage(SatelliteSubtypes, "SatelliteSubtype"))
        {
            isValid = false;
        }

        // Validate BeltSubtypes
        if (!ValidateEnumCoverage(BeltSubtypes, "BeltSubtype"))
        {
            isValid = false;
        }

        if (isValid)
        {
            GD.Print("PlanetaryTypeTagConfig: Configuration validation passed");
        }
        else
        {
            GD.PrintErr("PlanetaryTypeTagConfig: Configuration validation failed");
        }

        return isValid;
    }

    /// <summary>
    /// Validates that all enum values are covered in a dictionary
    /// </summary>
    private bool ValidateEnumCoverage<TEnum>(Dictionary<TEnum, SubtypeTagConfig> dictionary, string enumName)
        where TEnum : struct, Enum
    {
        bool allCovered = true;
        var allEnumValues = Enum.GetValues<TEnum>();

        foreach (var enumValue in allEnumValues)
        {
            if (!dictionary.ContainsKey(enumValue))
            {
                GD.PrintErr($"PlanetaryTypeTagConfig: Missing configuration for {enumName}.{enumValue}");
                allCovered = false;
            }
            else
            {
                var config = dictionary[enumValue];
                if (config == null)
                {
                    GD.PrintErr($"PlanetaryTypeTagConfig: Null configuration for {enumName}.{enumValue}");
                    allCovered = false;
                }
                else if (!config.Validate())
                {
                    GD.PrintErr($"PlanetaryTypeTagConfig: Invalid configuration for {enumName}.{enumValue}");
                    allCovered = false;
                }
            }
        }

        return allCovered;
    }

    /// <summary>
    /// Gets the tag configuration for a specific rocky planet subtype
    /// </summary>
    public SubtypeTagConfig? GetRockyPlanetConfig(RockyPlanetSubtype subtype)
    {
        return RockyPlanetSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a specific gas giant subtype
    /// </summary>
    public SubtypeTagConfig? GetGasGiantConfig(GasGiantSubtype subtype)
    {
        return GasGiantSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a specific ice giant subtype
    /// </summary>
    public SubtypeTagConfig? GetIceGiantConfig(IceGiantSubtype subtype)
    {
        return IceGiantSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a specific dwarf planet subtype
    /// </summary>
    public SubtypeTagConfig? GetDwarfPlanetConfig(DwarfPlanetSubtype subtype)
    {
        return DwarfPlanetSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a specific black hole subtype
    /// </summary>
    public SubtypeTagConfig? GetBlackHoleConfig(BlackHoleSubtype subtype)
    {
        return BlackHoleSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a specific neutron star subtype
    /// </summary>
    public SubtypeTagConfig? GetNeutronStarConfig(NeutronStarSubtype subtype)
    {
        return NeutronStarSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a specific star subtype
    /// </summary>
    public SubtypeTagConfig? GetStarConfig(StarSubtype subtype)
    {
        return StarSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a specific satellite subtype
    /// </summary>
    public SubtypeTagConfig? GetSatelliteConfig(SatelliteSubtype subtype)
    {
        return SatelliteSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a specific belt subtype
    /// </summary>
    public SubtypeTagConfig? GetBeltConfig(BeltSubtype subtype)
    {
        return BeltSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the tag configuration for a subtype using generic body type and subtype object.
    /// Allows callers to look up config without knowing the specific enum type at compile time.
    /// </summary>
    /// <param name="bodyType">The celestial body type</param>
    /// <param name="subtype">The subtype enum value (cast to object)</param>
    /// <returns>The subtype tag config, or null if not found</returns>
    public SubtypeTagConfig? GetConfigForSubtype(BodyClassification classification)
    {
        return classification switch
        {
            BodyClassification.RockyPlanet rp => GetRockyPlanetConfig(rp.Subtype),
            BodyClassification.GasGiant gg => GetGasGiantConfig(gg.Subtype),
            BodyClassification.IceGiant ig => GetIceGiantConfig(ig.Subtype),
            BodyClassification.DwarfPlanet dp => GetDwarfPlanetConfig(dp.Subtype),
            BodyClassification.BlackHole bh => GetBlackHoleConfig(bh.Subtype),
            BodyClassification.NeutronStar ns => GetNeutronStarConfig(ns.Subtype),
            BodyClassification.Star s => GetStarConfig(s.Subtype),
            BodyClassification.Satellite sat when sat.Subtype.HasValue
                => GetSatelliteConfig(sat.Subtype.Value),
            BodyClassification.Belt b when b.Subtype.HasValue
                => GetBeltConfig(b.Subtype.Value),
            _ => null,
        };
    }

    /// <summary>
    /// Gets all tags for a specific rocky planet subtype
    /// </summary>
    public HashSet<string>? GetRockyPlanetTags(RockyPlanetSubtype subtype)
    {
        return GetRockyPlanetConfig(subtype)?.Tags;
    }

    /// <summary>
    /// Gets all tags for a specific gas giant subtype
    /// </summary>
    public HashSet<string>? GetGasGiantTags(GasGiantSubtype subtype)
    {
        return GetGasGiantConfig(subtype)?.Tags;
    }

    /// <summary>
    /// Gets all tags for a specific ice giant subtype
    /// </summary>
    public HashSet<string>? GetIceGiantTags(IceGiantSubtype subtype)
    {
        return GetIceGiantConfig(subtype)?.Tags;
    }

    /// <summary>
    /// Checks if a specific rocky planet subtype has a particular tag
    /// </summary>
    public bool RockyPlanetHasTag(RockyPlanetSubtype subtype, string tag)
    {
        return GetRockyPlanetConfig(subtype)?.HasTag(tag) ?? false;
    }

    /// <summary>
    /// Checks if a specific gas giant subtype has a particular tag
    /// </summary>
    public bool GasGiantHasTag(GasGiantSubtype subtype, string tag)
    {
        return GetGasGiantConfig(subtype)?.HasTag(tag) ?? false;
    }

    /// <summary>
    /// Checks if a specific ice giant subtype has a particular tag
    /// </summary>
    public bool IceGiantHasTag(IceGiantSubtype subtype, string tag)
    {
        return GetIceGiantConfig(subtype)?.HasTag(tag) ?? false;
    }

    /// <summary>
    /// Gets the resource weight multiplier for a specific rocky planet subtype
    /// </summary>
    public float GetRockyPlanetResourceWeight(RockyPlanetSubtype subtype)
    {
        return GetRockyPlanetConfig(subtype)?.GetResourceWeight() ?? 1.0f;
    }

    /// <summary>
    /// Gets the resource weight multiplier for a specific gas giant subtype
    /// </summary>
    public float GetGasGiantResourceWeight(GasGiantSubtype subtype)
    {
        return GetGasGiantConfig(subtype)?.GetResourceWeight() ?? 1.0f;
    }

    /// <summary>
    /// Gets the resource weight multiplier for a specific ice giant subtype
    /// </summary>
    public float GetIceGiantResourceWeight(IceGiantSubtype subtype)
    {
        return GetIceGiantConfig(subtype)?.GetResourceWeight() ?? 1.0f;
    }
}