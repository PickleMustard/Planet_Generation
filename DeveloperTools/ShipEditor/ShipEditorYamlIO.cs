#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using DeveloperTools.Common;
using UtilityLibrary;

namespace DeveloperTools.ShipEditor;

/// <summary>
/// Writes ship category data back to YAML, one file per category
/// ({directory}/{category}.yaml), matching the format ShipConfigLoader reads.
/// Deletes orphan files for removed categories and resets dirty flags.
/// </summary>
public static class ShipEditorYamlIO
{
    public static void WriteAllCategories(string directoryPath,
        Dictionary<string, ShipEditorModel.ShipCategoryData> categories)
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
            foreach (var ship in category.Ships)
            {
                ship.IsNew = false;
                ship.IsDirty = false;
            }
        }
    }

    private static void WriteCategory(string filePath, ShipEditorModel.ShipCategoryData category)
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

    private static string BuildCategoryYaml(ShipEditorModel.ShipCategoryData category)
    {
        var sb = new StringBuilder();
        if (category.Ships.Count == 0)
        {
            sb.AppendLine("ships: []");
            return sb.ToString();
        }

        sb.AppendLine("ships:");
        for (int i = 0; i < category.Ships.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            WriteShip(sb, category.Ships[i]);
        }
        return sb.ToString();
    }

    private static void WriteShip(StringBuilder sb, ShipEditorModel.ShipEditEntry s)
    {
        YamlIndent.AppendLine(sb, 1, $"- name: {EditorYamlScalar.QuoteIfNeeded(s.Name)}");
        if (!string.IsNullOrEmpty(s.Description))
            YamlIndent.AppendLine(sb, 2, $"description: {EditorYamlScalar.QuoteIfNeeded(s.Description)}");
        YamlIndent.AppendLine(sb, 2, $"dry_mass: {EditorYamlScalar.FormatFloat(s.DryMass)}");
        YamlIndent.AppendLine(sb, 2, $"cargo_capacity: {EditorYamlScalar.FormatFloat(s.CargoCapacity)}");
        YamlIndent.AppendLine(sb, 2, $"fuel_capacity: {EditorYamlScalar.FormatFloat(s.FuelCapacity)}");
        if (!string.IsNullOrEmpty(s.EngineCategory))
            YamlIndent.AppendLine(sb, 2, $"engine_category: {EditorYamlScalar.QuoteIfNeeded(s.EngineCategory)}");
        YamlIndent.AppendLine(sb, 2, $"work_required: {EditorYamlScalar.FormatFloat(s.WorkRequired)}");

        EditorBlockIO.WriteRequiredResources(sb, s.RequiredResources, keyLevel: 2);
        EditorBlockIO.WriteVisual(sb, s.Visual, keyLevel: 2);
        EditorBlockIO.WriteIcon(sb, s.Icon, keyLevel: 2);
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
                && fileName != "ShipTemplates.yaml")
            {
                string stem = fileName[..^".yaml".Length];
                if (!keep.Contains(stem))
                {
                    var err = dirAccess.Remove(fileName);
                    if (err != Error.Ok)
                        GameLogger.Warning($"Failed to delete orphan ship file: {dir}/{fileName}. Error: {err}");
                }
            }
            fileName = dirAccess.GetNext();
        }
        dirAccess.ListDirEnd();
    }
}
#endif
