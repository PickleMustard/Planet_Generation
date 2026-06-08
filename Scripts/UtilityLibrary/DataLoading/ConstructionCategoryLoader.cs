using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Loads the developer-curated construction category config
/// (Configuration/UI/construction_categories.yaml). Each tab maps to a
/// constructable database; each category entry drives one button in the
/// docked construction menu. Order in YAML is preserved (button order).
/// Results are cached; call <see cref="ClearCache"/> to force a reload.
/// </summary>
public static class ConstructionCategoryLoader
{
    private const string ConfigPath = "res://Configuration/UI/construction_categories.yaml";

    private static List<CategoryTab>? _tabs;

    /// <summary>One construction-menu tab and its ordered category buttons.</summary>
    public sealed class CategoryTab
    {
        public string Name { get; init; } = "";
        public List<CategoryEntry> Categories { get; init; } = new();
    }

    /// <summary>A single category button: lookup key + display label.</summary>
    public sealed class CategoryEntry
    {
        public string Key { get; init; } = "";
        public string Label { get; init; } = "";
    }

    /// <summary>
    /// Loads and caches the tab/category config. Returns an empty list on any
    /// failure (missing file, malformed YAML) — the menu degrades to no buttons
    /// rather than throwing.
    /// </summary>
    public static List<CategoryTab> Load()
    {
        if (_tabs != null)
            return _tabs;

        _tabs = new List<CategoryTab>();

        string? yamlContent = BaseConfigLoader.ReadAllText(ConfigPath);
        if (yamlContent == null)
        {
            GameLogger.Warning($"ConstructionCategoryLoader: config not found: {ConfigPath}");
            return _tabs;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

            if (yamlData != null
                && yamlData.TryGetValue("tabs", out var tabsObj)
                && tabsObj is List<object> tabsList)
            {
                foreach (var tabObj in tabsList)
                {
                    if (tabObj is not Dictionary<object, object> tabDict)
                        continue;

                    var tab = new CategoryTab
                    {
                        Name = BaseConfigLoader.ReadString(tabDict, "name", ""),
                        Categories = ParseCategories(tabDict),
                    };
                    _tabs.Add(tab);
                }
            }

            GameLogger.Info(
                $"ConstructionCategoryLoader: loaded {_tabs.Count} tabs, "
                + $"{_tabs.Sum(t => t.Categories.Count)} categories");
        }
        catch (Exception e)
        {
            GD.PrintErr($"ConstructionCategoryLoader: error loading {ConfigPath}: {e.Message}");
        }

        return _tabs;
    }

    /// <summary>Returns the tab with the given name (case-insensitive), or null.</summary>
    public static CategoryTab? GetTab(string name)
    {
        return Load().FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Clears the cache so the next <see cref="Load"/> re-reads from disk.</summary>
    public static void ClearCache()
    {
        _tabs = null;
        GameLogger.Debug("ConstructionCategoryLoader: cache cleared");
    }

    private static List<CategoryEntry> ParseCategories(Dictionary<object, object> tabDict)
    {
        var entries = new List<CategoryEntry>();

        if (!tabDict.TryGetValue("categories", out var catsObj)
            || catsObj is not List<object> catsList)
            return entries;

        foreach (var catObj in catsList)
        {
            if (catObj is not Dictionary<object, object> catDict)
                continue;

            string key = BaseConfigLoader.ReadString(catDict, "key", "");
            if (string.IsNullOrEmpty(key))
                continue;

            string label = BaseConfigLoader.ReadString(catDict, "label", key);
            entries.Add(new CategoryEntry { Key = key, Label = label });
        }

        return entries;
    }
}
