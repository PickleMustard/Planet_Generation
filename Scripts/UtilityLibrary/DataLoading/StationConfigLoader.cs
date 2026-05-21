using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using Structures.Transfers;
using UtilityLibrary;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Loads and parses station YAML configurations from the Configuration/stations/ directory.
/// Supports recursive directory scanning, category inference, and duplicate detection.
/// </summary>
public static class StationConfigLoader
{
    private const string StationsDirectory = "res://Configuration/stations/";
    private const string StationTemplatesPath = "res://Configuration/stations/StationTemplates.yaml";

    private static List<StationDefinition>? _allStations;
    private static HashSet<string>? _inferredCategories;
    private static Dictionary<string, string>? _stationSourceFiles;
    private static List<StationTemplateCategory>? _templateCategories;

    public static int ModelsLoadedCount { get; private set; }
    public static int ModelsFailedCount { get; private set; }
    public static int IconsLoadedCount { get; private set; }
    public static int IconsFailedCount { get; private set; }

    public static void ResetLoadingStats()
    {
        ModelsLoadedCount = 0;
        ModelsFailedCount = 0;
        IconsLoadedCount = 0;
        IconsFailedCount = 0;
    }

    /// <summary>
    /// Loads all station definitions from the Configuration/stations/ directory.
    /// Scans recursively for all .yaml and .yml files, skipping StationTemplates.yaml.
    /// Results are cached for subsequent calls.
    /// </summary>
    /// <returns>List of all station definitions found</returns>
    public static List<StationDefinition> LoadAllStations()
    {
        if (_allStations != null)
            return _allStations;

        _allStations = new List<StationDefinition>();
        _stationSourceFiles = new Dictionary<string, string>();

        var files = BaseConfigLoader.GetYamlFilesRecursive(StationsDirectory);

        GameLogger.Info($"StationConfigLoader: Scanning {files.Count} files in {StationsDirectory}");

        foreach (var filePath in files)
        {
            // Skip template definitions file (used only for validation)
            if (filePath.EndsWith("StationTemplates.yaml"))
                continue;

            var fileStations = LoadStationsFromFile(filePath);

            foreach (var station in fileStations)
            {
                if (_stationSourceFiles.ContainsKey(station.Name))
                {
                    GD.PrintErr($"StationConfigLoader: Duplicate station '{station.Name}' found in {filePath} (first defined in {_stationSourceFiles[station.Name]})");
                    continue;
                }

                _allStations.Add(station);
                _stationSourceFiles[station.Name] = filePath;
            }
        }

        InferCategories(_allStations);
        ValidateStationTypes(_allStations);

        GameLogger.Info($"StationConfigLoader: Loaded {_allStations.Count} stations across {_inferredCategories?.Count ?? 0} categories");

        return _allStations;
    }

    /// <summary>
    /// Loads station definitions from a specific YAML file.
    /// </summary>
    /// <param name="filePath">Path to the YAML file (e.g., "res://Configuration/stations/Shipyard.yaml")</param>
    /// <returns>List of station definitions from that file</returns>
    public static List<StationDefinition> LoadStationsFromFile(string filePath)
    {
        var stations = new List<StationDefinition>();

        if (!Godot.FileAccess.FileExists(filePath))
        {
            GD.PrintErr($"StationConfigLoader: File not found: {filePath}");
            return stations;
        }

        try
        {
            using var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
            string yamlContent = file.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

            if (yamlData != null && yamlData.ContainsKey("stations"))
            {
                var stationsList = yamlData["stations"] as List<object>;
                if (stationsList != null)
                {
                    foreach (var stationObj in stationsList)
                    {
                        if (stationObj is Dictionary<object, object> stationDict)
                        {
                            var station = ParseStationDefinition(stationDict);
                            stations.Add(station);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"StationConfigLoader: Error loading from {filePath}: {e.Message}");
        }

        return stations;
    }

    /// <summary>
    /// Loads station template categories from StationTemplates.yaml.
    /// These categories are used for validation of station types.
    /// </summary>
    /// <returns>List of station template categories</returns>
    public static List<StationTemplateCategory> LoadStationTemplates()
    {
        if (_templateCategories != null)
            return _templateCategories;

        _templateCategories = new List<StationTemplateCategory>();

        if (!Godot.FileAccess.FileExists(StationTemplatesPath))
        {
            GameLogger.Warning($"StationConfigLoader: Templates file not found: {StationTemplatesPath}");
            return _templateCategories;
        }

        try
        {
            using var file = Godot.FileAccess.Open(StationTemplatesPath, Godot.FileAccess.ModeFlags.Read);
            string yamlContent = file.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<SysDict>(yamlContent);

            if (yamlData != null && yamlData.ContainsKey("categories"))
            {
                var categoriesList = yamlData["categories"] as List<object>;
                if (categoriesList != null)
                {
                    foreach (var categoryObj in categoriesList)
                    {
                        if (categoryObj is Dictionary<object, object> categoryDict)
                        {
                            var category = new StationTemplateCategory
                            {
                                Name = BaseConfigLoader.ReadString(categoryDict, "name", ""),
                                Description = BaseConfigLoader.ReadString(categoryDict, "description", ""),
                            };
                            _templateCategories.Add(category);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"StationConfigLoader: Error loading templates from {StationTemplatesPath}: {e.Message}");
        }

        return _templateCategories;
    }

    /// <summary>
    /// Gets all inferred station categories from loaded stations.
    /// Categories are inferred from the 'station_type' field of each station.
    /// </summary>
    /// <returns>List of unique category names</returns>
    public static List<string> GetStationCategories()
    {
        LoadAllStations(); // Ensure stations are loaded
        return _inferredCategories?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Gets a station definition by name (case-insensitive).
    /// </summary>
    /// <param name="name">The station name to look up</param>
    /// <returns>The station definition, or null if not found</returns>
    public static StationDefinition? GetStationByName(string name)
    {
        var stations = LoadAllStations();
        return stations.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears the cached station data to force a reload on next access.
    /// </summary>
    public static void ClearCache()
    {
        _allStations = null;
        _inferredCategories = null;
        _stationSourceFiles = null;
        _templateCategories = null;
        GameLogger.Debug("StationConfigLoader: Cache cleared");
    }

    private static void InferCategories(List<StationDefinition> stations)
    {
        _inferredCategories = new HashSet<string>();
        foreach (var station in stations)
        {
            if (!string.IsNullOrEmpty(station.StationType))
                _inferredCategories.Add(station.StationType);
        }
    }

    private static void ValidateStationTypes(List<StationDefinition> stations)
    {
        var validTypes = LoadStationTemplates().Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var station in stations)
        {
            if (!validTypes.Contains(station.StationType))
            {
                GD.PushWarning($"StationConfigLoader: Unknown station_type '{station.StationType}' for station '{station.Name}'");
            }
        }
    }

    private static StationDefinition ParseStationDefinition(Dictionary<object, object> dict)
    {
        string name = BaseConfigLoader.ReadString(dict, "name", "");

        var definition = new StationDefinition
        {
            Name = name,
            StationType = BaseConfigLoader.ReadString(dict, "station_type", ""),
            ConstructionTime = BaseConfigLoader.ReadFloat(dict, "construction_time", 0f),
            CanBuildShips = BaseConfigLoader.ReadBool(dict, "can_build_ships", false),
            MaxParallelShipBuilds = BaseConfigLoader.ReadInt(dict, "max_parallel_ship_builds", 1),
            CanBuildBuildings = BaseConfigLoader.ReadBool(dict, "can_build_buildings", false),
            BuildingWorkBudgetPerTick = BaseConfigLoader.ReadFloat(dict, "building_work_budget_per_tick", 1.0f),
            RegularSlots = BaseConfigLoader.ReadInt(dict, "regular_slots", 1),
            OvertimeSlots = BaseConfigLoader.ReadInt(dict, "overtime_slots", 0),
            OvertimeCostPercent = BaseConfigLoader.ReadFloat(dict, "overtime_cost_percent", 0.10f),
            RequiredResources = BaseConfigLoader.ReadResourceDict(dict, "required_resources"),
            Visual = ParseVisualDefinition(dict),
            Icon = ParseIconDefinition(dict, $"station:{name}"),
            BehaviorRefs = ParseBehaviorRefs(dict),
            StorageCapacity = BaseConfigLoader.ReadInt(dict, "storage_capacity", 0),
            SlotFilters = ParseSlotFilters(dict),
            TransferStation = ParseTransferStationDefinition(dict),
        };

        // Apply fallback if icon failed to load
        if (!definition.Icon.IsValid)
        {
            definition.Icon = IconDataLoader.CreateFallbackIconDefinition();
        }

        return definition;
    }

    private static IconDefinition ParseIconDefinition(Dictionary<object, object> dict, string context)
    {
        if (!dict.ContainsKey("icon"))
        {
            return new IconDefinition();
        }

        var iconDict = dict["icon"] as Dictionary<object, object>;
        if (iconDict == null)
            return new IconDefinition();

        string? basePath = BaseConfigLoader.ReadString(iconDict, "base_path", "");
        if (string.IsNullOrEmpty(basePath))
        {
            return new IconDefinition();
        }

        var icon = IconDataLoader.LoadIcon(basePath, context);

        // Track stats
        if (icon.IsValid)
            IconsLoadedCount++;
        else
            IconsFailedCount++;

        icon.Scale = BaseConfigLoader.ReadFloat(iconDict, "scale", 1.0f);
        icon.Tint = ReadColor(iconDict, "tint", Colors.White);

        return icon;
    }

    private static Color ReadColor(Dictionary<object, object> dict, string key, Color fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;

        if (dict[key] is not System.Collections.Generic.List<object> arr || arr.Count < 3)
            return fallback;

        float r = NodeToFloat(arr[0], fallback.R);
        float g = NodeToFloat(arr[1], fallback.G);
        float b = NodeToFloat(arr[2], fallback.B);
        float a = arr.Count >= 4 ? NodeToFloat(arr[3], fallback.A) : 1.0f;

        if (r > 1.0f || g > 1.0f || b > 1.0f || a > 1.0f)
        {
            r /= 255.0f;
            g /= 255.0f;
            b /= 255.0f;
            a = arr.Count >= 4 ? a / 255.0f : 1.0f;
        }

        return new Color(r, g, b, a);
    }

    private static float NodeToFloat(object node, float fallback)
    {
        try
        {
            if (node is long l)
                return (float)l;
            if (node is double d)
                return (float)d;
            if (node is float f)
                return f;
            if (node is int i)
                return (float)i;

            var s = node?.ToString();
            if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error parsing node to float: {e.Message}");
        }

        return fallback;
    }

    private static VisualDefinition ParseVisualDefinition(Dictionary<object, object> dict)
    {
        var visual = new VisualDefinition();

        if (!dict.ContainsKey("visual"))
            return visual;

        var visualDict = dict["visual"] as Dictionary<object, object>;
        if (visualDict == null)
            return visual;

        string? modelPath = BaseConfigLoader.ReadString(visualDict, "model_path", "");
        if (!string.IsNullOrEmpty(modelPath) && Godot.FileAccess.FileExists(modelPath))
        {
            visual.ModelPath = modelPath;
            try
            {
                visual.ModelPrototype = GD.Load<PackedScene>(modelPath);
                if (visual.ModelPrototype != null)
                {
                    GameLogger.Info($"StationConfigLoader: Loaded model prototype '{modelPath}'");
                    ModelsLoadedCount++;
                }
                else
                {
                    GameLogger.Error($"StationConfigLoader: Failed to load model at '{modelPath}'");
                    ModelsFailedCount++;
                }
            }
            catch (System.Exception ex)
            {
                GameLogger.Error($"StationConfigLoader: Exception loading model '{modelPath}': {ex.Message}");
                ModelsFailedCount++;
            }
        }

        visual.ModelMaterial = BaseConfigLoader.ReadString(visualDict, "model_material", "");
        visual.AnimationPath = BaseConfigLoader.ReadString(visualDict, "animation_path", "");
        visual.Scale = BaseConfigLoader.ReadFloat(visualDict, "scale", 1.0f);
        visual.RotationOffset = BaseConfigLoader.ReadVector3(visualDict, "rotation_offset", Vector3.Zero);
        visual.ShapeId = BaseConfigLoader.ReadString(visualDict, "shape_id", "hexagon").Trim();
        if (string.IsNullOrEmpty(visual.ShapeId)) visual.ShapeId = "hexagon";
        visual.ShapeSize = BaseConfigLoader.ReadFloat(visualDict, "shape_size", 64f);
        visual.ShapeColor = ReadColor(visualDict, "shape_color", visual.ShapeColor);

        return visual;
    }

    private static Godot.Collections.Array<string> ParseBehaviorRefs(Dictionary<object, object> dict)
    {
        var refs = new Godot.Collections.Array<string>();

        if (!dict.ContainsKey("behaviors"))
            return refs;

        if (dict["behaviors"] is not List<object> behaviorList)
            return refs;

        foreach (var entry in behaviorList)
        {
            if (entry is string s && !string.IsNullOrEmpty(s))
                refs.Add(s);
        }

        return refs;
    }

    /// <summary>
    /// Parses the slot_filters YAML block into a list of (filter, count) pairs.
    /// Keys use prefix syntax: "any", "state:solid", "state:fluid", "category:&lt;name&gt;",
    /// "resource:&lt;id&gt;". A bare key falls back to category lookup (legacy behavior).
    /// </summary>
    private static List<SlotFilterSpec> ParseSlotFilters(Dictionary<object, object> dict)
    {
        var specs = new List<SlotFilterSpec>();

        if (!dict.ContainsKey("slot_filters"))
            return specs;

        if (dict["slot_filters"] is not Dictionary<object, object> filtersDict)
            return specs;

        foreach (var kvp in filtersDict)
        {
            string key = ((string)kvp.Key).Trim();
            int count = (int)NodeToFloat(kvp.Value, 0f);
            if (count <= 0 || string.IsNullOrEmpty(key))
                continue;

            SlotFilter filter;
            if (string.Equals(key, "any", StringComparison.OrdinalIgnoreCase))
            {
                filter = SlotFilter.Any();
            }
            else if (key.StartsWith("state:", StringComparison.OrdinalIgnoreCase))
            {
                string stateName = key.Substring("state:".Length);
                filter = SlotFilter.ForState(StateOfMatterExtensions.Parse(stateName));
            }
            else if (key.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
            {
                filter = SlotFilter.ForCategory(key.Substring("category:".Length));
            }
            else if (key.StartsWith("resource:", StringComparison.OrdinalIgnoreCase))
            {
                filter = SlotFilter.ForResource(key.Substring("resource:".Length));
            }
            else
            {
                // Legacy bare key: treat as category name.
                filter = SlotFilter.ForCategory(key);
            }

            specs.Add(new SlotFilterSpec(filter, count));
        }

        return specs;
    }

    private static TransferStationDefinition? ParseTransferStationDefinition(Dictionary<object, object> dict)
    {
        if (!dict.ContainsKey("transfer_station"))
            return null;

        var stationDict = dict["transfer_station"] as Dictionary<object, object>;
        if (stationDict == null)
        {
            GameLogger.Warning("StationConfigLoader: transfer_station block is not a valid dictionary, skipping");
            return null;
        }

        return new TransferStationDefinition
        {
            CargoCapacity = BaseConfigLoader.ReadFloat(stationDict, "cargo_capacity", 500.0f),
            VehicleSpeed = BaseConfigLoader.ReadFloat(stationDict, "vehicle_speed", 50.0f),
            MaxConcurrentTransfers = BaseConfigLoader.ReadInt(stationDict, "max_concurrent_transfers", 2),
        };
    }

}
