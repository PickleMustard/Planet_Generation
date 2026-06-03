using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;
using Structures.Enums;
using UtilityLibrary.DataLoading;

namespace UtilityLibrary.NameGeneration;

/// <summary>
/// Centralized static class for generating names for all types of generated objects.
/// Handles loading name files, picking random names, and formatting names according to different conventions.
/// </summary>
public static class NameGenerator
{
    private static readonly Dictionary<string, Godot.Collections.Dictionary> _loadedNameFiles = new();
    private static readonly Dictionary<string, string[]> _cachedNameArrays = new();
    private static readonly Dictionary<string, string[]> _cachedAdjectiveArrays = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Picks a random name from a name dictionary, identical to the original PickName implementations.
    /// </summary>
    /// <param name="nameDict">Dictionary containing categories of names</param>
    /// <param name="format">Optional formatting to apply to the selected name</param>
    /// <returns>A randomly selected name, or empty string if no names available</returns>
    public static string PickName(Godot.Collections.Dictionary nameDict, NameFormat format = NameFormat.Default)
    {
        if (nameDict == null || nameDict.Count == 0)
            return "";

        var categories = new Godot.Collections.Array(nameDict.Keys);
        if (categories.Count == 0)
            return "";

        var random = Randomizer.rng;
        var selectedCategory = (string)categories[random.RandiRange(0, categories.Count - 1)];

        var names = (Godot.Collections.Array)nameDict[selectedCategory];
        if (names == null || names.Count == 0)
            return "";

        var selectedName = (string)names[random.RandiRange(0, names.Count - 1)];
        return FormatName(selectedName, format);
    }

    /// <summary>
    /// Picks a random name from a specific name file and category.
    /// </summary>
    /// <param name="nameFileName">Name of the name file (without .yml extension)</param>
    /// <param name="category">Specific category to pick from (optional, random if not specified)</param>
    /// <param name="format">Optional formatting to apply</param>
    /// <returns>A randomly selected name</returns>
    public static string PickNameFromFile(string nameFileName, string? category = null, NameFormat format = NameFormat.Default)
    {
        var nameData = LoadNameFile(nameFileName);
        if (nameData == null || nameData.Count == 0)
            return "";

        var random = Randomizer.rng;

        // If category is specified, use it; otherwise pick random category
        string selectedCategory;
        if (!string.IsNullOrEmpty(category) && nameData.ContainsKey(category))
        {
            selectedCategory = category;
        }
        else
        {
            var categories = new Godot.Collections.Array(nameData.Keys);
            if (categories.Count == 0)
                return "";

            // Filter out "categories" key if present
            var validCategories = new List<string>();
            foreach (var cat in categories)
            {
                var catStr = cat.AsString();
                if (catStr != "categories" && !string.IsNullOrWhiteSpace(catStr))
                    validCategories.Add(catStr);
            }

            if (validCategories.Count == 0)
                return "";

            selectedCategory = validCategories[random.RandiRange(0, validCategories.Count - 1)];
        }

        var names = (Godot.Collections.Array)nameData[selectedCategory];
        if (names == null || names.Count == 0)
            return "";

        var selectedName = (string)names[random.RandiRange(0, names.Count - 1)];
        return FormatName(selectedName, format);
    }

    /// <summary>
    /// Generates a name appropriate for a dominant body (star, neutron star, black hole).
    /// </summary>
    public static string GenerateDominantBodyName(OrbitalBodyType type = OrbitalBodyType.Star)
    {
        var nameFile = GetNameFileForCelestialBodyType(type);
        return PickNameFromFile(nameFile, format: NameFormat.NoSpacesLowerCase);
    }

    /// <summary>
    /// Generates a name appropriate for a planetary body (rocky planet, gas giant, etc.).
    /// </summary>
    public static string GeneratePlanetaryBodyName(OrbitalBodyType type)
    {
        var nameFile = GetNameFileForCelestialBodyType(type);
        return PickNameFromFile(nameFile);
    }

    /// <summary>
    /// Generates a name appropriate for a satellite body (moon, asteroid, comet, etc.).
    /// </summary>
    public static string GenerateSatelliteName(OrbitalBodyType type)
    {
        var nameFile = GetNameFileForSatelliteBodyType(type);
        return PickNameFromFile(nameFile);
    }

    /// <summary>
    /// Dispatcher that generates an appropriate name for any celestial body type.
    /// Routes to dominant or planetary naming based on the type.
    /// </summary>
    public static string GenerateCelestialBodyName(OrbitalBodyType type)
    {
        return type switch
        {
            OrbitalBodyType.Star or OrbitalBodyType.NeutronStar or OrbitalBodyType.BlackHole
                => GenerateDominantBodyName(type),
            _ => GeneratePlanetaryBodyName(type),
        };
    }

    /// <summary>
    /// Generates a ship name with optional adjective prefix.
    /// </summary>
    /// <param name="includeAdjective">Whether to include an adjective (30% chance if true)</param>
    /// <returns>A generated ship name</returns>
    public static string GenerateShipName(bool includeAdjective = true)
    {
        LoadShipNamesIfNeeded();

        if (_cachedNameArrays.TryGetValue("ships", out var names) && names != null && names.Length > 0)
        {
            var random = Randomizer.rng;

            // 30% chance: adjective + name (e.g., "Iron-Veined Voyager")
            // 70% chance: just a name
            if (includeAdjective && _cachedAdjectiveArrays.TryGetValue("ships", out var adjectives) &&
                adjectives != null && adjectives.Length > 0 && random.Randf() < 0.3f)
            {
                string adj = adjectives[random.Randi() % adjectives.Length];
                string name = names[random.Randi() % names.Length];

                // Convert adjective to title case and replace spaces with hyphens
                adj = ToTitleCase(adj).Replace(" ", "-");
                return $"{adj} {name}";
            }

            return names[random.Randi() % names.Length];
        }

        return $"Ship_{DateTime.Now.Ticks % 10000}";
    }

    /// <summary>
    /// Generates a station name.
    /// </summary>
    /// <returns>A generated station name</returns>
    public static string GenerateStationName()
    {
        // For now, stations can use the same naming as ships
        // In the future, we might want separate station name lists
        return GenerateShipName(includeAdjective: false);
    }

    /// <summary>
    /// Generates a name for a planet (for player settlement naming).
    /// </summary>
    /// <returns>A generated planet name</returns>
    public static string GeneratePlanetName()
    {
        return PickNameFromFile("rockyplanets", format: NameFormat.TitleCase);
    }

    /// <summary>
    /// Generates a name for a star system.
    /// </summary>
    /// <returns>A generated system name</returns>
    public static string GenerateSystemName()
    {
        return PickNameFromFile("centralbodies", format: NameFormat.TitleCase);
    }

    /// <summary>
    /// Generates a name for a player company.
    /// </summary>
    /// <returns>A generated company name</returns>
    public static string GenerateCompanyName()
    {
        return PickNameFromFile("companies", format: NameFormat.TitleCase);
    }

    /// <summary>
    /// Formats a name according to the specified format.
    /// </summary>
    public static string FormatName(string name, NameFormat format)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return format switch
        {
            NameFormat.LowerCase => name.ToLower(),
            NameFormat.TitleCase => ToTitleCase(name),
            NameFormat.NoSpaces => Regex.Replace(name, @"\s", ""),
            NameFormat.Hyphenated => name.Replace(" ", "-"),
            NameFormat.NoSpacesLowerCase => Regex.Replace(name, @"\s", "").ToLower(),
            _ => name
        };
    }

    /// <summary>
    /// Converts a string to title case.
    /// </summary>
    public static string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }

    /// <summary>
    /// Generates a combined name from an adjective and a noun.
    /// </summary>
    public static string GenerateCombinedName(string adjective, string noun, NameFormat adjectiveFormat = NameFormat.Hyphenated)
    {
        var formattedAdjective = FormatName(adjective, adjectiveFormat);
        return $"{formattedAdjective} {noun}";
    }

    /// <summary>
    /// Preloads all name files for faster access.
    /// </summary>
    public static void PreloadNameData()
    {
        var nameFiles = new[] { "satellites", "rockyplanets", "nonrocky", "centralbodies" };
        foreach (var nameFile in nameFiles)
        {
            LoadNameFile(nameFile);
        }
    }

    /// <summary>
    /// Clears all cached name data.
    /// </summary>
    public static void ClearNameCache()
    {
        lock (_lock)
        {
            _loadedNameFiles.Clear();
            _cachedNameArrays.Clear();
            _cachedAdjectiveArrays.Clear();
        }
    }

    #region Private Methods

    private static string GetNameFileForCelestialBodyType(OrbitalBodyType type)
    {
        return type switch
        {
            OrbitalBodyType.RockyPlanet => "rockyplanets",
            OrbitalBodyType.DwarfPlanet => "rockyplanets",
            OrbitalBodyType.GasGiant => "nonrocky",
            OrbitalBodyType.IceGiant => "nonrocky",
            OrbitalBodyType.Star => "centralbodies",
            OrbitalBodyType.NeutronStar => "centralbodies",
            OrbitalBodyType.BlackHole => "centralbodies",
            _ => "rockyplanets",
        };
    }

    private static string GetNameFileForSatelliteBodyType(OrbitalBodyType type)
    {
        return type switch
        {
            OrbitalBodyType.Moon => "satellites",
            OrbitalBodyType.Asteroid => "satellites",
            OrbitalBodyType.DwarfPlanet => "rockyplanets",
            _ => "satellites",
        };
    }

    private static Godot.Collections.Dictionary? LoadNameFile(string nameFileName)
    {
        lock (_lock)
        {
            if (_loadedNameFiles.TryGetValue(nameFileName, out var cached))
                return cached;

            try
            {
                var nameData = TemplateLoader.LoadNamesFile(nameFileName);
                _loadedNameFiles[nameFileName] = nameData;
                return nameData;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[NameGenerator] Failed to load name file '{nameFileName}': {e.Message}");
                return null;
            }
        }
    }

    private static void LoadShipNamesIfNeeded()
    {
        lock (_lock)
        {
            if (_cachedNameArrays.ContainsKey("ships"))
                return;

            var nameData = LoadNameFile("satellites");
            if (nameData == null)
                return;

            var allNames = new List<string>();
            var allAdjectives = new List<string>();

            // Helper to extract string array from variant
            void ExtractStrings(Godot.Collections.Dictionary data, string key, List<string> target)
            {
                if (data.TryGetValue(key, out var variant))
                {
                    var arr = variant.As<Godot.Collections.Array>();
                    if (arr != null)
                    {
                        foreach (var item in arr)
                        {
                            var str = item.AsString();
                            if (!string.IsNullOrEmpty(str))
                            {
                                target.Add(str);
                            }
                        }
                    }
                }
            }

            // Load mythology names
            ExtractStrings(nameData, "mythology", allNames);

            // Load scientists
            ExtractStrings(nameData, "scientists", allNames);

            // Load explorers
            ExtractStrings(nameData, "explorers", allNames);

            // Load adjectives
            ExtractStrings(nameData, "adjectives", allAdjectives);

            _cachedNameArrays["ships"] = allNames.ToArray();
            _cachedAdjectiveArrays["ships"] = allAdjectives.ToArray();

            GD.Print($"[NameGenerator] Loaded {allNames.Count} ship names and {allAdjectives.Count} adjectives");
        }
    }

    #endregion
}
