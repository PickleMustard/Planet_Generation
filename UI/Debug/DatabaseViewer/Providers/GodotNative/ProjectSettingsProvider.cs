#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UI.Debug;

namespace UI.Debug.DatabaseViewer;

/// <summary>
/// Data provider for project settings: all configurable project properties.
/// </summary>
[DebugData("Project Settings", Category = "Settings")]
public class ProjectSettingsProvider : IDataProvider
{
    private DebugDataNode _cachedData;
    private bool _needsRefresh = true;

    private static readonly string[] KnownCategories = new[]
    {
        "application",
        "audio",
        "autoload",
        "debug",
        "display",
        "gui",
        "input",
        "internationalization",
        "layer_names",
        "logging",
        "memory",
        "navigation",
        "network",
        "physics",
        "rendering",
        "shader_compiler",
        "threading",
        "xr"
    };

    private static readonly string[] CommonSettings = new[]
    {
        "application/config/name",
        "application/config/description",
        "application/config/version",
        "application/run/main_scene",
        "application/boot_splash/bg_color",
        "application/boot_splash/image",
        "application/config/icon",
        "application/config/macos_native_icon",
        "application/config/windows_native_icon",
        "application/config/use_custom_user_dir",
        "application/config/custom_user_dir_name",
        "application/config/project_settings_override",
        "audio/default_bus_layout",
        "audio/general/default_playback_type",
        "autoload",
        "display/window/size/viewport_width",
        "display/window/size/viewport_height",
        "display/window/size/window_width_override",
        "display/window/size/window_height_override",
        "display/window/size/resizable",
        "display/window/size/borderless",
        "display/window/size/fullscreen",
        "display/window/size/exclusive_fullscreen",
        "display/window/stretch/mode",
        "display/window/stretch/aspect",
        "display/window/stretch/scale_mode",
        "display/window/stretch/scale",
        "display/mouse_cursor/custom_image",
        "display/mouse_cursor/tooltip_position_offset",
        "debug/settings/profiling/profiling",
        "debug/settings/stdout/print_fps",
        "debug/settings/stdout/print_gpu_profile",
        "debug/settings/stdout/verbose_stdout",
        "gui/theme/custom",
        "gui/theme/custom_font",
        "gui/timers/incremental_search_max_interval_msec",
        "input/ui_accept",
        "input/ui_cancel",
        "internationalization/locale/fallback",
        "internationalization/locale/test",
        "internationalization/pseudolocalization/pseudolocalize",
        "layer_names/2d_physics/layer_1",
        "layer_names/2d_render/layer_1",
        "layer_names/3d_physics/layer_1",
        "layer_names/3d_render/layer_1",
        "logging/file_logging/enable_file_logging",
        "logging/file_logging/log_path",
        "physics/2d/default_gravity",
        "physics/3d/default_gravity",
        "rendering/driver/driver_name",
        "rendering/renderer/rendering_method",
        "rendering/anti_aliasing/quality/msaa_2d",
        "rendering/anti_aliasing/quality/msaa_3d",
        "rendering/anti_aliasing/quality/screen_space_aa",
        "rendering/environment/defaults/default_clear_color",
        "threading/worker_pool/max_threads",
        "threading/worker_pool/min_threads"
    };

    public string Name => "Project Settings";
    public string Category => "Settings";
    public bool NeedsRefresh => _needsRefresh;

    public DebugDataNode GetData()
    {
        return _cachedData ??= BuildSettingsData();
    }

    public void Refresh()
    {
        _cachedData = null;
        _needsRefresh = false;
    }

    public IEnumerable<string> Search(string pattern)
    {
        var data = GetData();
        var results = new List<string>();
        SearchRecursive(data, "", pattern.ToLower(), results);
        return results;
    }

    private void SearchRecursive(DebugDataNode node, string path, string pattern, List<string> results)
    {
        var currentPath = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";

        if (node.Name.ToLower().Contains(pattern) ||
            (node.HasValue && node.Value?.ToString()?.ToLower()?.Contains(pattern) == true))
        {
            results.Add(currentPath);
        }

        foreach (var prop in node.Properties.Values)
        {
            var propPath = $"{currentPath}.{prop.Name}";
            if (prop.Name.ToLower().Contains(pattern) ||
                (prop.HasValue && prop.Value?.ToString()?.ToLower()?.Contains(pattern) == true))
            {
                results.Add(propPath);
            }
        }

        foreach (var child in node.Children)
        {
            SearchRecursive(child, currentPath, pattern, results);
        }
    }

    private DebugDataNode BuildSettingsData()
    {
        var root = new DebugDataNode("Project Settings");

        var categories = new Dictionary<string, DebugDataNode>();

        foreach (var settingName in CommonSettings)
        {
            var value = ProjectSettings.GetSetting(settingName);
            var category = GetCategory(settingName);
            if (!categories.TryGetValue(category, out var categoryNode))
            {
                categoryNode = root.AddChild(category);
                categories[category] = categoryNode;
            }

            AddSettingNode(categoryNode, settingName, value, category);
        }

        var otherSettings = root.AddChild("Other Settings").SetCollapsed();

        try
        {
            var autoloads = ProjectSettings.GetSetting("autoload");
            if (autoloads.VariantType != Variant.Type.Nil)
            {
                var autoloadsNode = categories.TryGetValue("Autoload", out var existing)
                    ? existing
                    : otherSettings;
                var autoloadDict = autoloads.AsGodotDictionary();
                foreach (var key in autoloadDict.Keys)
                {
                    autoloadsNode.AddProperty(key.AsString(), autoloadDict[key].AsString());
                }
            }
        }
        catch
        {
        }

        return root;
    }

    private string GetCategory(string settingName)
    {
        var parts = settingName.Split('/');
        if (parts.Length > 0)
        {
            var firstPart = parts[0].ToLower();
            foreach (var known in KnownCategories)
            {
                if (firstPart.Contains(known))
                    return CapitalizeFirst(known);
            }
            return CapitalizeFirst(firstPart);
        }
        return "Other";
    }

    private string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }

    private void AddSettingNode(DebugDataNode categoryNode, string fullName, Variant value, string category)
    {
        var parts = fullName.Split('/');
        var current = categoryNode;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part)) continue;

            if (i == parts.Length - 1)
            {
                var typeStr = value.VariantType.ToString();
                var displayValue = FormatValue(value);
                current.AddProperty(part, displayValue).SetMetadata("type", typeStr).SetMetadata("fullPath", fullName);
            }
            else
            {
                var existing = current.Children.FirstOrDefault(c => c.Name == part);
                if (existing == null)
                {
                    existing = new DebugDataNode(part);
                    current.AddChild(existing);
                }
                current = existing;
            }
        }
    }

    private string FormatValue(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => "null",
            Variant.Type.Bool => value.AsBool().ToString().ToLower(),
            Variant.Type.Int => value.AsInt64().ToString(),
            Variant.Type.Float => value.AsDouble().ToString("F4"),
            Variant.Type.String => value.AsString(),
            Variant.Type.Vector2 => FormatVector2(value.AsVector2()),
            Variant.Type.Vector3 => FormatVector3(value.AsVector3()),
            Variant.Type.Color => FormatColor(value.AsColor()),
            Variant.Type.Array => $"Array[{value.AsGodotArray().Count}]",
            Variant.Type.Dictionary => $"Dict[{value.AsGodotDictionary().Count}]",
            _ => value.ToString()
        };
    }

    private string FormatVector2(Vector2 v) => $"({v.X:F2}, {v.Y:F2})";
    private string FormatVector3(Vector3 v) => $"({v.X:F2}, {v.Y:F2}, {v.Z:F2})";
    private string FormatColor(Color c) => $"RGBA({c.R:F2}, {c.G:F2}, {c.B:F2}, {c.A:F2})";
}
#endif
