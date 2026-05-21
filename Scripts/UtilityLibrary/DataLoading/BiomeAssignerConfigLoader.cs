using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using ProceduralGeneration.BiomeSystem;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

public static class BiomeAssignerConfigLoader
{
    public const string DefaultPath = "res://Configuration/Biomes/biome_assigner_config.yaml";

    public static BiomeAssignerConfig? Load() => Load(DefaultPath);

    public static BiomeAssignerConfig? Load(string filePath)
    {
        if (!Godot.FileAccess.FileExists(filePath))
        {
            GameLogger.Error($"BiomeAssignerConfigLoader: file not found {filePath}");
            return null;
        }

        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var raw = deserializer.Deserialize<SysDict>(text);
            if (raw == null || !raw.ContainsKey("assigners"))
            {
                GameLogger.Error($"BiomeAssignerConfigLoader: missing 'assigners' key in {filePath}");
                return null;
            }

            var assignersNode = raw["assigners"] as Dictionary<object, object>;
            if (assignersNode == null)
            {
                GameLogger.Error($"BiomeAssignerConfigLoader: 'assigners' is not a map in {filePath}");
                return null;
            }

            var cfg = new BiomeAssignerConfig();
            foreach (var kvp in assignersNode)
            {
                string? key = kvp.Key?.ToString();
                if (string.IsNullOrEmpty(key)) continue;

                // YAML keys may be either the legacy enum name ("Temperate") or the stable
                // ID ("subtype_rocky_temperate"). Normalize to stable ID.
                string subtypeId = key.StartsWith("subtype_")
                    ? key
                    : (Enum.TryParse<RockyPlanetSubtype>(key, ignoreCase: true, out var enumVal)
                        ? BiomeIdMapper.RockyPlanetSubtypeToId(enumVal)
                        : key);

                if (kvp.Value is not Dictionary<object, object> entryDict)
                {
                    GameLogger.Warning($"BiomeAssignerConfigLoader: entry for '{key}' is not a map");
                    continue;
                }

                var entry = new BiomeAssignerEntry
                {
                    Moisture = ParseMoisture(entryDict),
                    Rules = ParseRules(entryDict, key),
                };
                cfg.Assigners[subtypeId] = entry;
            }

            return cfg;
        }
        catch (Exception ex)
        {
            GameLogger.Error($"BiomeAssignerConfigLoader: failed to load {filePath}: {ex.Message}");
            return null;
        }
    }

    private static MoistureParams ParseMoisture(Dictionary<object, object> entry)
    {
        var p = new MoistureParams();
        if (!entry.TryGetValue("moisture", out var moistObj) || moistObj is not Dictionary<object, object> m)
        {
            return p;
        }

        string modeStr = ReadString(m, "mode", "whittaker");
        p.Mode = string.Equals(modeStr, "uniform_random", StringComparison.OrdinalIgnoreCase)
            ? MoistureMode.UniformRandom
            : MoistureMode.Whittaker;

        p.BaseOffset = ReadFloat(m, "base_offset", 0f);
        p.LatitudeFactorDivisor = ReadFloat(m, "latitude_factor_divisor", 9f);
        p.SizeFactorDivisor = ReadFloat(m, "size_factor_divisor", 100f);
        p.RandomMin = ReadFloat(m, "random_min", 0f);
        p.RandomMax = ReadFloat(m, "random_max", 0f);
        p.MaxMoisture = ReadFloat(m, "max_moisture", 2.7f);
        p.ConstantOffset = ReadFloat(m, "constant_offset", 0f);
        return p;
    }

    private static List<BiomeRule> ParseRules(Dictionary<object, object> entry, string subtypeName)
    {
        var rules = new List<BiomeRule>();
        if (!entry.TryGetValue("rules", out var rulesObj) || rulesObj is not List<object> list)
        {
            GameLogger.Warning($"BiomeAssignerConfigLoader: '{subtypeName}' has no 'rules' list");
            return rules;
        }

        foreach (var item in list)
        {
            if (item is not Dictionary<object, object> ruleDict) continue;

            string biomeName = ReadString(ruleDict, "biome", "");
            // Biome refs may be enum name ("Mountain") or stable ID ("biome_mountain"). Normalize to ID.
            string? biomeId = biomeName.StartsWith("biome_")
                ? biomeName
                : (Enum.TryParse<Biome.BiomeType>(biomeName, ignoreCase: true, out var v)
                    ? BiomeIdMapper.BiomeTypeToId(v)
                    : null);
            if (string.IsNullOrEmpty(biomeId))
            {
                GameLogger.Warning($"BiomeAssignerConfigLoader: '{subtypeName}' has unknown biome '{biomeName}'");
                continue;
            }

            var rule = new BiomeRule { Biome = biomeId };
            if (ruleDict.TryGetValue("when", out var whenObj) && whenObj is Dictionary<object, object> w)
            {
                rule.When = ParseConditions(w);
            }

            rules.Add(rule);
        }

        return rules;
    }

    private static BiomeRuleConditions ParseConditions(Dictionary<object, object> w)
    {
        var c = new BiomeRuleConditions();
        if (w.ContainsKey("height_above")) c.HeightAbove = ReadFloat(w, "height_above", 0f);
        if (w.ContainsKey("height_below")) c.HeightBelow = ReadFloat(w, "height_below", 0f);
        if (w.ContainsKey("moisture_above")) c.MoistureAbove = ReadFloat(w, "moisture_above", 0f);
        if (w.ContainsKey("moisture_below")) c.MoistureBelow = ReadFloat(w, "moisture_below", 0f);
        if (w.ContainsKey("abs_latitude_above")) c.AbsLatitudeAbove = ReadFloat(w, "abs_latitude_above", 0f);
        if (w.ContainsKey("abs_latitude_below")) c.AbsLatitudeBelow = ReadFloat(w, "abs_latitude_below", 0f);
        return c;
    }

    private static string ReadString(Dictionary<object, object> dict, string key, string fallback)
    {
        if (!dict.TryGetValue(key, out var value) || value == null) return fallback;
        return value as string ?? value.ToString() ?? fallback;
    }

    private static float ReadFloat(Dictionary<object, object> dict, string key, float fallback)
    {
        if (!dict.TryGetValue(key, out var node) || node == null) return fallback;
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
        catch
        {
        }
        return fallback;
    }
}
