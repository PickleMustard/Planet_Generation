using System.Collections.Generic;
using Godot;
using UtilityLibrary.DataLoading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.UI;

/// <summary>
/// Static lookup for "&lt;block&gt;.&lt;knob&gt;" tooltip strings used by the subtype editor
/// and IndividualPlanetGenerator controls. YAML lives at <see cref="DefaultPath"/>.
/// </summary>
public static class GenSettingTooltips
{
    public const string DefaultPath = "res://Configuration/UI/gen_setting_tooltips.yaml";

    private static Dictionary<string, string>? _cache;
    private static readonly object _gate = new();

    public static string Get(string key, string fallback = "")
    {
        EnsureLoaded();
        return _cache != null && _cache.TryGetValue(key, out var v) ? v : fallback;
    }

    public static IReadOnlyDictionary<string, string> All
    {
        get
        {
            EnsureLoaded();
            return _cache ?? new Dictionary<string, string>();
        }
    }

    /// <summary>Force reload (test / hot-reload paths).</summary>
    public static void Reload(string path = DefaultPath)
    {
        lock (_gate)
        {
            _cache = LoadFile(path);
        }
    }

    private static void EnsureLoaded()
    {
        if (_cache != null) return;
        lock (_gate)
        {
            _cache ??= LoadFile(DefaultPath);
        }
    }

    private static Dictionary<string, string> LoadFile(string path)
    {
        var result = new Dictionary<string, string>(System.StringComparer.Ordinal);

        string? text = BaseConfigLoader.ReadAllText(path);
        if (text == null)
        {
            GameLogger.Warning($"GenSettingTooltips: file not found {path} — tooltips disabled");
            return result;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var raw = deserializer.Deserialize<SysDict>(text);
            if (raw == null || !raw.TryGetValue("tooltips", out var tooltipsNode))
            {
                GameLogger.Warning($"GenSettingTooltips: missing 'tooltips' root key in {path}");
                return result;
            }

            if (tooltipsNode is not Dictionary<object, object> entries)
            {
                GameLogger.Warning($"GenSettingTooltips: 'tooltips' must be a map in {path}");
                return result;
            }

            foreach (var kvp in entries)
            {
                string? key = kvp.Key?.ToString();
                string? value = kvp.Value?.ToString();
                if (string.IsNullOrEmpty(key) || value == null) continue;
                result[key] = value;
            }
        }
        catch (System.Exception e)
        {
            GameLogger.Error($"GenSettingTooltips: failed to load {path}: {e.Message}");
        }

        return result;
    }
}
