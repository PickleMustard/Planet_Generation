#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using UtilityLibrary;

namespace DeveloperTools.Building2DEditor;

public static class Building2DEditorYamlIO
{
    public static void WriteAll(
        string shapesDirectory,
        IReadOnlyList<Building2DEditorModel.ShapeEditEntry> shapes,
        IReadOnlySet<string> loadedSourceFiles)
    {
        ArgumentNullException.ThrowIfNull(shapesDirectory);
        ArgumentNullException.ThrowIfNull(shapes);

        string root = shapesDirectory.TrimEnd('/') + "/";
        if (!DirAccess.DirExistsAbsolute(root))
            DirAccess.MakeDirRecursiveAbsolute(root);

        var written = new HashSet<string>();
        foreach (var entry in shapes)
        {
            string path = (entry.IsNew || string.IsNullOrEmpty(entry.SourceFilePath))
                ? $"{root}{entry.Id}.yaml"
                : entry.SourceFilePath;
            entry.SourceFilePath = path;
            string yaml = BuildShapeYaml(entry);
            WriteText(path, yaml);
            written.Add(path);
        }

        foreach (var oldFile in loadedSourceFiles)
        {
            if (written.Contains(oldFile)) continue;
            TryDeleteFile(oldFile);
        }

        foreach (var entry in shapes)
        {
            entry.IsNew = false;
            entry.IsDirty = false;
        }
    }

    private static string BuildShapeYaml(Building2DEditorModel.ShapeEditEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append("id: ").AppendLine(entry.Id);
        sb.Append("display_name: ").AppendLine(QuoteIfNeeded(entry.DisplayName));

        sb.AppendLine("vertices:");
        foreach (var v in entry.Vertices)
        {
            sb.Append(YamlIndent.Of(1))
              .Append("- [")
              .Append(FormatFloat(v.X)).Append(", ")
              .Append(FormatFloat(v.Y))
              .AppendLine("]");
        }

        sb.AppendLine("sides:");
        foreach (var side in entry.Sides)
        {
            if (side.Slots.Count == 0)
            {
                sb.Append(YamlIndent.Of(1)).AppendLine("- { slots: [] }");
            }
            else
            {
                var slots = new List<string>(side.Slots.Count);
                foreach (var s in side.Slots)
                {
                    string kind = s.Kind.ToString().ToLowerInvariant();
                    string state = s.StateOfMatter.ToString().ToLowerInvariant();
                    slots.Add($"{{ kind: {kind}, state: {state} }}");
                }
                sb.Append(YamlIndent.Of(1))
                  .Append("- { slots: [")
                  .Append(string.Join(", ", slots))
                  .AppendLine("] }");
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

    private static string QuoteIfNeeded(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        foreach (var c in s)
        {
            if (c == ':' || c == '#' || c == '"' || c == '\n' || c == '\r' || c == '\t')
                return $"\"{s.Replace("\"", "\\\"")}\"";
        }
        if (char.IsDigit(s[0]) || s[0] == '-')
            return $"\"{s}\"";
        return s;
    }

    private static void WriteText(string filePath, string content)
    {
        var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            var err = Godot.FileAccess.GetOpenError();
            GameLogger.Error($"Building2DEditorYamlIO: failed to open {filePath}: {err}");
            throw new InvalidOperationException($"Failed to write {filePath}: {err}");
        }
        try { file.StoreString(content); }
        finally { file.Close(); }
    }

    private static void TryDeleteFile(string filePath)
    {
        if (!Godot.FileAccess.FileExists(filePath)) return;
        int lastSlash = filePath.LastIndexOf('/');
        if (lastSlash <= 0) return;
        string dir = filePath[..(lastSlash + 1)];
        string fileName = filePath[(lastSlash + 1)..];
        var dirAccess = DirAccess.Open(dir);
        if (dirAccess == null) return;
        var err = dirAccess.Remove(fileName);
        if (err != Error.Ok)
            GameLogger.Warning($"Building2DEditorYamlIO: failed to delete {filePath}: {err}");
    }
}
#endif
