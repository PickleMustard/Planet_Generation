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

namespace DeveloperTools.ShipEditor;

/// <summary>
/// In-memory editable state for ship YAML configuration. Each YAML file in
/// Configuration/ships/ is one category (file stem = category name); entries are
/// the ships in that file's <c>ships:</c> list. Parses raw YAML directly so
/// round-trips preserve authored values; writes via ShipEditorYamlIO.
/// </summary>
public class ShipEditorModel
{
    public class ShipCategoryData
    {
        public string CategoryName { get; set; } = "";
        public List<ShipEditEntry> Ships { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    public class ShipEditEntry
    {
        public string Name { get; set; } = "";
        public float DryMass { get; set; }
        public float CargoCapacity { get; set; }
        public float FuelCapacity { get; set; }
        public string EngineCategory { get; set; } = "";
        public float WorkRequired { get; set; }
        public List<EditorResourceAmount> RequiredResources { get; set; } = new();
        public EditorVisual Visual { get; set; } = new();
        public EditorIcon Icon { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    private readonly string _shipsDirectory;
    private Dictionary<string, ShipCategoryData> _categories = new();

    public IReadOnlyDictionary<string, ShipCategoryData> Categories => _categories;

    public bool HasUnsavedChanges => _categories.Values.Any(c =>
        c.IsNew || c.IsDirty || c.Ships.Any(s => s.IsNew || s.IsDirty));

    public ShipEditorModel(string shipsDirectory)
    {
        ArgumentNullException.ThrowIfNull(shipsDirectory);
        _shipsDirectory = shipsDirectory.TrimEnd('/');
    }

    // ── Load ─────────────────────────────────────────────────────────────

    public void LoadFromDisk()
    {
        if (!DirAccess.DirExistsAbsolute(_shipsDirectory))
            throw new InvalidOperationException($"Ships directory not found: {_shipsDirectory}");

        var newCategories = new Dictionary<string, ShipCategoryData>();

        foreach (var file in DirAccess.GetFilesAt(_shipsDirectory))
        {
            if (!file.EndsWith(".yaml") && !file.EndsWith(".yml")) continue;
            if (file == "ShipTemplates.yaml") continue;

            string category = System.IO.Path.GetFileNameWithoutExtension(file);
            var bucket = new ShipCategoryData { CategoryName = category };
            foreach (var entry in ParseFile($"{_shipsDirectory}/{file}"))
                bucket.Ships.Add(entry);
            newCategories[category] = bucket;
        }

        _categories = newCategories;
    }

    private static List<ShipEditEntry> ParseFile(string filePath)
    {
        var entries = new List<ShipEditEntry>();
        using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GameLogger.Warning($"ShipEditorModel: cannot open {filePath}");
            return entries;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var yaml = deserializer.Deserialize<SysDict>(file.GetAsText());
            if (yaml != null && yaml.TryGetValue("ships", out var raw)
                && raw is List<object> list)
            {
                foreach (var obj in list)
                    if (obj is Dictionary<object, object> dict)
                        entries.Add(ParseEntry(dict));
            }
        }
        catch (Exception ex)
        {
            GameLogger.Error($"ShipEditorModel: parse error in {filePath}: {ex.Message}");
        }
        return entries;
    }

    private static ShipEditEntry ParseEntry(Dictionary<object, object> dict)
    {
        return new ShipEditEntry
        {
            Name = BaseConfigLoader.ReadString(dict, "name", ""),
            DryMass = BaseConfigLoader.ReadFloat(dict, "dry_mass", 0f),
            CargoCapacity = BaseConfigLoader.ReadFloat(dict, "cargo_capacity", 0f),
            FuelCapacity = BaseConfigLoader.ReadFloat(dict, "fuel_capacity", 0f),
            EngineCategory = BaseConfigLoader.ReadString(dict, "engine_category", ""),
            WorkRequired = BaseConfigLoader.ReadFloat(dict, "work_required", 0f),
            RequiredResources = EditorBlockIO.ParseRequiredResources(dict),
            Visual = EditorBlockIO.ParseVisual(dict),
            Icon = EditorBlockIO.ParseIcon(dict),
        };
    }

    // ── Category CRUD ────────────────────────────────────────────────────

    public void AddCategory(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_categories.ContainsKey(name))
            throw new ArgumentException($"Category '{name}' already exists", nameof(name));
        _categories[name] = new ShipCategoryData { CategoryName = name, IsNew = true };
    }

    public void DeleteCategory(string name)
    {
        if (!_categories.ContainsKey(name))
            throw new KeyNotFoundException($"Category '{name}' not found");
        _categories.Remove(name);
    }

    // ── Ship CRUD ────────────────────────────────────────────────────────

    public void AddShip(string categoryName, ShipEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_categories.TryGetValue(categoryName, out var cat))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");
        entry.IsNew = true;
        cat.Ships.Add(entry);
        cat.IsDirty = true;
    }

    public void DeleteShip(string categoryName, int index)
    {
        var ships = GetShipsOrThrow(categoryName);
        if (index < 0 || index >= ships.Count) throw new ArgumentOutOfRangeException(nameof(index));
        ships.RemoveAt(index);
        _categories[categoryName].IsDirty = true;
    }

    public void MoveShip(string categoryName, int fromIndex, int toIndex)
    {
        var ships = GetShipsOrThrow(categoryName);
        if (fromIndex < 0 || fromIndex >= ships.Count) throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= ships.Count) throw new ArgumentOutOfRangeException(nameof(toIndex));
        var entry = ships[fromIndex];
        ships.RemoveAt(fromIndex);
        ships.Insert(toIndex, entry);
        _categories[categoryName].IsDirty = true;
    }

    /// <summary>Flags an entry (and its category) dirty after in-place edits from the card.</summary>
    public void MarkDirty(string categoryName, int index)
    {
        var ships = GetShipsOrThrow(categoryName);
        if (index < 0 || index >= ships.Count) throw new ArgumentOutOfRangeException(nameof(index));
        ships[index].IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    private List<ShipEditEntry> GetShipsOrThrow(string categoryName)
    {
        if (!_categories.TryGetValue(categoryName, out var cat))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");
        return cat.Ships;
    }

    // ── Validation ───────────────────────────────────────────────────────

    public List<string> Validate()
    {
        var errors = new List<string>();
        var nameToCategories = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var engineCategories = new HashSet<string>(
            EngineConfigLoader.GetEngineCategories(), StringComparer.OrdinalIgnoreCase);

        foreach (var category in _categories.Values)
        {
            for (int i = 0; i < category.Ships.Count; i++)
            {
                var entry = category.Ships[i];
                if (string.IsNullOrEmpty(entry.Name))
                {
                    errors.Add($"Ship in category '{category.CategoryName}' at index {i} has empty name");
                }
                else
                {
                    if (!nameToCategories.TryGetValue(entry.Name, out var cats))
                        nameToCategories[entry.Name] = cats = new List<string>();
                    if (!cats.Contains(category.CategoryName)) cats.Add(category.CategoryName);
                }

                if (!string.IsNullOrEmpty(entry.EngineCategory)
                    && engineCategories.Count > 0
                    && !engineCategories.Contains(entry.EngineCategory))
                {
                    errors.Add($"Warning: Ship '{entry.Name}' has unknown engine_category '{entry.EngineCategory}'");
                }

                if (!string.IsNullOrEmpty(entry.Icon.BasePath) && !entry.Icon.BasePath!.StartsWith("res://"))
                    errors.Add($"Warning: Ship '{entry.Name}' icon.base_path '{entry.Icon.BasePath}' not starting with res://");
            }
        }

        foreach (var kvp in nameToCategories)
            if (kvp.Value.Count > 1)
                errors.Add($"Duplicate ship name '{kvp.Key}' found in categories: {string.Join(", ", kvp.Value)}");

        return errors;
    }
}
#endif
