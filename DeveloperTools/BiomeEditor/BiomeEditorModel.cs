#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using ProceduralGeneration.BiomeSystem;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DeveloperTools.BiomeEditor;

/// <summary>
/// In-memory aggregate of all five biome-related YAML sources:
///   1. Configuration/Biomes/biome_assigner_config.yaml
///   2. Configuration/ResourceDefinition/biome_resource_config.yaml
///   3. Configuration/ResourceDefinition/planetary_resource_config.yaml (RockyPlanet section only)
///   4. Configuration/ResourceDefinition/resource_groups.yaml
///   5. Configuration/SystemGen/{RockyPlanet,GasGiant,IceGiant,DwarfPlanet}.yaml (atmosphere range)
/// Each entity tracks SourceFilePath plus IsNew / IsDirty for round-trip writes.
/// </summary>
public class BiomeEditorModel
{
    // ── Edit types ──────────────────────────────────────────────────────

    public class AssignerEdit
    {
        public RockyPlanetSubtype Subtype { get; set; }
        public MoistureParams Moisture { get; set; } = new();
        public List<BiomeRule> Rules { get; set; } = new();
        public bool IsDirty { get; set; }
    }

    public class BiomeResourceEdit
    {
        public Biome.BiomeType Biome { get; set; }
        public Dictionary<string, float> Weights { get; set; } = new(StringComparer.Ordinal);
        public bool IsDirty { get; set; }
    }

    public class SubtypeResourceEdit
    {
        public RockyPlanetSubtype Subtype { get; set; }
        public float BaseResourceWeight { get; set; } = 1f;
        public List<string> ResourceGroups { get; set; } = new();
        public List<string> AddResources { get; set; } = new();
        public List<string> RemoveResources { get; set; } = new();
        public bool IsDirty { get; set; }
    }

    public class ResourceGroupEdit
    {
        public string GroupName { get; set; } = "";
        public List<string> ResourceIds { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    public class AtmosphereTemplateEdit
    {
        public string BodyTypeName { get; set; } = "";
        public string SourceFilePath { get; set; } = "";
        public float AtmosphereMin { get; set; }
        public float AtmosphereMax { get; set; }
        public List<string> PossibleSubtypes { get; set; } = new();
        public string? SubtypeListPath { get; set; }
        public bool IsDirty { get; set; }
    }

    // ── Source paths ────────────────────────────────────────────────────

    public const string AssignerPath = "res://Configuration/Biomes/biome_assigner_config.yaml";
    public const string BiomeResourcePath = "res://Configuration/ResourceDefinition/biome_resource_config.yaml";
    public const string PlanetaryResourcePath = "res://Configuration/ResourceDefinition/planetary_resource_config.yaml";
    public const string ResourceGroupsPath = "res://Configuration/ResourceDefinition/resource_groups.yaml";

    private static readonly (string typeName, string sysGenPath, string subtypeListPath)[] _atmTemplates =
    {
        ("RockyPlanet", "res://Configuration/SystemGen/RockyPlanet.yaml", "res://Configuration/planetary_types/rocky_planets.yaml"),
        ("GasGiant",    "res://Configuration/SystemGen/GasGiant.yaml",    "res://Configuration/planetary_types/gas_giants.yml"),
        ("IceGiant",    "res://Configuration/SystemGen/IceGiant.yaml",    "res://Configuration/planetary_types/ice_giants.yml"),
        ("DwarfPlanet", "res://Configuration/SystemGen/DwarfPlanet.yaml", null!),
    };

    public static IReadOnlyList<(string typeName, string sysGenPath, string? subtypeListPath)> AtmosphereTemplatePaths =>
        _atmTemplates.Select(t => (t.typeName, t.sysGenPath, (string?)t.subtypeListPath)).ToList();

    // ── State ───────────────────────────────────────────────────────────

    public Dictionary<RockyPlanetSubtype, AssignerEdit> Assigners { get; private set; } = new();
    public Dictionary<Biome.BiomeType, BiomeResourceEdit> BiomeResources { get; private set; } = new();
    public Dictionary<RockyPlanetSubtype, SubtypeResourceEdit> SubtypeResources { get; private set; } = new();
    public List<ResourceGroupEdit> ResourceGroups { get; private set; } = new();
    public Dictionary<string, AtmosphereTemplateEdit> AtmosphereTemplates { get; private set; } = new(StringComparer.Ordinal);

    public bool HasUnsavedChanges =>
        Assigners.Values.Any(a => a.IsDirty)
        || BiomeResources.Values.Any(b => b.IsDirty)
        || SubtypeResources.Values.Any(s => s.IsDirty)
        || ResourceGroups.Any(g => g.IsNew || g.IsDirty)
        || AtmosphereTemplates.Values.Any(a => a.IsDirty);

    public event Action<RockyPlanetSubtype>? AssignerChanged;
    public event Action<Biome.BiomeType>? BiomeWeightsChanged;
    public event Action<RockyPlanetSubtype>? SubtypeWeightsChanged;

    // ── Loading ─────────────────────────────────────────────────────────

    public void LoadFromDisk()
    {
        LoadAssigners();
        LoadBiomeResources();
        LoadSubtypeResources();
        LoadResourceGroups();
        LoadAtmosphereTemplates();
    }

    private void LoadAssigners()
    {
        Assigners.Clear();
        var cfg = BiomeAssignerConfigLoader.Load(AssignerPath);
        if (cfg == null)
        {
            GameLogger.Warning($"BiomeEditorModel: failed to load {AssignerPath}");
            return;
        }
        foreach (var kvp in cfg.Assigners)
        {
            var subtypeEnum = BiomeIdMapper.IdToRockyPlanetSubtype(kvp.Key);
            if (subtypeEnum == null)
            {
                GameLogger.Warning($"BiomeEditorModel: unknown rocky subtype id '{kvp.Key}'");
                continue;
            }
            Assigners[subtypeEnum.Value] = new AssignerEdit
            {
                Subtype = subtypeEnum.Value,
                Moisture = CloneMoisture(kvp.Value.Moisture),
                Rules = kvp.Value.Rules.Select(CloneRule).ToList(),
            };
        }
    }

    private void LoadBiomeResources()
    {
        BiomeResources.Clear();
        var cfg = ResourceConfigLoader.LoadBiomeResourceConfig(BiomeResourcePath);
        if (cfg == null) return;
        foreach (var entry in cfg.BiomesRaw)
        {
            BiomeResources[entry.Biome] = new BiomeResourceEdit
            {
                Biome = entry.Biome,
                Weights = new Dictionary<string, float>(entry.ResourceWeightModifiers, StringComparer.Ordinal),
            };
        }
    }

    private void LoadSubtypeResources()
    {
        SubtypeResources.Clear();
        var cfg = ResourceConfigLoader.LoadPlanetaryResourceConfig(PlanetaryResourcePath);
        if (cfg == null) return;
        foreach (var raw in cfg.RockyPlanetSubtypesRaw)
        {
            if (!Enum.TryParse<RockyPlanetSubtype>(raw.Subtype, out var subtype))
                continue;
            SubtypeResources[subtype] = new SubtypeResourceEdit
            {
                Subtype = subtype,
                BaseResourceWeight = raw.BaseResourceWeight,
                ResourceGroups = new List<string>(raw.ResourceGroups ?? new()),
                AddResources = new List<string>(raw.AddResources ?? new()),
                RemoveResources = new List<string>(raw.RemoveResources ?? new()),
            };
        }
    }

    private void LoadResourceGroups()
    {
        ResourceGroups.Clear();
        var groups = ResourceConfigLoader.LoadResourceGroups(ResourceGroupsPath);
        if (groups == null) return;
        foreach (var kvp in groups.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            ResourceGroups.Add(new ResourceGroupEdit
            {
                GroupName = kvp.Key,
                ResourceIds = new List<string>(kvp.Value ?? new()),
            });
        }
    }

    private void LoadAtmosphereTemplates()
    {
        AtmosphereTemplates.Clear();
        foreach (var (typeName, sysGenPath, subtypeListPath) in _atmTemplates)
        {
            var atm = LoadAtmosphereRange(sysGenPath);
            var subs = subtypeListPath != null ? LoadPossibleSubtypes(subtypeListPath) : new List<string>();
            AtmosphereTemplates[typeName] = new AtmosphereTemplateEdit
            {
                BodyTypeName = typeName,
                SourceFilePath = sysGenPath,
                AtmosphereMin = atm.min,
                AtmosphereMax = atm.max,
                PossibleSubtypes = subs,
                SubtypeListPath = subtypeListPath,
            };
        }
    }

    private static (float min, float max) LoadAtmosphereRange(string path)
    {
        if (!Godot.FileAccess.FileExists(path)) return (0f, 1f);
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (f == null) return (0f, 1f);
        string text = f.GetAsText();
        var d = new DeserializerBuilder().Build();
        try
        {
            var raw = d.Deserialize<Dictionary<object, object>>(text);
            if (raw == null) return (0f, 1f);
            object? root = raw.ContainsKey("celestial") ? raw["celestial"]
                         : raw.ContainsKey("satellite_group") ? raw["satellite_group"]
                         : null;
            if (root is not Dictionary<object, object> rdict) return (0f, 1f);
            if (!rdict.TryGetValue("template", out var tmpl) || tmpl is not Dictionary<object, object> tdict)
                return (0f, 1f);
            if (!tdict.TryGetValue("atmosphere", out var atmObj) || atmObj is not List<object> atmList || atmList.Count < 2)
                return (0f, 1f);
            return (ParseFloat(atmList[0]), ParseFloat(atmList[1]));
        }
        catch (Exception ex)
        {
            GameLogger.Warning($"BiomeEditorModel: failed parse atmosphere from {path}: {ex.Message}");
            return (0f, 1f);
        }
    }

    private static List<string> LoadPossibleSubtypes(string path)
    {
        var result = new List<string>();
        if (!Godot.FileAccess.FileExists(path)) return result;
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (f == null) return result;
        string text = f.GetAsText();
        var d = new DeserializerBuilder().Build();
        try
        {
            var raw = d.Deserialize<Dictionary<object, object>>(text);
            if (raw == null || !raw.TryGetValue("potential_types", out var ptObj) || ptObj is not List<object> ptList)
                return result;
            foreach (var item in ptList)
            {
                if (item is Dictionary<object, object> dict && dict.TryGetValue("name", out var nameObj))
                    result.Add(nameObj?.ToString() ?? "");
            }
        }
        catch { }
        return result;
    }

    private static float ParseFloat(object node)
    {
        return node switch
        {
            float f => f,
            double d => (float)d,
            long l => l,
            int i => i,
            string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) => v,
            _ => 0f,
        };
    }

    // ── Mutators (raise change events for preview tabs) ─────────────────

    public void UpdateAssignerMoisture(RockyPlanetSubtype subtype, MoistureParams updated)
    {
        if (!Assigners.TryGetValue(subtype, out var entry)) return;
        entry.Moisture = CloneMoisture(updated);
        entry.IsDirty = true;
        AssignerChanged?.Invoke(subtype);
    }

    public void UpdateAssignerRule(RockyPlanetSubtype subtype, int ruleIndex, BiomeRule updated)
    {
        if (!Assigners.TryGetValue(subtype, out var entry)) return;
        if (ruleIndex < 0 || ruleIndex >= entry.Rules.Count) return;
        entry.Rules[ruleIndex] = CloneRule(updated);
        entry.IsDirty = true;
        AssignerChanged?.Invoke(subtype);
    }

    public void AddAssignerRule(RockyPlanetSubtype subtype, BiomeRule rule)
    {
        if (!Assigners.TryGetValue(subtype, out var entry)) return;
        entry.Rules.Add(CloneRule(rule));
        entry.IsDirty = true;
        AssignerChanged?.Invoke(subtype);
    }

    public void RemoveAssignerRule(RockyPlanetSubtype subtype, int ruleIndex)
    {
        if (!Assigners.TryGetValue(subtype, out var entry)) return;
        if (ruleIndex < 0 || ruleIndex >= entry.Rules.Count) return;
        entry.Rules.RemoveAt(ruleIndex);
        entry.IsDirty = true;
        AssignerChanged?.Invoke(subtype);
    }

    public void MoveAssignerRule(RockyPlanetSubtype subtype, int fromIndex, int toIndex)
    {
        if (!Assigners.TryGetValue(subtype, out var entry)) return;
        if (fromIndex < 0 || fromIndex >= entry.Rules.Count) return;
        if (toIndex < 0 || toIndex >= entry.Rules.Count) return;
        var r = entry.Rules[fromIndex];
        entry.Rules.RemoveAt(fromIndex);
        entry.Rules.Insert(toIndex, r);
        entry.IsDirty = true;
        AssignerChanged?.Invoke(subtype);
    }

    public void SetBiomeResourceWeight(Biome.BiomeType biome, string resourceId, float value)
    {
        if (string.IsNullOrEmpty(resourceId)) return;
        if (!BiomeResources.TryGetValue(biome, out var edit))
        {
            edit = new BiomeResourceEdit { Biome = biome };
            BiomeResources[biome] = edit;
        }
        edit.Weights[resourceId] = value;
        edit.IsDirty = true;
        BiomeWeightsChanged?.Invoke(biome);
    }

    public void RemoveBiomeResourceWeight(Biome.BiomeType biome, string resourceId)
    {
        if (!BiomeResources.TryGetValue(biome, out var edit)) return;
        if (edit.Weights.Remove(resourceId))
        {
            edit.IsDirty = true;
            BiomeWeightsChanged?.Invoke(biome);
        }
    }

    public void UpdateSubtypeResource(RockyPlanetSubtype subtype, SubtypeResourceEdit updated)
    {
        SubtypeResources[subtype] = new SubtypeResourceEdit
        {
            Subtype = subtype,
            BaseResourceWeight = updated.BaseResourceWeight,
            ResourceGroups = new List<string>(updated.ResourceGroups),
            AddResources = new List<string>(updated.AddResources),
            RemoveResources = new List<string>(updated.RemoveResources),
            IsDirty = true,
        };
        SubtypeWeightsChanged?.Invoke(subtype);
    }

    public void AddResourceGroup(string groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;
        if (ResourceGroups.Any(g => g.GroupName == groupName)) return;
        ResourceGroups.Add(new ResourceGroupEdit { GroupName = groupName, IsNew = true, IsDirty = true });
    }

    public void RemoveResourceGroup(string groupName)
    {
        ResourceGroups.RemoveAll(g => g.GroupName == groupName);
    }

    public void UpdateResourceGroup(string groupName, List<string> resourceIds)
    {
        var g = ResourceGroups.FirstOrDefault(x => x.GroupName == groupName);
        if (g == null) return;
        g.ResourceIds = new List<string>(resourceIds);
        g.IsDirty = true;
    }

    public void UpdateAtmosphereTemplate(string typeName, float min, float max)
    {
        if (!AtmosphereTemplates.TryGetValue(typeName, out var edit)) return;
        edit.AtmosphereMin = min;
        edit.AtmosphereMax = max;
        edit.IsDirty = true;
    }

    // ── Build a runtime BiomeAssignerConfig from current edits ──────────

    public BiomeAssignerConfig BuildAssignerConfig()
    {
        var cfg = new BiomeAssignerConfig();
        foreach (var kvp in Assigners)
        {
            cfg.Assigners[BiomeIdMapper.RockyPlanetSubtypeToId(kvp.Key)] = new BiomeAssignerEntry
            {
                Moisture = CloneMoisture(kvp.Value.Moisture),
                Rules = kvp.Value.Rules.Select(CloneRule).ToList(),
            };
        }
        return cfg;
    }

    // ── Cloning helpers ─────────────────────────────────────────────────

    private static MoistureParams CloneMoisture(MoistureParams m) => new()
    {
        Mode = m.Mode,
        BaseOffset = m.BaseOffset,
        LatitudeFactorDivisor = m.LatitudeFactorDivisor,
        SizeFactorDivisor = m.SizeFactorDivisor,
        RandomMin = m.RandomMin,
        RandomMax = m.RandomMax,
        MaxMoisture = m.MaxMoisture,
        ConstantOffset = m.ConstantOffset,
    };

    private static BiomeRule CloneRule(BiomeRule r) => new()
    {
        Biome = r.Biome,
        When = new BiomeRuleConditions
        {
            HeightAbove = r.When.HeightAbove,
            HeightBelow = r.When.HeightBelow,
            MoistureAbove = r.When.MoistureAbove,
            MoistureBelow = r.When.MoistureBelow,
            AbsLatitudeAbove = r.When.AbsLatitudeAbove,
            AbsLatitudeBelow = r.When.AbsLatitudeBelow,
        },
    };

    // ── Convenience getters ─────────────────────────────────────────────

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

    public static List<string> GetAllBiomes() =>
        Enum.GetNames<Biome.BiomeType>().OrderBy(s => s).ToList();

    public static List<RockyPlanetSubtype> GetAllRockySubtypes() =>
        Enum.GetValues<RockyPlanetSubtype>().ToList();
}
#endif
