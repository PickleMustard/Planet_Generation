using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using UtilityLibrary;
using YamlDotNet.Serialization;

namespace Structures.Resources;

/// <summary>
/// Configuration for planetary resources.
/// Contains dictionaries mapping all 9 body type categories to their SubtypeResourceConfig entries.
/// References resource groups defined in a separate config file.
/// </summary>
public class PlanetaryResourceConfig
{
    /// <summary>
    /// Resource group definitions loaded from resource_groups.yaml.
    /// Not deserialized directly — set externally after loading.
    /// </summary>
    [YamlIgnore]
    public Dictionary<string, List<string>> ResourceGroups { get; set; } = new();

    // --- YAML deserialization targets (list-of-objects format) ---

    [YamlMember(Alias = "rockyPlanetSubtypes")]
    public List<SubtypeResourceConfig> RockyPlanetSubtypesRaw { get; set; } = new();

    [YamlMember(Alias = "gasGiantSubtypes")]
    public List<SubtypeResourceConfig> GasGiantSubtypesRaw { get; set; } = new();

    [YamlMember(Alias = "iceGiantSubtypes")]
    public List<SubtypeResourceConfig> IceGiantSubtypesRaw { get; set; } = new();

    [YamlMember(Alias = "dwarfPlanetSubtypes")]
    public List<SubtypeResourceConfig> DwarfPlanetSubtypesRaw { get; set; } = new();

    [YamlMember(Alias = "blackHoleSubtypes")]
    public List<SubtypeResourceConfig> BlackHoleSubtypesRaw { get; set; } = new();

    [YamlMember(Alias = "neutronStarSubtypes")]
    public List<SubtypeResourceConfig> NeutronStarSubtypesRaw { get; set; } = new();

    [YamlMember(Alias = "starSubtypes")]
    public List<SubtypeResourceConfig> StarSubtypesRaw { get; set; } = new();

    [YamlMember(Alias = "satelliteSubtypes")]
    public List<SubtypeResourceConfig> SatelliteSubtypesRaw { get; set; } = new();

    [YamlMember(Alias = "beltSubtypes")]
    public List<SubtypeResourceConfig> BeltSubtypesRaw { get; set; } = new();

    // --- Runtime dictionaries (built from Raw lists after deserialization) ---

    [YamlIgnore]
    public Dictionary<RockyPlanetSubtype, SubtypeResourceConfig> RockyPlanetSubtypes { get; set; } = new();

    [YamlIgnore]
    public Dictionary<GasGiantSubtype, SubtypeResourceConfig> GasGiantSubtypes { get; set; } = new();

    [YamlIgnore]
    public Dictionary<IceGiantSubtype, SubtypeResourceConfig> IceGiantSubtypes { get; set; } = new();

    [YamlIgnore]
    public Dictionary<DwarfPlanetSubtype, SubtypeResourceConfig> DwarfPlanetSubtypes { get; set; } = new();

    [YamlIgnore]
    public Dictionary<BlackHoleSubtype, SubtypeResourceConfig> BlackHoleSubtypes { get; set; } = new();

    [YamlIgnore]
    public Dictionary<NeutronStarSubtype, SubtypeResourceConfig> NeutronStarSubtypes { get; set; } = new();

    [YamlIgnore]
    public Dictionary<StarSubtype, SubtypeResourceConfig> StarSubtypes { get; set; } = new();

    [YamlIgnore]
    public Dictionary<SatelliteSubtype, SubtypeResourceConfig> SatelliteSubtypes { get; set; } = new();

    [YamlIgnore]
    public Dictionary<BeltSubtype, SubtypeResourceConfig> BeltSubtypes { get; set; } = new();

    /// <summary>
    /// Converts the deserialized Raw list properties into the runtime dictionaries.
    /// Must be called after YAML deserialization and before Validate().
    /// </summary>
    /// <returns>True if all lists were successfully converted, false if any errors occurred</returns>
    public bool BuildDictionaries()
    {
        bool allBuilt = true;

        allBuilt &= BuildDictionary(RockyPlanetSubtypesRaw, RockyPlanetSubtypes, "RockyPlanetSubtype");
        allBuilt &= BuildDictionary(GasGiantSubtypesRaw, GasGiantSubtypes, "GasGiantSubtype");
        allBuilt &= BuildDictionary(IceGiantSubtypesRaw, IceGiantSubtypes, "IceGiantSubtype");
        allBuilt &= BuildDictionary(DwarfPlanetSubtypesRaw, DwarfPlanetSubtypes, "DwarfPlanetSubtype");
        allBuilt &= BuildDictionary(BlackHoleSubtypesRaw, BlackHoleSubtypes, "BlackHoleSubtype");
        allBuilt &= BuildDictionary(NeutronStarSubtypesRaw, NeutronStarSubtypes, "NeutronStarSubtype");
        allBuilt &= BuildDictionary(StarSubtypesRaw, StarSubtypes, "StarSubtype");
        allBuilt &= BuildDictionary(SatelliteSubtypesRaw, SatelliteSubtypes, "SatelliteSubtype");
        allBuilt &= BuildDictionary(BeltSubtypesRaw, BeltSubtypes, "BeltSubtype");

        if (allBuilt)
        {
            GameLogger.Info("PlanetaryResourceConfig: All dictionaries built successfully");
        }
        else
        {
            GameLogger.Error("PlanetaryResourceConfig: Failed to build one or more dictionaries");
        }

        return allBuilt;
    }

    /// <summary>
    /// Converts a deserialized list of SubtypeResourceConfig into a dictionary keyed by enum.
    /// Parses each item's Subtype string into the target enum type.
    /// </summary>
    private bool BuildDictionary<TEnum>(
        List<SubtypeResourceConfig>? sourceList,
        Dictionary<TEnum, SubtypeResourceConfig> targetDict,
        string categoryName
    ) where TEnum : struct, Enum
    {
        targetDict.Clear();

        if (sourceList == null || sourceList.Count == 0)
        {
            GameLogger.Error($"PlanetaryResourceConfig: Empty or null list for {categoryName}");
            return false;
        }

        bool allParsed = true;
        foreach (var item in sourceList)
        {
            if (string.IsNullOrWhiteSpace(item.Subtype))
            {
                GameLogger.Error($"PlanetaryResourceConfig: Entry in {categoryName} has null/empty Subtype");
                allParsed = false;
                continue;
            }

            if (!Enum.TryParse<TEnum>(item.Subtype, out var enumValue))
            {
                GameLogger.Error($"PlanetaryResourceConfig: Unknown {categoryName} value '{item.Subtype}'");
                allParsed = false;
                continue;
            }

            if (targetDict.ContainsKey(enumValue))
            {
                GameLogger.Error($"PlanetaryResourceConfig: Duplicate {categoryName} entry '{item.Subtype}'");
                allParsed = false;
                continue;
            }

            targetDict[enumValue] = item;
        }

        return allParsed;
    }

    /// <summary>
    /// Validates the entire configuration for consistency.
    /// 1. Calls ResolveResources(ResourceGroups) on every subtype config
    /// 2. Checks all enum values for all 9 categories are covered
    /// 3. Validates every resolved resource ID exists in ResourceDatabase.Instance
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        bool isValid = true;

        // First, resolve resources for all subtype configs
        isValid &= ResolveAllResources();

        // Validate enum coverage for each category
        if (!ValidateEnumCoverage(RockyPlanetSubtypes, "RockyPlanetSubtype"))
        {
            isValid = false;
        }

        if (!ValidateEnumCoverage(GasGiantSubtypes, "GasGiantSubtype"))
        {
            isValid = false;
        }

        if (!ValidateEnumCoverage(IceGiantSubtypes, "IceGiantSubtype"))
        {
            isValid = false;
        }

        if (!ValidateEnumCoverage(DwarfPlanetSubtypes, "DwarfPlanetSubtype"))
        {
            isValid = false;
        }

        if (!ValidateEnumCoverage(BlackHoleSubtypes, "BlackHoleSubtype"))
        {
            isValid = false;
        }

        if (!ValidateEnumCoverage(NeutronStarSubtypes, "NeutronStarSubtype"))
        {
            isValid = false;
        }

        if (!ValidateEnumCoverage(StarSubtypes, "StarSubtype"))
        {
            isValid = false;
        }

        if (!ValidateEnumCoverage(SatelliteSubtypes, "SatelliteSubtype"))
        {
            isValid = false;
        }

        if (!ValidateEnumCoverage(BeltSubtypes, "BeltSubtype"))
        {
            isValid = false;
        }

        // Validate all resolved resource IDs exist in ResourceDatabase
        if (!ValidateResourceIdsExist())
        {
            isValid = false;
        }

        if (isValid)
        {
            GameLogger.Info("PlanetaryResourceConfig: Configuration validation passed");
        }
        else
        {
            GameLogger.Error("PlanetaryResourceConfig: Configuration validation failed");
        }

        return isValid;
    }

    /// <summary>
    /// Resolves resources for all subtype configs using the ResourceGroups dictionary.
    /// </summary>
    private bool ResolveAllResources()
    {
        bool allResolved = true;

        ResolveDictionary(RockyPlanetSubtypes, ref allResolved);
        ResolveDictionary(GasGiantSubtypes, ref allResolved);
        ResolveDictionary(IceGiantSubtypes, ref allResolved);
        ResolveDictionary(DwarfPlanetSubtypes, ref allResolved);
        ResolveDictionary(BlackHoleSubtypes, ref allResolved);
        ResolveDictionary(NeutronStarSubtypes, ref allResolved);
        ResolveDictionary(StarSubtypes, ref allResolved);
        ResolveDictionary(SatelliteSubtypes, ref allResolved);
        ResolveDictionary(BeltSubtypes, ref allResolved);

        return allResolved;
    }

    private void ResolveDictionary<TEnum>(Dictionary<TEnum, SubtypeResourceConfig> dict, ref bool allResolved)
        where TEnum : struct, Enum
    {
        foreach (var kvp in dict)
        {
            if (kvp.Value != null)
            {
                kvp.Value.ResolveResources(ResourceGroups);
            }
            else
            {
                GameLogger.Error($"PlanetaryResourceConfig: Null config for {typeof(TEnum).Name}.{kvp.Key}");
                allResolved = false;
            }
        }
    }

    /// <summary>
    /// Validates that all enum values are covered in a dictionary
    /// </summary>
    private bool ValidateEnumCoverage<TEnum>(Dictionary<TEnum, SubtypeResourceConfig> dictionary, string enumName)
        where TEnum : struct, Enum
    {
        bool allCovered = true;
        var allEnumValues = Enum.GetValues<TEnum>();

        foreach (var enumValue in allEnumValues)
        {
            if (!dictionary.ContainsKey(enumValue))
            {
                GameLogger.Error($"PlanetaryResourceConfig: Missing configuration for {enumName}.{enumValue}");
                allCovered = false;
            }
            else
            {
                var config = dictionary[enumValue];
                if (config == null)
                {
                    GameLogger.Error($"PlanetaryResourceConfig: Null configuration for {enumName}.{enumValue}");
                    allCovered = false;
                }
                else if (!config.Validate())
                {
                    GameLogger.Error($"PlanetaryResourceConfig: Invalid configuration for {enumName}.{enumValue}");
                    allCovered = false;
                }
            }
        }

        return allCovered;
    }

    /// <summary>
    /// Validates that all resolved resource IDs exist in ResourceDatabase
    /// </summary>
    private bool ValidateResourceIdsExist()
    {
        bool allValid = true;

        if (ResourceDatabase.Instance == null || !ResourceDatabase.Instance.IsLoaded)
        {
            GameLogger.Warning("PlanetaryResourceConfig: ResourceDatabase not loaded, skipping resource ID validation");
            return true;
        }

        allValid &= ValidateResourceIdsInDictionary(RockyPlanetSubtypes);
        allValid &= ValidateResourceIdsInDictionary(GasGiantSubtypes);
        allValid &= ValidateResourceIdsInDictionary(IceGiantSubtypes);
        allValid &= ValidateResourceIdsInDictionary(DwarfPlanetSubtypes);
        allValid &= ValidateResourceIdsInDictionary(BlackHoleSubtypes);
        allValid &= ValidateResourceIdsInDictionary(NeutronStarSubtypes);
        allValid &= ValidateResourceIdsInDictionary(StarSubtypes);
        allValid &= ValidateResourceIdsInDictionary(SatelliteSubtypes);
        allValid &= ValidateResourceIdsInDictionary(BeltSubtypes);

        return allValid;
    }

    private bool ValidateResourceIdsInDictionary<TEnum>(Dictionary<TEnum, SubtypeResourceConfig> dict)
        where TEnum : struct, Enum
    {
        bool allValid = true;
        foreach (var kvp in dict)
        {
            if (kvp.Value?.ResolvedResources != null)
            {
                foreach (var resourceId in kvp.Value.ResolvedResources)
                {
                    if (!ResourceDatabase.Instance.ValidateResourceExists(resourceId))
                    {
                        GameLogger.Error($"PlanetaryResourceConfig: Unknown resource ID '{resourceId}' in {typeof(TEnum).Name}.{kvp.Key}");
                        allValid = false;
                    }
                }
            }
        }
        return allValid;
    }

    /// <summary>
    /// Gets the resource config for a specific subtype based on body type and subtype enum.
    /// </summary>
    /// <param name="bodyType">The celestial body type</param>
    /// <param name="subtype">The subtype enum value (cast to object)</param>
    /// <returns>The subtype resource config, or null if not found</returns>
    public SubtypeResourceConfig? GetConfigForSubtype(CelestialBodyType bodyType, object? subtype)
    {
        if (subtype == null)
            return null;

        return bodyType switch
        {
            CelestialBodyType.RockyPlanet when subtype is RockyPlanetSubtype rps
                => GetRockyPlanetConfig(rps),
            CelestialBodyType.GasGiant when subtype is GasGiantSubtype ggs
                => GetGasGiantConfig(ggs),
            CelestialBodyType.IceGiant when subtype is IceGiantSubtype igs
                => GetIceGiantConfig(igs),
            CelestialBodyType.DwarfPlanet when subtype is DwarfPlanetSubtype dps
                => GetDwarfPlanetConfig(dps),
            CelestialBodyType.BlackHole when subtype is BlackHoleSubtype bhs
                => GetBlackHoleConfig(bhs),
            CelestialBodyType.NeutronStar when subtype is NeutronStarSubtype nss
                => GetNeutronStarConfig(nss),
            CelestialBodyType.Star when subtype is StarSubtype ss
                => GetStarConfig(ss),
            _ => null,
        };
    }

    /// <summary>
    /// Gets the resource config for a satellite subtype.
    /// Satellites use a separate enum and are not part of CelestialBodyType.
    /// </summary>
    /// <param name="subtype">The satellite or belt subtype</param>
    /// <returns>The subtype resource config, or null if not found</returns>
    public SubtypeResourceConfig? GetConfigForSatelliteSubtype(object? subtype)
    {
        if (subtype is SatelliteSubtype ss)
            return GetSatelliteConfig(ss);
        if (subtype is BeltSubtype bs)
            return GetBeltConfig(bs);
        return null;
    }

    /// <summary>
    /// Gets the resource config for a specific rocky planet subtype
    /// </summary>
    public SubtypeResourceConfig? GetRockyPlanetConfig(RockyPlanetSubtype subtype)
    {
        return RockyPlanetSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the resource config for a specific gas giant subtype
    /// </summary>
    public SubtypeResourceConfig? GetGasGiantConfig(GasGiantSubtype subtype)
    {
        return GasGiantSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the resource config for a specific ice giant subtype
    /// </summary>
    public SubtypeResourceConfig? GetIceGiantConfig(IceGiantSubtype subtype)
    {
        return IceGiantSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the resource config for a specific dwarf planet subtype
    /// </summary>
    public SubtypeResourceConfig? GetDwarfPlanetConfig(DwarfPlanetSubtype subtype)
    {
        return DwarfPlanetSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the resource config for a specific black hole subtype
    /// </summary>
    public SubtypeResourceConfig? GetBlackHoleConfig(BlackHoleSubtype subtype)
    {
        return BlackHoleSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the resource config for a specific neutron star subtype
    /// </summary>
    public SubtypeResourceConfig? GetNeutronStarConfig(NeutronStarSubtype subtype)
    {
        return NeutronStarSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the resource config for a specific star subtype
    /// </summary>
    public SubtypeResourceConfig? GetStarConfig(StarSubtype subtype)
    {
        return StarSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the resource config for a specific satellite subtype
    /// </summary>
    public SubtypeResourceConfig? GetSatelliteConfig(SatelliteSubtype subtype)
    {
        return SatelliteSubtypes.TryGetValue(subtype, out var config) ? config : null;
    }

    /// <summary>
    /// Gets the resource config for a specific belt subtype
    /// </summary>
    public SubtypeResourceConfig? GetBeltConfig(BeltSubtype subtype)
    {
        return BeltSubtypes.TryGetValue(subtype, out var config) ? config : null;
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