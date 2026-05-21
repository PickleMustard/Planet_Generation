using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Structures.Enums;
using Structures.Resources;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

public static class ResourceConfigLoader
{
    /// <summary>
    /// Loads resource configuration for a specific body subtype, with fallback chain:
    /// 1. Subtype-specific config (e.g., ResourceDefinitions/RockyPlanet/Ice_Resources.yaml)
    /// 2. Body type default config (e.g., ResourceDefinitions/RockyPlanet_Default.yaml)
    /// 3. Returns null to allow caller to use existing template resources
    /// </summary>
    public static Godot.Collections.Dictionary? LoadForSubtype(
        CelestialBodyType bodyType,
        object? subtype
    )
    {
        // Try subtype-specific config first
        if (subtype != null)
        {
            string subtypeName = subtype.ToString()!;
            string path =
                $"res://Configuration/ResourceDefinitions/{bodyType}/{subtypeName}_Resources.yaml";

            if (Godot.FileAccess.FileExists(path))
            {
                try
                {
                    return TemplateLoader.LoadWithoutValidation(path);
                }
                catch (Exception ex)
                {
                    GameLogger.Warning(
                        $"Failed to load subtype resource config {path}: {ex.Message}"
                    );
                }
            }
        }

        // Fallback to body type default
        string defaultPath = $"res://Configuration/ResourceDefinitions/{bodyType}_Default.yaml";
        if (Godot.FileAccess.FileExists(defaultPath))
        {
            try
            {
                return TemplateLoader.LoadWithoutValidation(defaultPath);
            }
            catch (Exception ex)
            {
                GameLogger.Warning(
                    $"Failed to load default resource config {defaultPath}: {ex.Message}"
                );
            }
        }

        // Return null to fall through to existing template-based loading
        return null;
    }

    public static List<ResourceDefinition> LoadResourceDefinitions(string filePath)
    {
        var definitions = new List<ResourceDefinition>();

        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Resource definition file not found: {filePath}");
            return definitions;
        }

        // For backward compatibility, we still validate if it's the old format
        // New category files will have different validation
        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(text);

            if (yamlData.ContainsKey("resources"))
            {
                var resourcesList = yamlData["resources"] as List<object>;
                foreach (var resourceObj in resourcesList!)
                {
                    if (resourceObj is Dictionary<object, object> resourceDict)
                    {
                        var definition = ParseResourceDefinition(resourceDict);
                        definitions.Add(definition);
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading resource definitions from {filePath}: {e.Message}\n{e.StackTrace}"
            );
        }

        return definitions;
    }

    /// <summary>
    /// Loads all resource definitions from category files in the specified directory.
    /// Resource type is inferred from the file name (e.g., "ore.yaml" → "ore").
    /// </summary>
    public static List<ResourceDefinition> LoadResourceDefinitionsFromCategories(
        string directoryPath
    )
    {
        var definitions = new List<ResourceDefinition>();

        if (!Godot.DirAccess.DirExistsAbsolute(directoryPath))
        {
            GD.PrintErr($"Resource categories directory not found: {directoryPath}");
            return definitions;
        }

        var dir = Godot.DirAccess.Open(directoryPath);
        if (dir == null)
        {
            GD.PrintErr($"Failed to open directory: {directoryPath}");
            return definitions;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (fileName.EndsWith(".yaml") && !fileName.StartsWith("."))
            {
                string filePath = Godot.ProjectSettings.GlobalizePath(
                    $"{directoryPath}/{fileName}"
                );
                GD.Print($"Loading resource category: {fileName}");

                var categoryDefinitions = LoadResourceDefinitionCategory(filePath);
                definitions.AddRange(categoryDefinitions);
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        GD.Print($"Loaded {definitions.Count} resources from {directoryPath}");
        return definitions;
    }

    /// <summary>
    /// Loads resource definitions from a single category file.
    /// Resource type is inferred from the file name.
    /// </summary>
    private static List<ResourceDefinition> LoadResourceDefinitionCategory(string filePath)
    {
        var definitions = new List<ResourceDefinition>();

        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Resource category file not found: {filePath}");
            return definitions;
        }

        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(text);

            // Infer resource type from file name
            string fileName = System.IO.Path.GetFileName(filePath);
            string resourceType = fileName.Replace(".yaml", "");

            if (yamlData.ContainsKey("resources"))
            {
                var resourcesList = yamlData["resources"] as List<object>;
                foreach (var resourceObj in resourcesList!)
                {
                    if (resourceObj is Dictionary<object, object> resourceDict)
                    {
                        // Check if resource_type field is present (should not be in new format)
                        if (resourceDict.ContainsKey("resource_type"))
                        {
                            GD.PrintErr(
                                $"Warning: resource_type field found in category file {fileName}. "
                                    + "Resource type should be inferred from file name."
                            );
                        }

                        var definition = ParseResourceDefinition(resourceDict);
                        definition.ResourceType = resourceType;
                        definitions.Add(definition);
                    }
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Validation failures are fail-loud — propagate so callers see the bad config.
            throw;
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading resource category from {filePath}: {e.Message}\n{e.StackTrace}"
            );
        }

        return definitions;
    }

    private static ResourceDefinition ParseResourceDefinition(Dictionary<object, object> dict)
    {
        // For backward compatibility, read resource_type if present
        // It will be overridden by file name inference for category files
        string resourceType = ReadString(dict, "resource_type", "");
        string idName = ReadString(dict, "id_name", "");

        if (!dict.ContainsKey("state_of_matter"))
        {
            throw new InvalidOperationException(
                $"Resource '{idName}' is missing required field 'state_of_matter'. "
                + "Every resource must declare 'solid' or 'fluid'."
            );
        }

        var definition = new ResourceDefinition
        {
            IdName = idName,
            ResourceTier = ReadInt(dict, "resource_tier", 0),
            ResourceType = resourceType, // May be overridden later
            Tags = ReadTags(dict, "tags"),
            TransportWeight = ReadFloat(dict, "transport_weight", 1.0f),
            MaxStackSize = ReadFloat(dict, "max_stack_size", 100f),
            StateOfMatter = StateOfMatterExtensions.Parse(ReadString(dict, "state_of_matter", "")),
            Icon = ParseIconDefinition(dict, $"resource:{idName}"),
        };

        // Apply fallback if icon failed to load
        if (!definition.Icon.IsValid)
        {
            definition.Icon = IconDataLoader.CreateFallbackIconDefinition();
        }

        return definition;
    }

    private static IconDefinition ParseIconDefinition(Dictionary<object, object> dict, string context)
    {
        if (!dict.ContainsKey("icon"))
        {
            return new IconDefinition(); // Return empty - fallback applied by caller
        }

        var iconDict = dict["icon"] as Dictionary<object, object>;
        if (iconDict == null)
            return new IconDefinition();

        // Get base_path (required)
        string? basePath = ReadString(iconDict, "base_path", "");
        if (string.IsNullOrEmpty(basePath))
        {
            GameLogger.Warning($"Icon section missing base_path for {context}");
            return new IconDefinition();
        }

        // Load all sizes via IconDataLoader
        var icon = IconDataLoader.LoadIcon(basePath, context);

        // Parse optional properties
        if (iconDict.ContainsKey("scale"))
        {
            icon.Scale = ReadFloat(iconDict, "scale", 1.0f);
        }

        if (iconDict.ContainsKey("tint"))
        {
            icon.Tint = ReadColor(iconDict, "tint", Colors.White);
        }

        return icon;
    }

    private static HashSet<string> ReadTags(Dictionary<object, object> dict, string key)
    {
        var tags = new HashSet<string>();

        if (!dict.ContainsKey(key) || dict[key] is not List<object> tagList)
            return tags;

        foreach (var tagObj in tagList)
        {
            string? tag = tagObj?.ToString();
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
    }

    private static Color ReadColor(Dictionary<object, object> dict, string key, Color fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        if (dict[key] is not List<object> arr || arr.Count < 3)
            return fallback;

        float r = NodeToFloat(arr[0], fallback.R);
        float g = NodeToFloat(arr[1], fallback.G);
        float b = NodeToFloat(arr[2], fallback.B);
        float a = arr.Count >= 4 ? NodeToFloat(arr[3], fallback.A) : 1.0f;

        if (r > 1.0f || g > 1.0f || b > 1.0f || a > 1.0f)
        {
            r /= 255.0f;
            g /= 255.0f;
            b /= 255.0f;
            a = arr.Count >= 4 ? a / 255.0f : 1.0f;
        }

        return new Color(r, g, b, a);
    }

    private static string ReadString(Dictionary<object, object> dict, string key, string fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        var value = dict[key];
        if (value is string s)
            return s;

        return value?.ToString() ?? fallback;
    }

    private static int ReadInt(Dictionary<object, object> dict, string key, int fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        return NodeToInt(dict[key], fallback);
    }

    private static float ReadFloat(Dictionary<object, object> dict, string key, float fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        return NodeToFloat(dict[key], fallback);
    }

    private static int NodeToInt(object node, int fallback)
    {
        try
        {
            if (node is long l)
                return (int)l;
            if (node is int i)
                return i;
            if (node is double d)
                return (int)d;
            if (node is float f)
                return (int)f;

            var s = node?.ToString();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error parsing node to int: {e.Message}");
        }

        return fallback;
    }

    private static float NodeToFloat(object node, float fallback)
    {
        try
        {
            if (node is long l)
                return (float)l;
            if (node is double d)
                return (float)d;
            if (node is float f)
                return f;
            if (node is int i)
                return (float)i;

            var s = node?.ToString();
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error parsing node to float: {e.Message}");
        }

        return fallback;
    }

    /// <summary>
    /// Loads planetary type tag configuration from the standard configuration file.
    /// </summary>
    /// <returns>Loaded planetary type tag configuration, or null if loading fails</returns>
    public static PlanetaryTypeTagConfig? LoadPlanetaryTypeTagConfig()
    {
        return LoadPlanetaryTypeTagConfig(
            "res://Configuration/ResourceDefinition/planetary_type_tags.yaml"
        );
    }

    /// <summary>
    /// Loads planetary type tag configuration from a YAML file.
    /// </summary>
    /// <param name="filePath">Path to the YAML configuration file</param>
    /// <returns>Loaded planetary type tag configuration, or null if loading fails</returns>
    public static PlanetaryTypeTagConfig? LoadPlanetaryTypeTagConfig(string filePath)
    {
        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Planetary type tag configuration file not found: {filePath}");
            return null;
        }

        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<PlanetaryTypeTagConfig>(text);

            if (config != null)
            {
                if (config.Validate())
                {
                    GD.Print(
                        $"Successfully loaded planetary type tag configuration from {filePath}"
                    );
                }
                else
                {
                    GD.PrintErr($"Planetary type tag configuration failed validation: {filePath}");
                    return null;
                }
            }

            return config;
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading planetary type tag configuration from {filePath}: {e.Message}\n{e.StackTrace}"
            );
            return null;
        }
    }

    /// <summary>
    /// Loads biome categories from the standard configuration file.
    /// </summary>
    /// <returns>Loaded biome category configuration, or null if loading fails</returns>
    public static BiomeCategoryConfig? LoadBiomeCategories()
    {
        return LoadBiomeCategories("res://Configuration/ResourceDefinition/biome_categories.yaml");
    }

    /// <summary>
    /// Loads biome categories from a YAML file.
    /// </summary>
    /// <param name="filePath">Path to the YAML configuration file</param>
    /// <returns>Loaded biome category configuration, or null if loading fails</returns>
    public static BiomeCategoryConfig? LoadBiomeCategories(string filePath)
    {
        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Biome categories file not found: {filePath}");
            return null;
        }

        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var rawData = deserializer.Deserialize<SysDict>(text);

            if (rawData == null || !rawData.ContainsKey("categories"))
            {
                GD.PrintErr($"Biome categories file missing 'categories' key: {filePath}");
                return null;
            }

            var config = new BiomeCategoryConfig();
            var categoriesList = rawData["categories"] as List<object>;

            if (categoriesList == null)
            {
                GD.PrintErr($"Biome categories 'categories' is not a list: {filePath}");
                return null;
            }

            foreach (var categoryObj in categoriesList)
            {
                if (categoryObj is Dictionary<object, object> categoryDict)
                {
                    var entry = ParseBiomeCategoryEntry(categoryDict);
                    if (entry != null && !string.IsNullOrEmpty(entry.CategoryId))
                    {
                        config.Categories[entry.CategoryId] = entry;
                    }
                }
            }

            if (config.Validate())
            {
                GD.Print($"Successfully loaded {config.Categories.Count} biome categories from {filePath}");
                return config;
            }
            else
            {
                GD.PrintErr($"Biome categories validation failed: {filePath}");
                return null;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading biome categories from {filePath}: {e.Message}\n{e.StackTrace}"
            );
            return null;
        }
    }

    private static BiomeCategoryEntry? ParseBiomeCategoryEntry(Dictionary<object, object> dict)
    {
        var entry = new BiomeCategoryEntry
        {
            CategoryId = ReadString(dict, "category_id", ""),
            DisplayName = ReadString(dict, "display_name", ""),
            Description = ReadString(dict, "description", ""),
        };

        if (dict.ContainsKey("biomes"))
        {
            var biomesList = dict["biomes"] as List<object>;
            if (biomesList != null)
            {
                foreach (var biomeObj in biomesList)
                {
                    if (biomeObj is string biomeName)
                    {
                        if (BiomeCategoryConfig.TryNormalizeBiomeId(biomeName, out var biomeId))
                        {
                            entry.Biomes.Add(biomeId);
                        }
                        else
                        {
                            GD.PrintErr($"BiomeCategoryEntry: Unknown biome '{biomeName}' in category '{entry.CategoryId}'");
                        }
                    }
                }
            }
        }

        return entry;
    }


    /// <summary>
    /// Loads resource groups from the standard configuration file.
    /// </summary>
    /// <returns>Dictionary mapping group names to resource ID lists, or null if loading fails</returns>
    public static Dictionary<string, List<string>>? LoadResourceGroups()
    {
        return LoadResourceGroups("res://Configuration/ResourceDefinition/resource_groups.yaml");
    }

    /// <summary>
    /// Loads resource groups from a YAML file.
    /// </summary>
    /// <param name="filePath">Path to the YAML configuration file</param>
    /// <returns>Dictionary mapping group names to resource ID lists, or null if loading fails</returns>
    public static Dictionary<string, List<string>>? LoadResourceGroups(string filePath)
    {
        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Resource groups file not found: {filePath}");
            return null;
        }

        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            // Use CamelCaseNamingConvention to map camelCase YAML keys to PascalCase
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var groups = deserializer.Deserialize<Dictionary<string, List<string>>>(text);

            if (groups != null)
            {
                // Validate all resource IDs exist in ResourceDatabase
                if (!ValidateResourceGroupIds(groups, filePath))
                {
                    GD.PrintErr($"Resource groups validation failed for {filePath}");
                    return null;
                }

                GD.Print($"Successfully loaded {groups.Count} resource groups from {filePath}");
            }

            return groups;
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading resource groups from {filePath}: {e.Message}\n{e.StackTrace}"
            );
            return null;
        }
    }

    /// <summary>
    /// Validates that all resource IDs in resource groups exist in ResourceDatabase.
    /// </summary>
    private static bool ValidateResourceGroupIds(
        Dictionary<string, List<string>> groups,
        string sourcePath
    )
    {
        if (ResourceDatabase.Instance == null || !ResourceDatabase.Instance.IsLoaded)
        {
            GameLogger.Warning(
                "Resource groups: ResourceDatabase not loaded, skipping resource ID validation"
            );
            return true;
        }

        bool allValid = true;
        foreach (var groupKvp in groups)
        {
            var groupName = groupKvp.Key;
            var resourceIds = groupKvp.Value;

            if (resourceIds == null)
            {
                GameLogger.Warning($"Resource group '{groupName}' has null resource list");
                continue;
            }

            foreach (var resourceId in resourceIds)
            {
                if (!ResourceDatabase.Instance.ValidateResourceExists(resourceId))
                {
                    GameLogger.Error(
                        $"Resource group '{groupName}' contains unknown resource ID '{resourceId}'"
                    );
                    allValid = false;
                }
            }
        }

        return allValid;
    }

    /// <summary>
    /// Loads planetary resource configuration from the standard configuration file.
    /// </summary>
    /// <returns>Loaded planetary resource configuration, or null if loading fails</returns>
    public static PlanetaryResourceConfig? LoadPlanetaryResourceConfig()
    {
        return LoadPlanetaryResourceConfig(
            "res://Configuration/ResourceDefinition/planetary_resource_config.yaml"
        );
    }

    /// <summary>
    /// Loads planetary resource configuration from a YAML file.
    /// Deserializes and builds dictionaries but does NOT assign resource groups or validate.
    /// The caller is responsible for setting ResourceGroups and calling Validate().
    /// </summary>
    /// <param name="filePath">Path to the YAML configuration file</param>
    /// <returns>Loaded planetary resource configuration, or null if loading fails</returns>
    public static PlanetaryResourceConfig? LoadPlanetaryResourceConfig(string filePath)
    {
        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Planetary resource configuration file not found: {filePath}");
            return null;
        }

        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<PlanetaryResourceConfig>(text);

            if (config != null)
            {
                if (!config.BuildDictionaries())
                {
                    GD.PrintErr($"Failed to build dictionaries from {filePath}");
                    return null;
                }

                GD.Print($"Successfully deserialized planetary resource configuration from {filePath}");
            }

            return config;
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading planetary resource configuration from {filePath}: {e.Message}\n{e.StackTrace}"
            );
            return null;
        }
    }

    /// <summary>
    /// Loads biome resource configuration from the standard configuration file.
    /// </summary>
    /// <returns>Loaded biome resource configuration, or null if loading fails</returns>
    public static BiomeResourceConfig? LoadBiomeResourceConfig()
    {
        return LoadBiomeResourceConfig(
            "res://Configuration/ResourceDefinition/biome_resource_config.yaml"
        );
    }

    /// <summary>
    /// Loads biome resource configuration from a YAML file.
    /// Deserializes and builds the dictionary but does NOT validate.
    /// The caller is responsible for calling Validate().
    /// </summary>
    /// <param name="filePath">Path to the YAML configuration file</param>
    /// <returns>Loaded biome resource configuration, or null if loading fails</returns>
    public static BiomeResourceConfig? LoadBiomeResourceConfig(string filePath)
    {
        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Biome resource configuration file not found: {filePath}");
            return null;
        }

        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<BiomeResourceConfig>(text);

            if (config != null)
            {
                if (!config.BuildDictionary())
                {
                    GD.PrintErr($"Failed to build biome dictionary from {filePath}");
                    return null;
                }

                GD.Print($"Successfully deserialized biome resource configuration from {filePath}");
            }

            return config;
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading biome resource configuration from {filePath}: {e.Message}\n{e.StackTrace}"
            );
            return null;
        }
    }
}
