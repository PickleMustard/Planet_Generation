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
/// YAML loader for <see cref="SubtypeDefinition"/> entries.
/// Reads one file per <see cref="BodyFamily"/> under <c>Configuration/Subtypes/</c>.
/// </summary>
public static class SubtypeDefinitionLoader
{
    public const string DefaultDir = "res://Configuration/Subtypes";

    private static readonly (BodyFamily family, string fileName)[] _files =
    {
        (BodyFamily.RockyPlanet,  "rocky_subtypes.yaml"),
        (BodyFamily.GasGiant,     "gas_giant_subtypes.yaml"),
        (BodyFamily.IceGiant,     "ice_giant_subtypes.yaml"),
        (BodyFamily.DwarfPlanet,  "dwarf_planet_subtypes.yaml"),
        (BodyFamily.Star,         "star_subtypes.yaml"),
        (BodyFamily.NeutronStar,  "neutron_star_subtypes.yaml"),
        (BodyFamily.BlackHole,    "black_hole_subtypes.yaml"),
        (BodyFamily.Satellite,    "satellite_subtypes.yaml"),
        (BodyFamily.Belt,         "belt_subtypes.yaml"),
    };

    public static List<SubtypeDefinition>? LoadAll() => LoadAll(DefaultDir);

    public static List<SubtypeDefinition>? LoadAll(string dirPath)
    {
        var result = new List<SubtypeDefinition>();

        foreach (var (family, fileName) in _files)
        {
            string path = $"{dirPath}/{fileName}";
            if (!Godot.FileAccess.FileExists(path))
            {
                GameLogger.Warning($"SubtypeDefinitionLoader: file not found {path} — skipping family {family}");
                continue;
            }

            var family_defs = LoadFile(path, family);
            if (family_defs != null) result.AddRange(family_defs);
        }

        return result;
    }

    public static List<SubtypeDefinition>? LoadFile(string filePath, BodyFamily family)
    {
        try
        {
            using var f = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var raw = deserializer.Deserialize<SysDict>(text);
            if (raw == null || !raw.TryGetValue("subtypes", out var subObj) || subObj is not List<object> subList)
            {
                GameLogger.Error($"SubtypeDefinitionLoader: missing or malformed 'subtypes' list in {filePath}");
                return null;
            }

            var result = new List<SubtypeDefinition>(subList.Count);
            foreach (var item in subList)
            {
                if (item is not Dictionary<object, object> entry) continue;
                var def = ParseSubtype(entry, family);
                if (def != null) result.Add(def);
            }
            return result;
        }
        catch (Exception ex)
        {
            GameLogger.Error($"SubtypeDefinitionLoader: failed to load {filePath}: {ex.Message}");
            return null;
        }
    }

    private static SubtypeDefinition? ParseSubtype(Dictionary<object, object> entry, BodyFamily family)
    {
        var def = new SubtypeDefinition
        {
            Id = ReadString(entry, "id", ""),
            DisplayName = ReadString(entry, "display_name", ""),
            Family = family,
            AtmosphereMin = ReadFloat(entry, "atmosphere_min", 0f),
            AtmosphereMax = ReadFloat(entry, "atmosphere_max", 0f),
            BaseHazard = ReadFloat(entry, "base_hazard", 0f),
            BaseResourceWeight = ReadFloat(entry, "base_resource_weight", 1.0f),
            MoistureMode = ReadString(entry, "moisture_mode", "whittaker"),
        };

        if (string.IsNullOrEmpty(def.Id))
        {
            GameLogger.Error("SubtypeDefinitionLoader: entry missing required 'id'");
            return null;
        }

        if (entry.TryGetValue("moisture_params", out var mpObj) && mpObj is Dictionary<object, object> mpDict)
        {
            foreach (var kvp in mpDict)
            {
                string? key = kvp.Key?.ToString();
                if (string.IsNullOrEmpty(key)) continue;
                def.MoistureParams[key] = ParseFloat(kvp.Value, 0f);
            }
        }

        if (entry.TryGetValue("assigner_rules", out var arObj) && arObj is List<object> arList)
        {
            foreach (var r in arList)
            {
                if (r is not Dictionary<object, object> rd) continue;
                var rule = new BiomeRuleDefinition { BiomeId = ReadString(rd, "biome_id", "") };
                if (string.IsNullOrEmpty(rule.BiomeId)) continue;
                if (rd.TryGetValue("when", out var w) && w is Dictionary<object, object> wd)
                {
                    if (wd.ContainsKey("height_above")) rule.HeightAbove = ReadFloat(wd, "height_above", 0f);
                    if (wd.ContainsKey("height_below")) rule.HeightBelow = ReadFloat(wd, "height_below", 0f);
                    if (wd.ContainsKey("moisture_above")) rule.MoistureAbove = ReadFloat(wd, "moisture_above", 0f);
                    if (wd.ContainsKey("moisture_below")) rule.MoistureBelow = ReadFloat(wd, "moisture_below", 0f);
                    if (wd.ContainsKey("abs_latitude_above")) rule.AbsLatitudeAbove = ReadFloat(wd, "abs_latitude_above", 0f);
                    if (wd.ContainsKey("abs_latitude_below")) rule.AbsLatitudeBelow = ReadFloat(wd, "abs_latitude_below", 0f);
                }
                def.AssignerRules.Add(rule);
            }
        }

        ReadStringList(entry, "resource_groups", def.ResourceGroups);
        ReadStringList(entry, "add_resources", def.AddResources);
        ReadStringList(entry, "remove_resources", def.RemoveResources);

        ReadRangeBlock(entry, "mesh", def.MeshRanges);
        ReadVerticesPerEdge(entry, "mesh", def.VerticesPerEdgeRanges);
        ReadRangeBlock(entry, "tectonics", def.TectonicRanges);
        ReadRangeBlock(entry, "spherical_harmonics", def.SphericalHarmonicsRanges);

        if (entry.TryGetValue("tectonics", out var tectNode) && tectNode is Dictionary<object, object> tectBlock)
            def.SampleVelocityAtEdgeMidpoint = ReadBool(tectBlock, "sample_velocity_at_edge_midpoint", true);

        return def;
    }

    private static void ReadRangeBlock(Dictionary<object, object> entry, string blockKey, Dictionary<string, FloatRange> target)
    {
        if (!entry.TryGetValue(blockKey, out var node) || node is not Dictionary<object, object> block) return;
        foreach (var kvp in block)
        {
            string? key = kvp.Key?.ToString();
            if (string.IsNullOrEmpty(key)) continue;
            // vertices_per_edge is a list-of-ranges, handled by ReadVerticesPerEdge.
            if (key == "vertices_per_edge") continue;
            // sample_velocity_at_edge_midpoint is a bool, not a range; read separately.
            if (key == "sample_velocity_at_edge_midpoint") continue;
            if (TryParseRange(kvp.Value, out var range))
                target[key] = range;
            else
                GameLogger.Warning($"SubtypeDefinitionLoader: '{blockKey}.{key}' is not a 2-element [min, max] list; skipping.");
        }
    }

    private static void ReadVerticesPerEdge(Dictionary<object, object> entry, string blockKey, List<FloatRange> target)
    {
        if (!entry.TryGetValue(blockKey, out var node) || node is not Dictionary<object, object> block) return;
        if (!block.TryGetValue("vertices_per_edge", out var vpeNode) || vpeNode is not List<object> rows) return;
        target.Clear();
        int idx = 0;
        foreach (var row in rows)
        {
            if (TryParseRange(row, out var r))
                target.Add(r);
            else
                GameLogger.Warning($"SubtypeDefinitionLoader: '{blockKey}.vertices_per_edge[{idx}]' is not a 2-element [min, max] list; skipping.");
            idx++;
        }
    }

    private static bool TryParseRange(object? node, out FloatRange range)
    {
        range = default;
        if (node is not List<object> list || list.Count != 2) return false;
        float min = ParseFloat(list[0], float.NaN);
        float max = ParseFloat(list[1], float.NaN);
        if (float.IsNaN(min) || float.IsNaN(max)) return false;
        range = new FloatRange(min, max);
        return true;
    }

    private static void ReadStringList(Dictionary<object, object> dict, string key, List<string> target)
    {
        if (!dict.TryGetValue(key, out var node) || node is not List<object> list) return;
        foreach (var item in list)
        {
            string? s = item?.ToString();
            if (!string.IsNullOrEmpty(s)) target.Add(s);
        }
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

    private static bool ReadBool(Dictionary<object, object> dict, string key, bool fallback)
    {
        if (!dict.TryGetValue(key, out var node) || node == null) return fallback;
        if (node is bool b) return b;
        return bool.TryParse(node.ToString(), out var v) ? v : fallback;
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
