#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using DeveloperTools.Common;
using UtilityLibrary;

namespace DeveloperTools.EngineEditor;

/// <summary>
/// Writes engine category data back to YAML, one file per category
/// ({directory}/{category}.yaml), matching the format EngineConfigLoader reads.
/// Deletes orphan files for removed categories and resets dirty flags.
/// </summary>
public static class EngineEditorYamlIO
{
    public static void WriteAllCategories(string directoryPath,
        Dictionary<string, EngineEditorModel.EngineCategoryData> categories)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        ArgumentNullException.ThrowIfNull(categories);

        string dir = directoryPath.TrimEnd('/');
        if (!DirAccess.DirExistsAbsolute(dir))
            DirAccess.MakeDirRecursiveAbsolute(dir);

        foreach (var kvp in categories)
            WriteCategory($"{dir}/{kvp.Key}.yaml", kvp.Value);

        DeleteOrphans(dir, categories.Keys);

        foreach (var category in categories.Values)
        {
            category.IsNew = false;
            category.IsDirty = false;
            foreach (var engine in category.Engines)
            {
                engine.IsNew = false;
                engine.IsDirty = false;
            }
        }
    }

    private static void WriteCategory(string filePath, EngineEditorModel.EngineCategoryData category)
    {
        string yaml = BuildCategoryYaml(category);
        var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            var err = Godot.FileAccess.GetOpenError();
            GameLogger.Error($"Failed to open file for writing: {filePath}. Error: {err}");
            throw new InvalidOperationException($"Failed to open file for writing: {filePath}. Error: {err}");
        }
        try { file.StoreString(yaml); }
        finally { file.Close(); }
    }

    private static string BuildCategoryYaml(EngineEditorModel.EngineCategoryData category)
    {
        var sb = new StringBuilder();
        if (category.Engines.Count == 0)
        {
            sb.AppendLine("engines: []");
            return sb.ToString();
        }

        sb.AppendLine("engines:");
        for (int i = 0; i < category.Engines.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            var e = category.Engines[i];
            YamlIndent.AppendLine(sb, 1, $"- name: {EditorYamlScalar.QuoteIfNeeded(e.Name)}");
            YamlIndent.AppendLine(sb, 2, $"specific_impulse: {EditorYamlScalar.FormatFloat(e.SpecificImpulse)}");
            YamlIndent.AppendLine(sb, 2, $"thrust: {EditorYamlScalar.FormatFloat(e.Thrust)}");
            if (!string.IsNullOrEmpty(e.Description))
                YamlIndent.AppendLine(sb, 2, $"description: {EditorYamlScalar.QuoteIfNeeded(e.Description)}");
        }
        return sb.ToString();
    }

    private static void DeleteOrphans(string dir, IEnumerable<string> categoryKeys)
    {
        var keep = new HashSet<string>(categoryKeys);
        var dirAccess = DirAccess.Open(dir);
        if (dirAccess == null) return;
        dirAccess.ListDirBegin();
        string fileName = dirAccess.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (fileName.EndsWith(".yaml") && !fileName.StartsWith(".")
                && fileName != "EngineTypes.yaml")
            {
                string stem = fileName[..^".yaml".Length];
                if (!keep.Contains(stem))
                {
                    var err = dirAccess.Remove(fileName);
                    if (err != Error.Ok)
                        GameLogger.Warning($"Failed to delete orphan engine file: {dir}/{fileName}. Error: {err}");
                }
            }
            fileName = dirAccess.GetNext();
        }
        dirAccess.ListDirEnd();
    }
}
#endif
