#if DEBUG
using System.Collections.Generic;
using System.Text;
using Godot;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.Common;

/// <summary>
/// Raw-YAML parse and emit helpers for blocks shared by the ship and station
/// editors: <c>required_resources</c>, <c>icon</c>, and <c>visual</c>. Parsing
/// reads stored values directly (no texture/model loading) so the editors
/// round-trip exactly; emission mirrors BuildingEditorYamlIO's conventions.
/// </summary>
public static class EditorBlockIO
{
    // ── Parse (from a deserialized entry dictionary) ─────────────────────

    /// <summary>Reads <c>required_resources:</c> (name → amount), preserving order.</summary>
    public static List<EditorResourceAmount> ParseRequiredResources(Dictionary<object, object> dict)
    {
        var list = new List<EditorResourceAmount>();
        if (!dict.TryGetValue("required_resources", out var raw)
            || raw is not Dictionary<object, object> resourceDict)
            return list;

        foreach (var kvp in resourceDict)
        {
            string id = kvp.Key?.ToString() ?? "";
            if (string.IsNullOrEmpty(id)) continue;
            list.Add(new EditorResourceAmount
            {
                ResourceId = id,
                Amount = BaseConfigLoader.NodeToInt(kvp.Value, 0)
            });
        }
        return list;
    }

    /// <summary>Reads the <c>icon:</c> block (raw base_path, scale, tint).</summary>
    public static EditorIcon ParseIcon(Dictionary<object, object> dict)
    {
        var icon = new EditorIcon();
        if (!dict.TryGetValue("icon", out var raw) || raw is not Dictionary<object, object> iconDict)
            return icon;

        string basePath = BaseConfigLoader.ReadString(iconDict, "base_path", "");
        icon.BasePath = string.IsNullOrEmpty(basePath) ? null : basePath;
        icon.Scale = BaseConfigLoader.ReadFloat(iconDict, "scale", 1.0f);
        icon.Tint = ReadColor(iconDict, "tint", Colors.White);
        return icon;
    }

    /// <summary>Reads the <c>visual:</c> block (no model/texture loading).</summary>
    public static EditorVisual ParseVisual(Dictionary<object, object> dict)
    {
        var v = new EditorVisual();
        if (!dict.TryGetValue("visual", out var raw) || raw is not Dictionary<object, object> visualDict)
            return v;

        string modelPath = BaseConfigLoader.ReadString(visualDict, "model_path", "");
        v.ModelPath = string.IsNullOrEmpty(modelPath) ? null : modelPath;
        string material = BaseConfigLoader.ReadString(visualDict, "model_material", "");
        v.ModelMaterial = string.IsNullOrEmpty(material) ? null : material;
        string anim = BaseConfigLoader.ReadString(visualDict, "animation_path", "");
        v.AnimationPath = string.IsNullOrEmpty(anim) ? null : anim;
        v.Scale = BaseConfigLoader.ReadFloat(visualDict, "scale", 1.0f);
        v.RotationOffset = BaseConfigLoader.ReadVector3(visualDict, "rotation_offset", Vector3.Zero);
        v.ShapeId = BaseConfigLoader.ReadString(visualDict, "shape_id", "hexagon").Trim();
        if (string.IsNullOrEmpty(v.ShapeId)) v.ShapeId = "hexagon";
        v.ShapeSize = BaseConfigLoader.ReadFloat(visualDict, "shape_size", 64f);
        return v;
    }

    private static Color ReadColor(Dictionary<object, object> dict, string key, Color fallback)
    {
        if (!dict.TryGetValue(key, out var raw)
            || raw is not List<object> arr || arr.Count < 3)
            return fallback;

        float r = BaseConfigLoader.NodeToFloat(arr[0], fallback.R);
        float g = BaseConfigLoader.NodeToFloat(arr[1], fallback.G);
        float b = BaseConfigLoader.NodeToFloat(arr[2], fallback.B);
        float a = arr.Count >= 4 ? BaseConfigLoader.NodeToFloat(arr[3], fallback.A) : 1.0f;

        if (r > 1.0f || g > 1.0f || b > 1.0f || a > 1.0f)
        {
            r /= 255.0f; g /= 255.0f; b /= 255.0f;
            a = arr.Count >= 4 ? a / 255.0f : 1.0f;
        }
        return new Color(r, g, b, a);
    }

    // ── Emit (into a category file builder) ──────────────────────────────

    /// <summary>Writes <c>required_resources:</c> at the given indent (key level / item level).</summary>
    public static void WriteRequiredResources(StringBuilder sb,
        List<EditorResourceAmount> resources, int keyLevel)
    {
        var valid = resources.FindAll(r => !string.IsNullOrEmpty(r.ResourceId));
        if (valid.Count == 0) return;

        YamlIndent.AppendLine(sb, keyLevel, "required_resources:");
        foreach (var r in valid)
            YamlIndent.AppendLine(sb, keyLevel + 1, $"{r.ResourceId}: {r.Amount}");
    }

    /// <summary>Writes the <c>visual:</c> block, omitting defaults. Skips it entirely if empty.</summary>
    public static void WriteVisual(StringBuilder sb, EditorVisual v, int keyLevel)
    {
        bool any = !string.IsNullOrEmpty(v.ModelPath)
                || !string.IsNullOrEmpty(v.ModelMaterial)
                || !string.IsNullOrEmpty(v.AnimationPath)
                || !Mathf.IsEqualApprox(v.Scale, 1f)
                || v.RotationOffset != Vector3.Zero
                || v.ShapeId != "hexagon"
                || !Mathf.IsEqualApprox(v.ShapeSize, 64f);
        if (!any) return;

        int f = keyLevel + 1;
        YamlIndent.AppendLine(sb, keyLevel, "visual:");
        if (!string.IsNullOrEmpty(v.ModelPath))
            YamlIndent.AppendLine(sb, f, $"model_path: \"{v.ModelPath}\"");
        if (!string.IsNullOrEmpty(v.ModelMaterial))
            YamlIndent.AppendLine(sb, f, $"model_material: \"{v.ModelMaterial}\"");
        if (!string.IsNullOrEmpty(v.AnimationPath))
            YamlIndent.AppendLine(sb, f, $"animation_path: \"{v.AnimationPath}\"");
        if (!Mathf.IsEqualApprox(v.Scale, 1f))
            YamlIndent.AppendLine(sb, f, $"scale: {EditorYamlScalar.FormatFloat(v.Scale)}");
        if (v.RotationOffset != Vector3.Zero)
            YamlIndent.AppendLine(sb, f, $"rotation_offset: {EditorYamlScalar.Vector3(v.RotationOffset)}");
        if (v.ShapeId != "hexagon")
            YamlIndent.AppendLine(sb, f, $"shape_id: {v.ShapeId}");
        if (!Mathf.IsEqualApprox(v.ShapeSize, 64f))
            YamlIndent.AppendLine(sb, f, $"shape_size: {EditorYamlScalar.FormatFloat(v.ShapeSize)}");
    }

    /// <summary>Writes the <c>icon:</c> block. Skipped when base_path is empty.</summary>
    public static void WriteIcon(StringBuilder sb, EditorIcon icon, int keyLevel)
    {
        if (string.IsNullOrEmpty(icon.BasePath)) return;

        int f = keyLevel + 1;
        YamlIndent.AppendLine(sb, keyLevel, "icon:");
        YamlIndent.AppendLine(sb, f, $"base_path: \"{icon.BasePath}\"");
        if (!Mathf.IsEqualApprox(icon.Scale, 1f))
            YamlIndent.AppendLine(sb, f, $"scale: {EditorYamlScalar.FormatFloat(icon.Scale)}");
        if (icon.Tint != Colors.White)
            YamlIndent.AppendLine(sb, f, $"tint: {EditorYamlScalar.Color(icon.Tint)}");
    }
}
#endif
