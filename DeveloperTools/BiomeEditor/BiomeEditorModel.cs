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

    public class BiomeDefinitionEdit
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public Color DefaultColor { get; set; } = Colors.Gray;
        public Dictionary<string, Color> ColorOverrides { get; set; } = new(StringComparer.Ordinal);
        public float HazardWeight { get; set; }
        public float GeothermalVentProbability { get; set; }
        public Dictionary<string, float> ResourceWeightModifiers { get; set; } = new(StringComparer.Ordinal);
        public List<string> Tags { get; set; } = new();
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    public class SubtypeDefinitionEdit
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public BodyFamily Family { get; set; }
        public float AtmosphereMin { get; set; }
        public float AtmosphereMax { get; set; }
        public float BaseHazard { get; set; }
        public float BaseResourceWeight { get; set; } = 1.0f;
        public string MoistureMode { get; set; } = "whittaker";
        public Dictionary<string, float> MoistureParams { get; set; } = new(StringComparer.Ordinal);
        public List<BiomeRuleDefinition> AssignerRules { get; set; } = new();
        public List<string> ResourceGroups { get; set; } = new();
        public List<string> AddResources { get; set; } = new();
        public List<string> RemoveResources { get; set; } = new();
        public Dictionary<string, FloatRange> MeshRanges { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, FloatRange> TectonicRanges { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, FloatRange> SphericalHarmonicsRanges { get; set; } = new(StringComparer.Ordinal);
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    // ── Source paths ────────────────────────────────────────────────────

    public const string AssignerPath = "res://Configuration/Biomes/biome_assigner_config.yaml";
    public const string BiomeResourcePath = "res://Configuration/ResourceDefinition/biome_resource_config.yaml";
    public const string PlanetaryResourcePath = "res://Configuration/ResourceDefinition/planetary_resource_config.yaml";
    public const string ResourceGroupsPath = "res://Configuration/ResourceDefinition/resource_groups.yaml";
    public const string BiomesDefinitionPath = "res://Configuration/Biomes/biomes.yaml";
    public const string SubtypesDir = "res://Configuration/Subtypes";

    public static string SubtypeFilePath(BodyFamily family) => family switch
    {
        BodyFamily.RockyPlanet => $"{SubtypesDir}/rocky_subtypes.yaml",
        BodyFamily.GasGiant => $"{SubtypesDir}/gas_giant_subtypes.yaml",
        BodyFamily.IceGiant => $"{SubtypesDir}/ice_giant_subtypes.yaml",
        BodyFamily.DwarfPlanet => $"{SubtypesDir}/dwarf_planet_subtypes.yaml",
        BodyFamily.Star => $"{SubtypesDir}/star_subtypes.yaml",
        BodyFamily.NeutronStar => $"{SubtypesDir}/neutron_star_subtypes.yaml",
        BodyFamily.BlackHole => $"{SubtypesDir}/black_hole_subtypes.yaml",
        BodyFamily.Satellite => $"{SubtypesDir}/satellite_subtypes.yaml",
        BodyFamily.Belt => $"{SubtypesDir}/belt_subtypes.yaml",
        _ => $"{SubtypesDir}/rocky_subtypes.yaml",
    };

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

    public Dictionary<string, BiomeDefinitionEdit> Biomes { get; private set; } = new(StringComparer.Ordinal);
    public Dictionary<string, SubtypeDefinitionEdit> Subtypes { get; private set; } = new(StringComparer.Ordinal);

    public bool HasUnsavedChanges =>
        Assigners.Values.Any(a => a.IsDirty)
        || BiomeResources.Values.Any(b => b.IsDirty)
        || SubtypeResources.Values.Any(s => s.IsDirty)
        || ResourceGroups.Any(g => g.IsNew || g.IsDirty)
        || AtmosphereTemplates.Values.Any(a => a.IsDirty)
        || Biomes.Values.Any(b => b.IsNew || b.IsDirty)
        || Subtypes.Values.Any(s => s.IsNew || s.IsDirty);

    public event Action<RockyPlanetSubtype>? AssignerChanged;
    public event Action<Biome.BiomeType>? BiomeWeightsChanged;
    public event Action<RockyPlanetSubtype>? SubtypeWeightsChanged;
    public event Action<string>? BiomeDefinitionChanged;
    public event Action<string>? SubtypeDefinitionChanged;
    public event Action? BiomeRegistryChanged;
    public event Action? SubtypeRegistryChanged;

    // ── Loading ─────────────────────────────────────────────────────────

    public void LoadFromDisk()
    {
        LoadAssigners();
        LoadBiomeResources();
        LoadSubtypeResources();
        LoadResourceGroups();
        LoadAtmosphereTemplates();
        LoadBiomeDefinitions();
        LoadSubtypeDefinitions();
    }

    private void LoadBiomeDefinitions()
    {
        Biomes.Clear();
        var loaded = BiomeDefinitionLoader.Load(BiomesDefinitionPath);
        if (loaded == null) return;
        foreach (var def in loaded)
        {
            if (string.IsNullOrEmpty(def.Id)) continue;
            Biomes[def.Id] = new BiomeDefinitionEdit
            {
                Id = def.Id,
                DisplayName = def.DisplayName,
                DefaultColor = def.DefaultColor,
                ColorOverrides = new Dictionary<string, Color>(def.ColorOverrides, StringComparer.Ordinal),
                HazardWeight = def.HazardWeight,
                GeothermalVentProbability = def.GeothermalVentProbability,
                ResourceWeightModifiers = new Dictionary<string, float>(def.ResourceWeightModifiers, StringComparer.Ordinal),
                Tags = new List<string>(def.Tags),
            };
        }
    }

    private void LoadSubtypeDefinitions()
    {
        Subtypes.Clear();
        var loaded = SubtypeDefinitionLoader.LoadAll(SubtypesDir);
        if (loaded == null) return;
        foreach (var def in loaded)
        {
            if (string.IsNullOrEmpty(def.Id)) continue;
            Subtypes[def.Id] = new SubtypeDefinitionEdit
            {
                Id = def.Id,
                DisplayName = def.DisplayName,
                Family = def.Family,
                AtmosphereMin = def.AtmosphereMin,
                AtmosphereMax = def.AtmosphereMax,
                BaseHazard = def.BaseHazard,
                BaseResourceWeight = def.BaseResourceWeight,
                MoistureMode = def.MoistureMode,
                MoistureParams = new Dictionary<string, float>(def.MoistureParams, StringComparer.Ordinal),
                AssignerRules = def.AssignerRules.Select(CloneRuleDef).ToList(),
                ResourceGroups = new List<string>(def.ResourceGroups),
                AddResources = new List<string>(def.AddResources),
                RemoveResources = new List<string>(def.RemoveResources),
                MeshRanges = new Dictionary<string, FloatRange>(def.MeshRanges, StringComparer.Ordinal),
                TectonicRanges = new Dictionary<string, FloatRange>(def.TectonicRanges, StringComparer.Ordinal),
                SphericalHarmonicsRanges = new Dictionary<string, FloatRange>(def.SphericalHarmonicsRanges, StringComparer.Ordinal),
            };
        }
    }

    private static BiomeRuleDefinition CloneRuleDef(BiomeRuleDefinition r) => new()
    {
        BiomeId = r.BiomeId,
        HeightAbove = r.HeightAbove,
        HeightBelow = r.HeightBelow,
        MoistureAbove = r.MoistureAbove,
        MoistureBelow = r.MoistureBelow,
        AbsLatitudeAbove = r.AbsLatitudeAbove,
        AbsLatitudeBelow = r.AbsLatitudeBelow,
    };

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

    // ── Biome definition CRUD ───────────────────────────────────────────

    public bool AddBiome(string id, string displayName)
    {
        if (string.IsNullOrEmpty(id) || Biomes.ContainsKey(id)) return false;
        Biomes[id] = new BiomeDefinitionEdit
        {
            Id = id,
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName,
            IsNew = true,
            IsDirty = true,
        };
        BiomeRegistryChanged?.Invoke();
        return true;
    }

    public void UpdateBiome(BiomeDefinitionEdit updated)
    {
        if (updated == null || string.IsNullOrEmpty(updated.Id)) return;
        if (!Biomes.ContainsKey(updated.Id)) return;
        updated.IsDirty = true;
        Biomes[updated.Id] = updated;
        BiomeDefinitionChanged?.Invoke(updated.Id);
    }

    public bool RemoveBiome(string id, bool cascade)
    {
        if (string.IsNullOrEmpty(id) || !Biomes.ContainsKey(id)) return false;
        if (cascade)
        {
            foreach (var sub in Subtypes.Values)
            {
                int removed = sub.AssignerRules.RemoveAll(r => r.BiomeId == id);
                if (removed > 0) sub.IsDirty = true;
            }
            foreach (var b in Biomes.Values)
            {
                // No biome→biome refs currently.
            }
        }
        Biomes.Remove(id);
        BiomeRegistryChanged?.Invoke();
        return true;
    }

    public bool RenameBiome(string oldId, string newId)
    {
        if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId)) return false;
        if (!Biomes.TryGetValue(oldId, out var edit)) return false;
        if (Biomes.ContainsKey(newId)) return false;
        Biomes.Remove(oldId);
        edit.Id = newId;
        edit.IsDirty = true;
        Biomes[newId] = edit;

        foreach (var sub in Subtypes.Values)
        {
            bool changed = false;
            foreach (var rule in sub.AssignerRules)
            {
                if (rule.BiomeId == oldId) { rule.BiomeId = newId; changed = true; }
            }
            if (changed) sub.IsDirty = true;
        }
        BiomeRegistryChanged?.Invoke();
        return true;
    }

    public IReadOnlyList<string> FindBiomeReferences(string biomeId)
    {
        var refs = new List<string>();
        if (string.IsNullOrEmpty(biomeId)) return refs;
        foreach (var sub in Subtypes.Values)
        {
            int count = sub.AssignerRules.Count(r => r.BiomeId == biomeId);
            if (count > 0) refs.Add($"Subtype {sub.Id}: {count} assigner rule(s)");
        }
        return refs;
    }

    // ── Subtype definition CRUD ─────────────────────────────────────────

    public bool AddSubtype(string id, string displayName, BodyFamily family)
    {
        if (string.IsNullOrEmpty(id) || Subtypes.ContainsKey(id)) return false;
        Subtypes[id] = new SubtypeDefinitionEdit
        {
            Id = id,
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName,
            Family = family,
            IsNew = true,
            IsDirty = true,
        };
        SubtypeRegistryChanged?.Invoke();
        return true;
    }

    public void UpdateSubtype(SubtypeDefinitionEdit updated)
    {
        if (updated == null || string.IsNullOrEmpty(updated.Id)) return;
        if (!Subtypes.ContainsKey(updated.Id)) return;
        updated.IsDirty = true;
        Subtypes[updated.Id] = updated;
        SubtypeDefinitionChanged?.Invoke(updated.Id);
    }

    public bool RemoveSubtype(string id, bool cascade)
    {
        if (string.IsNullOrEmpty(id) || !Subtypes.ContainsKey(id)) return false;
        if (cascade)
        {
            foreach (var biome in Biomes.Values)
            {
                if (biome.ColorOverrides.Remove(id))
                    biome.IsDirty = true;
            }
        }
        Subtypes.Remove(id);
        SubtypeRegistryChanged?.Invoke();
        return true;
    }

    public bool RenameSubtype(string oldId, string newId)
    {
        if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId)) return false;
        if (!Subtypes.TryGetValue(oldId, out var edit)) return false;
        if (Subtypes.ContainsKey(newId)) return false;
        Subtypes.Remove(oldId);
        edit.Id = newId;
        edit.IsDirty = true;
        Subtypes[newId] = edit;

        foreach (var biome in Biomes.Values)
        {
            if (biome.ColorOverrides.TryGetValue(oldId, out var c))
            {
                biome.ColorOverrides.Remove(oldId);
                biome.ColorOverrides[newId] = c;
                biome.IsDirty = true;
            }
        }
        SubtypeRegistryChanged?.Invoke();
        return true;
    }

    public IReadOnlyList<string> FindSubtypeReferences(string subtypeId)
    {
        var refs = new List<string>();
        if (string.IsNullOrEmpty(subtypeId)) return refs;
        foreach (var biome in Biomes.Values)
        {
            if (biome.ColorOverrides.ContainsKey(subtypeId))
                refs.Add($"Biome {biome.Id}: color_override");
        }
        return refs;
    }
}
#endif
