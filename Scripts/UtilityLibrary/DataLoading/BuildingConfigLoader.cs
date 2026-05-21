using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using Structures.Transfers;
using UtilityLibrary;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

public static class BuildingConfigLoader
{
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
    public static List<BuildingDefinition> LoadBuildingDefinitions(string filePath)
    {
        var definitions = new List<BuildingDefinition>();

        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Building definition file not found: {filePath}");
            return definitions;
        }

        var validation = YamlValidator.ValidateBuildingDefinition(filePath);
        if (!validation.IsValid)
        {
            GD.PrintErr($"YAML validation failed for {filePath}");
            foreach (var error in validation.Errors)
            {
                GD.PrintErr($"  - {error}");
            }
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

            if (yamlData.ContainsKey("buildings"))
            {
                var buildingsList = yamlData["buildings"] as List<object>;
                foreach (var buildingObj in buildingsList!)
                {
                    if (buildingObj is Dictionary<object, object> buildingDict)
                    {
                        var definition = ParseBuildingDefinition(buildingDict);
                        definitions.Add(definition);
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading building definitions from {filePath}: {e.Message}\n{e.StackTrace}"
            );
        }

        return definitions;
    }

    private static BuildingDefinition ParseBuildingDefinition(Dictionary<object, object> dict)
    {
        string idName = ReadString(dict, "id_name", "");

        var definition = new BuildingDefinition
        {
            IdName = idName,
            DisplayName = ReadString(dict, "display_name", ""),
            Description = ReadString(dict, "description", ""),
            Category = ReadString(dict, "category", ""),
            BuildingLimit = ReadInt(dict, "building_limit", -1),
            MaxResourceTier = ReadInt(dict, "max_resource_tier", 0),
            WorkRequired = ReadFloat(dict, "work_required", 100.0f),
            AllowedRecipeCategory = ReadString(dict, "allowed_recipe_category", ""),
            Placement = ParsePlacementRequirements(dict),
            RequiredResources = ParseRequiredResources(dict),
            Visual = ParseVisualDefinition(dict),
            Icon = ParseIconDefinition(dict, $"building:{idName}"),
            Sound = ParseSoundDefinition(dict),
            Demolishable = ReadBool(dict, "demolishable", true),
            DefaultLinkProfile = ReadString(dict, "link_profile", ""),
        };

        ParseBehaviorEntries(dict, definition);
        PopulateNodeLayoutFromShape(definition);

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

        string? basePath = ReadString(iconDict, "base_path", "");
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

        icon.Scale = ReadFloat(iconDict, "scale", 1.0f);
        icon.Tint = ReadColor(iconDict, "tint", Colors.White);

        return icon;
    }

    private static void PopulateNodeLayoutFromShape(BuildingDefinition definition)
    {
        string shapeId = definition.Visual?.ShapeId ?? "hexagon";
        var shape = BuildingShape2DDatabase.Instance.Get(shapeId);
        if (shape == null)
        {
            GameLogger.Warning(
                $"BuildingConfigLoader: building '{definition.IdName}' references " +
                $"unknown shape_id '{shapeId}'; no node layout populated");
            return;
        }
        definition.PopulateNodeLayoutFromShape(shape);
    }

    private static void ParseBehaviorEntries(Dictionary<object, object> dict, BuildingDefinition definition)
    {
        if (!dict.ContainsKey("behaviors"))
            return;

        if (dict["behaviors"] is not List<object> behaviorList)
            return;

        foreach (var entry in behaviorList)
        {
            if (entry is string s && !string.IsNullOrEmpty(s))
            {
                definition.BehaviorEntries.Add(new BuildingDefinition.BehaviorConfigEntry
                {
                    BehaviorId = s,
                    Config = new Dictionary<string, object>()
                });
            }
            else if (entry is Dictionary<object, object> entryDict)
            {
                string behaviorId = ReadString(entryDict, "behavior_id", "");
                if (string.IsNullOrEmpty(behaviorId))
                {
                    GD.PrintErr("BuildingConfigLoader: behavior entry missing 'behavior_id' key");
                    continue;
                }

                var config = new Dictionary<string, object>();
                foreach (var kvp in entryDict)
                {
                    string key = kvp.Key?.ToString() ?? "";
                    if (key == "behavior_id") continue;
                    config[key] = kvp.Value;
                }

                definition.BehaviorEntries.Add(new BuildingDefinition.BehaviorConfigEntry
                {
                    BehaviorId = behaviorId,
                    Config = config
                });
            }
        }
    }

    // Cache for biome categories - loaded once and reused
    private static BiomeCategoryConfig? _biomeCategoryCache;
    private static readonly object _categoryLock = new object();

    /// <summary>
    /// Gets or loads the biome category configuration.
    /// Uses lazy loading with caching for performance.
    /// </summary>
    private static BiomeCategoryConfig? GetBiomeCategoryConfig()
    {
        lock (_categoryLock)
        {
            if (_biomeCategoryCache == null)
            {
                _biomeCategoryCache = ResourceConfigLoader.LoadBiomeCategories();
            }
            return _biomeCategoryCache;
        }
    }

    /// <summary>
    /// Clears the cached biome category configuration.
    /// Call this if biome categories are reloaded at runtime.
    /// </summary>
    public static void ClearBiomeCategoryCache()
    {
        lock (_categoryLock)
        {
            _biomeCategoryCache = null;
        }
    }

    private static BuildingDefinition.PlacementRequirements ParsePlacementRequirements(
        Dictionary<object, object> dict
    )
    {
        var placement = new BuildingDefinition.PlacementRequirements();

        if (!dict.ContainsKey("placement_requirements"))
            return placement;

        var placementDict = dict["placement_requirements"] as Dictionary<object, object>;
        if (placementDict == null)
            return placement;

        placement.MinElevation = ReadFloat(placementDict, "min_elevation", 0.0f);
        placement.MaxElevation = ReadFloat(placementDict, "max_elevation", 1.0f);
        placement.MaxSlope = ReadFloat(placementDict, "max_slope", 45.0f);
        placement.CellCount = ReadInt(placementDict, "cell_count", 1);
        placement.RequiresAdjacent = ReadBool(placementDict, "requires_adjacent", false);

        if (placementDict.ContainsKey("biomes"))
        {
            var biomesList = placementDict["biomes"] as List<object>;
            if (biomesList != null)
            {
                // Check for empty list - deprecated, should use "*" instead
                if (biomesList.Count == 0)
                {
                    GD.PushWarning(
                        "BuildingConfigLoader: Empty 'biomes' list is deprecated. " +
                        "Use ['*'] to allow construction in any biome."
                    );
                    placement.AllowAnyBiome = true;
                }
                else
                {
                    // Collect all biome entries (including category: prefixes)
                    var biomeEntries = new List<string>();
                    bool hasWildcard = false;

                    foreach (var biomeObj in biomesList)
                    {
                        if (biomeObj is string biomeName)
                        {
                            if (biomeName == "*")
                            {
                                hasWildcard = true;
                            }
                            else
                            {
                                biomeEntries.Add(biomeName);
                            }
                        }
                    }

                    if (hasWildcard)
                    {
                        placement.AllowAnyBiome = true;

                        // Warn if other entries are defined alongside wildcard
                        if (biomeEntries.Count > 0)
                        {
                            GD.PushWarning(
                                $"BuildingConfigLoader: Wildcard '*' allows all biomes, " +
                                $"but additional entries were also specified: " +
                                $"{string.Join(", ", biomeEntries)}. " +
                                $"These additional entries will be ignored."
                            );
                        }
                    }
                    else
                    {
                        // Resolve biome entries (handles both individual biomes and categories)
                        var categoryConfig = GetBiomeCategoryConfig();
                        if (categoryConfig != null)
                        {
                            var resolvedBiomes = categoryConfig.ResolveBiomeEntries(
                                biomeEntries,
                                out bool _
                            );
                            foreach (var biome in resolvedBiomes)
                                placement.Biomes.Add(biome);
                        }
                        else
                        {
                            // Fallback: parse as individual biome names only
                            GD.PushWarning(
                                "BuildingConfigLoader: Could not load biome categories. " +
                                "Category references will not be resolved."
                            );
                            foreach (var entry in biomeEntries)
                            {
                                if (entry.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
                                {
                                    GD.PrintErr(
                                        $"BuildingConfigLoader: Cannot resolve category reference '{entry}' " +
                                        $"- biome categories not loaded."
                                    );
                                }
                                else if (TryParseBiomeType(entry, out Biome.BiomeType biomeType))
                                {
                                    placement.Biomes.Add(biomeType);
                                }
                                else
                                {
                                    GD.PrintErr($"Unknown biome type in building placement: {entry}");
                                }
                            }
                        }
                    }
                }
            }
        }

        // Eagerly load and instantiate custom behavior
        placement.ConfigurableBehavior = LoadPlacementBehavior(placementDict, placement);

        return placement;
    }

    /// <summary>
    /// Loads and instantiates a custom placement behavior from the configuration.
    /// Supports both file paths (res://), bare class names (legacy), and
    /// inline config mappings (new format with behavior_class key).
    /// </summary>
    private static IPlacementBehavior? LoadPlacementBehavior(
        Dictionary<object, object> placementDict,
        BuildingDefinition.PlacementRequirements requirements)
    {
        if (!placementDict.ContainsKey("configurable_behavior"))
            return null;

        var behaviorValue = placementDict["configurable_behavior"];
        if (behaviorValue == null)
            return null;

        // Support legacy bare string format (warn)
        if (behaviorValue is string bareString)
        {
            GD.PushWarning(
                "BuildingConfigLoader: Bare string 'configurable_behavior' is deprecated. " +
                "Use mapping with 'behavior_class' key and inline config.");
            return InstantiatePlacementBehavior(bareString, requirements);
        }

        if (behaviorValue is not Dictionary<object, object> behaviorDict)
            return null;

        string className = ReadString(behaviorDict, "behavior_class", "");
        if (string.IsNullOrWhiteSpace(className))
        {
            GD.PrintErr("BuildingConfigLoader: configurable_behavior mapping missing 'behavior_class' key");
            return null;
        }

        // Extract config keys (everything except behavior_class)
        var config = new Dictionary<string, object>();
        foreach (var kvp in behaviorDict)
        {
            string key = kvp.Key?.ToString() ?? "";
            if (key == "behavior_class") continue;
            config[key] = kvp.Value;
        }

        var behavior = InstantiatePlacementBehavior(className, requirements);

        // Apply inline config (e.g., min_atmosphere, max_atmosphere)
        if (behavior is AtmospherePlacementBehavior atm)
        {
            if (config.TryGetValue("min_atmosphere", out var minAtm))
                atm.MinAtmosphere = NodeToFloat(minAtm, atm.MinAtmosphere);
            if (config.TryGetValue("max_atmosphere", out var maxAtm))
                atm.MaxAtmosphere = NodeToFloat(maxAtm, atm.MaxAtmosphere);
        }

        return behavior;
    }

    /// <summary>
    /// Instantiates a placement behavior by class name using reflection.
    /// Supports both bare names and fully-qualified type names.
    /// </summary>
    private static IPlacementBehavior? InstantiatePlacementBehavior(
        string className,
        BuildingDefinition.PlacementRequirements requirements)
    {
        try
        {
            // Check if it's a file path (starts with res://)
            if (className.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                return LoadBehaviorFromFile(className, requirements);
            }

            var assembly = typeof(IPlacementBehavior).Assembly;

            // Try various namespace prefixes
            var type = assembly.GetType(className)
                ?? assembly.GetType($"Structures.Resources.{className}")
                ?? assembly.GetType($"Scripts.Structures.Resources.{className}");

            if (type == null)
            {
                GD.PrintErr($"BuildingConfigLoader: Could not find type '{className}'");
                return null;
            }

            if (!typeof(IPlacementBehavior).IsAssignableFrom(type))
            {
                GD.PrintErr($"BuildingConfigLoader: Type '{className}' does not implement IPlacementBehavior");
                return null;
            }

            // Try constructor with PlacementRequirements parameter first
            var constructor = type.GetConstructor(new[] { typeof(BuildingDefinition.PlacementRequirements) });
            if (constructor != null)
            {
                return (IPlacementBehavior)constructor.Invoke(new object[] { requirements });
            }

            // Fall back to parameterless constructor
            constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                return (IPlacementBehavior)constructor.Invoke(null);
            }

            GD.PrintErr($"BuildingConfigLoader: Type '{className}' has no valid constructor");
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"BuildingConfigLoader: Failed to instantiate '{className}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads a placement behavior from a C# script file using Godot's CSharpScript.
    /// </summary>
    private static IPlacementBehavior? LoadBehaviorFromFile(string filePath, BuildingDefinition.PlacementRequirements requirements)
    {
        try
        {
            var script = GD.Load<CSharpScript>(filePath);
            if (script == null)
            {
                GD.PrintErr($"BuildingConfigLoader: Could not load script at '{filePath}'");
                return null;
            }

            var variant = script.New();
            var godotObj = variant.AsGodotObject();

            if (godotObj is not IPlacementBehavior behavior)
            {
                GD.PrintErr($"BuildingConfigLoader: Script '{filePath}' does not implement IPlacementBehavior");
                godotObj.Free();
                return null;
            }

            return behavior;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"BuildingConfigLoader: Failed to load behavior from file '{filePath}': {ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, int> ParseRequiredResources(Dictionary<object, object> dict)
    {
        var resources = new Dictionary<string, int>();

        if (!dict.ContainsKey("required_resources"))
            return resources;

        var resourcesDict = dict["required_resources"] as Dictionary<object, object>;
        if (resourcesDict == null)
            return resources;

        foreach (var kvp in resourcesDict)
        {
            string resourceName = (string)kvp.Key;
            int quantity = NodeToInt(kvp.Value, 0);
            resources[resourceName] = quantity;
        }

        return resources;
    }

    private static VisualDefinition ParseVisualDefinition(
        Dictionary<object, object> dict
    )
    {
        var visual = new VisualDefinition();

        if (!dict.ContainsKey("visual"))
            return visual;

        var visualDict = dict["visual"] as Dictionary<object, object>;
        if (visualDict == null)
            return visual;

        string? modelPath = ValidateFilePath(
            ReadString(visualDict, "model_path", ""),
            "visual.model_path"
        );
        visual.ModelPath = modelPath;

        // Eagerly load the PackedScene prototype
        if (!string.IsNullOrEmpty(modelPath))
        {
            try
            {
                visual.ModelPrototype = GD.Load<PackedScene>(modelPath);
                if (visual.ModelPrototype != null)
                {
                    GameLogger.Info($"BuildingConfigLoader: Loaded model prototype '{modelPath}'");
                    ModelsLoadedCount++;
                }
                else
                {
                    GameLogger.Error($"BuildingConfigLoader: Failed to load model at '{modelPath}'");
                    ModelsFailedCount++;
                }
            }
            catch (System.Exception ex)
            {
                GameLogger.Error($"BuildingConfigLoader: Exception loading model '{modelPath}': {ex.Message}");
                visual.ModelPrototype = null;
                ModelsFailedCount++;
            }
        }

        visual.ModelMaterial = ValidateFilePath(
            ReadString(visualDict, "model_material", ""),
            "visual.model_material"
        );
        visual.AnimationPath = ValidateFilePath(
            ReadString(visualDict, "animation_path", ""),
            "visual.animation_path"
        );
        string animName = ReadString(visualDict, "animation_name", "");
        visual.AnimationName = string.IsNullOrEmpty(animName) ? null : animName;
        visual.Scale = ReadFloat(visualDict, "scale", 1.0f);
        visual.RotationOffset = ReadVector3(visualDict, "rotation_offset", Vector3.Zero);
        visual.ShapeId = ReadString(visualDict, "shape_id", "hexagon").Trim();
        if (string.IsNullOrEmpty(visual.ShapeId))
            visual.ShapeId = "hexagon";
        visual.ShapeSize = ReadFloat(visualDict, "shape_size", 64f);
        visual.ShapeColor = ReadColor(visualDict, "shape_color", visual.ShapeColor);

        return visual;
    }

    private static BuildingDefinition.SoundDefinition ParseSoundDefinition(
        Dictionary<object, object> dict
    )
    {
        var sound = new BuildingDefinition.SoundDefinition();

        if (!dict.ContainsKey("sound"))
            return sound;

        var soundDict = dict["sound"] as Dictionary<object, object>;
        if (soundDict == null)
            return sound;

        sound.Building = ValidateFilePath(
            ReadString(soundDict, "building", ""),
            "sound.building"
        );
        sound.Finished = ValidateFilePath(
            ReadString(soundDict, "finished", ""),
            "sound.finished"
        );
        sound.Idle = ValidateFilePath(ReadString(soundDict, "idle", ""), "sound.idle");
        sound.Fabricating = ValidateFilePath(
            ReadString(soundDict, "fabricating", ""),
            "sound.fabricating"
        );

        return sound;
    }

    private static string? ValidateFilePath(string? path, string fieldName)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (!Godot.FileAccess.FileExists(path))
        {
            GD.PrintErr($"File not found for '{fieldName}': {path} — using default");
            return null;
        }

        return path;
    }

    private static bool TryParseBiomeType(string name, out Biome.BiomeType biomeType)
    {
        biomeType = Biome.BiomeType.Tundra;

        if (string.IsNullOrWhiteSpace(name))
            return false;

        string normalized = name.Replace("_", "").Replace(" ", "").Trim();

        foreach (Biome.BiomeType type in Enum.GetValues(typeof(Biome.BiomeType)))
        {
            string enumName = type.ToString().Replace("_", "");
            if (string.Equals(normalized, enumName, StringComparison.OrdinalIgnoreCase))
            {
                biomeType = type;
                return true;
            }
        }

        return false;
    }

    private static string ReadString(Dictionary<object, object> dict, string key, string fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        var value = dict[key];
        if (value is string s)
            return s;
        if (value is null)
            return fallback;

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

    private static List<string> ReadStringList(Dictionary<object, object> dict, string key)
    {
        var list = new List<string>();
        if (!dict.ContainsKey(key))
            return list;

        var items = dict[key] as List<object>;
        if (items == null)
            return list;

        foreach (var item in items)
        {
            if (item is string s)
                list.Add(s);
        }

        return list;
    }

    private static bool ReadBool(Dictionary<object, object> dict, string key, bool fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        var value = dict[key];
        if (value is bool b)
            return b;

        if (value is string s)
        {
            if (bool.TryParse(s, out bool result))
                return result;
        }

        return fallback;
    }

    private static Vector3 ReadVector3(
        Dictionary<object, object> dict,
        string key,
        Vector3 fallback
    )
    {
        if (!dict.ContainsKey(key))
            return fallback;

        var value = dict[key];
        if (value is List<object> list && list.Count >= 3)
        {
            float x = NodeToFloat(list[0], 0);
            float y = NodeToFloat(list[1], 0);
            float z = NodeToFloat(list[2], 0);
            return new Vector3(x, y, z);
        }

        return fallback;
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
}
