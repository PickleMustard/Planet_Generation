#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using UtilityLibrary;

namespace DeveloperTools.LinkProfileEditor;

/// <summary>
/// Writes LinkProfile editor data to <c>link_profiles.yaml</c>.
/// Output mirrors the existing format. All indentation goes through
/// <see cref="YamlIndent"/> to guarantee space-based output.
/// </summary>
public static class LinkProfileEditorYamlIO
{
    public static void WriteAll(string filePath, IReadOnlyList<LinkProfileEditorModel.LinkProfileEditEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(entries);

        string yamlContent = BuildYaml(entries);

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
            file.StoreString(yamlContent);
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Failed to write link profiles to {filePath}: {ex.Message}");
            throw;
        }
        finally
        {
            file.Close();
        }

        foreach (var entry in entries)
        {
            entry.IsNew = false;
            entry.IsDirty = false;
        }
    }

    private static string BuildYaml(IReadOnlyList<LinkProfileEditorModel.LinkProfileEditEntry> entries)
    {
        var sb = new StringBuilder();

        if (entries.Count == 0)
        {
            sb.AppendLine("link_profiles: []");
            return sb.ToString();
        }

        sb.AppendLine("link_profiles:");
        foreach (var e in entries)
        {
            YamlIndent.AppendLine(sb, 1, $"- id_name: {e.IdName}");
            YamlIndent.AppendLine(sb, 2, $"tier: {e.Tier}");
            YamlIndent.AppendLine(sb, 2, $"transport_speed: {FormatFloat(e.TransportSpeed)}");
            YamlIndent.AppendLine(sb, 2, $"package_size: {e.PackageSize}");
            YamlIndent.AppendLine(sb, 2, $"bundle_time: {e.BundleTime}");
            YamlIndent.AppendLine(sb, 2, $"slot_capacity: {e.SlotCapacity}");
            YamlIndent.AppendLine(sb, 2, $"state_of_matter: {e.StateOfMatter.ToString().ToLowerInvariant()}");
            YamlIndent.AppendLine(sb, 2, $"construction_work: {FormatFloat(e.ConstructionWork)}");
            if (e.CostPerDistance.Count > 0)
            {
                YamlIndent.AppendLine(sb, 2, "cost_per_distance:");
                foreach (var kvp in e.CostPerDistance)
                {
                    YamlIndent.AppendLine(sb, 3, $"- resource: {kvp.Key}");
                    YamlIndent.AppendLine(sb, 4, $"amount: {kvp.Value}");
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
}
#endif
