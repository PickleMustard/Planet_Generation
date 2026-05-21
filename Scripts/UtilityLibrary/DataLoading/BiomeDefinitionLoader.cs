using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Structures.Resources;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// YAML loader for <see cref="BiomeDefinition"/> entries. Returns an empty list if the file
/// is missing — the migrator tool produces this file from the legacy enum-keyed configs.
/// </summary>
public static class BiomeDefinitionLoader
{
    public const string DefaultPath = "res://Configuration/Biomes/biomes.yaml";

    public static List<BiomeDefinition>? Load() => Load(DefaultPath);

    public static List<BiomeDefinition>? Load(string filePath)
    {
        if (!Godot.FileAccess.FileExists(filePath))
        {
            GameLogger.Warning($"BiomeDefinitionLoader: file not found {filePath} — returning empty list");
            return new List<BiomeDefinition>();
        }

        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var raw = deserializer.Deserialize<SysDict>(text);
            if (raw == null || !raw.TryGetValue("biomes", out var biomesObj) || biomesObj is not List<object> biomesList)
            {
                GameLogger.Error($"BiomeDefinitionLoader: missing or malformed 'biomes' list in {filePath}");
                return null;
            }

            var result = new List<BiomeDefinition>(biomesList.Count);
            foreach (var item in biomesList)
            {
                if (item is not Dictionary<object, object> entry) continue;
                var def = ParseBiome(entry);
                if (def != null) result.Add(def);
            }
            return result;
        }
        catch (Exception ex)
        {
            GameLogger.Error($"BiomeDefinitionLoader: failed to load {filePath}: {ex.Message}");
            return null;
        }
    }

    private static BiomeDefinition? ParseBiome(Dictionary<object, object> entry)
    {
        var def = new BiomeDefinition
        {
            Id = ReadString(entry, "id", ""),
            DisplayName = ReadString(entry, "display_name", ""),
            DefaultColor = ParseColor(ReadString(entry, "default_color", "#808080")),
            HazardWeight = ReadFloat(entry, "hazard_weight", 0f),
            GeothermalVentProbability = ReadFloat(entry, "geothermal_vent_probability", 0f),
        };

        if (string.IsNullOrEmpty(def.Id))
        {
            GameLogger.Error("BiomeDefinitionLoader: entry missing required 'id'");
            return null;
        }

        if (entry.TryGetValue("color_overrides", out var coObj) && coObj is Dictionary<object, object> coDict)
        {
            foreach (var kvp in coDict)
            {
                string? subtypeId = kvp.Key?.ToString();
                if (string.IsNullOrEmpty(subtypeId)) continue;
                def.ColorOverrides[subtypeId] = ParseColor(kvp.Value?.ToString() ?? "#808080");
            }
        }

        if (entry.TryGetValue("resource_weight_modifiers", out var rwObj) && rwObj is Dictionary<object, object> rwDict)
        {
            foreach (var kvp in rwDict)
            {
                string? resourceId = kvp.Key?.ToString();
                if (string.IsNullOrEmpty(resourceId)) continue;
                def.ResourceWeightModifiers[resourceId] = ParseFloat(kvp.Value, 1.0f);
            }
        }

        if (entry.TryGetValue("tags", out var tagsObj) && tagsObj is List<object> tagsList)
        {
            foreach (var tag in tagsList)
            {
                string? s = tag?.ToString();
                if (!string.IsNullOrEmpty(s)) def.Tags.Add(s);
            }
        }

        return def;
    }

    private static Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Colors.Gray;
        try { return new Color(hex); }
        catch { return Colors.Gray; }
    }

    private static string ReadString(Dictionary<object, object> dict, string key, string fallback)
    {
        if (!dict.TryGetValue(key, out var value) || value == null) return fallback;
        return value as string ?? value.ToString() ?? fallback;
    }

    private static float ReadFloat(Dictionary<object, object> dict, string key, float fallback)
    {
        if (!dict.TryGetValue(key, out var node) || node == null) return fallback;
        return ParseFloat(node, fallback);
    }

    private static float ParseFloat(object? node, float fallback)
    {
        if (node == null) return fallback;
        try
        {
            switch (node)
            {
                case long l: return l;
                case int i: return i;
                case double d: return (float)d;
                case float f: return f;
            }
            if (float.TryParse(node.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch { }
        return fallback;
    }
}
