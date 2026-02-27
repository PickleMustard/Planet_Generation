using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Structures.Enums;
using Structures.Resources;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary
{
    public static class ResourceConfigLoader
    {
        public static List<ResourceDefinition> LoadResourceDefinitions(string filePath)
        {
            var definitions = new List<ResourceDefinition>();

            if (!Godot.FileAccess.FileExists(filePath))
            {
                GD.PrintErr($"Resource definition file not found: {filePath}");
                return definitions;
            }

            var validation = YamlValidator.ValidateResourceDefinition(filePath);
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

                if (yamlData.ContainsKey("resources") && yamlData["resources"] is List<object> resourcesList)
                {
                    foreach (var resourceObj in resourcesList)
                    {
                        if (resourceObj is SysDict resourceDict)
                        {
                            var definition = ParseResourceDefinition(resourceDict);
                            definitions.Add(definition);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error loading resource definitions from {filePath}: {e.Message}\n{e.StackTrace}");
            }

            return definitions;
        }

        private static ResourceDefinition ParseResourceDefinition(SysDict dict)
        {
            var definition = new ResourceDefinition
            {
                IdName = ReadString(dict, "id_name", ""),
                ResourceTier = ReadInt(dict, "resource_tier", 0),
                ResourceType = ReadString(dict, "resource_type", ""),
                DisplayColor = ReadColor(dict, "display_color", Colors.White),
                BiomeAffinity = ReadBiomeAffinity(dict, "biome_affinity"),
                MinElevation = ReadFloat(dict, "min_elevation", 0.0f),
                MaxElevation = ReadFloat(dict, "max_elevation", 1.0f)
            };

            return definition;
        }

        private static Color ReadColor(SysDict dict, string key, Color fallback)
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

        private static Dictionary<Biome.BiomeType, float> ReadBiomeAffinity(SysDict dict, string key)
        {
            var affinity = new Dictionary<Biome.BiomeType, float>();

            if (!dict.ContainsKey(key) || dict[key] is not SysDict affinityDict)
                return affinity;

            foreach (var kvp in affinityDict)
            {
                string biomeName = kvp.Key;
                float value = NodeToFloat(kvp.Value, 1.0f);

                if (TryParseBiomeType(biomeName, out Biome.BiomeType biomeType))
                {
                    affinity[biomeType] = value;
                }
                else
                {
                    GD.PrintErr($"Unknown biome type in affinity: {biomeName}");
                }
            }

            return affinity;
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

        private static string ReadString(SysDict dict, string key, string fallback)
        {
            if (!dict.ContainsKey(key))
                return fallback;

            var value = dict[key];
            if (value is string s)
                return s;

            return value?.ToString() ?? fallback;
        }

        private static int ReadInt(SysDict dict, string key, int fallback)
        {
            if (!dict.ContainsKey(key))
                return fallback;

            return NodeToInt(dict[key], fallback);
        }

        private static float ReadFloat(SysDict dict, string key, float fallback)
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
    }
}
