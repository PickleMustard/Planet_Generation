#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using UtilityLibrary;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Writes building entries back to YAML on disk, grouping by SourceFilePath so
/// multi-entry files (example_building.yaml) round-trip correctly. New entries
/// default to one file per building at {root}/{Category}/{IdName}.yaml.
/// All indentation uses <see cref="YamlIndent"/> to guarantee
/// space-based output (YAML forbids tab indentation).
/// </summary>
public static class BuildingEditorYamlIO
{
    /// <summary>
    /// Writes every building back to its SourceFilePath, deletes orphan YAML files
    /// (loaded but no longer referenced, plus stray YAML in category subdirs we wrote),
    /// removes empty / deleted category directories, and resets IsNew/IsDirty flags.
    /// </summary>
    public static void WriteAll(
        string buildingsDirectory,
        Dictionary<string, BuildingEditorModel.BuildingCategoryData> categories,
        IReadOnlySet<string> loadedSourceFiles)
    {
        ArgumentNullException.ThrowIfNull(buildingsDirectory);
        ArgumentNullException.ThrowIfNull(categories);

        string root = buildingsDirectory.TrimEnd('/') + "/";
        if (!DirAccess.DirExistsAbsolute(root))
            DirAccess.MakeDirRecursiveAbsolute(root);

        // Group entries by SourceFilePath across all categories so multi-entry
        // files (e.g. example_building.yaml) get rewritten with all their entries.
        var entriesByFile = new Dictionary<string, List<BuildingEditorModel.BuildingEditEntry>>();
        var dirsTouched = new HashSet<string>();

        foreach (var kvp in categories)
        {
            string category = kvp.Key;
            string categoryDir = $"{root}{category}/";

            foreach (var entry in kvp.Value.Buildings)
            {
                string sourcePath = (entry.IsNew || string.IsNullOrEmpty(entry.SourceFilePath))
                    ? DefaultSourcePath(root, category, entry.IdName)
                    : entry.SourceFilePath;
                entry.SourceFilePath = sourcePath;

                if (!entriesByFile.TryGetValue(sourcePath, out var list))
                    entriesByFile[sourcePath] = list = new();
                list.Add(entry);

                int lastSlash = sourcePath.LastIndexOf('/');
                if (lastSlash > 0)
                    dirsTouched.Add(sourcePath[..(lastSlash + 1)]);
            }

            // Ensure category dir exists even if currently empty so it shows up next load.
            if (!DirAccess.DirExistsAbsolute(categoryDir))
                DirAccess.MakeDirRecursiveAbsolute(categoryDir);
        }

        foreach (var (filePath, list) in entriesByFile)
        {
            // Ensure containing dir exists for newly-introduced category directories.
            int lastSlash = filePath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                string dir = filePath[..(lastSlash + 1)];
                if (!DirAccess.DirExistsAbsolute(dir))
                    DirAccess.MakeDirRecursiveAbsolute(dir);
            }

            string yaml = BuildBuildingsYaml(list);
            WriteText(filePath, yaml);
        }

        // Orphan cleanup: any previously-loaded file we did NOT write goes away.
        var writtenSet = new HashSet<string>(entriesByFile.Keys);
        foreach (var oldFile in loadedSourceFiles)
        {
            if (writtenSet.Contains(oldFile)) continue;
            TryDeleteFile(oldFile);
        }

        // Remove now-empty subdirectories that were once loaded.
        foreach (var subdir in DirAccess.GetDirectoriesAt(root))
        {
            if (!categories.ContainsKey(subdir))
            {
                string fullSubdir = $"{root}{subdir}/";
                DeleteAllYamlInDir(fullSubdir);
                TryRemoveEmptyDir(fullSubdir);
            }
        }

        // Reset dirty flags.
        foreach (var category in categories.Values)
        {
            category.IsNew = false;
            category.IsDirty = false;
            foreach (var entry in category.Buildings)
            {
                entry.IsNew = false;
                entry.IsDirty = false;
            }
        }
    }

    private static string DefaultSourcePath(string root, string category, string idName)
    {
        string safeId = string.IsNullOrEmpty(idName) ? "new_building" : idName;
        return $"{root}{category}/{safeId}.yaml";
    }

    // ── YAML emission ────────────────────────────────────────────────────

    private static string BuildBuildingsYaml(List<BuildingEditorModel.BuildingEditEntry> entries)
    {
        var sb = new StringBuilder();
        if (entries.Count == 0)
        {
            sb.AppendLine("buildings: []");
            return sb.ToString();
        }

        sb.AppendLine("buildings:");
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.AppendLine();
            WriteBuilding(sb, entries[i]);
        }
        return sb.ToString();
    }

    private static void WriteBuilding(StringBuilder sb, BuildingEditorModel.BuildingEditEntry entry)
    {
        // Level 1: list item "- id_name: ..."
        // Level 2: list item properties "display_name: ..."
        // Level 3: nested block keys (icon, placement, behavior config)
        // Level 4: deep nested (placement fields, behavior config fields)
        // Level 5: very deep nested (configurable_behavior config values)

        YamlIndent.AppendLine(sb, 1, $"- id_name: {entry.IdName}");
        YamlIndent.AppendLine(sb, 2, $"display_name: {QuoteIfNeeded(entry.DisplayName)}");
        if (!string.IsNullOrEmpty(entry.Description))
            YamlIndent.AppendLine(sb, 2, $"description: {QuoteIfNeeded(entry.Description)}");
        if (!string.IsNullOrEmpty(entry.Category))
            YamlIndent.AppendLine(sb, 2, $"category: {entry.Category}");
        if (entry.BuildingLimit != -1)
            YamlIndent.AppendLine(sb, 2, $"building_limit: {entry.BuildingLimit}");
        YamlIndent.AppendLine(sb, 2, $"max_resource_tier: {entry.MaxResourceTier}");
        YamlIndent.AppendLine(sb, 2, $"work_required: {FormatFloat(entry.WorkRequired)}");
        if (!entry.Demolishable)
            YamlIndent.AppendLine(sb, 2, $"demolishable: false");
        if (!string.IsNullOrEmpty(entry.LinkProfile))
            YamlIndent.AppendLine(sb, 2, $"link_profile: {QuoteIfNeeded(entry.LinkProfile!)}");
        if (!string.IsNullOrEmpty(entry.AllowedRecipeCategory))
            YamlIndent.AppendLine(sb, 2, $"allowed_recipe_category: {QuoteIfNeeded(entry.AllowedRecipeCategory!)}");

        WritePlacement(sb, entry.Placement);

        if (entry.RequiredResources.Count > 0)
        {
            YamlIndent.AppendLine(sb, 2, "required_resources:");
            foreach (var req in entry.RequiredResources)
            {
                if (string.IsNullOrEmpty(req.ResourceId)) continue;
                YamlIndent.AppendLine(sb, 3, $"{req.ResourceId}: {req.Amount}");
            }
        }

        if (entry.Behaviors.Count > 0)
        {
            YamlIndent.AppendLine(sb, 2, "behaviors:");
            foreach (var beh in entry.Behaviors)
                WriteBehavior(sb, beh);
        }

        WriteSpecifier(sb, entry);
        WriteVisual(sb, entry.Visual);
        WriteIcon(sb, entry.Icon);
    }

    private static void WriteSpecifier(StringBuilder sb, BuildingEditorModel.BuildingEditEntry entry)
    {
        if (!entry.SpecifierEnabled || entry.SpecifierEntries.Count == 0)
            return;

        YamlIndent.AppendLine(sb, 2, "specifier:");
        YamlIndent.AppendLine(sb, 3,
            $"values: [{string.Join(", ", entry.SpecifierEntries.Select(s => s.Value))}]");

        bool anyLabel = entry.SpecifierEntries.Any(s => !string.IsNullOrEmpty(s.Label));
        if (anyLabel)
        {
            string labels = string.Join(", ",
                entry.SpecifierEntries.Select(s => $"\"{Escape(s.Label)}\""));
            YamlIndent.AppendLine(sb, 3, $"labels: [{labels}]");
        }

        YamlIndent.AppendLine(sb, 3, $"default: {entry.SpecifierDefault}");
    }

    private static void WritePlacement(StringBuilder sb, BuildingEditorModel.PlacementReqEdit p)
    {
        YamlIndent.AppendLine(sb, 2, "placement_requirements:");

        if (p.AllowAnyBiome)
            YamlIndent.AppendLine(sb, 3, "biomes: [\"*\"]");
        else if (p.Biomes.Count > 0)
        {
            var ordered = p.Biomes.OrderBy(b => b, StringComparer.OrdinalIgnoreCase);
            YamlIndent.AppendLine(sb, 3, $"biomes: [{string.Join(", ", ordered)}]");
        }

        YamlIndent.AppendLine(sb, 3, $"min_elevation: {FormatFloat(p.MinElevation)}");
        YamlIndent.AppendLine(sb, 3, $"max_elevation: {FormatFloat(p.MaxElevation)}");
        YamlIndent.AppendLine(sb, 3, $"max_slope: {FormatFloat(p.MaxSlope)}");
        YamlIndent.AppendLine(sb, 3, $"cell_count: {p.CellCount}");
        if (p.RequiresAdjacent)
            YamlIndent.AppendLine(sb, 3, "requires_adjacent: true");

        if (!string.IsNullOrEmpty(p.ConfigurableBehavior))
        {
            if (p.ConfigurableBehaviorConfig.Count == 0
                && PlacementBehaviorSchemaRegistry.GetSchema(p.ConfigurableBehavior).Count == 0)
            {
                YamlIndent.AppendLine(sb, 3, $"configurable_behavior: \"{p.ConfigurableBehavior}\"");
            }
            else
            {
                YamlIndent.AppendLine(sb, 3, "configurable_behavior:");
                YamlIndent.AppendLine(sb, 4, $"behavior_class: {p.ConfigurableBehavior}");
                var schema = PlacementBehaviorSchemaRegistry.GetSchema(p.ConfigurableBehavior);
                var written = new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in schema)
                {
                    if (p.ConfigurableBehaviorConfig.TryGetValue(field.Name, out var val))
                    {
                        sb.Append(YamlIndent.Of(4)).Append(field.Name).Append(": ")
                          .AppendLine(EmitTyped(val, field.FieldType, indentLevel: 4));
                        written.Add(field.Name);
                    }
                }
                foreach (var kvp in p.ConfigurableBehaviorConfig)
                {
                    if (written.Contains(kvp.Key)) continue;
                    sb.Append(YamlIndent.Of(4)).Append(kvp.Key).Append(": ")
                      .AppendLine(EmitUntyped(kvp.Value, indentLevel: 4));
                }
            }
        }
    }

    private static void WriteBehavior(StringBuilder sb, BuildingEditorModel.BehaviorEntryEdit beh)
    {
        YamlIndent.AppendLine(sb, 3, $"- behavior_id: {beh.BehaviorId}");
        var schema = BehaviorSchemaRegistry.GetSchema(beh.BehaviorId);
        var written = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in schema)
        {
            if (beh.Config.TryGetValue(field.Name, out var val))
            {
                sb.Append(YamlIndent.Of(4)).Append(field.Name).Append(": ")
                  .AppendLine(EmitTyped(val, field.FieldType, indentLevel: 4));
                written.Add(field.Name);
            }
        }
        foreach (var kvp in beh.Config)
        {
            if (written.Contains(kvp.Key)) continue;
            sb.Append(YamlIndent.Of(4)).Append(kvp.Key).Append(": ")
              .AppendLine(EmitUntyped(kvp.Value, indentLevel: 4));
        }
    }

    private static void WriteVisual(StringBuilder sb, BuildingEditorModel.VisualEdit v)
    {
        bool any = !string.IsNullOrEmpty(v.ModelResourcePath)
                || !string.IsNullOrEmpty(v.ModelMaterial)
                || !string.IsNullOrEmpty(v.AnimationPath)
                || !string.IsNullOrEmpty(v.AnimationName)
                || !Mathf.IsEqualApprox(v.Scale, 1f)
                || v.RotationOffset != Vector3.Zero
                || v.ShapeId != "hexagon"
                || !Mathf.IsEqualApprox(v.ShapeSize, 64f);
        if (!any) return;

        YamlIndent.AppendLine(sb, 2, "visual:");
        if (!string.IsNullOrEmpty(v.ModelResourcePath))
            YamlIndent.AppendLine(sb, 3, $"model_resource: \"{v.ModelResourcePath}\"");
        if (!string.IsNullOrEmpty(v.ModelMaterial))
            YamlIndent.AppendLine(sb, 3, $"model_material: \"{v.ModelMaterial}\"");
        if (!string.IsNullOrEmpty(v.AnimationPath))
            YamlIndent.AppendLine(sb, 3, $"animation_path: \"{v.AnimationPath}\"");
        if (!string.IsNullOrEmpty(v.AnimationName))
            YamlIndent.AppendLine(sb, 3, $"animation_name: \"{v.AnimationName}\"");
        if (!Mathf.IsEqualApprox(v.Scale, 1f))
            YamlIndent.AppendLine(sb, 3, $"scale: {FormatFloat(v.Scale)}");
        if (v.RotationOffset != Vector3.Zero)
        {
            YamlIndent.AppendLine(sb, 3,
                $"rotation_offset: [{FormatFloat(v.RotationOffset.X)}, " +
                $"{FormatFloat(v.RotationOffset.Y)}, {FormatFloat(v.RotationOffset.Z)}]");
        }
        if (v.ShapeId != "hexagon")
            YamlIndent.AppendLine(sb, 3, $"shape_id: {v.ShapeId}");
        if (!Mathf.IsEqualApprox(v.ShapeSize, 64f))
            YamlIndent.AppendLine(sb, 3, $"shape_size: {FormatFloat(v.ShapeSize)}");
    }

    private static void WriteIcon(StringBuilder sb, BuildingEditorModel.IconEdit icon)
    {
        if (string.IsNullOrEmpty(icon.ResourcePath)) return;

        YamlIndent.AppendLine(sb, 2, "icon:");
        YamlIndent.AppendLine(sb, 3, $"resource: \"{icon.ResourcePath}\"");
        if (!Mathf.IsEqualApprox(icon.Scale, 1f))
            YamlIndent.AppendLine(sb, 3, $"scale: {FormatFloat(icon.Scale)}");
        if (icon.Tint != Colors.White)
        {
            YamlIndent.AppendLine(sb, 3,
                $"tint: [{FormatFloat(icon.Tint.R)}, {FormatFloat(icon.Tint.G)}, " +
                $"{FormatFloat(icon.Tint.B)}, {FormatFloat(icon.Tint.A)}]");
        }
    }

    // ── Typed value emission (schema-known) ──────────────────────────────

    private static string EmitTyped(object value, BehaviorFieldType type, int indentLevel)
    {
        switch (type)
        {
            case BehaviorFieldType.Float:
                return FormatFloat(ConvertToFloat(value));
            case BehaviorFieldType.Int:
                return ConvertToInt(value).ToString(CultureInfo.InvariantCulture);
            case BehaviorFieldType.Bool:
                return (value is bool b ? b : ParseBool(value)) ? "true" : "false";
            case BehaviorFieldType.String:
                return QuoteIfNeeded(value?.ToString() ?? "");
            case BehaviorFieldType.ResourceId:
            case BehaviorFieldType.RecipeId:
                return $"\"{Escape(value?.ToString() ?? "")}\"";
            case BehaviorFieldType.StringList:
            case BehaviorFieldType.RecipeIdList:
                return EmitStringList(value);
            case BehaviorFieldType.SlotFiltersDict:
            case BehaviorFieldType.StringIntDict:
                return EmitStringIntDict(value, indentLevel);
            default:
                return EmitUntyped(value, indentLevel);
        }
    }

    private static string EmitStringList(object value)
    {
        var items = new List<string>();
        if (value is IEnumerable<string> ss)
        {
            foreach (var s in ss) items.Add($"\"{Escape(s)}\"");
        }
        else if (value is List<object> lo)
        {
            foreach (var s in lo) items.Add($"\"{Escape(s?.ToString() ?? "")}\"");
        }
        return $"[{string.Join(", ", items)}]";
    }

    private static string EmitStringIntDict(object value, int indentLevel)
    {
        var entries = new List<KeyValuePair<string, int>>();
        if (value is Dictionary<string, int> sid)
        {
            foreach (var kvp in sid) entries.Add(kvp);
        }
        else if (value is Dictionary<object, object> oo)
        {
            foreach (var kvp in oo)
            {
                string k = kvp.Key?.ToString() ?? "";
                int v = ConvertToInt(kvp.Value);
                entries.Add(new KeyValuePair<string, int>(k, v));
            }
        }
        if (entries.Count == 0) return "{}";

        var sb = new StringBuilder();
        string indent = YamlIndent.Of(indentLevel + 1);
        foreach (var e in entries)
        {
            sb.AppendLine();
            sb.Append(indent).Append(e.Key).Append(": ").Append(e.Value);
        }
        return sb.ToString();
    }

    // ── Untyped (preserves arbitrary YAML round-trip for unknown keys) ───

    private static string EmitUntyped(object? value, int indentLevel)
    {
        switch (value)
        {
            case null:
                return "null";
            case bool b:
                return b ? "true" : "false";
            case int i:
                return i.ToString(CultureInfo.InvariantCulture);
            case long l:
                return l.ToString(CultureInfo.InvariantCulture);
            case float f:
                return FormatFloat(f);
            case double d:
                return FormatFloat((float)d);
            case string s:
                return QuoteIfNeeded(s);
            case List<object> list:
                {
                    var parts = list.Select(x => EmitUntypedInline(x)).ToList();
                    return $"[{string.Join(", ", parts)}]";
                }
            case Dictionary<object, object> dict:
                {
                    if (dict.Count == 0) return "{}";
                    var sb = new StringBuilder();
                    string childIndent = YamlIndent.Of(indentLevel + 1);
                    foreach (var kvp in dict)
                    {
                        sb.AppendLine();
                        sb.Append(childIndent)
                          .Append(kvp.Key?.ToString() ?? "")
                          .Append(": ")
                          .Append(EmitUntyped(kvp.Value, indentLevel + 1));
                    }
                    return sb.ToString();
                }
            default:
                return QuoteIfNeeded(value.ToString() ?? "");
        }
    }

    private static string EmitUntypedInline(object? value)
    {
        switch (value)
        {
            case null: return "null";
            case bool b: return b ? "true" : "false";
            case int i: return i.ToString(CultureInfo.InvariantCulture);
            case long l: return l.ToString(CultureInfo.InvariantCulture);
            case float f: return FormatFloat(f);
            case double d: return FormatFloat((float)d);
            case string s: return $"\"{Escape(s)}\"";
            default: return $"\"{Escape(value.ToString() ?? "")}\"";
        }
    }

    // ── Formatting helpers ──────────────────────────────────────────────

    private static string FormatFloat(float value)
    {
        if (Mathf.IsEqualApprox(value, Mathf.Round(value)))
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Escape(string s)
    {
        return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Quotes a scalar when it contains characters YAML would interpret
    /// (colons, leading dashes, braces, brackets, etc.) or starts with a digit
    /// that would otherwise scan as a number. Bare alphanumeric strings stay unquoted.
    /// </summary>
    private static string QuoteIfNeeded(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        if (NeedsQuoting(s)) return $"\"{Escape(s)}\"";
        return s;
    }

    private static bool NeedsQuoting(string s)
    {
        if (s.Length == 0) return true;
        char c0 = s[0];
        if (char.IsDigit(c0) || c0 == '-' || c0 == '?' || c0 == '*' || c0 == '&'
            || c0 == '[' || c0 == ']' || c0 == '{' || c0 == '}' || c0 == '#'
            || c0 == '|' || c0 == '>' || c0 == '!' || c0 == '%' || c0 == '@' || c0 == '`')
            return true;
        if (s == "true" || s == "false" || s == "null" || s == "~"
            || s == "True" || s == "False" || s == "Null"
            || s == "yes" || s == "no" || s == "on" || s == "off")
            return true;
        foreach (var c in s)
        {
            if (c == ':' || c == '#' || c == '"' || c == '\n' || c == '\r' || c == '\t')
                return true;
        }
        return false;
    }

    private static float ConvertToFloat(object value)
    {
        if (value is float f) return f;
        if (value is double d) return (float)d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is string s
            && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
            return r;
        return 0f;
    }

    private static int ConvertToInt(object? value)
    {
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is float f) return (int)f;
        if (value is double d) return (int)d;
        if (value is string s
            && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r))
            return r;
        return 0;
    }

    private static bool ParseBool(object value)
    {
        return value is string s && bool.TryParse(s, out var b) && b;
    }

    // ── File system helpers ─────────────────────────────────────────────

    private static void WriteText(string filePath, string content)
    {
        var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            var err = Godot.FileAccess.GetOpenError();
            GameLogger.Error($"Failed to open file for writing: {filePath}. Error: {err}");
            throw new InvalidOperationException(
                $"Failed to open file for writing: {filePath}. Error: {err}");
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
            GameLogger.Warning($"Failed to delete orphan building file: {filePath}. Error: {err}");
    }

    private static void DeleteAllYamlInDir(string dirPath)
    {
        var dirAccess = DirAccess.Open(dirPath);
        if (dirAccess == null) return;
        dirAccess.ListDirBegin();
        string fileName = dirAccess.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!fileName.StartsWith(".")
                && (fileName.EndsWith(".yaml") || fileName.EndsWith(".yml")))
            {
                var err = dirAccess.Remove(fileName);
                if (err != Error.Ok)
                    GameLogger.Warning($"Failed to delete building file: {dirPath}{fileName}. Error: {err}");
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

        string parent = dirPath.TrimEnd('/');
        int lastSlash = parent.LastIndexOf('/');
        if (lastSlash < 0) return;
        string parentDir = parent[..(lastSlash + 1)];
        string leaf = parent[(lastSlash + 1)..];
        var parentAccess = DirAccess.Open(parentDir);
        if (parentAccess == null) return;
        var err = parentAccess.Remove(leaf);
        if (err != Error.Ok)
            GameLogger.Warning($"Failed to remove empty building dir: {dirPath}. Error: {err}");
    }
}
#endif
