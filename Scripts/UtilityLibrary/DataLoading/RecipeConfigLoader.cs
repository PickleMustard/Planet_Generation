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

        var validation = YamlValidator.ValidateRecipeDefinition(filePath);
        if (!validation.IsValid)
        {
            // Surface validation issues (empty keys, missing fields, etc.) but still
            // try to load — the editor needs to display malformed entries so the user
            // can fix them. Hard parse failures will be caught by the try/catch below.
            GD.PrintErr($"YAML validation issues in {filePath}");
            foreach (var error in validation.Errors)
            {
                GD.PrintErr($"  - {error}");
            }
        }

        string? rawText = BaseConfigLoader.ReadAllText(filePath);
        if (rawText == null)
        {
            GD.PrintErr($"Recipe definition file not found: {filePath}");
            return definitions;
        }

        try
        {
            string text = RecipeYamlPreprocessor.Preprocess(rawText).Text;

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
            ConditionalOutputs = ParseConditionalOutputs(dict, recipeId),
            Tags = ReadTags(dict, "tags"),
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

    private static List<ConditionalOutput> ParseConditionalOutputs(
        Dictionary<object, object> dict,
        string recipeId)
    {
        var results = new List<ConditionalOutput>();

        if (!dict.ContainsKey("conditional_outputs") || dict["conditional_outputs"] is null)
            return results;

        if (dict["conditional_outputs"] is not List<object> entries)
        {
            GameLogger.Warning(
                $"Recipe '{recipeId}': 'conditional_outputs' must be a list, got {dict["conditional_outputs"].GetType().Name}");
            return results;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] is not Dictionary<object, object> entry)
            {
                GameLogger.Warning($"Recipe '{recipeId}': conditional_outputs[{i}] is not a mapping; skipped.");
                continue;
            }

            var co = new ConditionalOutput
            {
                Resource = ReadString(entry, "resource", string.Empty),
                Amount = ReadFloat(entry, "amount", 0f),
            };

            if (string.IsNullOrWhiteSpace(co.Resource))
            {
                GameLogger.Warning(
                    $"Recipe '{recipeId}': conditional_outputs[{i}] missing 'resource'; skipped.");
                continue;
            }

            if (!entry.ContainsKey("condition") || entry["condition"] is null)
            {
                GameLogger.Warning(
                    $"Recipe '{recipeId}': conditional_outputs[{i}] missing 'condition'; skipped.");
                continue;
            }

            object conditionNode = entry["condition"];

            // New structured form: condition is a list of rule mappings.
            if (conditionNode is List<object> ruleList)
            {
                var rules = ParseRuleList(ruleList, recipeId, i);
                if (rules.Count == 0)
                {
                    GameLogger.Warning(
                        $"Recipe '{recipeId}': conditional_outputs[{i}] structured condition has no valid rules; skipped.");
                    continue;
                }

                try
                {
                    co.CompiledCondition = ConditionRuleCompiler.BuildAst(rules);
                }
                catch (RecipeExpressionException ex)
                {
                    GD.PrintErr(
                        $"Recipe '{recipeId}': conditional_outputs[{i}] could not build AST from rules: {ex.Message}");
                    continue;
                }

                co.Rules = rules;
                results.Add(co);
                continue;
            }

            // Legacy form: condition is a raw expression string.
            string conditionStr = conditionNode as string ?? conditionNode.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(conditionStr))
            {
                GameLogger.Warning(
                    $"Recipe '{recipeId}': conditional_outputs[{i}] has empty condition; skipped.");
                continue;
            }

            try
            {
                co.CompiledCondition = RecipeExpressionEvaluator.Compile(conditionStr);
            }
            catch (RecipeExpressionException ex)
            {
                GD.PrintErr(
                    $"Recipe '{recipeId}': conditional_outputs[{i}] condition failed to parse: {ex.Message}");
                continue;
            }

            if (ConditionRuleCompiler.TryDecompose(co.CompiledCondition, out var decomposed))
            {
                co.Rules = decomposed;
            }
            else
            {
                co.Condition = conditionStr;
            }
            results.Add(co);
        }

        return results;
    }

    private static List<ConditionRule> ParseRuleList(List<object> ruleList, string recipeId, int outputIndex)
    {
        var rules = new List<ConditionRule>();
        for (int j = 0; j < ruleList.Count; j++)
        {
            if (ruleList[j] is not Dictionary<object, object> ruleDict)
            {
                GameLogger.Warning(
                    $"Recipe '{recipeId}': conditional_outputs[{outputIndex}].condition[{j}] is not a mapping; skipped.");
                continue;
            }

            string variable = ReadString(ruleDict, "var", string.Empty);
            string opSymbol = ReadString(ruleDict, "op", string.Empty);
            float value = ReadFloat(ruleDict, "value", 0f);
            string joinStr = ReadString(ruleDict, "join", "AND");

            if (string.IsNullOrWhiteSpace(variable))
            {
                GameLogger.Warning(
                    $"Recipe '{recipeId}': conditional_outputs[{outputIndex}].condition[{j}] missing 'var'; skipped.");
                continue;
            }

            if (!ConditionOperatorExtensions.TryParseSymbol(opSymbol, out var op))
            {
                GameLogger.Warning(
                    $"Recipe '{recipeId}': conditional_outputs[{outputIndex}].condition[{j}] has invalid 'op' '{opSymbol}'; skipped.");
                continue;
            }

            ConditionJoinExtensions.TryParseYaml(joinStr, out var join);

            rules.Add(new ConditionRule
            {
                Join = join,
                Variable = variable,
                Operator = op,
                Value = value,
            });
        }
        return rules;
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

        // Get wrapper resource path (required)
        string? basePath = ReadString(iconDict, "resource", "");
        if (string.IsNullOrEmpty(basePath))
        {
            GameLogger.Warning($"Icon section missing resource for {context}");
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

    private static HashSet<string> ReadTags(Dictionary<object, object> dict, string key)
    {
        var tags = new HashSet<string>();

        if (!dict.ContainsKey(key) || dict[key] is not List<object> tagList)
            return tags;

        foreach (var tagObj in tagList)
        {
            string? tag = tagObj?.ToString();
            if (!string.IsNullOrWhiteSpace(tag))
            {
                tags.Add(tag);
            }
        }

        return tags;
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
