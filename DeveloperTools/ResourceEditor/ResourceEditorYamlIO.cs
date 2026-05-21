#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using UtilityLibrary;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DeveloperTools.ResourceEditor;

/// <summary>
/// Writes resource category data back to YAML files on disk.
/// Output format matches existing category files exactly:
/// tags in flow style [tag1, tag2], icon section conditional,
/// transport_weight omitted when default, strings quoted.
/// All indentation uses <see cref="YamlIndent"/> to guarantee
/// space-based output (YAML forbids tab indentation).
/// </summary>
public static class ResourceEditorYamlIO
{
    /// <summary>
    /// Writes a single category's resources to a YAML file.
    /// Throws on write failure after logging via GameLogger.
    /// </summary>
    public static void WriteCategory(string filePath,
        ResourceEditorModel.ResourceCategoryData category)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(category);

        string yamlContent = BuildCategoryYaml(category);

        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            var error = FileAccess.GetOpenError();
            GameLogger.Error(
                $"Failed to open file for writing: {filePath}. Error: {error}");
            throw new InvalidOperationException(
                $"Failed to open file for writing: {filePath}. Error: {error}");
        }

        try
        {
            file.StoreString(yamlContent);
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Failed to write category to {filePath}: {ex.Message}");
            throw;
        }
        finally
        {
            file.Close();
        }
    }

    /// <summary>
    /// Writes all categories to the directory, deletes orphan YAML files,
    /// and resets all IsNew/IsDirty flags on categories and entries.
    /// </summary>
    public static void WriteAllCategories(string directoryPath,
        Dictionary<string, ResourceEditorModel.ResourceCategoryData> categories)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(categories);

        // Ensure directory exists
        if (!DirAccess.DirExistsAbsolute(directoryPath))
        {
            var dir = DirAccess.Open(directoryPath);
            if (dir == null)
            {
                // Try creating with MakeDirRecursive
                DirAccess.MakeDirRecursiveAbsolute(directoryPath);
            }
        }

        // Write each category
        foreach (var kvp in categories)
        {
            string filePath = $"{directoryPath}/{kvp.Key}.yaml";
            WriteCategory(filePath, kvp.Value);
        }

        // Delete orphan files (YAML files in directory not matching any category key)
        var dirAccess = DirAccess.Open(directoryPath);
        if (dirAccess != null)
        {
            dirAccess.ListDirBegin();
            string fileName = dirAccess.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.EndsWith(".yaml") && !fileName.StartsWith("."))
                {
                    string categoryName = fileName.Replace(".yaml", "");
                    if (!categories.ContainsKey(categoryName))
                    {
                        var removeError = dirAccess.Remove(fileName);
                        if (removeError != Error.Ok)
                        {
                        GameLogger.Warning(
                            $"Failed to delete orphan file: {directoryPath}/{fileName}. " +
                            $"Error: {removeError}");
                        }
                    }
                }
                fileName = dirAccess.GetNext();
            }
            dirAccess.ListDirEnd();
        }

        // Reset all dirty/new flags
        foreach (var category in categories.Values)
        {
            category.IsNew = false;
            category.IsDirty = false;
            foreach (var entry in category.Resources)
            {
                entry.IsNew = false;
                entry.IsDirty = false;
            }
        }
    }

    /// <summary>
    /// Builds the YAML string for a category, matching existing file format.
    /// Uses <see cref="YamlIndent"/> for all indentation to guarantee
    /// space-based output per YAML specification.
    /// </summary>
    private static string BuildCategoryYaml(
        ResourceEditorModel.ResourceCategoryData category)
    {
        var sb = new StringBuilder();

        if (category.Resources.Count == 0)
        {
            sb.AppendLine("resources: []");
            return sb.ToString();
        }

        // Level 0: root keys like "resources:"
        // Level 1: list item prefix "- id_name: ..."
        // Level 2: list item properties "resource_tier: ..."
        // Level 3: nested block keys "base_path: ..." (icon sub-properties)

        sb.AppendLine("resources:");

        foreach (var entry in category.Resources)
        {
            YamlIndent.AppendLine(sb, 1, $"- id_name: {entry.IdName}");
            YamlIndent.AppendLine(sb, 2, $"resource_tier: {entry.ResourceTier}");
            YamlIndent.AppendLine(sb, 2, $"state_of_matter: " +
                $"{entry.StateOfMatter.ToString().ToLowerInvariant()}");
            YamlIndent.AppendLine(sb, 2, $"max_stack_size: {(int)entry.MaxStackSize}");

            // transport_weight: omit if default 1.0
            if (!Mathf.IsEqualApprox(entry.TransportWeight, 1.0f))
            {
                YamlIndent.AppendLine(sb, 2, $"transport_weight: " +
                    $"{FormatFloat(entry.TransportWeight)}");
            }

            // tags: flow style if present
            if (entry.Tags.Count > 0)
            {
                var tagList = string.Join(", ", entry.Tags);
                YamlIndent.AppendLine(sb, 2, $"tags: [{tagList}]");
            }

            // icon section: omit if IconBasePath is null/empty
            if (!string.IsNullOrEmpty(entry.IconBasePath))
            {
                YamlIndent.AppendLine(sb, 2, "icon:");
                YamlIndent.AppendLine(sb, 3, $"base_path: \"{entry.IconBasePath}\"");

                // scale: omit if default 1.0
                if (!Mathf.IsEqualApprox(entry.IconScale, 1.0f))
                {
                    YamlIndent.AppendLine(sb, 3, $"scale: " +
                        $"{FormatFloat(entry.IconScale)}");
                }

                // tint: omit if default white
                if (entry.IconTint != Colors.White)
                {
                    YamlIndent.AppendLine(sb, 3, $"tint: " +
                        $"[{FormatFloat(entry.IconTint.R)}, " +
                        $"{FormatFloat(entry.IconTint.G)}, " +
                        $"{FormatFloat(entry.IconTint.B)}, " +
                        $"{FormatFloat(entry.IconTint.A)}]");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a float for YAML output. Uses integer form for whole numbers,
    /// otherwise minimal decimal places.
    /// </summary>
    private static string FormatFloat(float value)
    {
        if (Mathf.IsEqualApprox(value, Mathf.Round(value)))
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
#endif
