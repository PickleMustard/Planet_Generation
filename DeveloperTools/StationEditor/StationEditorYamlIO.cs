#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using DeveloperTools.Common;
using UtilityLibrary;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace DeveloperTools.StationEditor;

/// <summary>
/// Writes station category data back to YAML, one file per category
/// ({directory}/{category}.yaml), matching the format StationConfigLoader reads
/// — including behaviors, slot_filters, and the optional transfer_station block.
/// Deletes orphan files for removed categories and resets dirty flags.
/// </summary>
public static class StationEditorYamlIO
{
    public static void WriteAllCategories(string directoryPath,
        Dictionary<string, StationEditorModel.StationCategoryData> categories)
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
            foreach (var station in category.Stations)
            {
                station.IsNew = false;
                station.IsDirty = false;
            }
        }
    }

    private static void WriteCategory(string filePath, StationEditorModel.StationCategoryData category)
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

    private static string BuildCategoryYaml(StationEditorModel.StationCategoryData category)
    {
        var sb = new StringBuilder();
        if (category.Stations.Count == 0)
        {
            sb.AppendLine("stations: []");
            return sb.ToString();
        }

        sb.AppendLine("stations:");
        for (int i = 0; i < category.Stations.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            WriteStation(sb, category.Stations[i]);
        }
        return sb.ToString();
    }

    private static void WriteStation(StringBuilder sb, StationEditorModel.StationEditEntry s)
    {
        YamlIndent.AppendLine(sb, 1, $"- name: {EditorYamlScalar.QuoteIfNeeded(s.Name)}");
        if (!string.IsNullOrEmpty(s.StationType))
            YamlIndent.AppendLine(sb, 2, $"station_type: {EditorYamlScalar.QuoteIfNeeded(s.StationType)}");
        YamlIndent.AppendLine(sb, 2, $"construction_time: {EditorYamlScalar.FormatFloat(s.ConstructionTime)}");

        if (s.Behaviors.Count > 0)
        {
            YamlIndent.AppendLine(sb, 2, "behaviors:");
            foreach (var beh in s.Behaviors)
            {
                if (string.IsNullOrEmpty(beh.BehaviorId)) continue;

                if (beh.Config.Count == 0)
                {
                    // Bare string entry
                    YamlIndent.AppendLine(sb, 3, $"- {EditorYamlScalar.QuoteIfNeeded(beh.BehaviorId)}");
                }
                else
                {
                    YamlIndent.AppendLine(sb, 3, $"- behavior_id: {EditorYamlScalar.QuoteIfNeeded(beh.BehaviorId)}");
                    WriteBehaviorConfig(sb, beh.Config, indent: 4);
                }
            }
        }

        EditorBlockIO.WriteRequiredResources(sb, s.RequiredResources, keyLevel: 2);

        EditorBlockIO.WriteVisual(sb, s.Visual, keyLevel: 2);
        EditorBlockIO.WriteIcon(sb, s.Icon, keyLevel: 2);
    }

    private static void WriteBehaviorConfig(StringBuilder sb, SysDict config, int indent)
    {
        foreach (var kvp in config)
        {
            string key = EditorYamlScalar.QuoteIfNeeded(kvp.Key);
            object val = kvp.Value;

            if (val is Dictionary<object, object> nested)
            {
                YamlIndent.AppendLine(sb, indent, $"{key}:");
                foreach (var inner in nested)
                {
                    string innerKey = EditorYamlScalar.QuoteIfNeeded(inner.Key.ToString());
                    string innerVal = FormatConfigValue(inner.Value);
                    YamlIndent.AppendLine(sb, indent + 1, $"{innerKey}: {innerVal}");
                }
            }
            else
            {
                string valStr = FormatConfigValue(val);
                YamlIndent.AppendLine(sb, indent, $"{key}: {valStr}");
            }
        }
    }

    private static string FormatConfigValue(object val)
    {
        if (val is bool b) return b ? "true" : "false";
        if (val is int i) return i.ToString();
        if (val is long l) return l.ToString();
        if (val is double d) return EditorYamlScalar.FormatFloat((float)d);
        if (val is float f) return EditorYamlScalar.FormatFloat(f);
        if (val is string s) return EditorYamlScalar.QuoteIfNeeded(s);
        return val?.ToString() ?? "";
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
                && fileName != "StationTemplates.yaml")
            {
                string stem = fileName[..^".yaml".Length];
                if (!keep.Contains(stem))
                {
                    var err = dirAccess.Remove(fileName);
                    if (err != Error.Ok)
                        GameLogger.Warning($"Failed to delete orphan station file: {dir}/{fileName}. Error: {err}");
                }
            }
            fileName = dirAccess.GetNext();
        }
        dirAccess.ListDirEnd();
    }
}
#endif
