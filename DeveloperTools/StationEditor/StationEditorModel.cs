#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Constructables.Stations;
using DeveloperTools.Common;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace DeveloperTools.StationEditor;

/// <summary>
/// In-memory editable state for station YAML configuration. Each YAML file in
/// Configuration/stations/ is one category (file stem = category name); entries
/// are the stations in that file's <c>stations:</c> list. Full fidelity:
/// behaviors (with inline config dicts), and the optional transfer_station block
/// all round-trip. Parses raw YAML directly; writes via StationEditorYamlIO.
/// </summary>
public class StationEditorModel
{
    /// <summary>A storage slot filter as its raw YAML token + slot count.</summary>
    public class SlotFilterEdit
    {
        /// <summary>e.g. "any", "state:solid", "category:ore", "resource:iron_ore".</summary>
        public string Token { get; set; } = "any";
        public int Count { get; set; } = 1;
    }

    /// <summary>Behavior entry with ID and optional inline config.</summary>
    public class BehaviorConfigEdit
    {
        public string BehaviorId { get; set; } = "";
        public SysDict Config { get; set; } = new();
    }

    public class StationCategoryData
    {
        public string CategoryName { get; set; } = "";
        public List<StationEditEntry> Stations { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    public class StationEditEntry
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string StationType { get; set; } = "";
        public float ConstructionTime { get; set; }
        public List<BehaviorConfigEdit> Behaviors { get; set; } = new();
        public List<EditorResourceAmount> RequiredResources { get; set; } = new();
        public EditorVisual Visual { get; set; } = new();
        public EditorIcon Icon { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    private readonly string _stationsDirectory;
    private Dictionary<string, StationCategoryData> _categories = new();

    public IReadOnlyDictionary<string, StationCategoryData> Categories => _categories;

    public bool HasUnsavedChanges => _categories.Values.Any(c =>
        c.IsNew || c.IsDirty || c.Stations.Any(s => s.IsNew || s.IsDirty));

    public StationEditorModel(string stationsDirectory)
    {
        ArgumentNullException.ThrowIfNull(stationsDirectory);
        _stationsDirectory = stationsDirectory.TrimEnd('/');
    }

    // ── Load ─────────────────────────────────────────────────────────────

    public void LoadFromDisk()
    {
        if (!DirAccess.DirExistsAbsolute(_stationsDirectory))
            throw new InvalidOperationException($"Stations directory not found: {_stationsDirectory}");

        var newCategories = new Dictionary<string, StationCategoryData>();

        foreach (var file in DirAccess.GetFilesAt(_stationsDirectory))
        {
            if (!file.EndsWith(".yaml") && !file.EndsWith(".yml")) continue;
            if (file == "StationTemplates.yaml") continue;

            string category = System.IO.Path.GetFileNameWithoutExtension(file);
            var bucket = new StationCategoryData { CategoryName = category };
            foreach (var entry in ParseFile($"{_stationsDirectory}/{file}"))
                bucket.Stations.Add(entry);
            newCategories[category] = bucket;
        }

        _categories = newCategories;
    }

    private static List<StationEditEntry> ParseFile(string filePath)
    {
        var entries = new List<StationEditEntry>();
        using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GameLogger.Warning($"StationEditorModel: cannot open {filePath}");
            return entries;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var yaml = deserializer.Deserialize<SysDict>(file.GetAsText());
            if (yaml != null && yaml.TryGetValue("stations", out var raw) && raw is List<object> list)
            {
                foreach (var obj in list)
                    if (obj is Dictionary<object, object> dict)
                        entries.Add(ParseEntry(dict));
            }
        }
        catch (Exception ex)
        {
            GameLogger.Error($"StationEditorModel: parse error in {filePath}: {ex.Message}");
        }
        return entries;
    }

    private static StationEditEntry ParseEntry(Dictionary<object, object> dict)
    {
        return new StationEditEntry
        {
            Name = BaseConfigLoader.ReadString(dict, "name", ""),
            Description = BaseConfigLoader.ReadString(dict, "description", ""),
            StationType = BaseConfigLoader.ReadString(dict, "station_type", ""),
            ConstructionTime = BaseConfigLoader.ReadFloat(dict, "construction_time", 0f),
            Behaviors = ParseBehaviorEntries(dict),
            RequiredResources = EditorBlockIO.ParseRequiredResources(dict),
            Visual = EditorBlockIO.ParseVisual(dict),
            Icon = EditorBlockIO.ParseIcon(dict),
        };
    }

    private static List<BehaviorConfigEdit> ParseBehaviorEntries(Dictionary<object, object> dict)
    {
        var entries = new List<BehaviorConfigEdit>();
        if (!dict.ContainsKey("behaviors"))
            return entries;

        if (dict["behaviors"] is not List<object> behaviorList)
            return entries;

        foreach (var entry in behaviorList)
        {
            if (entry is string s && !string.IsNullOrEmpty(s))
            {
                entries.Add(new BehaviorConfigEdit { BehaviorId = s });
            }
            else if (entry is Dictionary<object, object> entryDict)
            {
                string behaviorId = BaseConfigLoader.ReadString(entryDict, "behavior_id", "");
                if (string.IsNullOrEmpty(behaviorId))
                    continue;

                var config = new SysDict();
                foreach (var kvp in entryDict)
                {
                    string key = kvp.Key?.ToString() ?? "";
                    if (key == "behavior_id") continue;
                    config[key] = kvp.Value;
                }

                entries.Add(new BehaviorConfigEdit { BehaviorId = behaviorId, Config = config });
            }
        }

        return entries;
    }

    // ── Category CRUD ────────────────────────────────────────────────────

    public void AddCategory(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_categories.ContainsKey(name))
            throw new ArgumentException($"Category '{name}' already exists", nameof(name));
        _categories[name] = new StationCategoryData { CategoryName = name, IsNew = true };
    }

    public void DeleteCategory(string name)
    {
        if (!_categories.ContainsKey(name))
            throw new KeyNotFoundException($"Category '{name}' not found");
        _categories.Remove(name);
    }

    // ── Station CRUD ─────────────────────────────────────────────────────

    public void AddStation(string categoryName, StationEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_categories.TryGetValue(categoryName, out var cat))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");
        entry.IsNew = true;
        cat.Stations.Add(entry);
        cat.IsDirty = true;
    }

    public void DeleteStation(string categoryName, int index)
    {
        var stations = GetStationsOrThrow(categoryName);
        if (index < 0 || index >= stations.Count) throw new ArgumentOutOfRangeException(nameof(index));
        stations.RemoveAt(index);
        _categories[categoryName].IsDirty = true;
    }

    public void MoveStation(string categoryName, int fromIndex, int toIndex)
    {
        var stations = GetStationsOrThrow(categoryName);
        if (fromIndex < 0 || fromIndex >= stations.Count) throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= stations.Count) throw new ArgumentOutOfRangeException(nameof(toIndex));
        var entry = stations[fromIndex];
        stations.RemoveAt(fromIndex);
        stations.Insert(toIndex, entry);
        _categories[categoryName].IsDirty = true;
    }

    public void MarkDirty(string categoryName, int index)
    {
        var stations = GetStationsOrThrow(categoryName);
        if (index < 0 || index >= stations.Count) throw new ArgumentOutOfRangeException(nameof(index));
        stations[index].IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    private List<StationEditEntry> GetStationsOrThrow(string categoryName)
    {
        if (!_categories.TryGetValue(categoryName, out var cat))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");
        return cat.Stations;
    }

    // ── Lookups ──────────────────────────────────────────────────────────

    /// <summary>All concrete IStationBehavior class names, sorted.</summary>
    public static List<string> GetAllBehaviorTypes()
    {
        Type[] types;
        try { types = typeof(IStationBehavior).Assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        { types = ex.Types.Where(t => t != null).ToArray()!; }

        return types
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && typeof(IStationBehavior).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(s => s)
            .ToList();
    }

    /// <summary>Known station_type names from StationTemplates.yaml, sorted.</summary>
    public static List<string> GetAllStationTypes()
    {
        try
        {
            return StationConfigLoader.LoadStationTemplates()
                .Select(t => t.Name).Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(s => s).ToList();
        }
        catch { return new List<string>(); }
    }

    // ── Validation ───────────────────────────────────────────────────────

    public List<string> Validate()
    {
        var errors = new List<string>();
        var nameToCategories = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var behaviorIds = new HashSet<string>(GetAllBehaviorTypes(), StringComparer.OrdinalIgnoreCase);
        var stationTypes = new HashSet<string>(GetAllStationTypes(), StringComparer.OrdinalIgnoreCase);

        foreach (var category in _categories.Values)
        {
            for (int i = 0; i < category.Stations.Count; i++)
            {
                var entry = category.Stations[i];
                if (string.IsNullOrEmpty(entry.Name))
                {
                    errors.Add($"Station in category '{category.CategoryName}' at index {i} has empty name");
                }
                else
                {
                    if (!nameToCategories.TryGetValue(entry.Name, out var cats))
                        nameToCategories[entry.Name] = cats = new List<string>();
                    if (!cats.Contains(category.CategoryName)) cats.Add(category.CategoryName);
                }

                if (string.IsNullOrEmpty(entry.StationType))
                    errors.Add($"Station '{entry.Name}' has empty station_type");
                else if (stationTypes.Count > 0 && !stationTypes.Contains(entry.StationType))
                    errors.Add($"Warning: Station '{entry.Name}' has unknown station_type '{entry.StationType}'");

                foreach (var beh in entry.Behaviors)
                {
                    if (string.IsNullOrEmpty(beh.BehaviorId))
                        errors.Add($"Station '{entry.Name}' has an empty behavior entry");
                    else if (!beh.BehaviorId.StartsWith("res://") && !behaviorIds.Contains(beh.BehaviorId))
                        errors.Add($"Station '{entry.Name}' references unknown behavior '{beh.BehaviorId}'");
                }

                if (!string.IsNullOrEmpty(entry.Icon.ResourcePath) && !entry.Icon.ResourcePath!.StartsWith("res://"))
                    errors.Add($"Warning: Station '{entry.Name}' icon.resource '{entry.Icon.ResourcePath}' not starting with res://");
            }
        }

        foreach (var kvp in nameToCategories)
            if (kvp.Value.Count > 1)
                errors.Add($"Duplicate station name '{kvp.Key}' found in categories: {string.Join(", ", kvp.Value)}");

        return errors;
    }
}
#endif
