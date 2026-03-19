using System;
using System.Collections.Generic;
using System.Linq;

using Godot;

using Structures.Enums;

namespace UtilityLibrary;

/// <summary>
/// Loads and caches planetary type definitions from YAML configuration files
/// in Configuration/planetary_types/. Provides type lists for GUI dropdowns
/// with display-friendly names and internal identifiers for enum mapping.
/// </summary>
public static class PlanetaryTypeLoader
{
    /// <summary>
    /// Represents a single type entry parsed from a planetary_types YAML file.
    /// </summary>
    /// <param name="Name">Internal identifier matching enum values (e.g., "RockyPlanet", "AsteroidBelt")</param>
    /// <param name="DisplayName">Human-friendly display text for GUI dropdowns (e.g., "Rocky Planet", "Asteroid Belt")</param>
    public record TypeEntry(string Name, string DisplayName);

    private static readonly string BasePath = "res://Configuration/planetary_types/";

    // Cached results per category
    private static List<TypeEntry>? _dominantTypes;
    private static List<TypeEntry>? _planetaryTypes;
    private static List<TypeEntry>? _satelliteTypes;
    private static List<TypeEntry>? _satelliteBeltTypes;

    /// <summary>
    /// Returns the list of dominant body types (Star, NeutronStar, BlackHole)
    /// loaded from dominant_bodies.yml.
    /// </summary>
    public static List<TypeEntry> GetDominantBodyTypes()
    {
        _dominantTypes ??= LoadCategory("dominant_bodies");
        return _dominantTypes;
    }

    /// <summary>
    /// Returns the list of planetary body types (RockyPlanet, DwarfPlanet, GasGiant, IceGiant)
    /// loaded from planetary_bodies.yml.
    /// </summary>
    public static List<TypeEntry> GetPlanetaryBodyTypes()
    {
        _planetaryTypes ??= LoadCategory("planetary_bodies");
        return _planetaryTypes;
    }

    /// <summary>
    /// Returns the list of satellite body types (Asteroid, Comet, Moon, etc.)
    /// loaded from satellite_bodies.yml.
    /// </summary>
    public static List<TypeEntry> GetSatelliteBodyTypes()
    {
        _satelliteTypes ??= LoadCategory("satellite_bodies");
        return _satelliteTypes;
    }

    /// <summary>
    /// Returns the list of satellite belt/group types (AsteroidBelt, IceBelt, Comet)
    /// loaded from satellite_belts.yml.
    /// </summary>
    public static List<TypeEntry> GetSatelliteBeltTypes()
    {
        _satelliteBeltTypes ??= LoadCategory("satellite_belts");
        return _satelliteBeltTypes;
    }

    /// <summary>
    /// Gets the display name for a given internal name within a category.
    /// Falls back to the internal name if not found.
    /// </summary>
    public static string GetDisplayName(List<TypeEntry> types, string internalName)
    {
        var entry = types.FirstOrDefault(t =>
            t.Name.Equals(internalName, StringComparison.OrdinalIgnoreCase)
        );
        return entry?.DisplayName ?? internalName;
    }

    /// <summary>
    /// Gets the internal name for a given display name within a category.
    /// Falls back to the display name if not found.
    /// </summary>
    public static string GetInternalName(List<TypeEntry> types, string displayName)
    {
        var entry = types.FirstOrDefault(t =>
            t.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase)
        );
        return entry?.Name ?? displayName;
    }

    /// <summary>
    /// Finds the index of a type entry by its internal name.
    /// Returns -1 if not found.
    /// </summary>
    public static int GetIndexByName(List<TypeEntry> types, string internalName)
    {
        for (int i = 0; i < types.Count; i++)
        {
            if (types[i].Name.Equals(internalName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Converts an internal name string to CelestialBodyType enum.
    /// Used for mapping dominant/planetary body dropdown selections to the
    /// existing generation pipeline enum.
    /// </summary>
    public static CelestialBodyType ToCelestialBodyType(string name)
    {
        return (CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), name, ignoreCase: true);
    }

    /// <summary>
    /// Converts an internal name string to SatelliteBodyType enum.
    /// </summary>
    public static SatelliteBodyType ToSatelliteBodyType(string name)
    {
        return (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), name, ignoreCase: true);
    }

    /// <summary>
    /// Converts an internal name string to SatelliteGroupTypes enum.
    /// </summary>
    public static SatelliteGroupTypes ToSatelliteGroupType(string name)
    {
        return (SatelliteGroupTypes)Enum.Parse(typeof(SatelliteGroupTypes), name, ignoreCase: true);
    }

    /// <summary>
    /// Converts an internal name string to DominantBodyType enum.
    /// </summary>
    public static DominantBodyType ToDominantBodyType(string name)
    {
        return (DominantBodyType)Enum.Parse(typeof(DominantBodyType), name, ignoreCase: true);
    }

    /// <summary>
    /// Converts an internal name string to PlanetaryBodyType enum.
    /// </summary>
    public static PlanetaryBodyType ToPlanetaryBodyType(string name)
    {
        return (PlanetaryBodyType)Enum.Parse(typeof(PlanetaryBodyType), name, ignoreCase: true);
    }

    /// <summary>
    /// Populates a Godot OptionButton from a list of TypeEntry values.
    /// Stores the internal name as item metadata for easy retrieval.
    /// </summary>
    public static void PopulateOptionButton(OptionButton optionButton, List<TypeEntry> types)
    {
        ArgumentNullException.ThrowIfNull(optionButton);
        ArgumentNullException.ThrowIfNull(types);

        optionButton.Clear();
        for (int i = 0; i < types.Count; i++)
        {
            optionButton.AddItem(types[i].DisplayName);
            optionButton.SetItemMetadata(i, types[i].Name);
        }
    }

    /// <summary>
    /// Gets the internal name stored as metadata for the currently selected item
    /// in an OptionButton. Returns null if no selection or no metadata.
    /// </summary>
    public static string? GetSelectedInternalName(OptionButton optionButton)
    {
        if (optionButton == null || optionButton.Selected < 0)
            return null;

        var metadata = optionButton.GetItemMetadata(optionButton.Selected);
        return metadata.AsString();
    }

    /// <summary>
    /// Selects the OptionButton item whose internal name metadata matches the given name.
    /// Returns true if a match was found and selected.
    /// </summary>
    public static bool SelectByInternalName(OptionButton optionButton, string internalName)
    {
        ArgumentNullException.ThrowIfNull(optionButton);

        for (int i = 0; i < optionButton.ItemCount; i++)
        {
            var metadata = optionButton.GetItemMetadata(i).AsString();
            if (metadata.Equals(internalName, StringComparison.OrdinalIgnoreCase))
            {
                optionButton.Select(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Loads a category YAML file and parses its potential_types list into TypeEntry objects.
    /// Handles both structured (name/display_name) and bare string formats.
    /// </summary>
    private static List<TypeEntry> LoadCategory(string categoryFileName)
    {
        var result = new List<TypeEntry>();
        string fullPath = ResolveCategoryPath(categoryFileName);

        try
        {
            var raw = TemplateLoader.LoadWithoutValidation(fullPath);

            if (
                !raw.TryGetValue("potential_types", out var typesVariant)
                || typesVariant.As<Godot.Collections.Array>()
                    is not Godot.Collections.Array typesList
            )
            {
                GameLogger.Warning(
                    $"PlanetaryTypeLoader: No 'potential_types' key found in {fullPath}"
                );
                return result;
            }

            foreach (var item in typesList)
            {
                if (item.VariantType == Variant.Type.Dictionary)
                {
                    // Structured format: { name: "Star", display_name: "Star" }
                    var dict = item.AsGodotDictionary();
                    string name = dict.ContainsKey("name") ? (string)dict["name"] : "";
                    string displayName = dict.ContainsKey("display_name")
                        ? (string)dict["display_name"]
                        : FormatPascalCase(name);

                    if (!string.IsNullOrEmpty(name))
                    {
                        result.Add(new TypeEntry(name, displayName));
                    }
                }
                else if (item.VariantType == Variant.Type.String)
                {
                    // Bare string format: "AsteroidBelt"
                    string name = item.AsString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        result.Add(new TypeEntry(name, FormatPascalCase(name)));
                    }
                }
            }
        }
        catch (Exception e)
        {
            GameLogger.Error(
                $"PlanetaryTypeLoader: Failed to load category '{categoryFileName}': {e.Message}"
            );
        }

        return result;
    }

    /// <summary>
    /// Resolves a category filename to a full res:// path, trying .yml then .yaml extensions.
    /// </summary>
    private static string ResolveCategoryPath(string categoryFileName)
    {
        string ymlPath = BasePath + categoryFileName + ".yml";
        if (FileAccess.FileExists(ymlPath))
            return ymlPath;

        string yamlPath = BasePath + categoryFileName + ".yaml";
        if (FileAccess.FileExists(yamlPath))
            return yamlPath;

        // Default to .yml — let TemplateLoader handle the error if missing
        return ymlPath;
    }

    /// <summary>
    /// Converts a PascalCase string to a space-separated display name.
    /// e.g., "RockyPlanet" → "Rocky Planet", "BlackHole" → "Black Hole"
    /// Used as fallback when display_name is not provided in YAML.
    /// </summary>
    private static string FormatPascalCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        var chars = new List<char> { pascalCase[0] };
        for (int i = 1; i < pascalCase.Length; i++)
        {
            if (char.IsUpper(pascalCase[i]) && !char.IsUpper(pascalCase[i - 1]))
            {
                chars.Add(' ');
            }
            chars.Add(pascalCase[i]);
        }
        return new string(chars.ToArray());
    }
}
