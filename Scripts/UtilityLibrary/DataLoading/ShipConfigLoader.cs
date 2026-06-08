using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Logistics;
using Registries;
using Structures.Resources;
using UtilityLibrary;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Loads and parses ship YAML configurations from the Configuration/ships/ directory.
/// Supports recursive directory scanning, category inference, and duplicate detection.
/// </summary>
public static class ShipConfigLoader
{
    private const string ShipsDirectory = "res://Configuration/ships/";
    private const string ShipTemplatesPath = "res://Configuration/ships/ShipTemplates.yaml";

    private static List<ShipDefinition>? _allShips;
    private static HashSet<string>? _inferredCategories;
    private static Dictionary<string, string>? _shipSourceFiles;
    private static List<ShipTemplateCategory>? _templateCategories;

    public static int ModelsLoadedCount { get; private set; }
    public static int ModelsFailedCount { get; private set; }
    public static int IconsLoadedCount { get; private set; }
    public static int IconsFailedCount { get; private set; }

    public static void ResetLoadingStats()
    {
        ModelsLoadedCount = 0;
        ModelsFailedCount = 0;
        IconsLoadedCount = 0;
        IconsFailedCount = 0;
    }

    /// <summary>
    /// Loads all ship definitions from the Configuration/ships/ directory.
    /// Scans recursively for all .yaml and .yml files, skipping ShipTemplates.yaml.
    /// Results are cached for subsequent calls.
    /// </summary>
    /// <returns>List of all ship definitions found</returns>
    public static List<ShipDefinition> LoadAllShips()
    {
        if (_allShips != null)
            return _allShips;

        _allShips = new List<ShipDefinition>();
        _shipSourceFiles = new Dictionary<string, string>();

        var files = BaseConfigLoader.GetYamlFilesRecursive(ShipsDirectory);

        GameLogger.Info($"ShipConfigLoader: Scanning {files.Count} files in {ShipsDirectory}");

        foreach (var filePath in files)
        {
            // Skip template definitions file (used only for validation)
            if (filePath.EndsWith("ShipTemplates.yaml"))
                continue;

            var fileShips = LoadShipsFromFile(filePath);

            foreach (var ship in fileShips)
            {
                if (_shipSourceFiles.ContainsKey(ship.Name))
                {
                    GD.PrintErr($"ShipConfigLoader: Duplicate ship '{ship.Name}' found in {filePath} (first defined in {_shipSourceFiles[ship.Name]})");
                    continue;
                }

                _allShips.Add(ship);
                _shipSourceFiles[ship.Name] = filePath;
            }
        }

        InferCategories(_allShips);
        ValidateShipTemplates(_allShips);

        GameLogger.Info($"ShipConfigLoader: Loaded {_allShips.Count} ships across {_inferredCategories?.Count ?? 0} categories");

        return _allShips;
    }

    /// <summary>
    /// Loads ship definitions from a specific YAML file.
    /// </summary>
    /// <param name="filePath">Path to the YAML file (e.g., "res://Configuration/ships/Cargo_Freighter.yaml")</param>
    /// <returns>List of ship definitions from that file</returns>
    public static List<ShipDefinition> LoadShipsFromFile(string filePath)
    {
        var ships = new List<ShipDefinition>();

        string? yamlContent = BaseConfigLoader.ReadAllText(filePath);
        if (yamlContent == null)
        {
            GD.PrintErr($"ShipConfigLoader: File not found: {filePath}");
            return ships;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

            if (yamlData != null && yamlData.ContainsKey("ships"))
            {
                var shipsList = yamlData["ships"] as List<object>;
                if (shipsList != null)
                {
                    foreach (var shipObj in shipsList)
                    {
                        if (shipObj is Dictionary<object, object> shipDict)
                        {
                            var ship = ParseShipDefinition(shipDict);
                            ships.Add(ship);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"ShipConfigLoader: Error loading from {filePath}: {e.Message}");
        }

        return ships;
    }

    /// <summary>
    /// Loads ship template categories from ShipTemplates.yaml.
    /// These categories are used for validation and organization.
    /// </summary>
    /// <returns>List of ship template categories</returns>
    public static List<ShipTemplateCategory> LoadShipTemplates()
    {
        if (_templateCategories != null)
            return _templateCategories;

        _templateCategories = new List<ShipTemplateCategory>();

        string? yamlContent = BaseConfigLoader.ReadAllText(ShipTemplatesPath);
        if (yamlContent == null)
        {
            GameLogger.Warning($"ShipConfigLoader: Templates file not found: {ShipTemplatesPath}");
            return _templateCategories;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

            if (yamlData != null && yamlData.ContainsKey("categories"))
            {
                var categoriesList = yamlData["categories"] as List<object>;
                if (categoriesList != null)
                {
                    foreach (var categoryObj in categoriesList)
                    {
                        if (categoryObj is Dictionary<object, object> categoryDict)
                        {
                            var category = new ShipTemplateCategory
                            {
                                Name = BaseConfigLoader.ReadString(categoryDict, "name", ""),
                                Description = BaseConfigLoader.ReadString(categoryDict, "description", ""),
                            };
                            _templateCategories.Add(category);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"ShipConfigLoader: Error loading templates from {ShipTemplatesPath}: {e.Message}");
        }

        return _templateCategories;
    }

    /// <summary>
    /// Gets all inferred ship categories from loaded ships.
    /// Categories are inferred from the ship file organization (future: from ship_template field).
    /// </summary>
    /// <returns>List of unique category names</returns>
    public static List<string> GetShipCategories()
    {
        LoadAllShips(); // Ensure ships are loaded
        return _inferredCategories?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Gets a ship definition by name (case-insensitive).
    /// </summary>
    /// <param name="name">The ship name to look up</param>
    /// <returns>The ship definition, or null if not found</returns>
    public static ShipDefinition? GetShipByName(string name)
    {
        var ships = LoadAllShips();
        return ships.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears the cached ship data to force a reload on next access.
    /// </summary>
    public static void ClearCache()
    {
        _allShips = null;
        _inferredCategories = null;
        _shipSourceFiles = null;
        _templateCategories = null;
        GameLogger.Debug("ShipConfigLoader: Cache cleared");
    }

    private static void InferCategories(List<ShipDefinition> ships)
    {
        _inferredCategories = new HashSet<string>();
        foreach (var ship in ships)
        {
            // Infer category from engine_category field
            if (!string.IsNullOrEmpty(ship.EngineCategory))
                _inferredCategories.Add(ship.EngineCategory);
        }
    }

    private static void ValidateShipTemplates(List<ShipDefinition> ships)
    {
        // Validate engine categories exist against loaded engine categories
        var engineCategories = EngineConfigLoader.GetEngineCategories();

        foreach (var ship in ships)
        {
            // Note: Ships don't have a direct template field in current YAML,
            // but we could infer from filename or add a ship_template field
            // For now, just validate engine category
            if (!string.IsNullOrEmpty(ship.EngineCategory) && !engineCategories.Contains(ship.EngineCategory))
            {
                GD.PushWarning($"ShipConfigLoader: Unknown engine_category '{ship.EngineCategory}' for ship '{ship.Name}'");
            }
        }
    }

    private static ShipDefinition ParseShipDefinition(Dictionary<object, object> dict)
    {
        string name = BaseConfigLoader.ReadString(dict, "name", "");

        var definition = new ShipDefinition
        {
            Name = name,
            Description = BaseConfigLoader.ReadString(dict, "description", ""),
            ShipLevel = BaseConfigLoader.ReadInt(dict, "ship_level", 1),
            DryMass = BaseConfigLoader.ReadFloat(dict, "dry_mass", 0f),
            CargoCapacity = BaseConfigLoader.ReadFloat(dict, "cargo_capacity", 0f),
            FuelCapacity = BaseConfigLoader.ReadFloat(dict, "fuel_capacity", 0f),
            EngineCategory = BaseConfigLoader.ReadString(dict, "engine_category", ""),
            WorkRequired = BaseConfigLoader.ReadFloat(dict, "work_required", 0f),
            RequiredResources = BaseConfigLoader.ReadResourceDict(dict, "required_resources"),
            Visual = ParseVisualDefinition(dict),
            Icon = ParseIconDefinition(dict, $"ship:{name}"),
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
            return new IconDefinition();
        }

        var iconDict = dict["icon"] as Dictionary<object, object>;
        if (iconDict == null)
            return new IconDefinition();

        string? basePath = BaseConfigLoader.ReadString(iconDict, "resource", "");
        if (string.IsNullOrEmpty(basePath))
        {
            return new IconDefinition();
        }

        var icon = IconDataLoader.LoadIcon(basePath, context);

        // Track stats
        if (icon.IsValid)
            IconsLoadedCount++;
        else
            IconsFailedCount++;

        icon.Scale = BaseConfigLoader.ReadFloat(iconDict, "scale", 1.0f);
        icon.Tint = ReadColor(iconDict, "tint", Colors.White);

        return icon;
    }

    private static Color ReadColor(Dictionary<object, object> dict, string key, Color fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        if (dict[key] is not System.Collections.Generic.List<object> arr || arr.Count < 3)
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
            if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error parsing node to float: {e.Message}");
        }

        return fallback;
    }

    private static VisualDefinition ParseVisualDefinition(Dictionary<object, object> dict)
    {
        var visual = new VisualDefinition();

        if (!dict.ContainsKey("visual"))
            return visual;

        var visualDict = dict["visual"] as Dictionary<object, object>;
        if (visualDict == null)
            return visual;

        string? modelPath = BaseConfigLoader.ReadString(visualDict, "model_resource", "");
        if (!string.IsNullOrEmpty(modelPath))
        {
            visual.ModelResourcePath = modelPath;
            try
            {
                var modelConfig = GD.Load<ModelConfig>(modelPath);
                visual.ModelPrototype = modelConfig?.Model;
                if (visual.ModelPrototype != null)
                {
                    // Wrapper supplies the defaults; explicit YAML keys below override them.
                    visual.Scale = modelConfig!.Scale;
                    visual.RotationOffset = modelConfig.RotationOffset;
                    GameLogger.Info($"ShipConfigLoader: Loaded model prototype '{modelPath}'");
                    ModelsLoadedCount++;
                }
                else
                {
                    GameLogger.Error($"ShipConfigLoader: Failed to load model wrapper at '{modelPath}'");
                    ModelsFailedCount++;
                }
            }
            catch (System.Exception ex)
            {
                GameLogger.Error($"ShipConfigLoader: Exception loading model '{modelPath}': {ex.Message}");
                visual.ModelResourcePath = null;
                ModelsFailedCount++;
            }
        }

        visual.ModelMaterial = BaseConfigLoader.ReadString(visualDict, "model_material", "");
        visual.AnimationPath = BaseConfigLoader.ReadString(visualDict, "animation_path", "");
        // Wrapper defaults already applied above; only override when YAML sets the key.
        if (visualDict.ContainsKey("scale"))
            visual.Scale = BaseConfigLoader.ReadFloat(visualDict, "scale", visual.Scale);
        if (visualDict.ContainsKey("rotation_offset"))
            visual.RotationOffset = BaseConfigLoader.ReadVector3(visualDict, "rotation_offset", visual.RotationOffset);
        visual.ShapeId = BaseConfigLoader.ReadString(visualDict, "shape_id", "hexagon").Trim();
        if (string.IsNullOrEmpty(visual.ShapeId)) visual.ShapeId = "hexagon";
        visual.ShapeSize = BaseConfigLoader.ReadFloat(visualDict, "shape_size", 64f);
        visual.ShapeColor = ReadColor(visualDict, "shape_color", visual.ShapeColor);

        return visual;
    }
}
