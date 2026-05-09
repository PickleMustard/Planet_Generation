using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Structures.Resources;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

public static class RecipeConfigLoader
{
    public static List<RecipeDefinition> LoadRecipeDefinitions(string filePath)
    {
        var definitions = new List<RecipeDefinition>();

        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"Recipe definition file not found: {filePath}");
            return definitions;
        }

        var validation = YamlValidator.ValidateRecipeDefinition(filePath);
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

            if (yamlData.ContainsKey("recipes"))
            {
                var recipesList = yamlData["recipes"] as List<object>;
                if (recipesList != null)
                {
                    foreach (var recipeObj in recipesList)
                    {
                        if (recipeObj is Dictionary<object, object> recipeDict)
                        {
                            var definition = ParseRecipeDefinition(recipeDict);
                            definitions.Add(definition);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr(
                $"Error loading recipe definitions from {filePath}: {e.Message}\n{e.StackTrace}"
            );
        }

        return definitions;
    }

    private static RecipeDefinition ParseRecipeDefinition(Dictionary<object, object> dict)
    {
        string recipeId = ReadString(dict, "recipe_id", "");

        var definition = new RecipeDefinition
        {
            RecipeId = recipeId,
            DisplayName = ReadString(dict, "display_name", ""),
            Description = ReadString(dict, "description", ""),
            Category = ReadString(dict, "category", ""),
            WorkRequired = ReadFloat(dict, "work_required", 10.0f),
            InputResources = ParseResourceList(dict, "input_resources"),
            OutputResources = ParseResourceList(dict, "output_resources"),
            Icon = ParseIconDefinition(dict, $"recipe:{recipeId}"),
        };

        // Apply fallback if icon failed to load
        if (!definition.Icon.IsValid)
        {
            definition.Icon = IconDataLoader.CreateFallbackIconDefinition();
        }

        return definition;
    }

    /// <summary>
    /// Parses a resource list. Accepts two YAML shapes:
    ///   mapping form          ->  output_resources: { iron: 1, copper: 2 }
    ///   list-of-mappings form ->  output_resources: [ - iron: 10, - carbon: 2 ]
    /// </summary>
    private static Dictionary<string, float> ParseResourceList(
        Dictionary<object, object> dict,
        string key)
    {
        var resources = new Dictionary<string, float>();

        if (!dict.ContainsKey(key) || dict[key] is null)
            return resources;

        var node = dict[key];

        if (node is Dictionary<object, object> mapping)
        {
            foreach (var kvp in mapping)
            {
                string resourceName = kvp.Key?.ToString() ?? "";
                if (!string.IsNullOrEmpty(resourceName))
                {
                    resources[resourceName] = NodeToFloat(kvp.Value, 0f);
                }
            }
            return resources;
        }

        if (node is List<object> resourceList)
        {
            foreach (var item in resourceList)
            {
                if (item is Dictionary<object, object> resourceDict)
                {
                    foreach (var kvp in resourceDict)
                    {
                        string resourceName = kvp.Key?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(resourceName))
                        {
                            resources[resourceName] = NodeToFloat(kvp.Value, 0f);
                        }
                    }
                }
            }
            return resources;
        }

        GameLogger.Warning($"Unsupported '{key}' YAML form: {node.GetType().Name}");
        return resources;
    }

    private static IconDefinition ParseIconDefinition(Dictionary<object, object> dict, string context)
    {
        if (!dict.ContainsKey("icon"))
        {
            return new IconDefinition(); // Return empty - fallback applied by caller
        }

        var iconDict = dict["icon"] as Dictionary<object, object>;
        if (iconDict == null)
            return new IconDefinition();

        // Get base_path (required)
        string? basePath = ReadString(iconDict, "base_path", "");
        if (string.IsNullOrEmpty(basePath))
        {
            GameLogger.Warning($"Icon section missing base_path for {context}");
            return new IconDefinition();
        }

        // Load all sizes via IconDataLoader
        var icon = IconDataLoader.LoadIcon(basePath, context);

        // Parse optional properties
        if (iconDict.ContainsKey("scale"))
        {
            icon.Scale = ReadFloat(iconDict, "scale", 1.0f);
        }

        if (iconDict.ContainsKey("tint"))
        {
            icon.Tint = ReadColor(iconDict, "tint", Colors.White);
        }

        return icon;
    }

    private static Color ReadColor(Dictionary<object, object> dict, string key, Color fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        if (dict[key] is not System.Collections.Generic.List<object> arr || arr.Count < 3)
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

    private static float ReadFloat(Dictionary<object, object> dict, string key, float fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        return NodeToFloat(dict[key], fallback);
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
