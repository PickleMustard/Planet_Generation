#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Constructables.Buildings;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// In-memory editable state for building YAML configuration.
/// Categories typically correspond to subdirectories of Configuration/Buildings/
/// (Power, Agriculture, ...) but the model also accommodates multi-entry root-level
/// files like example_building.yaml by grouping entries on their declared category
/// while preserving the original SourceFilePath for round-trip.
/// </summary>
public class BuildingEditorModel
{
    // ── Nested edit types ────────────────────────────────────────────────

    public class RequiredResourceEdit
    {
        public string ResourceId { get; set; } = "";
        public int Amount { get; set; } = 1;
    }

    public class BehaviorEntryEdit
    {
        public string BehaviorId { get; set; } = "";
        public Dictionary<string, object> Config { get; set; } = new();
    }

    public class PlacementReqEdit
    {
        /// <summary>Mixed entries: bare biome names ("Grassland") and "category:&lt;name&gt;" tokens.</summary>
        public HashSet<string> Biomes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool AllowAnyBiome { get; set; }
        public float MinElevation { get; set; }
        public float MaxElevation { get; set; } = 1f;
        public float MaxSlope { get; set; } = 45f;
        public int CellCount { get; set; } = 1;
        public bool RequiresAdjacent { get; set; }
        public string? ConfigurableBehavior { get; set; }
        public Dictionary<string, object> ConfigurableBehaviorConfig { get; set; } = new();
    }

    public class VisualEdit
    {
        public string? ModelPath { get; set; }
        public string? ModelMaterial { get; set; }
        public string? AnimationPath { get; set; }
        public string? AnimationName { get; set; }
        public float Scale { get; set; } = 1f;
        public Vector3 RotationOffset { get; set; } = Vector3.Zero;
        public string ShapeId { get; set; } = "hexagon";
        public float ShapeSize { get; set; } = 64f;
        public Color ShapeColor { get; set; } = new Color(0.30f, 0.45f, 0.60f, 1f);
    }

    public class IconEdit
    {
        public string? BasePath { get; set; }
        public float Scale { get; set; } = 1f;
        public Color Tint { get; set; } = Colors.White;
    }

    public class SpecifierEntryEdit
    {
        public int Value { get; set; }
        public string Label { get; set; } = "";
    }

    public class BuildingEditEntry
    {
        public string IdName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public int MaxResourceTier { get; set; } = 0;
        public float WorkRequired { get; set; } = 100f;
        public int BuildingLimit { get; set; } = -1;
        public bool Demolishable { get; set; } = true;
        public string? LinkProfile { get; set; }
        public string? AllowedRecipeCategory { get; set; }
        public List<RequiredResourceEdit> RequiredResources { get; set; } = new();
        public PlacementReqEdit Placement { get; set; } = new();
        public List<BehaviorEntryEdit> Behaviors { get; set; } = new();
        public VisualEdit Visual { get; set; } = new();
        public IconEdit Icon { get; set; } = new();
        public bool SpecifierEnabled { get; set; }
        public List<SpecifierEntryEdit> SpecifierEntries { get; set; } = new();
        public int SpecifierDefault { get; set; }
        public string SourceFilePath { get; set; } = "";
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    public class BuildingCategoryData
    {
        public string CategoryName { get; set; } = "";
        public List<BuildingEditEntry> Buildings { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    // ── Members ──────────────────────────────────────────────────────────

    private readonly string _buildingsDirectory;
    private Dictionary<string, BuildingCategoryData> _categories =
        new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _loadedSourceFiles = new();

    public IReadOnlyDictionary<string, BuildingCategoryData> Categories => _categories;
    public string BuildingsDirectory => _buildingsDirectory;
    public IReadOnlySet<string> LoadedSourceFiles => _loadedSourceFiles;

    public bool HasUnsavedChanges => _categories.Values.Any(c =>
        c.IsNew || c.IsDirty || c.Buildings.Any(b => b.IsNew || b.IsDirty));

    public BuildingEditorModel(string buildingsDirectory)
    {
        ArgumentNullException.ThrowIfNull(buildingsDirectory);
        _buildingsDirectory = buildingsDirectory.TrimEnd('/') + "/";
    }

    // ── Loading ──────────────────────────────────────────────────────────

    public void LoadFromDisk()
    {
        if (!DirAccess.DirExistsAbsolute(_buildingsDirectory))
        {
            throw new InvalidOperationException(
                $"Buildings directory not found: {_buildingsDirectory}");
        }

        var newCategories = new Dictionary<string, BuildingCategoryData>(
            StringComparer.OrdinalIgnoreCase);
        var loadedFiles = new HashSet<string>();

        void LoadFile(string filePath)
        {
            loadedFiles.Add(filePath);
            var defs = BuildingConfigLoader.LoadBuildingDefinitions(filePath);
            foreach (var def in defs)
            {
                string category = !string.IsNullOrEmpty(def.Category)
                    ? def.Category
                    : DefaultCategoryFromPath(filePath);

                if (!newCategories.TryGetValue(category, out var bucket))
                {
                    bucket = new BuildingCategoryData { CategoryName = category };
                    newCategories[category] = bucket;
                }
                bucket.Buildings.Add(MapDefinitionToEntry(def, filePath));
            }
        }

        // Root-level YAML files (e.g., example_building.yaml — multi-entry).
        foreach (var file in DirAccess.GetFilesAt(_buildingsDirectory))
        {
            if (!IsYaml(file)) continue;
            LoadFile(_buildingsDirectory + file);
        }

        // Subdirectories — also seed an empty category for each subdir so the
        // directory is preserved in the UI even when it has no buildings yet.
        foreach (var subdir in DirAccess.GetDirectoriesAt(_buildingsDirectory))
        {
            string subdirPath = _buildingsDirectory + subdir + "/";
            foreach (var file in DirAccess.GetFilesAt(subdirPath))
            {
                if (!IsYaml(file)) continue;
                LoadFile(subdirPath + file);
            }
            if (!newCategories.ContainsKey(subdir))
                newCategories[subdir] = new BuildingCategoryData { CategoryName = subdir };
        }

        _categories = newCategories;
        _loadedSourceFiles = loadedFiles;
    }

    private static bool IsYaml(string fileName)
    {
        return fileName.EndsWith(".yaml") || fileName.EndsWith(".yml");
    }

    private static string DefaultCategoryFromPath(string filePath)
    {
        int lastSlash = filePath.LastIndexOf('/');
        if (lastSlash <= 0) return "uncategorized";
        string parent = filePath[..lastSlash];
        int slash = parent.LastIndexOf('/');
        return slash >= 0 ? parent[(slash + 1)..] : "uncategorized";
    }

    // ── Category mutators ────────────────────────────────────────────────

    public void AddCategory(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (_categories.ContainsKey(name))
            throw new ArgumentException($"Category '{name}' already exists");
        _categories[name] = new BuildingCategoryData
        {
            CategoryName = name,
            IsNew = true
        };
    }

    public void DeleteCategory(string name)
    {
        if (!_categories.ContainsKey(name))
            throw new KeyNotFoundException($"Category '{name}' not found");
        _categories.Remove(name);
    }

    // ── Building mutators ────────────────────────────────────────────────

    public void AddBuilding(string categoryName, BuildingEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_categories.TryGetValue(categoryName, out var cat))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");

        entry.IsNew = true;
        entry.IsDirty = false;
        if (string.IsNullOrEmpty(entry.Category))
            entry.Category = categoryName.ToLowerInvariant();
        if (string.IsNullOrEmpty(entry.SourceFilePath))
            entry.SourceFilePath = DefaultSourceFilePath(categoryName, entry.IdName);

        cat.Buildings.Add(entry);
        cat.IsDirty = true;
    }

    public void DeleteBuilding(string categoryName, int index)
    {
        var (_, list) = GetBuildingsOrThrow(categoryName);
        if (index < 0 || index >= list.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        list.RemoveAt(index);
        _categories[categoryName].IsDirty = true;
    }

    public void MoveBuilding(string categoryName, int fromIndex, int toIndex)
    {
        var (_, list) = GetBuildingsOrThrow(categoryName);
        if (fromIndex < 0 || fromIndex >= list.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex >= list.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));
        var entry = list[fromIndex];
        list.RemoveAt(fromIndex);
        list.Insert(toIndex, entry);
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateBuildingField(string categoryName, int index,
        string fieldName, object? value)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        switch (fieldName)
        {
            case "IdName": entry.IdName = value?.ToString() ?? ""; break;
            case "DisplayName": entry.DisplayName = value?.ToString() ?? ""; break;
            case "Description": entry.Description = value?.ToString() ?? ""; break;
            case "Category": entry.Category = value?.ToString() ?? ""; break;
            case "MaxResourceTier": entry.MaxResourceTier = Convert.ToInt32(value); break;
            case "WorkRequired": entry.WorkRequired = Convert.ToSingle(value); break;
            case "BuildingLimit": entry.BuildingLimit = Convert.ToInt32(value); break;
            case "Demolishable": entry.Demolishable = Convert.ToBoolean(value); break;
            case "LinkProfile":
                {
                    string s = value?.ToString() ?? "";
                    entry.LinkProfile = string.IsNullOrEmpty(s) ? null : s;
                    break;
                }
            case "AllowedRecipeCategory":
                {
                    string s = value?.ToString() ?? "";
                    entry.AllowedRecipeCategory = string.IsNullOrEmpty(s) ? null : s;
                    break;
                }
            default:
                throw new ArgumentException($"Unknown field name: {fieldName}", nameof(fieldName));
        }
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    // ── Required Resources ───────────────────────────────────────────────

    public void AddRequiredResource(string categoryName, int index, RequiredResourceEdit slot)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.RequiredResources.Add(slot);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateRequiredResource(string categoryName, int index, int slotIndex,
        string resourceId, int amount)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.RequiredResources.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        var slot = entry.RequiredResources[slotIndex];
        slot.ResourceId = resourceId;
        slot.Amount = amount;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void RemoveRequiredResource(string categoryName, int index, int slotIndex)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.RequiredResources.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        entry.RequiredResources.RemoveAt(slotIndex);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    // ── Behaviors ────────────────────────────────────────────────────────

    public void AddBehavior(string categoryName, int index, string behaviorId)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        var row = new BehaviorEntryEdit { BehaviorId = behaviorId };
        SeedBehaviorConfigDefaults(row);
        entry.Behaviors.Add(row);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateBehaviorId(string categoryName, int index, int rowIndex, string newId)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (rowIndex < 0 || rowIndex >= entry.Behaviors.Count)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        var row = entry.Behaviors[rowIndex];
        row.BehaviorId = newId;
        row.Config = new Dictionary<string, object>();
        SeedBehaviorConfigDefaults(row);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateBehaviorConfig(string categoryName, int index, int rowIndex,
        string fieldName, object? value)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (rowIndex < 0 || rowIndex >= entry.Behaviors.Count)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        var row = entry.Behaviors[rowIndex];
        if (value == null)
            row.Config.Remove(fieldName);
        else
            row.Config[fieldName] = value;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void RemoveBehavior(string categoryName, int index, int rowIndex)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (rowIndex < 0 || rowIndex >= entry.Behaviors.Count)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        entry.Behaviors.RemoveAt(rowIndex);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    private static void SeedBehaviorConfigDefaults(BehaviorEntryEdit row)
    {
        var schema = BehaviorSchemaRegistry.GetSchema(row.BehaviorId);
        foreach (var field in schema)
        {
            if (field.Default != null)
                row.Config[field.Name] = field.Default;
        }
    }

    // ── Specifier ────────────────────────────────────────────────────────

    public void SetSpecifierEnabled(string categoryName, int index, bool enabled)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.SpecifierEnabled = enabled;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void AddSpecifierEntry(string categoryName, int index, SpecifierEntryEdit slot)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.SpecifierEntries.Add(slot);
        if (entry.SpecifierEntries.Count == 1)
            entry.SpecifierDefault = slot.Value;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateSpecifierEntry(string categoryName, int index, int slotIndex,
        int value, string label)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.SpecifierEntries.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        var slot = entry.SpecifierEntries[slotIndex];
        int oldValue = slot.Value;
        slot.Value = value;
        slot.Label = label;
        if (entry.SpecifierDefault == oldValue)
            entry.SpecifierDefault = value;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void DeleteSpecifierEntry(string categoryName, int index, int slotIndex)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (slotIndex < 0 || slotIndex >= entry.SpecifierEntries.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        int removedValue = entry.SpecifierEntries[slotIndex].Value;
        entry.SpecifierEntries.RemoveAt(slotIndex);
        if (entry.SpecifierDefault == removedValue && entry.SpecifierEntries.Count > 0)
            entry.SpecifierDefault = entry.SpecifierEntries[0].Value;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void SetSpecifierDefault(string categoryName, int index, int value)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.SpecifierDefault = value;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    // ── Placement ────────────────────────────────────────────────────────

    public void UpdatePlacementField(string categoryName, int index,
        string fieldName, object? value)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        var p = entry.Placement;
        switch (fieldName)
        {
            case "MinElevation": p.MinElevation = Convert.ToSingle(value); break;
            case "MaxElevation": p.MaxElevation = Convert.ToSingle(value); break;
            case "MaxSlope": p.MaxSlope = Convert.ToSingle(value); break;
            case "CellCount": p.CellCount = Convert.ToInt32(value); break;
            case "RequiresAdjacent": p.RequiresAdjacent = Convert.ToBoolean(value); break;
            case "ConfigurableBehavior":
                {
                    string s = value?.ToString() ?? "";
                    p.ConfigurableBehavior = string.IsNullOrEmpty(s) ? null : s;
                    p.ConfigurableBehaviorConfig.Clear();
                    if (p.ConfigurableBehavior != null)
                    {
                        var schema = PlacementBehaviorSchemaRegistry.GetSchema(p.ConfigurableBehavior);
                        foreach (var field in schema)
                            if (field.Default != null)
                                p.ConfigurableBehaviorConfig[field.Name] = field.Default;
                    }
                    break;
                }
            default:
                throw new ArgumentException($"Unknown placement field: {fieldName}", nameof(fieldName));
        }
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void SetPlacementBiomes(string categoryName, int index, HashSet<string> biomes)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.Placement.Biomes = new HashSet<string>(biomes, StringComparer.OrdinalIgnoreCase);
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void SetAllowAnyBiome(string categoryName, int index, bool allow)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        entry.Placement.AllowAnyBiome = allow;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdatePlacementBehaviorConfig(string categoryName, int index,
        string fieldName, object? value)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        if (value == null)
            entry.Placement.ConfigurableBehaviorConfig.Remove(fieldName);
        else
            entry.Placement.ConfigurableBehaviorConfig[fieldName] = value;
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    // ── Visual / Icon ────────────────────────────────────────────────────

    public void UpdateVisual(string categoryName, int index, string fieldName, object? value)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        var v = entry.Visual;
        switch (fieldName)
        {
            case "ModelPath":
                {
                    string s = value?.ToString() ?? "";
                    v.ModelPath = string.IsNullOrEmpty(s) ? null : s;
                    break;
                }
            case "ModelMaterial":
                {
                    string s = value?.ToString() ?? "";
                    v.ModelMaterial = string.IsNullOrEmpty(s) ? null : s;
                    break;
                }
            case "AnimationPath":
                {
                    string s = value?.ToString() ?? "";
                    v.AnimationPath = string.IsNullOrEmpty(s) ? null : s;
                    break;
                }
            case "AnimationName":
                {
                    string s = value?.ToString() ?? "";
                    v.AnimationName = string.IsNullOrEmpty(s) ? null : s;
                    break;
                }
            case "Scale": v.Scale = Convert.ToSingle(value); break;
            case "RotationOffset":
                v.RotationOffset = value is Vector3 vec
                    ? vec
                    : throw new ArgumentException("RotationOffset requires Vector3");
                break;
            case "ShapeId": v.ShapeId = value?.ToString() ?? "hexagon"; break;
            case "ShapeSize": v.ShapeSize = Convert.ToSingle(value); break;
            case "ShapeColor":
                v.ShapeColor = value is Color c
                    ? c
                    : throw new ArgumentException("ShapeColor requires Color");
                break;
            default:
                throw new ArgumentException($"Unknown visual field: {fieldName}", nameof(fieldName));
        }
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    public void UpdateIcon(string categoryName, int index, string fieldName, object? value)
    {
        var entry = GetEntryOrThrow(categoryName, index);
        switch (fieldName)
        {
            case "BasePath":
                {
                    string s = value?.ToString() ?? "";
                    entry.Icon.BasePath = string.IsNullOrEmpty(s) ? null : s;
                    break;
                }
            case "Scale": entry.Icon.Scale = Convert.ToSingle(value); break;
            case "Tint":
                entry.Icon.Tint = value is Color c
                    ? c
                    : throw new ArgumentException("Tint requires Color");
                break;
            default:
                throw new ArgumentException($"Unknown icon field: {fieldName}", nameof(fieldName));
        }
        entry.IsDirty = true;
        _categories[categoryName].IsDirty = true;
    }

    // ── Static enumerators ───────────────────────────────────────────────

    public static List<string> GetAllBiomes()
    {
        return Enum.GetNames<Biome.BiomeType>().OrderBy(s => s).ToList();
    }

    private static List<string>? _cachedBiomeCategories;
    public static List<string> GetAllBiomeCategories()
    {
        if (_cachedBiomeCategories != null) return _cachedBiomeCategories;
        try
        {
            var cfg = ResourceConfigLoader.LoadBiomeCategories();
            _cachedBiomeCategories = cfg?.Categories.Keys.OrderBy(s => s).ToList()
                ?? new List<string>();
        }
        catch
        {
            _cachedBiomeCategories = new List<string>();
        }
        return _cachedBiomeCategories;
    }

    public static List<string> GetAllBehaviorTypes()
    {
        return DiscoverConcreteTypes(typeof(IBuildingBehavior))
            .Where(t => t.Name != "BulkStorageRoutingBehavior")
            .Select(t => t.Name)
            .OrderBy(s => s).ToList();
    }

    public static List<string> GetAllPlacementBehaviorTypes()
    {
        return DiscoverConcreteTypes(typeof(IPlacementBehavior))
            .Where(t => t.Name != "DefaultPlacementBehavior")
            .Select(t => t.Name)
            .OrderBy(s => s).ToList();
    }

    private static IEnumerable<Type> DiscoverConcreteTypes(Type iface)
    {
        Type[] types;
        try
        {
            types = iface.Assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }
        foreach (var t in types)
        {
            if (t == null) continue;
            if (t.IsAbstract || t.IsInterface) continue;
            if (!iface.IsAssignableFrom(t)) continue;
            yield return t;
        }
    }

    public static List<string> GetAllResourceIds()
    {
        try
        {
            var db = ResourceDatabase.Instance;
            if (db != null && db.IsLoaded)
                return db.GetAllResources().Keys.OrderBy(s => s).ToList();
        }
        catch { }
        return new List<string>();
    }

    public static List<string> GetAllRecipeIds()
    {
        try
        {
            var db = RecipeDatabase.Instance;
            if (db != null && db.IsLoaded)
                return db.GetAllRecipes().Keys.OrderBy(s => s).ToList();
        }
        catch { }
        return new List<string>();
    }

    // ── Validation ───────────────────────────────────────────────────────

    public List<string> Validate()
    {
        var errors = new List<string>();
        var nameToCategories = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var resourceIds = new HashSet<string>(GetAllResourceIds(), StringComparer.OrdinalIgnoreCase);
        var recipeIds = new HashSet<string>(GetAllRecipeIds(), StringComparer.OrdinalIgnoreCase);
        var behaviorIds = new HashSet<string>(GetAllBehaviorTypes(), StringComparer.OrdinalIgnoreCase);
        var placementIds = new HashSet<string>(GetAllPlacementBehaviorTypes(), StringComparer.OrdinalIgnoreCase);

        foreach (var category in _categories.Values)
        {
            for (int i = 0; i < category.Buildings.Count; i++)
            {
                var entry = category.Buildings[i];

                if (string.IsNullOrEmpty(entry.IdName))
                {
                    errors.Add($"Building in category '{category.CategoryName}' at index {i} has empty id_name");
                }
                else
                {
                    if (!nameToCategories.TryGetValue(entry.IdName, out var cats))
                        nameToCategories[entry.IdName] = cats = new List<string>();
                    if (!cats.Contains(category.CategoryName))
                        cats.Add(category.CategoryName);
                }

                if (string.IsNullOrEmpty(entry.Category))
                    errors.Add($"Building '{entry.IdName}' has empty category");

                foreach (var req in entry.RequiredResources)
                {
                    if (string.IsNullOrEmpty(req.ResourceId)) continue;
                    if (!resourceIds.Contains(req.ResourceId))
                        errors.Add($"Building '{entry.IdName}' requires unknown resource '{req.ResourceId}'");
                }

                foreach (var beh in entry.Behaviors)
                {
                    if (string.IsNullOrEmpty(beh.BehaviorId))
                    {
                        errors.Add($"Building '{entry.IdName}' has an empty behavior_id");
                        continue;
                    }
                    if (!behaviorIds.Contains(beh.BehaviorId)
                        && !BehaviorSchemaRegistry.IsKnown(beh.BehaviorId))
                    {
                        errors.Add($"Building '{entry.IdName}' references unknown behavior '{beh.BehaviorId}'");
                    }
                    if (beh.BehaviorId == "ManufacturingBehavior"
                        && beh.Config.TryGetValue("default_recipe", out var rec)
                        && rec is string rs && !string.IsNullOrEmpty(rs)
                        && !recipeIds.Contains(rs))
                    {
                        errors.Add($"Building '{entry.IdName}' default_recipe '{rs}' not in RecipeDatabase");
                    }
                }

                if (entry.Placement.CellCount < 1)
                    errors.Add($"Building '{entry.IdName}' has cell_count < 1");
                if (entry.Placement.MinElevation > entry.Placement.MaxElevation)
                    errors.Add($"Building '{entry.IdName}' has min_elevation > max_elevation");

                if (!entry.Placement.AllowAnyBiome
                    && entry.Placement.Biomes.Count == 0
                    && string.IsNullOrEmpty(entry.Placement.ConfigurableBehavior))
                {
                    errors.Add($"Building '{entry.IdName}' has no biomes selected and no configurable_behavior");
                }

                if (!string.IsNullOrEmpty(entry.Placement.ConfigurableBehavior)
                    && !entry.Placement.ConfigurableBehavior!.StartsWith("res://")
                    && !placementIds.Contains(entry.Placement.ConfigurableBehavior)
                    && !PlacementBehaviorSchemaRegistry.IsKnown(entry.Placement.ConfigurableBehavior))
                {
                    errors.Add($"Building '{entry.IdName}' references unknown placement behavior '{entry.Placement.ConfigurableBehavior}'");
                }

                // Visual warnings (sentinel "Warning:" prefix so module can split errors vs warnings)
                if (!string.IsNullOrEmpty(entry.Visual.ModelPath))
                {
                    if (!entry.Visual.ModelPath!.StartsWith("res://"))
                        errors.Add($"Warning: Building '{entry.IdName}' model_path '{entry.Visual.ModelPath}' not starting with res://");
                    else if (!Godot.FileAccess.FileExists(entry.Visual.ModelPath))
                        errors.Add($"Warning: Building '{entry.IdName}' model_path '{entry.Visual.ModelPath}' does not exist on disk");
                }

                if (!string.IsNullOrEmpty(entry.Icon.BasePath) && !entry.Icon.BasePath!.StartsWith("res://"))
                    errors.Add($"Warning: Building '{entry.IdName}' icon.base_path '{entry.Icon.BasePath}' not starting with res://");

                if (entry.SpecifierEnabled)
                {
                    if (entry.SpecifierEntries.Count == 0)
                    {
                        errors.Add($"Building '{entry.IdName}' specifier enabled but has no values");
                    }
                    else
                    {
                        var seenValues = new HashSet<int>();
                        foreach (var s in entry.SpecifierEntries)
                        {
                            if (!seenValues.Add(s.Value))
                                errors.Add($"Building '{entry.IdName}' specifier has duplicate value {s.Value}");
                        }
                        if (!seenValues.Contains(entry.SpecifierDefault))
                        {
                            errors.Add(
                                $"Building '{entry.IdName}' specifier default {entry.SpecifierDefault} is not one of the configured values");
                        }
                    }
                }
            }
        }

        foreach (var kvp in nameToCategories)
        {
            if (kvp.Value.Count > 1)
                errors.Add($"Duplicate id_name '{kvp.Key}' in categories: {string.Join(", ", kvp.Value)}");
        }

        return errors;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    public string DefaultSourceFilePath(string categoryName, string idName)
    {
        string safeId = string.IsNullOrEmpty(idName) ? "new_building" : idName;
        return $"{_buildingsDirectory}{categoryName}/{safeId}.yaml";
    }

    private BuildingEditEntry GetEntryOrThrow(string categoryName, int index)
    {
        var (_, list) = GetBuildingsOrThrow(categoryName);
        if (index < 0 || index >= list.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return list[index];
    }

    private (BuildingCategoryData category, List<BuildingEditEntry> list)
        GetBuildingsOrThrow(string categoryName)
    {
        if (!_categories.TryGetValue(categoryName, out var c))
            throw new KeyNotFoundException($"Category '{categoryName}' not found");
        return (c, c.Buildings);
    }

    private static BuildingEditEntry MapDefinitionToEntry(BuildingDefinition def, string sourceFilePath)
    {
        var entry = new BuildingEditEntry
        {
            IdName = def.IdName ?? "",
            DisplayName = def.DisplayName ?? "",
            Description = def.Description ?? "",
            Category = def.Category ?? "",
            MaxResourceTier = def.MaxResourceTier,
            WorkRequired = def.WorkRequired,
            BuildingLimit = def.BuildingLimit,
            Demolishable = def.Demolishable,
            LinkProfile = string.IsNullOrEmpty(def.DefaultLinkProfile) ? null : def.DefaultLinkProfile,
            AllowedRecipeCategory = string.IsNullOrEmpty(def.AllowedRecipeCategory) ? null : def.AllowedRecipeCategory,
            SourceFilePath = sourceFilePath,
            Visual = MapVisual(def.Visual),
            Icon = MapIcon(def.Icon),
            Placement = MapPlacement(def.Placement),
        };

        foreach (var kvp in def.RequiredResources)
        {
            entry.RequiredResources.Add(new RequiredResourceEdit
            {
                ResourceId = kvp.Key,
                Amount = kvp.Value
            });
        }

        foreach (var beh in def.BehaviorEntries)
        {
            entry.Behaviors.Add(new BehaviorEntryEdit
            {
                BehaviorId = beh.BehaviorId,
                Config = new Dictionary<string, object>(beh.Config)
            });
        }

        if (def.Specifier != null)
        {
            entry.SpecifierEnabled = true;
            for (int i = 0; i < def.Specifier.Values.Count; i++)
            {
                string label = i < def.Specifier.Labels.Count ? def.Specifier.Labels[i] : "";
                entry.SpecifierEntries.Add(new SpecifierEntryEdit
                {
                    Value = def.Specifier.Values[i],
                    Label = label
                });
            }
            entry.SpecifierDefault = def.Specifier.Default;
        }

        return entry;
    }

    private static VisualEdit MapVisual(VisualDefinition v)
    {
        return new VisualEdit
        {
            ModelPath = v.ModelPath,
            ModelMaterial = v.ModelMaterial,
            AnimationPath = v.AnimationPath,
            AnimationName = v.AnimationName,
            Scale = v.Scale,
            RotationOffset = v.RotationOffset,
            ShapeId = v.ShapeId,
            ShapeSize = v.ShapeSize,
            ShapeColor = v.ShapeColor
        };
    }

    private static IconEdit MapIcon(IconDefinition icon)
    {
        return new IconEdit
        {
            BasePath = icon.BasePath,
            Scale = icon.Scale,
            Tint = icon.Tint
        };
    }

    private static PlacementReqEdit MapPlacement(BuildingDefinition.PlacementRequirements p)
    {
        var edit = new PlacementReqEdit
        {
            AllowAnyBiome = p.AllowAnyBiome,
            MinElevation = p.MinElevation,
            MaxElevation = p.MaxElevation,
            MaxSlope = p.MaxSlope,
            CellCount = p.CellCount,
            RequiresAdjacent = p.RequiresAdjacent
        };

        foreach (var biome in p.Biomes)
            edit.Biomes.Add(biome.ToString());

        if (p.ConfigurableBehavior != null)
        {
            edit.ConfigurableBehavior = p.ConfigurableBehavior.GetType().Name;
            if (p.ConfigurableBehavior is AtmospherePlacementBehavior atm)
            {
                edit.ConfigurableBehaviorConfig["min_atmosphere"] = atm.MinAtmosphere;
                edit.ConfigurableBehaviorConfig["max_atmosphere"] = atm.MaxAtmosphere;
            }
        }

        return edit;
    }
}
#endif
