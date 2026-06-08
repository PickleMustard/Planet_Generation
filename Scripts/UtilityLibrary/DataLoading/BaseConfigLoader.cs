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
    /// Resolves a res:// path to one that <see cref="Godot.FileAccess"/> can actually open.
    /// Exported builds remap resource paths for compression (e.g. foo.yaml -> foo.yaml.remap
    /// pointing at the packed file). Godot's ResourceLoader resolves these transparently, but
    /// raw FileAccess/DirAccess do not. This mirrors that resolution manually:
    ///   1. the path itself, 2. its ".remap" sidecar, 3. ".yaml"/".yml" extension fallbacks
    ///      (each re-checked for a ".remap").
    /// In the editor (no remaps) this returns the original path on the first probe.
    /// </summary>
    /// <param name="path">A res:// (or user://) path.</param>
    /// <returns>An openable path, or null if nothing resolves.</returns>
    public static string? ResolveResPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // a) Direct hit (editor, or non-remapped export).
        if (Godot.FileAccess.FileExists(path))
            return path;

        // b) .remap sidecar resolution.
        string? viaRemap = ResolveViaRemap(path);
        if (viaRemap != null)
            return viaRemap;

        // c) Extension fallbacks (caller may have passed an extension-less path).
        foreach (var ext in new[] { ".yaml", ".yml" })
        {
            string candidate = path + ext;
            if (Godot.FileAccess.FileExists(candidate))
                return candidate;

            string? candidateRemap = ResolveViaRemap(candidate);
            if (candidateRemap != null)
                return candidateRemap;
        }

        return null;
    }

    /// <summary>
    /// If a "<paramref name="path"/>.remap" sidecar exists, parses it and returns the remap target.
    /// </summary>
    private static string? ResolveViaRemap(string path)
    {
        string remapPath = path + ".remap";
        if (!Godot.FileAccess.FileExists(remapPath))
            return null;

        using var rf = Godot.FileAccess.Open(remapPath, Godot.FileAccess.ModeFlags.Read);
        if (rf == null)
            return null;

        string target = ParseRemapTarget(rf.GetAsText());
        return string.IsNullOrEmpty(target) ? null : target;
    }

    /// <summary>
    /// Parses the target path out of a Godot ".remap" file (INI format with a
    /// <c>path="res://..."</c> entry under <c>[remap]</c>).
    /// </summary>
    public static string ParseRemapTarget(string remapContents)
    {
        if (string.IsNullOrEmpty(remapContents))
            return "";

        foreach (var raw in remapContents.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("path"))
                continue;

            int eq = line.IndexOf('=');
            if (eq < 0)
                continue;

            var value = line.Substring(eq + 1).Trim().Trim('"');
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return "";
    }

    /// <summary>
    /// Export-safe replacement for <c>FileAccess.FileExists</c>. Returns true if the path
    /// resolves (directly or via a ".remap" sidecar).
    /// </summary>
    public static bool ResExists(string path) => ResolveResPath(path) != null;

    /// <summary>
    /// Export-safe text read. Resolves remaps, opens the file, and returns its contents.
    /// Returns null (with a warning) if the path cannot be resolved or opened.
    /// </summary>
    public static string? ReadAllText(string path)
    {
        string? resolved = ResolveResPath(path);
        if (resolved == null)
        {
            GameLogger.Warning($"BaseConfigLoader.ReadAllText: cannot resolve path: {path}");
            return null;
        }

        using var file = Godot.FileAccess.Open(resolved, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GameLogger.Warning($"BaseConfigLoader.ReadAllText: failed to open: {resolved}");
            return null;
        }

        return file.GetAsText();
    }

    /// <summary>
    /// Returns the .yaml/.yml files directly inside a directory (non-recursive).
    /// Export-safe: strips ".remap"/".import" suffixes from listed entries before filtering,
    /// so remapped files are still discovered, and returns the logical (un-suffixed) paths.
    /// </summary>
    public static List<string> GetYamlFilesInDir(string directory)
    {
        var files = new List<string>();

        if (!DirAccess.DirExistsAbsolute(directory))
        {
            GameLogger.Warning($"BaseConfigLoader: Directory not found: {directory}");
            return files;
        }

        // Normalize so callers may pass a directory with or without a trailing slash.
        string prefix = directory.EndsWith("/") ? directory : directory + "/";

        var seen = new HashSet<string>();
        foreach (var entry in DirAccess.GetFilesAt(directory))
        {
            string name = entry;
            if (name.EndsWith(".remap"))
                name = name.Substring(0, name.Length - ".remap".Length);
            if (name.EndsWith(".import"))
                name = name.Substring(0, name.Length - ".import".Length);

            if (!seen.Add(name)) // dedup foo.yaml + foo.yaml.remap
                continue;

            if (name.EndsWith(".yaml") || name.EndsWith(".yml"))
                files.Add(prefix + name);
        }

        return files;
    }

    /// <summary>
    /// Recursively scans a directory for all .yaml and .yml files.
    /// Export-safe: see <see cref="GetYamlFilesInDir"/>. Subdirectories are not remapped,
    /// so directory enumeration needs no suffix handling.
    /// </summary>
    /// <param name="directory">The directory path to scan (e.g., "res://Configuration/stations/")</param>
    /// <returns>List of logical file paths found</returns>
    public static List<string> GetYamlFilesRecursive(string directory)
    {
        var files = GetYamlFilesInDir(directory);

        if (!DirAccess.DirExistsAbsolute(directory))
            return files;

        string prefix = directory.EndsWith("/") ? directory : directory + "/";
        var subdirs = DirAccess.GetDirectoriesAt(directory);
        foreach (var subdir in subdirs)
            files.AddRange(GetYamlFilesRecursive(prefix + subdir + "/"));

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
