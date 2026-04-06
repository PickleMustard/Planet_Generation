using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Structures.Enums;
using Structures.Resources;
using Structures.Transfers;
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
            BuildingLimit = ReadInt(dict, "building_limit", -1),
            BuildingTime = ReadFloat(dict, "building_time", 60.0f),
            WorkRequired = ReadFloat(dict, "work_required", 100.0f),
            Placement = ParsePlacementRequirements(dict),
            RequiredResources = ParseRequiredResources(dict),
            Production = ParseProductionDefinition(dict),
            Visual = ParseVisualDefinition(dict),
            Sound = ParseSoundDefinition(dict),
            TransferStation = ParseTransferStationDefinition(dict),
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
                    // Check for wildcard operator
                    bool hasWildcard = false;
                    var otherBiomes = new List<string>();

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
                                otherBiomes.Add(biomeName);
                            }
                        }
                    }

                    if (hasWildcard)
                    {
                        placement.AllowAnyBiome = true;

                        // Warn if other biomes are defined alongside wildcard
                        if (otherBiomes.Count > 0)
                        {
                            GD.PushWarning(
                                $"BuildingConfigLoader: Wildcard '*' allows all biomes, " +
                                $"but additional biomes were also specified: " +
                                $"{string.Join(", ", otherBiomes)}. " +
                                $"These additional biomes will be ignored."
                            );
                        }
                    }
                    else
                    {
                        // Parse normal biome names
                        foreach (var biomeName in otherBiomes)
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

        production.DefaultRecipe = ReadString(productionDict, "default_recipe", "");
        production.AlternativeRecipes = ReadStringList(productionDict, "alternative_recipes");
        production.InputStorageAmount = ReadInt(productionDict, "input_storage_amount", 0);
        production.OutputStorageAmount = ReadInt(productionDict, "output_storage_amount", 0);
        production.ProductionSpeed = ReadFloat(productionDict, "production_speed", 1.0f);

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

        visual.ModelPath = ValidateFilePath(
            ReadString(visualDict, "model_path", ""),
            "visual.model_path"
        );
        visual.ModelMaterial = ValidateFilePath(
            ReadString(visualDict, "model_material", ""),
            "visual.model_material"
        );
        visual.AnimationPath = ValidateFilePath(
            ReadString(visualDict, "animation_path", ""),
            "visual.animation_path"
        );
        visual.Scale = ReadFloat(visualDict, "scale", 1.0f);
        visual.RotationOffset = ReadVector3(visualDict, "rotation_offset", Vector3.Zero);

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

    private static TransferStationDefinition? ParseTransferStationDefinition(
        Dictionary<object, object> dict
    )
    {
        if (!dict.ContainsKey("transfer_station"))
            return null;

        var stationDict = dict["transfer_station"] as Dictionary<object, object>;
        if (stationDict == null)
            return null;

        return new TransferStationDefinition
        {
            CargoCapacity = ReadFloat(stationDict, "cargo_capacity", 500.0f),
            VehicleSpeed = ReadFloat(stationDict, "vehicle_speed", 50.0f),
            MaxConcurrentTransfers = ReadInt(stationDict, "max_concurrent_transfers", 2),
        };
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
