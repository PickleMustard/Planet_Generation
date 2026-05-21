using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace Constructables.Buildings;

/// <summary>
/// Shared type-safe readers for behavior Configure() dicts.
/// Mirrors BuildingConfigLoader's NodeToXxx helpers but works on
/// Dictionary&lt;string, object&gt; instead of Dictionary&lt;object, object&gt;.
/// </summary>
public static class BehaviorConfigHelper
{
    public static string? ReadString(Dictionary<string, object> d, string key, string? fallback)
    {
        if (!d.TryGetValue(key, out var val)) return fallback;
        return val?.ToString() ?? fallback;
    }

    public static float ReadFloat(Dictionary<string, object> d, string key, float fallback)
    {
        if (!d.TryGetValue(key, out var val)) return fallback;
        return NodeToFloat(val, fallback);
    }

    public static int ReadInt(Dictionary<string, object> d, string key, int fallback)
    {
        if (!d.TryGetValue(key, out var val)) return fallback;
        return NodeToInt(val, fallback);
    }

    public static bool ReadBool(Dictionary<string, object> d, string key, bool fallback)
    {
        if (!d.TryGetValue(key, out var val)) return fallback;
        if (val is bool b) return b;
        if (val is string s && bool.TryParse(s, out var result)) return result;
        return fallback;
    }

    public static List<string> ReadStringList(Dictionary<string, object> d, string key)
    {
        var list = new List<string>();
        if (!d.TryGetValue(key, out var val)) return list;
        if (val is not List<object> items) return list;
        foreach (var item in items)
            if (item is string s) list.Add(s);
        return list;
    }

    public static Dictionary<string, int> ReadStringIntDict(Dictionary<string, object> d, string key)
    {
        var result = new Dictionary<string, int>();
        if (!d.TryGetValue(key, out var val)) return result;
        if (val is not Dictionary<object, object> dict) return result;
        foreach (var kvp in dict)
        {
            string k = kvp.Key?.ToString() ?? "";
            int v = NodeToInt(kvp.Value, 0);
            if (!string.IsNullOrEmpty(k) && v > 0)
                result[k] = v;
        }
        return result;
    }

    public static float NodeToFloat(object node, float fallback)
    {
        try
        {
            if (node is long l) return (float)l;
            if (node is double d) return (float)d;
            if (node is float f) return f;
            if (node is int i) return (float)i;

            var s = node?.ToString();
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch (Exception e)
        {
            GD.PrintErr($"BehaviorConfigHelper: Error parsing node to float: {e.Message}");
        }

        return fallback;
    }

    public static int NodeToInt(object node, int fallback)
    {
        try
        {
            if (node is long l) return (int)l;
            if (node is int i) return i;
            if (node is double d) return (int)d;
            if (node is float f) return (int)f;

            var s = node?.ToString();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch (Exception e)
        {
            GD.PrintErr($"BehaviorConfigHelper: Error parsing node to int: {e.Message}");
        }

        return fallback;
    }
}
