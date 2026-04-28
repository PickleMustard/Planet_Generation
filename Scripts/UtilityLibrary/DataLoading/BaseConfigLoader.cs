using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Shared utilities for YAML configuration loading across all config loaders.
/// Provides consistent parsing behavior for common data types and directory scanning.
/// </summary>
public static class BaseConfigLoader
{
    /// <summary>
    /// Recursively scans a directory for all .yaml and .yml files.
    /// </summary>
    /// <param name="directory">The directory path to scan (e.g., "res://Configuration/stations/")</param>
    /// <returns>List of file paths found</returns>
    public static List<string> GetYamlFilesRecursive(string directory)
    {
        var files = new List<string>();

        if (!DirAccess.DirExistsAbsolute(directory))
        {
            GameLogger.Warning($"BaseConfigLoader: Directory not found: {directory}");
            return files;
        }

        var currentFiles = DirAccess.GetFilesAt(directory);
        foreach (var file in currentFiles)
        {
            if (file.EndsWith(".yaml") || file.EndsWith(".yml"))
                files.Add(directory + file);
        }

        var subdirs = DirAccess.GetDirectoriesAt(directory);
        foreach (var subdir in subdirs)
            files.AddRange(GetYamlFilesRecursive(directory + subdir + "/"));

        return files;
    }

    /// <summary>
    /// Reads a string value from a dictionary with a fallback default.
    /// </summary>
    /// <param name="dict">The dictionary to read from</param>
    /// <param name="key">The key to look up</param>
    /// <param name="fallback">Default value if key not found</param>
    /// <returns>The string value or fallback</returns>
    public static string ReadString(Dictionary<object, object> dict, string key, string fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        var value = dict[key];
        return value is string s ? s : value?.ToString() ?? fallback;
    }

    /// <summary>
    /// Reads an integer value from a dictionary with a fallback default.
    /// </summary>
    /// <param name="dict">The dictionary to read from</param>
    /// <param name="key">The key to look up</param>
    /// <param name="fallback">Default value if key not found or parsing fails</param>
    /// <returns>The integer value or fallback</returns>
    public static int ReadInt(Dictionary<object, object> dict, string key, int fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        return NodeToInt(dict[key], fallback);
    }

    /// <summary>
    /// Reads a float value from a dictionary with a fallback default.
    /// </summary>
    /// <param name="dict">The dictionary to read from</param>
    /// <param name="key">The key to look up</param>
    /// <param name="fallback">Default value if key not found or parsing fails</param>
    /// <returns>The float value or fallback</returns>
    public static float ReadFloat(Dictionary<object, object> dict, string key, float fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        return NodeToFloat(dict[key], fallback);
    }

    /// <summary>
    /// Reads a boolean value from a dictionary with a fallback default.
    /// </summary>
    /// <param name="dict">The dictionary to read from</param>
    /// <param name="key">The key to look up</param>
    /// <param name="fallback">Default value if key not found</param>
    /// <returns>The boolean value or fallback</returns>
    public static bool ReadBool(Dictionary<object, object> dict, string key, bool fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        var value = dict[key];
        if (value is bool b)
            return b;

        if (value is string s)
            return bool.TryParse(s, out bool result) && result;

        return fallback;
    }

    /// <summary>
    /// Reads a resource dictionary (resource name -> amount) from a dictionary.
    /// </summary>
    /// <param name="dict">The dictionary to read from</param>
    /// <param name="key">The key for the resource dictionary</param>
    /// <returns>Dictionary of resource names to amounts</returns>
    public static Dictionary<string, int> ReadResourceDict(Dictionary<object, object> dict, string key)
    {
        var result = new Dictionary<string, int>();

        if (!dict.ContainsKey(key))
            return result;

        if (dict[key] is not Dictionary<object, object> resourceDict)
            return result;

        foreach (var kvp in resourceDict)
        {
            string resourceName = kvp.Key?.ToString() ?? "";
            int amount = NodeToInt(kvp.Value, 0);

            if (!string.IsNullOrEmpty(resourceName) && amount > 0)
                result[resourceName] = amount;
        }

        return result;
    }

    /// <summary>
    /// Reads a list of strings from a dictionary.
    /// </summary>
    /// <param name="dict">The dictionary to read from</param>
    /// <param name="key">The key for the string list</param>
    /// <returns>List of strings</returns>
    public static List<string> ReadStringList(Dictionary<object, object> dict, string key)
    {
        var list = new List<string>();

        if (!dict.ContainsKey(key))
            return list;

        if (dict[key] is not List<object> items)
            return list;

        foreach (var item in items)
        {
            if (item is string s)
                list.Add(s);
        }

        return list;
    }

    /// <summary>
    /// Converts a YAML node object to an integer.
    /// </summary>
    /// <param name="node">The node to convert</param>
    /// <param name="fallback">Default value if conversion fails</param>
    /// <returns>The integer value or fallback</returns>
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
            GD.PrintErr($"BaseConfigLoader: Error parsing node to int: {e.Message}");
        }

        return fallback;
    }

    /// <summary>
    /// Converts a YAML node object to a float.
    /// </summary>
    /// <param name="node">The node to convert</param>
    /// <param name="fallback">Default value if conversion fails</param>
    /// <returns>The float value or fallback</returns>
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
            GD.PrintErr($"BaseConfigLoader: Error parsing node to float: {e.Message}");
        }

        return fallback;
    }

    /// <summary>
    /// Reads a Vector3 value from a dictionary with a fallback default.
    /// Expects the value to be a list of 3 floats [x, y, z].
    /// </summary>
    /// <param name="dict">The dictionary to read from</param>
    /// <param name="key">The key to look up</param>
    /// <param name="fallback">Default value if key not found or parsing fails</param>
    /// <returns>The Vector3 value or fallback</returns>
    public static Vector3 ReadVector3(Dictionary<object, object> dict, string key, Vector3 fallback)
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
}
