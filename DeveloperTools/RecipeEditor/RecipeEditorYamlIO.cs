#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace DeveloperTools.RecipeEditor;

/// <summary>
/// Writes recipe category data back to YAML files on disk, preserving the
/// per-recipe source-file association so a category split across multiple
/// files (e.g. Power/) round-trips correctly.
/// All indentation uses <see cref="YamlIndent"/> to guarantee
/// space-based output (YAML forbids tab indentation).
/// </summary>
public static class RecipeEditorYamlIO
{
    /// <summary>
    /// Writes every recipe back to its source file, deletes orphan files within
    /// each category subdirectory, removes empty/deleted category directories,
    /// and resets all IsNew/IsDirty flags.
    /// </summary>
    public static void WriteAll(string recipesDirectory,
        Dictionary<string, RecipeEditorModel.RecipeCategoryData> categories)
    {
        ArgumentNullException.ThrowIfNull(recipesDirectory);
        ArgumentNullException.ThrowIfNull(categories);

        string root = recipesDirectory.TrimEnd('/') + "/";

        if (!DirAccess.DirExistsAbsolute(root))
        {
            DirAccess.MakeDirRecursiveAbsolute(root);
        }

        // Group every recipe by its source file path.
        var recipesByFile = new Dictionary<string, List<RecipeEditorModel.RecipeEditEntry>>();
        var filesByCategory = new Dictionary<string, HashSet<string>>();

        foreach (var kvp in categories)
        {
            string category = kvp.Key;
            string categoryDir = $"{root}{category}/";
            if (!DirAccess.DirExistsAbsolute(categoryDir))
            {
                DirAccess.MakeDirRecursiveAbsolute(categoryDir);
            }

            filesByCategory[category] = new HashSet<string>();

            foreach (var entry in kvp.Value.Recipes)
            {
                string sourcePath = string.IsNullOrEmpty(entry.SourceFilePath)
                    ? DefaultSourcePath(root, category)
                    : entry.SourceFilePath;
                entry.SourceFilePath = sourcePath;

                if (!recipesByFile.ContainsKey(sourcePath))
                    recipesByFile[sourcePath] = new List<RecipeEditorModel.RecipeEditEntry>();
                recipesByFile[sourcePath].Add(entry);
                filesByCategory[category].Add(sourcePath);
            }
        }

        // Write each file.
        foreach (var (filePath, recipes) in recipesByFile)
        {
            string yaml = BuildRecipesYaml(recipes);
            WriteText(filePath, yaml);
        }

        // Orphan cleanup: any YAML file inside a known category subdir that we
        // didn't write goes away.
        foreach (var (category, keepFiles) in filesByCategory)
        {
            string categoryDir = $"{root}{category}/";
            DeleteUnwrittenYaml(categoryDir, keepFiles);
        }

        // Remove subdirectories that are no longer in the model.
        var existingDirs = DirAccess.GetDirectoriesAt(root);
        foreach (var subdir in existingDirs)
        {
            if (!categories.ContainsKey(subdir))
            {
                string fullSubdir = $"{root}{subdir}/";
                DeleteUnwrittenYaml(fullSubdir, new HashSet<string>());
                TryRemoveEmptyDir(fullSubdir);
            }
        }

        // Reset dirty/new flags.
        foreach (var category in categories.Values)
        {
            category.IsNew = false;
            category.IsDirty = false;
            foreach (var entry in category.Recipes)
            {
                entry.IsNew = false;
                entry.IsDirty = false;
            }
        }
    }

    private static string DefaultSourcePath(string root, string category)
    {
        string fileName = $"{category.ToLowerInvariant()}_recipes.yaml";
        return $"{root}{category}/{fileName}";
    }

    private static void WriteText(string filePath, string content)
    {
        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            var error = FileAccess.GetOpenError();
            GameLogger.Error($"Failed to open file for writing: {filePath}. Error: {error}");
            throw new InvalidOperationException(
                $"Failed to open file for writing: {filePath}. Error: {error}");
        }

        try
        {
            file.StoreString(content);
        }
        finally
        {
            file.Close();
        }
    }

    private static void DeleteUnwrittenYaml(string dirPath, HashSet<string> keep)
    {
        var dirAccess = DirAccess.Open(dirPath);
        if (dirAccess == null) return;

        dirAccess.ListDirBegin();
        string fileName = dirAccess.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!fileName.StartsWith(".") &&
                (fileName.EndsWith(".yaml") || fileName.EndsWith(".yml")))
            {
                string fullPath = $"{dirPath}{fileName}";
                if (!keep.Contains(fullPath))
                {
                    var err = dirAccess.Remove(fileName);
                    if (err != Error.Ok)
                    {
                        GameLogger.Warning(
                            $"Failed to delete orphan recipe file: {fullPath}. Error: {err}");
                    }
                }
            }
            fileName = dirAccess.GetNext();
        }
        dirAccess.ListDirEnd();
    }

    private static void TryRemoveEmptyDir(string dirPath)
    {
        var dirAccess = DirAccess.Open(dirPath);
        if (dirAccess == null) return;
        dirAccess.ListDirBegin();
        string fileName = dirAccess.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!fileName.StartsWith("."))
            {
                dirAccess.ListDirEnd();
                return;
            }
            fileName = dirAccess.GetNext();
        }
        dirAccess.ListDirEnd();

        // Empty — remove
        string parent = dirPath.TrimEnd('/');
        int lastSlash = parent.LastIndexOf('/');
        if (lastSlash < 0) return;
        string parentDir = parent.Substring(0, lastSlash + 1);
        string leaf = parent.Substring(lastSlash + 1);
        var parentAccess = DirAccess.Open(parentDir);
        if (parentAccess == null) return;
        var err = parentAccess.Remove(leaf);
        if (err != Error.Ok)
        {
            GameLogger.Warning($"Failed to remove empty category directory: {dirPath}. Error: {err}");
        }
    }

    /// <summary>
    /// Builds the YAML for a list of recipes belonging to one file.
    /// Output format mirrors hand-authored recipe files exactly.
    /// Uses <see cref="YamlIndent"/> for all indentation.
    /// </summary>
    private static string BuildRecipesYaml(List<RecipeEditorModel.RecipeEditEntry> recipes)
    {
        var sb = new StringBuilder();

        if (recipes.Count == 0)
        {
            sb.AppendLine("recipes: []");
            return sb.ToString();
        }

        // Level 0: root keys "recipes:"
        // Level 1: list item prefix "- recipe_id: ..."
        // Level 2: list item properties "display_name: ..."
        // Level 3: nested block properties "resource: ..." (icon), slot keys
        // Level 4: deep nested slots "- key: amount"

        sb.AppendLine("recipes:");
        foreach (var entry in recipes)
        {
            YamlIndent.AppendLine(sb, 1, $"- recipe_id: \"{entry.RecipeId}\"");
            YamlIndent.AppendLine(sb, 2, $"display_name: \"{Escape(entry.DisplayName)}\"");
            if (!string.IsNullOrEmpty(entry.Description))
            {
                YamlIndent.AppendLine(sb, 2, $"description: {Escape(entry.Description)}");
            }
            if (!string.IsNullOrEmpty(entry.Category))
            {
                YamlIndent.AppendLine(sb, 2, $"category: {entry.Category}");
            }
            YamlIndent.AppendLine(sb, 2, $"work_required: {FormatFloat(entry.WorkRequired)}");

            if (entry.Tags.Count > 0)
            {
                YamlIndent.AppendLine(sb, 2, $"tags: [{string.Join(", ", entry.Tags.OrderBy(t => t))}]");
            }

            if (!string.IsNullOrEmpty(entry.IconResourcePath))
            {
                YamlIndent.AppendLine(sb, 2, "icon:");
                YamlIndent.AppendLine(sb, 3, $"resource: \"{entry.IconResourcePath}\"");
                if (!Mathf.IsEqualApprox(entry.IconScale, 1.0f))
                {
                    YamlIndent.AppendLine(sb, 3, $"scale: {FormatFloat(entry.IconScale)}");
                }
                if (entry.IconTint != Colors.White)
                {
                    YamlIndent.AppendLine(sb, 3,
                        $"tint: [{FormatFloat(entry.IconTint.R)}, " +
                        $"{FormatFloat(entry.IconTint.G)}, " +
                        $"{FormatFloat(entry.IconTint.B)}, " +
                        $"{FormatFloat(entry.IconTint.A)}]");
                }
            }

            if (entry.Inputs.Count > 0)
            {
                YamlIndent.AppendLine(sb, 2, "input_resources:");
                foreach (var slot in entry.Inputs)
                {
                    string key = slot.Kind == RecipeEditorModel.SlotKind.Tag
                        ? $"tag:{slot.Key}"
                        : slot.Key;
                    YamlIndent.AppendLine(sb, 3, $"- {key}: {FormatFloat(slot.Amount)}");
                }
            }

            YamlIndent.AppendLine(sb, 2, "output_resources:");
            foreach (var slot in entry.Outputs)
            {
                string key = slot.Kind == RecipeEditorModel.SlotKind.Tag
                    ? $"tag:{slot.Key}"
                    : slot.Key;
                YamlIndent.AppendLine(sb, 3, $"- {key}: {FormatFloat(slot.Amount)}");
            }

            if (entry.ConditionalOutputs.Count > 0)
            {
                YamlIndent.AppendLine(sb, 2, "conditional_outputs:");
                foreach (var co in entry.ConditionalOutputs)
                {
                    bool hasRules = co.Rules.Count > 0;
                    bool hasLegacy = !string.IsNullOrWhiteSpace(co.LegacyCondition);

                    if (hasRules)
                    {
                        YamlIndent.AppendLine(sb, 3, "- condition:");
                        for (int r = 0; r < co.Rules.Count; r++)
                        {
                            var rule = co.Rules[r];
                            // First rule omits 'join'; subsequent rules write AND or OR.
                            if (r == 0)
                            {
                                YamlIndent.AppendLine(sb, 4, $"- var: {rule.Variable}");
                            }
                            else
                            {
                                YamlIndent.AppendLine(sb, 4, $"- join: {rule.Join.ToYamlString()}");
                                YamlIndent.AppendLine(sb, 5, $"var: {rule.Variable}");
                            }
                            YamlIndent.AppendLine(sb, 5, $"op: \"{rule.Operator.ToSymbol()}\"");
                            YamlIndent.AppendLine(sb, 5, $"value: {FormatFloat(rule.Value)}");
                        }
                        YamlIndent.AppendLine(sb, 4, $"resource: {co.Resource}");
                        YamlIndent.AppendLine(sb, 4, $"amount: {FormatFloat(co.Amount)}");
                    }
                    else if (hasLegacy)
                    {
                        // Preserve unconvertible legacy expressions verbatim.
                        YamlIndent.AppendLine(sb, 3, $"- condition: \"{Escape(co.LegacyCondition!)}\"");
                        YamlIndent.AppendLine(sb, 4, $"resource: {co.Resource}");
                        YamlIndent.AppendLine(sb, 4, $"amount: {FormatFloat(co.Amount)}");
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static string FormatFloat(float value)
    {
        if (Mathf.IsEqualApprox(value, Mathf.Round(value)))
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Escape(string s)
    {
        if (s == null) return "";
        return s.Replace("\"", "\\\"");
    }
}
#endif
