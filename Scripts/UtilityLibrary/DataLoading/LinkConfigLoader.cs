using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using UtilityLibrary;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

public static class LinkConfigLoader
{
    public static List<LinkProfile> LoadLinkProfiles(string filePath)
    {
        var definitions = new List<LinkProfile>();

        string? text = BaseConfigLoader.ReadAllText(filePath);
        if (text == null)
        {
            GameLogger.Error($"Link profile definition file not found: {filePath}");
            return definitions;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(text);

            if (yamlData.ContainsKey("link_profiles"))
            {
                var profilesList = yamlData["link_profiles"] as List<object>;
                foreach (var profileObj in profilesList!)
                {
                    if (profileObj is Dictionary<object, object> profileDict)
                    {
                        var definition = ParseLinkProfile(profileDict);
                        definitions.Add(definition);
                    }
                }
            }
        }
        catch (Exception e)
        {
            GameLogger.Error($"Error loading link profiles from {filePath}: {e.Message}\n{e.StackTrace}");
            throw;
        }

        return definitions;
    }

    private static LinkProfile ParseLinkProfile(Dictionary<object, object> dict)
    {
        string idName = ReadString(dict, "id_name", "");

        string stateRaw = ReadString(dict, "state_of_matter", "");
        if (string.IsNullOrWhiteSpace(stateRaw))
        {
            throw new InvalidOperationException(
                $"Link profile '{idName}' is missing required 'state_of_matter' (expected 'solid' or 'fluid').");
        }

        var profile = new LinkProfile
        {
            IdName = idName,
            Tier = ReadInt(dict, "tier", 0),
            TransportSpeed = ReadFloat(dict, "transport_speed", 0f),
            PackageSize = ReadInt(dict, "package_size", 0),
            BundleTime = ReadInt(dict, "bundle_time", 0),
            SlotCapacity = ReadInt(dict, "slot_capacity", 0),
            StateOfMatter = StateOfMatterExtensions.Parse(stateRaw),
        };

        return profile;
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
            GameLogger.Error($"Error parsing node to int: {e.Message}");
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
            GameLogger.Error($"Error parsing node to float: {e.Message}");
        }

        return fallback;
    }
}
