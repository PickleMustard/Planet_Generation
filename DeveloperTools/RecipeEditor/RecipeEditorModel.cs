#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.RecipeEditor;

/// <summary>
/// In-memory editable state for recipe YAML configuration.
/// Categories correspond to subdirectories of Configuration/Recipes/.
/// Each recipe tracks its source file so save preserves the existing multi-file layout.
/// </summary>
public class RecipeEditorModel
{
    public enum SlotKind { Resource, Tag }

    public class InputSlot
    {
        public SlotKind Kind { get; set; } = SlotKind.Resource;
        public string Key { get; set; } = "";
        public float Amount { get; set; } = 1f;
    }

    public class OutputSlot
    {
        public SlotKind Kind { get; set; } = SlotKind.Resource;
        public string Key { get; set; } = "";
        public float Amount { get; set; } = 1f;
    }

    public class ConditionalOutputSlot
    {
        public List<ConditionRule> Rules { get; set; } = new();

        /// <summary>
        /// Set only for legacy expressions the rule builder cannot represent
        /// (arithmetic, parens, etc.). Rendered read-only by the editor and
        /// written back to YAML verbatim. Null/empty otherwise.
        /// </summary>
        public string? LegacyCondition { get; set; }

        public string Resource { get; set; } = "";
        public float Amount { get; set; } = 1f;
    }

    public class RecipeEditEntry
    {
        public string RecipeId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public float WorkRequired { get; set; } = 10f;
        public List<InputSlot> Inputs { get; set; } = new();
        public List<OutputSlot> Outputs { get; set; } = new();
        public List<ConditionalOutputSlot> ConditionalOutputs { get; set; } = new();
        public HashSet<string> Tags { get; set; } = new();
        public string? IconBasePath { get; set; }
        public float IconScale { get; set; } = 1.0f;
        public Color IconTint { get; set; } = Colors.White;
        public string SourceFilePath { get; set; } = "";
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    public class RecipeCategoryData
    {
        public string CategoryName { get; set; } = "";
        public List<RecipeEditEntry> Recipes { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    private readonly string _recipesDirectory;
    private Dictionary<string, RecipeCategoryData> _categories = new();

    public IReadOnlyDictionary<string, RecipeCategoryData> Categories => _categories;

    public bool HasUnsavedChanges => _categories.Values.Any(c =>
        c.IsNew || c.IsDirty || c.Recipes.Any(r => r.IsNew || r.IsDirty));

    public RecipeEditorModel(string recipesDirectory)
    {
        ArgumentNullException.ThrowIfNull(recipesDirectory);
        _recipesDirectory = recipesDirectory.TrimEnd('/') + "/";
    }

    public string RecipesDirectory => _recipesDirectory;

    public void LoadFromDisk()
    {
        if (!DirAccess.DirExistsAbsolute(_recipesDirectory))
        {
            throw new InvalidOperationException(
                $"Recipes directory not found: {_recipesDirectory}");
        }

        var newCategories = new Dictionary<string, RecipeCategoryData>();

        foreach (var subdir in DirAccess.GetDirectoriesAt(_recipesDirectory))
        {
            string categoryName = subdir;
            string subdirPath = _recipesDirectory + subdir + "/";

            var category = new RecipeCategoryData
            {
                CategoryName = categoryName,
                IsNew = false,
                IsDirty = false
            };

            foreach (var file in DirAccess.GetFilesAt(subdirPath))
            {
                if (!file.EndsWith(".yaml") && !file.EndsWith(".yml")) continue;
                string filePath = subdirPath + file;
                var defs = RecipeConfigLoader.LoadRecipeDefinitions(filePath);
                foreach (var def in defs)
                {
                    category.Recipes.Add(MapDefinitionToEntry(def, filePath));
                }
            }

            newCategories[categoryName] = category;
        }

        _categories = newCategories;
    }

    public void AddCategory(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Category name cannot be empty", nameof(name));
        if (_categories.ContainsKey(name))
            throw new ArgumentException($"Category '{name}' already exists", nameof(name));

        _categories[name] = new RecipeCategoryData
        {
            CategoryName = name,
            IsNew = true,
            IsDirty = false
        };
    }

    public void DeleteCategory(string name)
    {
        if (!_categories.ContainsKey(name))
            throw new KeyNotFoundException($"Category '{name}' not found");
        _categories.Remove(name);
    }

    public void AddRecipe(string categoryName, RecipeEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_categories.ContainsKey(categoryName))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");

        entry.IsNew = true;
        entry.IsDirty = false;
        entry.Category = string.IsNullOrEmpty(entry.Category)
            ? categoryName.ToLowerInvariant()
            : entry.Category;
        if (string.IsNullOrEmpty(entry.SourceFilePath))
        {
            entry.SourceFilePath = DefaultSourceFilePath(categoryName);
        }

        _categories[categoryName].Recipes.Add(entry);
        _categories[categoryName].IsDirty = true;
    }

    public void DeleteRecipe(string categoryName, int index)
    {
        var (_, recipes) = GetRecipesOrThrow(categoryName);
        if (index < 0 || index >= recipes.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        recipes.RemoveAt(index);
        _categories[categoryName].IsDirty = true;
    }

    public void MoveRecipe(string categoryName, int fromIndex, int toIndex)
    {
        var (_, recipes) = GetRecipesOrThrow(categoryName);
        if (fromIndex < 0 || fromIndex >= recipes.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= recipes.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));

        var entry = recipes[fromIndex];
        recipes.RemoveAt(fromIndex);
        recipes.Insert(toIndex, entry);
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateRecipeField(string categoryName, int index,
        string fieldName, object? value)
    {
        var entry = GetEntryOrThrow(categoryName, index);

        switch (fieldName)
        {
            case "RecipeId":
                entry.RecipeId = value?.ToString() ?? "";
                break;
            case "DisplayName":
                entry.DisplayName = value?.ToString() ?? "";
                break;
            case "Description":
                entry.Description = value?.ToString() ?? "";
                break;
            case "Category":
                entry.Category = value?.ToString() ?? "";
                break;
            case "WorkRequired":
                entry.WorkRequired = Convert.ToSingle(value);
                break;
            case "IconBasePath":
                entry.IconBasePath = value?.ToString();
                break;
            case "IconScale":
                entry.IconScale = Convert.ToSingle(value);
                break;
            case "IconTint":
                entry.IconTint = value is Color c
                    ? c
                    : throw new ArgumentException(
                        $"Cannot convert '{value}' to Color for field 'IconTint'");
                break;
            default:
                throw new ArgumentException($"Unknown field name: {fieldName}",
                    nameof(fieldName));
        }

        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateRecipeTags(string categoryName, int index, HashSet<string> newTags)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.Tags = new HashSet<string>(newTags);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void AddInputSlot(string categoryName, int index, InputSlot slot)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.Inputs.Add(slot);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateInputSlot(string categoryName, int index, int slotIndex,
        SlotKind kind, string key, float amount)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.Inputs.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        var s = entry.Inputs[slotIndex];
        s.Kind = kind;
        s.Key = key;
        s.Amount = amount;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void DeleteInputSlot(string categoryName, int index, int slotIndex)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.Inputs.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        entry.Inputs.RemoveAt(slotIndex);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void AddOutputSlot(string categoryName, int index, OutputSlot slot)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.Outputs.Add(slot);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateOutputSlot(string categoryName, int index, int slotIndex,
        SlotKind kind, string key, float amount)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.Outputs.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        var s = entry.Outputs[slotIndex];
        s.Kind = kind;
        s.Key = key;
        s.Amount = amount;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void DeleteOutputSlot(string categoryName, int index, int slotIndex)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.Outputs.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        entry.Outputs.RemoveAt(slotIndex);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void AddConditionalOutputSlot(string categoryName, int index, ConditionalOutputSlot slot)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.ConditionalOutputs.Add(slot);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateConditionalOutputSlot(string categoryName, int index, int slotIndex,
        List<ConditionRule> rules, string resource, float amount)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.ConditionalOutputs.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        var s = entry.ConditionalOutputs[slotIndex];
        s.Rules = rules ?? new List<ConditionRule>();
        // Editing through the rule UI always supersedes any legacy expression.
        s.LegacyCondition = null;
        s.Resource = resource;
        s.Amount = amount;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void DeleteConditionalOutputSlot(string categoryName, int index, int slotIndex)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.ConditionalOutputs.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        entry.ConditionalOutputs.RemoveAt(slotIndex);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    /// <summary>All resource IDs known to ResourceDatabase plus the special 'power' key.</summary>
    public static List<string> GetAllResourceIds()
    {
        var ids = new List<string> { "power" };
        try
        {
            var db = ResourceDatabase.Instance;
            if (db.IsLoaded)
            {
                ids.AddRange(db.GetAllResources().Keys);
            }
        }
        catch
        {
            // database not loaded yet — return just power
        }
        return ids.Distinct().OrderBy(s => s).ToList();
    }

    /// <summary>Union of every Tag across every resource in ResourceDatabase.</summary>
    public static List<string> GetAllResourceTags()
    {
        var tags = new HashSet<string>();
        try
        {
            var db = ResourceDatabase.Instance;
            if (db.IsLoaded)
            {
                foreach (var def in db.GetAllResources().Values)
                {
                    if (def.Tags != null)
                        tags.UnionWith(def.Tags);
                }
            }
        }
        catch
        {
        }
        return tags.OrderBy(s => s).ToList();
    }

    /// <summary>Union of every Tag across every recipe in the model.</summary>
    public HashSet<string> GetAllRecipeTags()
    {
        var tags = new HashSet<string>();
        foreach (var category in _categories.Values)
        {
            foreach (var recipe in category.Recipes)
            {
                tags.UnionWith(recipe.Tags);
            }
        }
        return tags;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        var nameToCategories = new Dictionary<string, List<string>>();
        var resourceIds = new HashSet<string>(GetAllResourceIds());
        var resourceTags = new HashSet<string>(GetAllResourceTags());

        foreach (var category in _categories.Values)
        {
            for (int i = 0; i < category.Recipes.Count; i++)
            {
                var entry = category.Recipes[i];

                if (string.IsNullOrEmpty(entry.RecipeId))
                {
                    errors.Add(
                        $"Recipe in category '{category.CategoryName}' at index {i} has empty recipe_id");
                }
                else
                {
                    if (!nameToCategories.ContainsKey(entry.RecipeId))
                        nameToCategories[entry.RecipeId] = new List<string>();
                    if (!nameToCategories[entry.RecipeId].Contains(category.CategoryName))
                        nameToCategories[entry.RecipeId].Add(category.CategoryName);
                }

                if (entry.Outputs.Count == 0)
                {
                    errors.Add($"Recipe '{entry.RecipeId}' has no outputs");
                }

                // Duplicate input keys
                var seenInputKeys = new HashSet<string>();
                for (int s = 0; s < entry.Inputs.Count; s++)
                {
                    var slot = entry.Inputs[s];
                    if (string.IsNullOrWhiteSpace(slot.Key))
                    {
                        errors.Add($"Recipe '{entry.RecipeId}' has empty input key at slot {s}");
                        continue;
                    }
                    string fullKey = slot.Kind == SlotKind.Tag ? $"tag:{slot.Key}" : slot.Key;
                    if (!seenInputKeys.Add(fullKey))
                    {
                        errors.Add($"Recipe '{entry.RecipeId}' has duplicate input '{fullKey}'");
                    }

                    if (slot.Kind == SlotKind.Resource && !resourceIds.Contains(slot.Key))
                    {
                        errors.Add(
                            $"Recipe '{entry.RecipeId}' references unknown resource '{slot.Key}' in inputs");
                    }
                    if (slot.Kind == SlotKind.Tag && !resourceTags.Contains(slot.Key))
                    {
                        errors.Add(
                            $"Recipe '{entry.RecipeId}' references unknown tag '{slot.Key}' in inputs");
                    }
                }

                var seenOutputKeys = new HashSet<string>();
                for (int s = 0; s < entry.Outputs.Count; s++)
                {
                    var slot = entry.Outputs[s];
                    if (string.IsNullOrWhiteSpace(slot.Key))
                    {
                        errors.Add($"Recipe '{entry.RecipeId}' has empty output key at slot {s}");
                        continue;
                    }
                    string fullKey = slot.Kind == SlotKind.Tag ? $"tag:{slot.Key}" : slot.Key;
                    if (!seenOutputKeys.Add(fullKey))
                    {
                        errors.Add($"Recipe '{entry.RecipeId}' has duplicate output '{fullKey}'");
                    }

                    if (slot.Kind == SlotKind.Resource && !resourceIds.Contains(slot.Key))
                    {
                        errors.Add(
                            $"Recipe '{entry.RecipeId}' references unknown resource '{slot.Key}' in outputs");
                    }
                    if (slot.Kind == SlotKind.Tag && !resourceTags.Contains(slot.Key))
                    {
                        errors.Add(
                            $"Recipe '{entry.RecipeId}' references unknown tag '{slot.Key}' in outputs");
                    }
                }

                for (int s = 0; s < entry.ConditionalOutputs.Count; s++)
                {
                    var co = entry.ConditionalOutputs[s];
                    bool hasRules = co.Rules.Count > 0;
                    bool hasLegacy = !string.IsNullOrWhiteSpace(co.LegacyCondition);

                    if (!hasRules && !hasLegacy)
                    {
                        errors.Add(
                            $"Recipe '{entry.RecipeId}' conditional output {s} has empty condition");
                    }
                    else if (hasLegacy)
                    {
                        try
                        {
                            Structures.Resources.RecipeExpressionEvaluator.Compile(co.LegacyCondition!);
                        }
                        catch (Structures.Resources.RecipeExpressionException ex)
                        {
                            errors.Add(
                                $"Recipe '{entry.RecipeId}' conditional output {s} has invalid legacy condition: {ex.Message}");
                        }
                    }
                    else
                    {
                        for (int r = 0; r < co.Rules.Count; r++)
                        {
                            var rule = co.Rules[r];
                            if (string.IsNullOrWhiteSpace(rule.Variable))
                            {
                                errors.Add(
                                    $"Recipe '{entry.RecipeId}' conditional output {s} rule {r} has empty variable");
                                continue;
                            }
                            bool known = false;
                            foreach (var v in Structures.Resources.RecipeExpressionEvaluator.AllowedVariables)
                            {
                                if (v == rule.Variable) { known = true; break; }
                            }
                            if (!known)
                            {
                                errors.Add(
                                    $"Recipe '{entry.RecipeId}' conditional output {s} rule {r} references unknown variable '{rule.Variable}'");
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(co.Resource))
                    {
                        errors.Add(
                            $"Recipe '{entry.RecipeId}' conditional output {s} has empty resource");
                    }
                    else if (!resourceIds.Contains(co.Resource))
                    {
                        errors.Add(
                            $"Recipe '{entry.RecipeId}' conditional output {s} references unknown resource '{co.Resource}'");
                    }
                }

                if (!string.IsNullOrEmpty(entry.IconBasePath) && !entry.IconBasePath.StartsWith("res://"))
                {
                    errors.Add(
                        $"Recipe '{entry.RecipeId}' has icon path not starting with res://: '{entry.IconBasePath}'");
                }
            }
        }

        foreach (var kvp in nameToCategories)
        {
            if (kvp.Value.Count > 1)
            {
                errors.Add(
                    $"Duplicate recipe_id '{kvp.Key}' found in categories: {string.Join(", ", kvp.Value)}");
            }
        }

        return errors;
    }

    public string DefaultSourceFilePath(string categoryName)
    {
        string fileName = $"{categoryName.ToLowerInvariant()}_recipes.yaml";
        return $"{_recipesDirectory}{categoryName}/{fileName}";
    }

    private RecipeEditEntry GetEntryOrThrow(string categoryName, int index)
    {
        var (_, recipes) = GetRecipesOrThrow(categoryName);
        if (index < 0 || index >= recipes.Count)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} out of range [0, {recipes.Count})");
        return recipes[index];
    }

    private (RecipeCategoryData category, List<RecipeEditEntry> recipes) GetRecipesOrThrow(string categoryName)
    {
        if (!_categories.ContainsKey(categoryName))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");
        var c = _categories[categoryName];
        return (c, c.Recipes);
    }

    private static RecipeEditEntry MapDefinitionToEntry(RecipeDefinition def, string sourceFilePath)
    {
        var entry = new RecipeEditEntry
        {
            RecipeId = def.RecipeId ?? "",
            DisplayName = def.DisplayName ?? "",
            Description = def.Description ?? "",
            Category = def.Category ?? "",
            WorkRequired = def.WorkRequired,
            Tags = new HashSet<string>(def.Tags ?? new HashSet<string>()),
            IconBasePath = def.Icon?.BasePath,
            IconScale = def.Icon?.Scale ?? 1.0f,
            IconTint = def.Icon?.Tint ?? Colors.White,
            SourceFilePath = sourceFilePath,
            IsNew = false,
            IsDirty = false
        };

        foreach (var kvp in def.InputResources)
        {
            string key = RecipeYamlPreprocessor.IsEmptyKeyPlaceholder(kvp.Key) ? "" : kvp.Key;
            if (RecipeDefinition.IsTagInput(key))
            {
                entry.Inputs.Add(new InputSlot
                {
                    Kind = SlotKind.Tag,
                    Key = RecipeDefinition.GetTagName(key),
                    Amount = kvp.Value
                });
            }
            else
            {
                entry.Inputs.Add(new InputSlot
                {
                    Kind = SlotKind.Resource,
                    Key = key,
                    Amount = kvp.Value
                });
            }
        }

        foreach (var kvp in def.OutputResources)
        {
            string key = RecipeYamlPreprocessor.IsEmptyKeyPlaceholder(kvp.Key) ? "" : kvp.Key;
            if (RecipeDefinition.IsTagInput(key))
            {
                entry.Outputs.Add(new OutputSlot
                {
                    Kind = SlotKind.Tag,
                    Key = RecipeDefinition.GetTagName(key),
                    Amount = kvp.Value
                });
            }
            else
            {
                entry.Outputs.Add(new OutputSlot
                {
                    Kind = SlotKind.Resource,
                    Key = key,
                    Amount = kvp.Value
                });
            }
        }

        foreach (var co in def.ConditionalOutputs)
        {
            var slot = new ConditionalOutputSlot
            {
                Resource = co.Resource ?? "",
                Amount = co.Amount,
            };

            if (co.Rules != null && co.Rules.Count > 0)
            {
                // Deep copy so editor mutations don't leak back into the loaded definition.
                foreach (var rule in co.Rules)
                {
                    slot.Rules.Add(new ConditionRule
                    {
                        Join = rule.Join,
                        Variable = rule.Variable,
                        Operator = rule.Operator,
                        Value = rule.Value,
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(co.Condition))
            {
                slot.LegacyCondition = co.Condition;
            }

            entry.ConditionalOutputs.Add(slot);
        }

        return entry;
    }
}
#endif
