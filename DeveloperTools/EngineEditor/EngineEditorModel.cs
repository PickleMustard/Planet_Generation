#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using DeveloperTools.Common;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace DeveloperTools.EngineEditor;

/// <summary>
/// In-memory editable state for engine YAML configuration. Each YAML file in
/// Configuration/engines/ is one category (file stem = engine category, e.g.
/// "Chemical"); entries are the engines in that file's <c>engines:</c> list.
/// Parses raw YAML directly; writes via EngineEditorYamlIO.
/// </summary>
public class EngineEditorModel
{
    public class EngineCategoryData
    {
        public string CategoryName { get; set; } = "";
        public List<EngineEditEntry> Engines { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    public class EngineEditEntry
    {
        public string Name { get; set; } = "";
        public float SpecificImpulse { get; set; }
        public float Thrust { get; set; }
        public string Description { get; set; } = "";
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    private readonly string _enginesDirectory;
    private Dictionary<string, EngineCategoryData> _categories = new();

    public IReadOnlyDictionary<string, EngineCategoryData> Categories => _categories;

    public bool HasUnsavedChanges => _categories.Values.Any(c =>
        c.IsNew || c.IsDirty || c.Engines.Any(e => e.IsNew || e.IsDirty));

    public EngineEditorModel(string enginesDirectory)
    {
        ArgumentNullException.ThrowIfNull(enginesDirectory);
        _enginesDirectory = enginesDirectory.TrimEnd('/');
    }

    // ── Load ─────────────────────────────────────────────────────────────

    public void LoadFromDisk()
    {
        if (!DirAccess.DirExistsAbsolute(_enginesDirectory))
            throw new InvalidOperationException($"Engines directory not found: {_enginesDirectory}");

        var newCategories = new Dictionary<string, EngineCategoryData>();

        foreach (var file in DirAccess.GetFilesAt(_enginesDirectory))
        {
            if (!file.EndsWith(".yaml") && !file.EndsWith(".yml")) continue;
            if (file == "EngineTypes.yaml") continue;

            string category = System.IO.Path.GetFileNameWithoutExtension(file);
            var bucket = new EngineCategoryData { CategoryName = category };
            foreach (var entry in ParseFile($"{_enginesDirectory}/{file}"))
                bucket.Engines.Add(entry);
            newCategories[category] = bucket;
        }

        _categories = newCategories;
    }

    private static List<EngineEditEntry> ParseFile(string filePath)
    {
        var entries = new List<EngineEditEntry>();
        using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GameLogger.Warning($"EngineEditorModel: cannot open {filePath}");
            return entries;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var yaml = deserializer.Deserialize<SysDict>(file.GetAsText());
            if (yaml != null && yaml.TryGetValue("engines", out var raw) && raw is List<object> list)
            {
                foreach (var obj in list)
                    if (obj is Dictionary<object, object> dict)
                        entries.Add(ParseEntry(dict));
            }
        }
        catch (Exception ex)
        {
            GameLogger.Error($"EngineEditorModel: parse error in {filePath}: {ex.Message}");
        }
        return entries;
    }

    private static EngineEditEntry ParseEntry(Dictionary<object, object> dict)
    {
        return new EngineEditEntry
        {
            Name = BaseConfigLoader.ReadString(dict, "name", ""),
            SpecificImpulse = BaseConfigLoader.ReadFloat(dict, "specific_impulse", 0f),
            Thrust = BaseConfigLoader.ReadFloat(dict, "thrust", 0f),
            Description = BaseConfigLoader.ReadString(dict, "description", ""),
        };
    }

    // ── Category CRUD ────────────────────────────────────────────────────

    public void AddCategory(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_categories.ContainsKey(name))
            throw new ArgumentException($"Category '{name}' already exists", nameof(name));
        _categories[name] = new EngineCategoryData { CategoryName = name, IsNew = true };
    }

    public void DeleteCategory(string name)
    {
        if (!_categories.ContainsKey(name))
            throw new KeyNotFoundException($"Category '{name}' not found");
        _categories.Remove(name);
    }

    // ── Engine CRUD ──────────────────────────────────────────────────────

    public void AddEngine(string categoryName, EngineEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_categories.TryGetValue(categoryName, out var cat))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");
        entry.IsNew = true;
        cat.Engines.Add(entry);
        cat.IsDirty = true;
    }

    public void DeleteEngine(string categoryName, int index)
    {
        var engines = GetEnginesOrThrow(categoryName);
        if (index < 0 || index >= engines.Count) throw new ArgumentOutOfRangeException(nameof(index));
        engines.RemoveAt(index);
        _categories[categoryName].IsDirty = true;
    }

    public void MoveEngine(string categoryName, int fromIndex, int toIndex)
    {
        var engines = GetEnginesOrThrow(categoryName);
        if (fromIndex < 0 || fromIndex >= engines.Count) throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= engines.Count) throw new ArgumentOutOfRangeException(nameof(toIndex));
        var entry = engines[fromIndex];
        engines.RemoveAt(fromIndex);
        engines.Insert(toIndex, entry);
        _categories[categoryName].IsDirty = true;
    }

    public void MarkDirty(string categoryName, int index)
    {
        var engines = GetEnginesOrThrow(categoryName);
        if (index < 0 || index >= engines.Count) throw new ArgumentOutOfRangeException(nameof(index));
        engines[index].IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    private List<EngineEditEntry> GetEnginesOrThrow(string categoryName)
    {
        if (!_categories.TryGetValue(categoryName, out var cat))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");
        return cat.Engines;
    }

    // ── Validation ───────────────────────────────────────────────────────

    public List<string> Validate()
    {
        var errors = new List<string>();
        var nameToCategories = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in _categories.Values)
        {
            for (int i = 0; i < category.Engines.Count; i++)
            {
                var entry = category.Engines[i];
                if (string.IsNullOrEmpty(entry.Name))
                {
                    errors.Add($"Engine in category '{category.CategoryName}' at index {i} has empty name");
                }
                else
                {
                    if (!nameToCategories.TryGetValue(entry.Name, out var cats))
                        nameToCategories[entry.Name] = cats = new List<string>();
                    if (!cats.Contains(category.CategoryName)) cats.Add(category.CategoryName);
                }

                if (entry.SpecificImpulse <= 0)
                    errors.Add($"Warning: Engine '{entry.Name}' has non-positive specific_impulse");
                if (entry.Thrust <= 0)
                    errors.Add($"Warning: Engine '{entry.Name}' has non-positive thrust");
            }
        }

        foreach (var kvp in nameToCategories)
            if (kvp.Value.Count > 1)
                errors.Add($"Duplicate engine name '{kvp.Key}' found in categories: {string.Join(", ", kvp.Value)}");

        return errors;
    }
}
#endif
