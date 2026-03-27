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

public static class BuildingConfigLoader
{
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
        var definition = new BuildingDefinition
        {
            IdName = ReadString(dict, "id_name", ""),
            DisplayName = ReadString(dict, "display_name", ""),
            Description = ReadString(dict, "description", ""),
            Category = ReadString(dict, "category", ""),
            BuildingTime = ReadFloat(dict, "building_time", 60.0f),
            WorkRequired = ReadFloat(dict, "work_required", 100.0f),
            Placement = ParsePlacementRequirements(dict),
            RequiredResources = ParseRequiredResources(dict),
            Production = ParseProductionDefinition(dict),
            Visual = ParseVisualDefinition(dict),
        };

        return definition;
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
                foreach (var biomeObj in biomesList)
                {
                    if (biomeObj is string biomeName)
                    {
                        if (TryParseBiomeType(biomeName, out Biome.BiomeType biomeType))
                        {
                            placement.Biomes.Add(biomeType);
                        }
                        else
                        {
                            GD.PrintErr($"Unknown biome type in building placement: {biomeName}");
                        }
                    }
                }
            }
        }

        return placement;
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

    private static BuildingDefinition.ProductionDefinition ParseProductionDefinition(
        Dictionary<object, object> dict
    )
    {
        var production = new BuildingDefinition.ProductionDefinition();

        if (!dict.ContainsKey("production"))
            return production;

        var productionDict = dict["production"] as Dictionary<object, object>;
        if (productionDict == null)
            return production;

        production.ExtractionRate = ReadFloat(productionDict, "extraction_rate", 0.0f);

        if (productionDict.ContainsKey("resources"))
        {
            var resourcesList = productionDict["resources"] as List<object>;
            if (resourcesList != null)
            {
                foreach (var resourceObj in resourcesList)
                {
                    if (resourceObj is string resourceName)
                    {
                        production.Resources.Add(resourceName);
                    }
                }
            }
        }

        if (productionDict.ContainsKey("recipes"))
        {
            var recipesList = productionDict["recipes"] as List<object>;
            if (recipesList != null)
            {
                foreach (var recipeObj in recipesList)
                {
                    if (recipeObj is string recipeId)
                    {
                        production.Recipes.Add(recipeId);
                    }
                }
            }
        }

        return production;
    }

    private static BuildingDefinition.VisualDefinition ParseVisualDefinition(
        Dictionary<object, object> dict
    )
    {
        var visual = new BuildingDefinition.VisualDefinition();

        if (!dict.ContainsKey("visual"))
            return visual;

        var visualDict = dict["visual"] as Dictionary<object, object>;
        if (visualDict == null)
            return visual;

        visual.ModelPath = ReadString(visualDict, "model_path", "");
        visual.Scale = ReadFloat(visualDict, "scale", 1.0f);
        visual.RotationOffset = ReadVector3(visualDict, "rotation_offset", Vector3.Zero);

        return visual;
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
