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
        var definition = new RecipeDefinition
        {
            RecipeId = ReadString(dict, "recipe_id", ""),
            DisplayName = ReadString(dict, "display_name", ""),
            Description = ReadString(dict, "description", ""),
            Category = ReadString(dict, "category", ""),
            WorkRequired = ReadFloat(dict, "work_required", 10.0f),
            InputResources = ParseResourceList(dict, "input_resources"),
            OutputResources = ParseResourceList(dict, "output_resources"),
        };

        return definition;
    }

    /// <summary>
    /// Parses a resource list in the YAML format of list-of-single-key-dicts:
    /// input_resources:
    ///   - iron: 10
    ///   - carbon: 2
    /// </summary>
    private static Dictionary<string, float> ParseResourceList(
        Dictionary<object, object> dict,
        string key)
    {
        var resources = new Dictionary<string, float>();

        if (!dict.ContainsKey(key))
            return resources;

        var resourceList = dict[key] as List<object>;
        if (resourceList == null)
            return resources;

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
