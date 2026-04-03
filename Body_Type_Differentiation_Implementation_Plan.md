# Celestial Body Type Differentiation Implementation Plan

## Executive Summary

This document outlines the comprehensive implementation plan for adding body type differentiation to the Planet Generation system. The system will enable celestial bodies to have subtypes selected probabilistically based on their orbital distance from their parent body, with unique biomes, resources, and coloration for each subtype.

## Core Architecture Principles

1. **Hierarchical Type System**: `CelestialBodyType` → `XSubtype` (AU-based within same parent type)
2. **AU-Based Selection**: 1000 units = 1 AU, with separate probability files per body type
3. **Parent-Relative Distances**: Satellites use parent-body distances, belts use dominant-body distances
4. **Manual Override Priority**: Template-specified subtypes override probability, UI integration for manual type selection
5. **Full Validation**: YAML validation for AU probability files
6. **Debug Integration**: Expose body types in debug UI
7. **Backward Compatibility**: All new fields optional, fallback chains at every level

## Phase 1: Core Constants and Enums

### 1.1 OrbitalMath Constants Extension
**File**: `Scripts/UtilityLibrary/GameMath/Orbital/OrbitalMath.cs`

```csharp
// Add to existing OrbitalMath class
public const float UNITS_PER_AU = 1000f;

public static float ConvertUnitsToAU(float units) => units / UNITS_PER_AU;
public static float ConvertAUToUnits(float au) => au * UNITS_PER_AU;

public static float CalculateDistanceFromParentAU(Vector3 parentPos, Vector3 childPos) {
    return ConvertUnitsToAU((parentPos - childPos).Length());
}
```

### 1.2 Complete Subtype Enums Definition
**File**: `Scripts/Structures/Enums/CelestialSubtypes.cs`

```csharp
namespace Structures.Enums;

// Star subtypes - equal probability as specified
public enum StarSubtype {
    MainSequence,    // G-type like our Sun
    RedGiant,
    WhiteDwarf,
    RedDwarf,        // M-type, most common
    BlueSupergiant,  // O/B type
    YellowDwarf,     // F/G type
    BinaryPrimary,   // Part of binary system
    VariableStar
}

// Rocky planet subtypes - AU-based probability
public enum RockyPlanetSubtype {
    Scoured,    0.5 AU
    Desert,      // 0.5-0.8 AU
    Temperate,   // 0.8-1.2 AU (Earth-like)
    Tropical,    // 0.8-1.2 AU variant
    Ocean,       // 0.8-1.2 AU variant
    Cool,        // 1.2-2.0 AU
    Ice,         // >2.0 AU
    Rusted,      // Special conditions
    Volcanic     // High tectonic activity
}

// Gas giant subtypes - AU-based probability
public enum GasGiantSubtype {
    HotJupiter,      // Close to star
    StandardJupiter, // Jupiter-like
    ColdJupiter,     // Far from star
    FailedStar,      // Brown dwarf
    RingedGiant,     // Prominent ring system
    StormyGiant,     // Dominant storm features
    PuffyGiant       // Low density
}

// Ice giant subtypes - AU-based probability  
public enum IceGiantSubtype {
    StandardNeptune, // Neptune-like
    UranusType,      // Uranus-like (different axial tilt)
    MethaneGiant,    // Rich in methane
    AmmoniaGiant,    // Rich in ammonia
    ToxicGiant,      // High toxicity compounds
    SilverGiant,     // Reflective atmosphere
    Cryovolcanic     // Active ice volcanism
}

// Dwarf planet subtypes - AU-based probability
public enum DwarfPlanetSubtype {
    IcyKuiper,       // Kuiper belt object
    RockyBelt,       // Asteroid belt object
    MixedComposition,
    Plutoid,         // Pluto-like
    CeresType,       // Asteroid belt dwarf
    ScatteredDisk,   // Highly eccentric
    DetachedObject   // Very distant
}

// Black hole subtypes - equal probability
public enum BlackHoleSubtype {
    StellarMass,     // 3-20 solar masses
    Intermediate,    // 100-1000 solar masses
    Supermassive,    // >1 million solar masses
    Primordial,      // Formed in early universe
    Rotating,        // Kerr black hole
    Charged          // Reissner–Nordström
}

// Neutron star subtypes - equal probability
public enum NeutronStarSubtype {
    Pulsar,          // Emits radio pulses
    Magnetar,        // Extremely magnetic
    Isolated,        // No companion
    Binary,          // In binary system
    Millisecond,     // Rapid rotation
    XRayPulsar       // X-ray emissions
}

// Satellite subtypes - parent-body AU based
public enum SatelliteSubtype {
    // Moon subtypes
    RockyMoon,
    IcyMoon,
    VolcanicMoon,
    TidallyLocked,
    CapturedAsteroid,
    
    // Asteroid subtypes
    Carbonaceous,    // C-type
    Silicate,        // S-type
    Metallic,        // M-type
    IceAsteroid,     // Comet-like
    
    // Comet subtypes
    ShortPeriod,
    LongPeriod,
    HalleyType,
    EnckeType
}

// Belt subtypes - AU-based placement
public enum BeltSubtype {
    AsteroidBelt,    // Rocky/metallic
    IceBelt,         // Kuiper belt-like
    DebrisDisk,      // Protoplanetary remnant
    DustRing,        // Fine particles
    ResonantBelt,    // Orbital resonances
    ShepherdBelt     // Shepherd moons present
}
```

### 1.3 CelestialBody Subtype Property Extension
**File**: `Scripts/ProceduralGeneration/CelestialBody.cs`

```csharp
// Add to CelestialBody class
private object? _subtype;

public object? Subtype {
    get => _subtype;
    set {
        _subtype = value;
        // Could trigger biome/color system updates
    }
}

// Helper methods for type-safe access
public T? GetSubtype<T>() where T : Enum => _subtype as T?;
public bool HasSubtype<T>() where T : Enum => _subtype is T;

// Update the Builder class to include subtype
public class Builder {
    // Existing fields...
    internal object? _subtype;
    
    // Add subtype setter method
    public Builder WithSubtype(object subtype) {
        _subtype = subtype;
        return this;
    }
    
    // Update Build() method to pass subtype
    private CelestialBody Build() {
        var body = new CelestialBody(this);
        if (_subtype != null) {
            body.Subtype = _subtype;
        }
        return body;
    }
}
```

## Phase 2: Configuration System

### 2.1 Directory Structure
```
Configuration/
├── AUProbability/
│   ├── Star_AU.yaml
│   ├── RockyPlanet_AU.yaml
│   ├── GasGiant_AU.yaml
│   ├── IceGiant_AU.yaml
│   ├── DwarfPlanet_AU.yaml
│   ├── BlackHole_AU.yaml
│   ├── NeutronStar_AU.yaml
│   ├── Satellite_AU.yaml          # For moons/asteroids/comets
│   └── Belt_AU.yaml               # For asteroid/ice belts
│
├── SystemGen/                     # Existing - body templates
│   ├── Star.yaml
│   ├── RockyPlanet.yaml
│   ├── GasGiant.yaml
│   ├── IceGiant.yaml
│   ├── DwarfPlanet.yaml
│   ├── BlackHole.yaml
│   └── NeutronStar.yaml
│
└── planetary_types/              # Existing - subtype definitions
    ├── star.yml
    ├── rocky_planets.yaml
    ├── gas_giants.yml
    ├── ice_giants.yml
    ├── dwarf_planets.yml
    ├── black_holes.yml
    ├── neutron_stars.yml
    ├── satellites.yml
    └── belts.yml
```

### 2.2 AU Probability File Format Examples

**File**: `Configuration/AUProbability/RockyPlanet_AU.yaml`
```yaml
schema_version: 1.0
body_type: RockyPlanet
description: "AU-based probability distribution for RockyPlanet subtypes"
max_considered_au: 100.0
range_overlap_policy: "use_first"

au_ranges:
  - range:
      min_au: 0.0
      max_au: 0.5
      exclusive_max: false
    name: "inner_scorched"
    subtype_distribution:
      - subtype: Scoured
        weight: 0.8
        required_biomes: ["StoneDesert", "SandDesert", "Mountain"]
      - subtype: Volcanic
        weight: 0.2
        required_biomes: ["Mountain", "Desert"]
  
  - range:
      min_au: 0.5
      max_au: 0.8
      exclusive_max: false
    name: "hot_rocky"
    subtype_distribution:
      - subtype: Desert
        weight: 0.6
        required_biomes: ["SandDesert", "StoneDesert"]
      - subtype: Scoured
        weight: 0.3
        required_biomes: ["StoneDesert", "SandDesert", "Mountain"]
      - subtype: Rusted
        weight: 0.1
        required_biomes: ["RustedPlain", "RustedDesert", "RustedMountain"]
  
  - range:
      min_au: 0.8
      max_au: 1.2
      exclusive_max: false
    name: "temperate_zone"
    subtype_distribution:
      - subtype: Temperate
        weight: 0.6
        required_biomes: ["Forest", "Grassland", "Coastal", "Ocean"]
      - subtype: Ocean
        weight: 0.2
        required_biomes: ["Ocean", "Coastal"]
      - subtype: Tropical
        weight: 0.2
        required_biomes: ["Rainforest", "Forest", "Coastal"]
  
  - range:
      min_au: 1.2
      max_au: 2.5
      exclusive_max: false
    name: "cool_rocky"
    subtype_distribution:
      - subtype: Ice
        weight: 0.5
        required_biomes: ["Icecap", "Glacier", "FrozenPlain"]
      - subtype: Temperate
        weight: 0.3
        required_biomes: ["Forest", "Grassland", "Taiga"]
      - subtype: Rusted
        weight: 0.2
        required_biomes: ["RustedPlain", "RustedDesert"]
  
  - range:
      min_au: 2.5
      max_au: 100.0
      exclusive_max: false
    name: "outer_frozen"
    subtype_distribution:
      - subtype: Ice
        weight: 0.9
        required_biomes: ["Icecap", "Glacier", "FrozenPlain"]
      - subtype: Rusted
        weight: 0.1
        required_biomes: ["RustedPlain", "RustedDesert"]

default_subtype: Temperate
```

**File**: `Configuration/AUProbability/Star_AU.yaml`
```yaml
schema_version: 1.0
body_type: Star
description: "Equal probability distribution for Star subtypes"
max_considered_au: 100.0
range_overlap_policy: "use_first"

au_ranges:
  - range:
      min_au: 0.0
      max_au: 100.0
      exclusive_max: false
    name: "all_distances"
    subtype_distribution:
      - subtype: MainSequence
        weight: 0.125
      - subtype: RedGiant
        weight: 0.125
      - subtype: WhiteDwarf
        weight: 0.125
      - subtype: RedDwarf
        weight: 0.125
      - subtype: BlueSupergiant
        weight: 0.125
      - subtype: YellowDwarf
        weight: 0.125
      - subtype: BinaryPrimary
        weight: 0.125
      - subtype: VariableStar
        weight: 0.125

default_subtype: MainSequence
```

**File**: `Configuration/AUProbability/Satellite_AU.yaml`
```yaml
schema_version: 1.0
body_type: Satellite
description: "AU-based probability for satellite subtypes relative to parent body"

parent_body_influence:
  RockyPlanet:
    au_ranges:
      - range: { min_au: 0.001, max_au: 0.01 }
        subtype_distribution:
          - subtype: RockyMoon
            weight: 0.9
          - subtype: CapturedAsteroid
            weight: 0.1
      - range: { min_au: 0.01, max_au: 0.1 }
        subtype_distribution:
          - subtype: RockyMoon
            weight: 0.6
          - subtype: IcyMoon
            weight: 0.4
      - range: { min_au: 0.1, max_au: 0.5 }
        subtype_distribution:
          - subtype: IcyMoon
            weight: 0.7
          - subtype: TidallyLocked
            weight: 0.3
  
  GasGiant:
    au_ranges:
      - range: { min_au: 0.01, max_au: 0.05 }
        subtype_distribution:
          - subtype: VolcanicMoon
            weight: 0.7
          - subtype: RockyMoon
            weight: 0.3
      - range: { min_au: 0.05, max_au: 0.2 }
        subtype_distribution:
          - subtype: IcyMoon
            weight: 0.8
          - subtype: RockyMoon
            weight: 0.2
  
  IceGiant:
    au_ranges:
      - range: { min_au: 0.01, max_au: 0.1 }
        subtype_distribution:
          - subtype: IcyMoon
            weight: 0.9
          - subtype: CapturedAsteroid
            weight: 0.1

default:
  au_ranges:
    - range: { min_au: 0.0, max_au: 100.0 }
      subtype_distribution:
        - subtype: RockyMoon
          weight: 1.0
```

### 2.3 YAML Validator Extension
**File**: `Scripts/UtilityLibrary/DataLoading/YamlValidator.cs`

```csharp
// Extend existing YamlValidator class
public static class YamlValidator {
    // Existing validators...
    
    public static bool ValidateAUProbability(string filePath, string yamlContent, out List<string> errors) {
        errors = new List<string>();
        
        try {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(yamlContent));
            
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            
            // Validate required fields
            ValidateField(root, "schema_version", errors);
            ValidateField(root, "body_type", errors);
            ValidateField(root, "au_ranges", errors);
            
            // Validate body_type is valid CelestialBodyType
            string bodyType = root["body_type"].ToString();
            if (!Enum.IsDefined(typeof(CelestialBodyType), bodyType)) {
                errors.Add($"Invalid body_type: {bodyType}");
            }
            
            // Validate au_ranges structure
            var auRanges = root["au_ranges"] as YamlSequenceNode;
            if (auRanges != null) {
                ValidateAURanges(auRanges, errors);
            }
            
            // Validate total probability coverage
            ValidateRangeCoverage(auRanges, errors);
            
            return errors.Count == 0;
        }
        catch (Exception ex) {
            errors.Add($"Validation error: {ex.Message}");
            return false;
        }
    }
    
    private static void ValidateAURanges(YamlSequenceNode ranges, List<string> errors) {
        float lastMax = 0;
        
        for (int i = 0; i < ranges.Count; i++) {
            var range = ranges[i] as YamlMappingNode;
            if (range == null) {
                errors.Add($"Range {i} is not a mapping node");
                continue;
            }
            
            // Validate range bounds
            float minAu = GetFloatValue(range["range"]?["min_au"], errors, $"range[{i}].min_au");
            float maxAu = GetFloatValue(range["range"]?["max_au"], errors, $"range[{i}].max_au");
            
            if (minAu >= maxAu) {
                errors.Add($"Range {i}: min_au ({minAu}) must be less than max_au ({maxAu})");
            }
            
            if (i > 0 && minAu < lastMax) {
                errors.Add($"Range {i}: min_au ({minAu}) overlaps with previous range max ({lastMax})");
            }
            
            lastMax = maxAu;
            
            // Validate subtype distribution weights sum to ~1.0
            var distribution = range["subtype_distribution"] as YamlSequenceNode;
            if (distribution != null) {
                float totalWeight = 0;
                foreach (var item in distribution) {
                    var weightNode = (item as YamlMappingNode)?["weight"];
                    if (weightNode != null) {
                        totalWeight += GetFloatValue(weightNode, errors, "weight");
                    }
                }
                
                if (Math.Abs(totalWeight - 1.0f) > 0.01f) {
                    errors.Add($"Range {i}: subtype weights sum to {totalWeight}, should be ~1.0");
                }
            }
        }
    }
    
    private static void ValidateRangeCoverage(YamlSequenceNode ranges, List<string> errors) {
        if (ranges == null || ranges.Count == 0) {
            errors.Add("No AU ranges defined");
            return;
        }
        
        float firstMin = GetFloatValue(ranges[0]?["range"]?["min_au"], errors, "first_range.min_au");
        if (firstMin > 0) {
            errors.Add($"First range starts at {firstMin} AU, gap from 0 to {firstMin}");
        }
    }
    
    // Helper methods
    private static void ValidateField(YamlMappingNode node, string fieldName, List<string> errors) {
        if (!node.Children.ContainsKey(fieldName)) {
            errors.Add($"Missing required field: {fieldName}");
        }
    }
    
    private static float GetFloatValue(YamlNode? node, List<string> errors, string context) {
        if (node == null) {
            errors.Add($"Missing value for: {context}");
            return 0f;
        }
        
        try {
            return Convert.ToSingle(node.ToString());
        }
        catch {
            errors.Add($"Invalid float value for {context}: {node}");
            return 0f;
        }
    }
}
```

### 2.4 Configuration Loader System
**File**: `Scripts/UtilityLibrary/DataLoading/AUProbabilityLoader.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YamlDotNet.Serialization;
using Structures.Enums;

namespace UtilityLibrary.DataLoading;

public static class AUProbabilityLoader {
    private static readonly Dictionary<CelestialBodyType, AUProbabilityConfig> _configCache = new();
    
    public static AUProbabilityConfig LoadForType(CelestialBodyType bodyType) {
        // Check cache first
        if (_configCache.TryGetValue(bodyType, out var cachedConfig)) {
            return cachedConfig;
        }
        
        string fileName = $"{bodyType}_AU.yaml";
        string fullPath = $"Configuration/AUProbability/{fileName}";
        
        try {
            // Load and validate
            var raw = TemplateLoader.Load(fullPath, TemplateLoader.AUProbabilityValidator);
            var config = ParseConfig(raw, bodyType);
            
            // Cache for future use
            _configCache[bodyType] = config;
            
            return config;
        }
        catch (FileNotFoundException) {
            #if DEBUG
            GD.PrintErr($"AU probability config not found for {bodyType}, using default");
            #endif
            
            // Create default config for this type
            var defaultConfig = CreateDefaultConfig(bodyType);
            _configCache[bodyType] = defaultConfig;
            
            return defaultConfig;
        }
        catch (Exception ex) {
            #if DEBUG
            GD.PrintErr($"Error loading AU probability config for {bodyType}: {ex.Message}");
            #endif
            
            // Fallback to default
            var defaultConfig = CreateDefaultConfig(bodyType);
            _configCache[bodyType] = defaultConfig;
            
            return defaultConfig;
        }
    }
    
    public static SatelliteAUProbabilityConfig LoadSatelliteConfig() {
        return (SatelliteAUProbabilityConfig)LoadForType(CelestialBodyType.Satellite);
    }
    
    public static BeltAUProbabilityConfig LoadBeltConfig() {
        return (BeltAUProbabilityConfig)LoadForType(CelestialBodyType.Belt);
    }
    
    private static AUProbabilityConfig ParseConfig(Godot.Collections.Dictionary raw, CelestialBodyType bodyType) {
        var config = new AUProbabilityConfig {
            BodyType = bodyType,
            SchemaVersion = raw.GetValueOrDefault("schema_version", "1.0").ToString(),
            MaxConsideredAU = Convert.ToSingle(raw.GetValueOrDefault("max_considered_au", 100f)),
            RangeOverlapPolicy = raw.GetValueOrDefault("range_overlap_policy", "use_first").ToString()
        };
        
        // Parse default subtype
        if (raw.TryGetValue("default_subtype", out var defaultSubtype)) {
            config.DefaultSubtype = ParseSubtypeFromString(bodyType, defaultSubtype.ToString());
        }
        
        // Parse AU ranges
        var auRanges = raw["au_ranges"] as Godot.Collections.Array;
        if (auRanges != null) {
            foreach (var rangeVariant in auRanges) {
                var rangeDict = rangeVariant as Godot.Collections.Dictionary;
                if (rangeDict != null) {
                    config.AURanges.Add(ParseRange(rangeDict, bodyType));
                }
            }
        }
        
        // For satellite config, parse parent body influence
        if (bodyType == CelestialBodyType.Satellite) {
            var satelliteConfig = new SatelliteAUProbabilityConfig {
                BodyType = bodyType,
                SchemaVersion = config.SchemaVersion,
                MaxConsideredAU = config.MaxConsideredAU,
                RangeOverlapPolicy = config.RangeOverlapPolicy,
                DefaultSubtype = config.DefaultSubtype,
                AURanges = config.AURanges
            };
            
            if (raw.TryGetValue("parent_body_influence", out var influenceVariant)) {
                var influenceDict = influenceVariant as Godot.Collections.Dictionary;
                if (influenceDict != null) {
                    foreach (var kvp in influenceDict) {
                        var parentType = (CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), kvp.Key.ToString());
                        var parentConfig = ParseConfig(kvp.Value as Godot.Collections.Dictionary, bodyType);
                        satelliteConfig.ParentBodyInfluence[parentType] = parentConfig;
                    }
                }
            }
            
            return satelliteConfig;
        }
        
        return config;
    }
    
    private static AUProbabilityRange ParseRange(Godot.Collections.Dictionary rangeDict, CelestialBodyType bodyType) {
        var range = new AUProbabilityRange();
        
        // Parse range bounds
        var rangeBounds = rangeDict["range"] as Godot.Collections.Dictionary;
        if (rangeBounds != null) {
            range.MinAU = Convert.ToSingle(rangeBounds.GetValueOrDefault("min_au", 0f));
            range.MaxAU = Convert.ToSingle(rangeBounds.GetValueOrDefault("max_au", 0f));
            range.ExclusiveMax = Convert.ToBoolean(rangeBounds.GetValueOrDefault("exclusive_max", false));
        }
        
        range.Name = rangeDict.GetValueOrDefault("name", "").ToString();
        
        // Parse subtype distribution
        var distribution = rangeDict["subtype_distribution"] as Godot.Collections.Array;
        if (distribution != null) {
            foreach (var distVariant in distribution) {
                var distDict = distVariant as Godot.Collections.Dictionary;
                if (distDict != null) {
                    var subtypeProb = new SubtypeProbability {
                        Subtype = ParseSubtypeFromString(bodyType, distDict.GetValueOrDefault("subtype", "").ToString()),
                        Weight = Convert.ToSingle(distDict.GetValueOrDefault("weight", 1.0f))
                    };
                    
                    // Parse required biomes
                    var requiredBiomes = distDict.GetValueOrDefault("required_biomes", new Godot.Collections.Array()) as Godot.Collections.Array;
                    if (requiredBiomes != null) {
                        foreach (var biomeVariant in requiredBiomes) {
                            subtypeProb.RequiredBiomes.Add(biomeVariant.ToString());
                        }
                    }
                    
                    range.SubtypeDistribution.Add(subtypeProb);
                }
            }
        }
        
        return range;
    }
    
    private static object ParseSubtypeFromString(CelestialBodyType bodyType, string subtypeString) {
        return bodyType switch {
            CelestialBodyType.Star => Enum.Parse(typeof(StarSubtype), subtypeString),
            CelestialBodyType.RockyPlanet => Enum.Parse(typeof(RockyPlanetSubtype), subtypeString),
            CelestialBodyType.GasGiant => Enum.Parse(typeof(GasGiantSubtype), subtypeString),
            CelestialBodyType.IceGiant => Enum.Parse(typeof(IceGiantSubtype), subtypeString),
            CelestialBodyType.DwarfPlanet => Enum.Parse(typeof(DwarfPlanetSubtype), subtypeString),
            CelestialBodyType.BlackHole => Enum.Parse(typeof(BlackHoleSubtype), subtypeString),
            CelestialBodyType.NeutronStar => Enum.Parse(typeof(NeutronStarSubtype), subtypeString),
            CelestialBodyType.Satellite => Enum.Parse(typeof(SatelliteSubtype), subtypeString),
            CelestialBodyType.Belt => Enum.Parse(typeof(BeltSubtype), subtypeString),
            _ => throw new ArgumentException($"Unsupported body type: {bodyType}")
        };
    }
    
    private static AUProbabilityConfig CreateDefaultConfig(CelestialBodyType bodyType) {
        return new AUProbabilityConfig {
            BodyType = bodyType,
            SchemaVersion = "1.0",
            MaxConsideredAU = 100f,
            RangeOverlapPolicy = "use_first",
            AURanges = new List<AUProbabilityRange> {
                new AUProbabilityRange {
                    MinAU = 0f,
                    MaxAU = 100f,
                    ExclusiveMax = false,
                    Name = "default_range",
                    SubtypeDistribution = new List<SubtypeProbability> {
                        new SubtypeProbability {
                            Subtype = GetDefaultSubtype(bodyType),
                            Weight = 1.0f
                        }
                    }
                }
            },
            DefaultSubtype = GetDefaultSubtype(bodyType)
        };
    }
    
    private static object GetDefaultSubtype(CelestialBodyType bodyType) {
        return bodyType switch {
            CelestialBodyType.RockyPlanet => RockyPlanetSubtype.Temperate,
            CelestialBodyType.GasGiant => GasGiantSubtype.StandardJupiter,
            CelestialBodyType.IceGiant => IceGiantSubtype.StandardNeptune,
            CelestialBodyType.DwarfPlanet => DwarfPlanetSubtype.IcyKuiper,
            CelestialBodyType.Star => StarSubtype.MainSequence,
            CelestialBodyType.BlackHole => BlackHoleSubtype.StellarMass,
            CelestialBodyType.NeutronStar => NeutronStarSubtype.Pulsar,
            CelestialBodyType.Satellite => SatelliteSubtype.RockyMoon,
            CelestialBodyType.Belt => BeltSubtype.AsteroidBelt,
            _ => null
        };
    }
}
```

## Phase 3: Probability Selection System

### 3.1 Data Classes
**File**: `Scripts/ProceduralGeneration/Data/AUProbabilityData.cs`

```csharp
using System.Collections.Generic;
using Structures.Enums;

namespace ProceduralGeneration.Data;

public class AUProbabilityConfig {
    public string SchemaVersion { get; set; } = "1.0";
    public CelestialBodyType BodyType { get; set; }
    public List<AUProbabilityRange> AURanges { get; set; } = new();
    public object? DefaultSubtype { get; set; }
    public float MaxConsideredAU { get; set; } = 100f;
    public string RangeOverlapPolicy { get; set; } = "use_first";
}

public class AUProbabilityRange {
    public float MinAU { get; set; }
    public float MaxAU { get; set; }
    public bool ExclusiveMax { get; set; }
    public string Name { get; set; } = "";
    public List<SubtypeProbability> SubtypeDistribution { get; set; } = new();
}

public class SubtypeProbability {
    public object Subtype { get; set; }  // Will be parsed based on body type
    public float Weight { get; set; }
    public List<string> RequiredBiomes { get; set; } = new();
}

public class SatelliteAUProbabilityConfig : AUProbabilityConfig {
    public Dictionary<CelestialBodyType, AUProbabilityConfig> ParentBodyInfluence { get; set; } = new();
    public AUProbabilityConfig DefaultConfig { get; set; } = new();
}

public class BeltAUProbabilityConfig : AUProbabilityConfig {
    // Belt-specific configuration
}
```

### 3.2 AU Probability Manager
**File**: `Scripts/ProceduralGeneration/AUProbabilityManager.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Enums;
using UtilityLibrary.DataLoading;
using ProceduralGeneration.Data;

namespace ProceduralGeneration;

public class AUProbabilityManager {
    private readonly RandomNumberGenerator _rng;
    
    public AUProbabilityManager(RandomNumberGenerator rng) {
        _rng = rng;
    }
    
    // Main entry point for subtype selection
    public object? SelectSubtype(CelestialBodyType bodyType, float distanceAU, object? manualOverride = null) {
        // Manual override takes absolute precedence
        if (manualOverride != null) {
            return manualOverride;
        }
        
        // Check if this body type has AU probability configuration
        if (!HasAUProbabilityConfig(bodyType)) {
            return SelectDefaultSubtype(bodyType);
        }
        
        var config = AUProbabilityLoader.LoadForType(bodyType);
        return SelectSubtypeFromConfig(config, distanceAU);
    }
    
    // For satellites: parent-relative distance
    public object? SelectSatelliteSubtype(SatelliteBodyType satType, CelestialBodyType parentType, 
                                         float distanceFromParentAU, object? manualOverride = null) {
        if (manualOverride != null) return manualOverride;
        
        // Load satellite-specific config
        var config = AUProbabilityLoader.LoadSatelliteConfig();
        
        // Check if parent type has specific rules
        if (config.ParentBodyInfluence.TryGetValue(parentType, out var parentConfig)) {
            return SelectSubtypeFromConfig(parentConfig, distanceFromParentAU);
        }
        
        // Fallback to default satellite rules
        return SelectSubtypeFromConfig(config.DefaultConfig, distanceFromParentAU);
    }
    
    // For belts: distance from dominant body
    public object? SelectBeltSubtype(SatelliteGroupTypes beltType, float distanceFromStarAU, 
                                    object? manualOverride = null) {
        if (manualOverride != null) return manualOverride;
        
        var config = AUProbabilityLoader.LoadBeltConfig();
        return SelectSubtypeFromConfig(config, distanceFromStarAU);
    }
    
    private object SelectSubtypeFromConfig(AUProbabilityConfig config, float distanceAU) {
        // Find matching AU range
        var range = FindMatchingRange(config, distanceAU);
        
        if (range == null) {
            // Use default from config or fallback
            return config.DefaultSubtype ?? SelectDefaultSubtype(config.BodyType);
        }
        
        // Weighted random selection
        return WeightedRandomSelection(range.SubtypeDistribution);
    }
    
    private AUProbabilityRange? FindMatchingRange(AUProbabilityConfig config, float distanceAU) {
        foreach (var range in config.AURanges) {
            if (distanceAU >= range.MinAU && 
                (distanceAU < range.MaxAU || (distanceAU == range.MaxAU && !range.ExclusiveMax))) {
                return range;
            }
        }
        return null;
    }
    
    private object WeightedRandomSelection(List<SubtypeProbability> probabilities) {
        float total = probabilities.Sum(p => p.Weight);
        float random = _rng.Randf() * total;
        
        float cumulative = 0;
        foreach (var prob in probabilities) {
            cumulative += prob.Weight;
            if (random <= cumulative) {
                return prob.Subtype;
            }
        }
        
        // Fallback
        return probabilities.Last().Subtype;
    }
    
    private object SelectDefaultSubtype(CelestialBodyType bodyType) {
        return bodyType switch {
            CelestialBodyType.RockyPlanet => RockyPlanetSubtype.Temperate,
            CelestialBodyType.GasGiant => GasGiantSubtype.StandardJupiter,
            CelestialBodyType.IceGiant => IceGiantSubtype.StandardNeptune,
            CelestialBodyType.DwarfPlanet => DwarfPlanetSubtype.IcyKuiper,
            CelestialBodyType.Star => StarSubtype.MainSequence,
            CelestialBodyType.BlackHole => BlackHoleSubtype.StellarMass,
            CelestialBodyType.NeutronStar => NeutronStarSubtype.Pulsar,
            CelestialBodyType.Satellite => SatelliteSubtype.RockyMoon,
            CelestialBodyType.Belt => BeltSubtype.AsteroidBelt,
            _ => null
        };
    }
    
    private bool HasAUProbabilityConfig(CelestialBodyType bodyType) {
        try {
            var config = AUProbabilityLoader.LoadForType(bodyType);
            return config != null && config.AURanges.Count > 0;
        }
        catch {
            return false;
        }
    }
}
```

## Phase 4: SystemGenerator Integration

### 4.1 Distance Calculation Utilities
**File**: `Scripts/ProceduralGeneration/OrbitalDistanceCalculator.cs`

```csharp
using Godot;
using UtilityLibrary.GameMath.Orbital;

namespace ProceduralGeneration;

public static class OrbitalDistanceCalculator {
    public static float CalculateDistanceFromStarAU(Godot.Collections.Dictionary body, Barycenter barycenter) {
        // Get orbital parameters
        float apogee = GetApogeeFromBody(body);
        float perigee = GetPerigeeFromBody(body);
        
        // Calculate semi-major axis
        float semiMajorAxis = (apogee + perigee) / 2f;
        
        // Convert to AU
        return OrbitalMath.ConvertUnitsToAU(semiMajorAxis);
    }
    
    public static float CalculateDistanceFromParentAU(CelestialBody parent, CelestialBody child) {
        return OrbitalMath.CalculateDistanceFromParentAU(parent.GlobalPosition, child.GlobalPosition);
    }
    
    public static float CalculateBeltDistanceAU(Godot.Collections.Dictionary belt, CelestialBody parent) {
        // For belts, calculate average distance from parent
        float ringApogee = belt.ContainsKey("ring_apogee") ? (float)belt["ring_apogee"] : 1000f;
        float ringPerigee = belt.ContainsKey("ring_perigee") ? (float)belt["ring_perigee"] : 500f;
        
        float avgDistance = (ringApogee + ringPerigee) / 2f;
        return OrbitalMath.ConvertUnitsToAU(avgDistance);
    }
    
    private static float GetApogeeFromBody(Godot.Collections.Dictionary body) {
        if (body.ContainsKey("orbital_parameters")) {
            var orbitalParams = (Godot.Collections.Dictionary)body["orbital_parameters"];
            if (orbitalParams.ContainsKey("apogee")) {
                return (float)orbitalParams["apogee"];
            }
        }
        
        // Fallback to template or default
        if (body.ContainsKey("template")) {
            var template = (Godot.Collections.Dictionary)body["template"];
            if (template.ContainsKey("apogee")) {
                return (float)template["apogee"];
            }
        }
        
        return 1000f; // Default
    }
    
    private static float GetPerigeeFromBody(Godot.Collections.Dictionary body) {
        if (body.ContainsKey("orbital_parameters")) {
            var orbitalParams = (Godot.Collections.Dictionary)body["orbital_parameters"];
            if (orbitalParams.ContainsKey("perigee")) {
                return (float)orbitalParams["perigee"];
            }
        }
        
        if (body.ContainsKey("template")) {
            var template = (Godot.Collections.Dictionary)body["template"];
            if (template.ContainsKey("perigee")) {
                return (float)template["perigee"];
            }
        }
        
        return 500f; // Default
    }
}
```

### 4.2 SystemGenerator Modifications
**File**: `Scripts/ProceduralGeneration/SystemGenerator.cs`

```csharp
// Add these methods to the existing SystemGenerator class

private void CreateAndQueuePlanetaryBody(Godot.Collections.Dictionary body, Barycenter barycenter) {
    // ... existing orbital calculations ...
    
    // Get body type from template
    string typeString = (string)body["type"];
    CelestialBodyType bodyType = (CelestialBodyType)Enum.Parse(typeof(CelestialBodyType), typeString);
    
    // Calculate AU distance from primary star
    float distanceAU = OrbitalDistanceCalculator.CalculateDistanceFromStarAU(body, barycenter);
    
    // Check for manual subtype override in template
    object? manualSubtype = null;
    if (body.TryGetValue("subtype", out var subtypeVariant)) {
        manualSubtype = ParseSubtype(bodyType, (string)subtypeVariant);
    }
    
    // Select subtype using AU probability
    var auProbabilityManager = new AUProbabilityManager(GetRNGForBody(body));
    object? subtype = auProbabilityManager.SelectSubtype(bodyType, distanceAU, manualSubtype);
    
    // Store subtype in body dict for later use
    if (subtype != null) {
        body["subtype"] = subtype.ToString();
    }
    
    // ... continue with existing body creation ...
    var mesh = new UnifiedCelestialMesh();
    CelestialBody celBody = CelestialBody.Builder.BuildFromBodyDict(body, mesh);
    
    // Pass subtype to CelestialBody if available
    if (subtype != null) {
        celBody.Subtype = subtype;
    }
    
    // ... rest of existing code ...
}

private void CreateAndQueueSatelliteBelt(Godot.Collections.Dictionary belt, Barycenter barycenter) {
    // ... existing belt generation ...
    
    // Find parent body
    CelestialBody? parentBody = FindParentBodyForBelt(belt);
    if (parentBody == null) return;
    
    // Calculate distance from parent
    float distanceAU = OrbitalDistanceCalculator.CalculateBeltDistanceAU(belt, parentBody);
    
    // Get belt type
    string beltTypeString = (string)belt["type"];
    SatelliteGroupTypes beltType = (SatelliteGroupTypes)Enum.Parse(typeof(SatelliteGroupTypes), beltTypeString);
    
    // Select belt subtype
    var auProbabilityManager = new AUProbabilityManager(GetRNGForBelt(belt));
    object? beltSubtype = auProbabilityManager.SelectBeltSubtype(beltType, distanceAU);
    
    // Store subtype for later use
    if (beltSubtype != null) {
        belt["subtype"] = beltSubtype.ToString();
    }
    
    // ... continue with belt generation ...
}

private void GenerateSatelliteBelt(Godot.Collections.Dictionary belt, CelestialBody parentBody) {
    // ... existing satellite generation ...
    
    // Calculate distance from parent body
    float distanceAU = OrbitalDistanceCalculator.CalculateDistanceFromParentAU(parentBody, satellitePosition);
    
    // Get satellite type
    string satTypeString = (string)belt["type"]; // Or from individual satellite
    SatelliteBodyType satType = (SatelliteBodyType)Enum.Parse(typeof(SatelliteBodyType), satTypeString);
    
    // Select satellite subtype
    var auProbabilityManager = new AUProbabilityManager(GetRNGForSatellite(belt));
    object? subtype = auProbabilityManager.SelectSatelliteSubtype(satType, parentBody.Type, distanceAU);
    
    // Store and use subtype
    if (subtype != null) {
        belt["subtype"] = subtype.ToString();
        ApplySubtypeToSatellite(belt, subtype);
    }
    
    // ... rest of satellite generation ...
}

private object? ParseSubtype(CelestialBodyType bodyType, string subtypeString) {
    return bodyType switch {
        CelestialBodyType.RockyPlanet => Enum.Parse(typeof(RockyPlanetSubtype), subtypeString),
        CelestialBodyType.GasGiant => Enum.Parse(typeof(GasGiantSubtype), subtypeString),
        CelestialBodyType.IceGiant => Enum.Parse(typeof(IceGiantSubtype), subtypeString),
        CelestialBodyType.DwarfPlanet => Enum.Parse(typeof(DwarfPlanetSubtype), subtypeString),
        CelestialBodyType.Star => Enum.Parse(typeof(StarSubtype), subtypeString),
        CelestialBodyType.BlackHole => Enum.Parse(typeof(BlackHoleSubtype), subtypeString),
        CelestialBodyType.NeutronStar => Enum.Parse(typeof(NeutronStarSubtype), subtypeString),
        CelestialBodyType.Satellite => Enum.Parse(typeof(SatelliteSubtype), subtypeString),
        CelestialBodyType.Belt => Enum.Parse(typeof(BeltSubtype), subtypeString),
        _ => null
    };
}
```

## Phase 5: Biome System Integration

### 5.1 Biome Assigner Interface
**File**: `Scripts/ProceduralGeneration/Biome/IBiomeAssigner.cs`

```csharp
using Structures.Enums;

namespace ProceduralGeneration.Biome;

public interface IBiomeAssigner {
    Biome.BiomeType AssignBiome(UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f);
    float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoome = 0.5f);
}
```

### 5.2 Rocky Planet Subtype Assigners
**File**: `Scripts/ProceduralGeneration/Biome/RockyPlanetBiomeAssigners.cs`

```csharp
using Godot;
using Structures.Enums;

namespace ProceduralGeneration.Biome;

public class TemperatePlanetBiomeAssigner : IBiomeAssigner {
    public Biome.BiomeType AssignBiome(UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f) {
        // Existing implementation from BiomeAssigner.AssignBiome()
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);
        
        Biome.BiomeType result;
        if (normalizedHeight > 0.9f) result = Biome.BiomeType.Icecap;
        else if (normalizedHeight > 0.68f) result = Biome.BiomeType.Mountain;
        else if (normalizedHeight > 0.4f && (latitude > 0.8f || latitude < -0.8f)) result = Biome.BiomeType.Tundra;
        else if (normalizedHeight > 0.4f && (latitude > 0.7f || latitude < -0.7f)) result = Biome.BiomeType.Taiga;
        else if (normalizedHeight0.05f) result = Biome.BiomeType.Ocean;
        else if (normalizedHeight0.07f) result = Biome.BiomeType.Coastal;
        else if (normalizedHeight0.3f && moisture0.2f) result = Biome.BiomeType.Desert;
        else if (normalizedHeight0.3f && moisture0.5f) result = Biome.BiomeType.Grassland;
        else if (normalizedHeight > 0.3f && moisture0.7f) result = Biome.BiomeType.Forest;
        else result = Biome.BiomeType.Rainforest;
        
        return result;
    }
    
    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f) {
        // Existing implementation
        float latitudeFactor = Mathf.Clamp(continent.averagedCenter.Y / 9f, 0f, 1f);
        float sizeFactor = continent.cells.Count / 100f;
        float randomVariation = rng.RandfRange(-0.4f, 0.2f);
        float value = 2.7f - (baseMoisture + latitudeFactor + sizeFactor + randomVariation) / 2.7f;
        return value;
    }
}

public class ScouredPlanetBiomeAssigner : IBiomeAssigner {
    public Biome.BiomeType AssignBiome(UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f) {
        // Simplified biome logic for scorched planets
        float normalizedHeight = height / generator.maxHeight;
        
        if (normalizedHeight > 0.8f) return Biome.BiomeType.Mountain;
        if (normalizedHeight > 0.4f) return Biome.BiomeType.StoneDesert;
        if (normalizedHeight > 0.1f) return Biome.BiomeType.SandDesert;
        return Biome.BiomeType.Desert;
    }
    
    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f) {
        // Scoured planets are very dry
        return rng.RandfRange(0.0f, 0.1f);
    }
}

public class IcePlanetBiomeAssigner : IBiomeAssigner {
    public Biome.BiomeType AssignBiome(UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f) {
        // Ice planet biome logic
        float normalizedHeight = height / generator.maxHeight;
        
        if (normalizedHeight > 0.7f) return Biome.BiomeType.Mountain;
        if (normalizedHeight > 0.4f) return Biome.BiomeType.Icecap;
        if (normalizedHeight > 0.1f) return Biome.BiomeType.Glacier;
        return Biome.BiomeType.FrozenPlain;
    }
    
    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f) {
        // Ice planets have high moisture (but frozen)
        return rng.RandfRange(0.7f, 0.9f);
    }
}
```

### 5.3 Biome Assigner Factory
**File**: `Scripts/ProceduralGeneration/Biome/BiomeAssignerFactory.cs`

```csharp
using System;
using Structures.Enums;

namespace ProceduralGeneration.Biome;

public static class BiomeAssignerFactory {
    public static IBiomeAssigner GetAssigner(CelestialBodyType type, object? subtype = null) {
        return type switch {
            CelestialBodyType.RockyPlanet => GetRockyPlanetAssigner((RockyPlanetSubtype?)subtype),
            CelestialBodyType.GasGiant => GetGasGiantAssigner((GasGiantSubtype?)subtype),
            CelestialBodyType.IceGiant => GetIceGiantAssigner((IceGiantSubtype?)subtype),
            CelestialBodyType.Star => GetStarAssigner((StarSubtype?)subtype),
            CelestialBodyType.DwarfPlanet => GetDwarfPlanetAssigner((DwarfPlanetSubtype?)subtype),
            CelestialBodyType.BlackHole => GetBlackHoleAssigner((BlackHoleSubtype?)subtype),
            CelestialBodyType.NeutronStar => GetNeutronStarAssigner((NeutronStarSubtype?)subtype),
            _ => new DefaultBiomeAssigner()
        };
    }
    
    private static IBiomeAssigner GetRockyPlanetAssigner(RockyPlanetSubtype? subtype) {
        return subtype switch {
            RockyPlanetSubtype.Scoured => new ScouredPlanetBiomeAssigner(),
            RockyPlanetSubtype.Desert => new DesertPlanetBiomeAssigner(),
            RockyPlanetSubtype.Temperate => new TemperatePlanetBiomeAssigner(),
            RockyPlanetSubtype.Ice => new IcePlanetBiomeAssigner(),
            RockyPlanetSubtype.Tropical => new TropicalPlanetBiomeAssigner(),
            RockyPlanetSubtype.Ocean => new OceanPlanetBiomeAssigner(),
            RockyPlanetSubtype.Rusted => new RustedPlanetBiomeAssigner(),
            RockyPlanetSubtype.Volcanic => new VolcanicPlanetBiomeAssigner(),
            _ => new TemperatePlanetBiomeAssigner() // Default
        };
    }
    
    // Similar methods for other body types...
    
    private class DefaultBiomeAssigner : IBiomeAssigner {
        public Biome.BiomeType AssignBiome(UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f) {
            return Biome.BiomeType.Desert; // Simple default
        }
        
        public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f) {
            return 0.5f; // Default moisture
        }
    }
}
```

## Phase 6: UI Integration

### 6.1 SystemGenerator UI Extension
**File**: `UI/SystemGeneratorUI.cs` (Extend existing)

```csharp
// Add to existing SystemGeneratorUI class

public partial class SystemGeneratorUI : Control {
    // Existing fields...
    
    // New fields for body type selection
    private OptionButton _dominantTypeDropdown;
    private OptionButton _planetaryTypeDropdown;
    private OptionButton _beltTypeDropdown;
    
    // New: Subtype override toggle and dropdown
    private CheckBox _overrideSubtypeCheck;
    private OptionButton _subtypeDropdown;
    private Label _subtypeInfoLabel;
    
    public override void _Ready() {
        // ... existing initialization ...
        
        // Initialize new UI elements
        InitializeTypeSelectionUI();
    }
    
    private void InitializeTypeSelectionUI() {
        // Dominant body type dropdown
        _dominantTypeDropdown = GetNode<OptionButton>("DominantTypeDropdown");
        PopulateBodyTypeDropdown(_dominantTypeDropdown, 
            PlanetaryTypeLoader.GetDominantBodyTypes());
        
        // Planetary body type dropdown
        _planetaryTypeDropdown = GetNode<OptionButton>("PlanetaryTypeDropdown");
        PopulateBodyTypeDropdown(_planetaryTypeDropdown,
            PlanetaryTypeLoader.GetPlanetaryBodyTypes());
        
        // Subtype override controls
        _overrideSubtypeCheck = GetNode<CheckBox>("OverrideSubtypeCheck");
        _subtypeDropdown = GetNode<OptionButton>("SubtypeDropdown");
        _subtypeInfoLabel = GetNode<Label>("SubtypeInfoLabel");
        
        // Connect signals
        _overrideSubtypeCheck.Toggled += OnOverrideSubtypeToggled;
        _planetaryTypeDropdown.ItemSelected += OnPlanetaryTypeSelected;
    }
    
    private void OnOverrideSubtypeToggled(bool toggled) {
        _subtypeDropdown.Visible = toggled;
        _subtypeInfoLabel.Visible = toggled;
        
        if (toggled) {
            // Update subtype dropdown based on selected body type
            UpdateSubtypeDropdown();
        }
    }
    
    private void OnPlanetaryTypeSelected(int index) {
        if (_overrideSubtypeCheck.ButtonPressed) {
            UpdateSubtypeDropdown();
        }
    }
    
    private void UpdateSubtypeDropdown() {
        string selectedType = _planetaryTypeDropdown.GetItemText(_planetaryTypeDropdown.Selected);
        CelestialBodyType bodyType = (CelestialBodyType)Enum.Parse(
            typeof(CelestialBodyType), selectedType);
        
        // Clear and repopulate subtype dropdown
        _subtypeDropdown.Clear();
        
        var subtypes = GetSubtypesForBodyType(bodyType);
        foreach (var subtype in subtypes) {
            _subtypeDropdown.AddItem(subtype.DisplayName);
        }
        
        // Update info label with AU probability info
        UpdateSubtypeInfo(bodyType);
    }
    
    private void UpdateSubtypeInfo(CelestialBodyType bodyType) {
        // Load AU probability config and show summary
        try {
            var config = AUProbabilityLoader.LoadForType(bodyType);
            string info = $"AU-based probability distribution active.\n";
            info += $"Covers {config.AURanges.Count} distance ranges.\n";
            info += $"Manual override will disable AU probability.";
            
            _subtypeInfoLabel.Text = info;
        }
        catch {
            _subtypeInfoLabel.Text = "No AU probability configuration found.\nUsing default subtype.";
        }
    }
    
    // When generating system, include type overrides in the request
    private void OnGenerateButtonPressed() {
        var generationRequest = new SystemGenerationRequest {
            // Existing fields...
            
            // New: Type overrides
            OverrideDominantType = _dominantTypeDropdown.GetSelectedType(),
            OverridePlanetaryType = _planetaryTypeDropdown.GetSelectedType(),
            OverrideSubtype = _overrideSubtypeCheck.ButtonPressed ? 
                _subtypeDropdown.GetSelectedSubtype() : null,
            DisableAUProbability = _overrideSubtypeCheck.ButtonPressed
        };
        
        SignalBus.Instance?.EmitGenerateSystemRequested(generationRequest);
    }
}
```

### 6.2 Debug UI Extension
**File**: `Scripts/ProceduralGeneration/CelestialBodyDebug.cs` (Extend existing)

```csharp
// Add to existing CelestialBodyDebug class

public partial class CelestialBodyDebug : Control {
    // Existing fields...
    
    // New fields for type display
    private Label _bodyTypeLabel;
    private Label _subtypeLabel;
    private Label _distanceAULabel;
    private Label _probabilityInfoLabel;
    
    public override void _Ready() {
        // ... existing initialization ...
        
        // Initialize type display
        _bodyTypeLabel = GetNode<Label>("BodyTypeLabel");
        _subtypeLabel = GetNode<Label>("SubtypeLabel");
        _distanceAULabel = GetNode<Label>("DistanceAULabel");
        _probabilityInfoLabel = GetNode<Label>("ProbabilityInfoLabel");
    }
    
    public void UpdateBodyInfo(CelestialBody body) {
        // ... existing debug info ...
        
        // Update type info
        _bodyTypeLabel.Text = $"Type: {body.Type}";
        
        if (body.Subtype != null) {
            _subtypeLabel.Text = $"Subtype: {body.Subtype}";
            _subtypeLabel.Modulate = Colors.Cyan;
        } else {
            _subtypeLabel.Text = "Subtype: (none)";
            _subtypeLabel.Modulate = Colors.Gray;
        }
        
        // Calculate and show AU distance
        float distanceAU = CalculateBodyDistanceAU(body);
        _distanceAULabel.Text = $"Distance: {distanceAU:F2} AU";
        
        // Show probability info if applicable
        UpdateProbabilityInfo(body, distanceAU);
    }
    
    private void UpdateProbabilityInfo(CelestialBody body, float distanceAU) {
        if (!HasAUProbabilityConfig(body.Type)) {
            _probabilityInfoLabel.Text = "No AU probability config";
            _probabilityInfoLabel.Modulate = Colors.Gray;
            return;
        }
        
        try {
            var config = AUProbabilityLoader.LoadForType(body.Type);
            var range = FindMatchingRange(config, distanceAU);
            
            if (range != null) {
                string info = $"AU Range: {range.Name}\n";
                info += $"({range.MinAU:F2}-{range.MaxAU:F2} AU)";
                _probabilityInfoLabel.Text = info;
                _probabilityInfoLabel.Modulate = Colors.LightGreen;
            } else {
                _probabilityInfoLabel.Text = "Outside defined AU ranges";
                _probabilityInfoLabel.Modulate = Colors.Yellow;
            }
        }
        catch {
            _probabilityInfoLabel.Text = "Error loading probability config";
            _probabilityInfoLabel.Modulate = Colors.Red;
        }
    }
}
```

## Phase 7: Resource System Integration

### 7.1 Resource Config Loader with Subtype Support
**File**: `Scripts/UtilityLibrary/DataLoading/ResourceConfigLoader.cs` (Extend existing)

```csharp
// Add to existing ResourceConfigLoader class

public static class ResourceConfigLoader {
    public static Godot.Collections.Dictionary LoadForSubtype(CelestialBodyType bodyType, object? subtype) {
        // Try subtype-specific config first
        if (subtype != null) {
            string subtypeName = subtype.ToString().Replace("Subtype", "");
            string path = $"Configuration/ResourceDefinitions/{bodyType}/{subtypeName}_Resources.yaml";
            
            if (FileExists(path)) {
                return TemplateLoader.Load(path, TemplateLoader.ResourceDefinitionValidator);
            }
        }
        
        // Fallback to body type default
        string defaultPath = $"Configuration/ResourceDefinitions/{bodyType}_Default.yaml";
        if (FileExists(defaultPath)) {
            return TemplateLoader.Load(defaultPath, TemplateLoader.ResourceDefinitionValidator);
        }
        
        // Ultimate fallback
        return CreateFallbackResourceConfig();
    }
    
    private static bool FileExists(string path) {
        return FileAccess.FileExists(path);
    }
    
    private static Godot.Collections.Dictionary CreateFallbackResourceConfig() {
        return new Godot.Collections.Dictionary {
            ["primary_count"] = new Godot.Collections.Array { 1, 3 },
            ["secondary_count"] = new Godot.Collections.Array { 0, 5 },
            ["balance_penalty_factor"] = 0.8f,
            ["balance_threshold"] = 0.15f,
            ["primary"] = new Godot.Collections.Array {
                new Godot.Collections.Dictionary {
                    ["resource_id"] = "iron_ore",
                    ["base_weight"] = 1.0f,
                    ["elevation_weight_curve"] = new Godot.Collections.Array { 0.0f, 0.3f, 0.6f, 0.9f, 1.0f }
                }
            },
            ["secondary"] = new Godot.Collections.Array()
        };
    }
}
```

## Phase 8: Color System Integration

### 8.1 Color Mapper Interface
**File**: `Scripts/ProceduralGeneration/Color/IColorMapper.cs`

```csharp
using Godot;

namespace ProceduralGeneration.Color;

public interface IColorMapper {
    Color GetBiomeColor(Biome.BiomeType biome, float height);
}
```

### 8.2 Subtype-Specific Color Mappers
**File**: `Scripts/ProceduralGeneration/Color/RockyPlanetColorMappers.cs`

```csharp
using Godot;
using Structures.Enums;

namespace ProceduralGeneration.Color;

public class TemperatePlanetColorMapper : IColorMapper {
    public Color GetBiomeColor(Biome.BiomeType biome, float height) {
        // Existing implementation from UnifiedCelestialMesh.GetBiomeColor()
        switch (biome) {
            case Biome.BiomeType.Tundra:
                return new Color(0.85f, 0.85f, 0.8f);
            case Biome.BiomeType.Icecap:
                return Colors.White;
            case Biome.BiomeType.Desert:
                return new Color(0.9f, 0.8f, 0.5f);
            case Biome.BiomeType.Grassland:
                return new Color(0.5f, 0.8f, 0.3f);
            case Biome.BiomeType.Forest:
                return new Color(0.2f, 0.6f, 0.2f);
            case Biome.BiomeType.Rainforest:
                return new Color(0.1f, 0.4f, 0.1f);
            case Biome.BiomeType.Taiga:
                return new Color(0.4f, 0.5f, 0.3f);
            case Biome.BiomeType.Ocean:
                return new Color(0.1f, 0.3f, 0.7f);
            case Biome.BiomeType.Coastal:
                return new Color(0.8f, 0.7f, 0.4f);
            case Biome.BiomeType.Mountain:
                return new Color(0.6f, 0.5f, 0.4f);
            default:
                return Colors.Gray;
        }
    }
}

public class ScouredPlanetColorMapper : IColorMapper {
    public Color GetBiomeColor(Biome.BiomeType biome, float height) {
        switch (biome) {
            case Biome.BiomeType.Mountain:
                return new Color(0.7f, 0.5f, 0.4f); // Rocky brown
            case Biome.BiomeType.StoneDesert:
                return new Color(0.8f, 0.6f, 0.5f); // Light brown
            case Biome.BiomeType.SandDesert:
                return new Color(0.9f, 0.7f, 0.5f); // Sandy orange
            case Biome.BiomeType.Desert:
                return new Color(0.8f, 0.5f, 0.3f); // Reddish desert
            default:
                return new Color(0.7f, 0.4f, 0.2f); // Default scorched color
        }
    }
}

public class IcePlanetColorMapper : IColorMapper {
    public Color GetBiomeColor(Biome.BiomeType biome, float height) {
        switch (biome) {
            case Biome.BiomeType.Mountain:
                return new Color(0.8f, 0.8f, 0.9f); // Icy blue-gray
            case Biome.BiomeType.Icecap:
                return Colors.White;
            case Biome.BiomeType.Glacier:
                return new Color(0.7f, 0.8f, 0.9f); // Light blue
            case Biome.BiomeType.FrozenPlain:
                return new Color(0.9f, 0.95f, 1.0f); // Very light blue
            default:
                return new Color(0.8f, 0.85f, 0.95f); // Default ice color
        }
    }
}
```

### 8.3 Color Mapper Factory
**File**: `Scripts/ProceduralGeneration/Color/ColorMapperFactory.cs`

```csharp
using System;

namespace ProceduralGeneration.Color;

public static class ColorMapperFactory {
    public static IColorMapper GetMapper(CelestialBodyType type, object? subtype = null) {
        return type switch {
            CelestialBodyType.RockyPlanet => GetRockyPlanetMapper(subtype),
            CelestialBodyType.GasGiant => GetGasGiantMapper(subtype),
            CelestialBodyType.IceGiant => GetIceGiantMapper(subtype),
            CelestialBodyType.Star => GetStarMapper(subtype),
            CelestialBodyType.DwarfPlanet => GetDwarfPlanetMapper(subtype),
            CelestialBodyType.BlackHole => GetBlackHoleMapper(subtype),
            CelestialBodyType.NeutronStar => GetNeutronStarMapper(subtype),
            _ => new DefaultColorMapper()
        };
    }
    
    private static IColorMapper GetRockyPlanetMapper(object? subtype) {
        if (subtype is Structures.Enums.RockyPlanetSubtype rockySubtype) {
            return rockySubtype switch {
                Structures.Enums.RockyPlanetSubtype.Scoured => new ScouredPlanetColorMapper(),
                Structures.Enums.RockyPlanetSubtype.Desert => new DesertPlanetColorMapper(),
                Structures.Enums.RockyPlanetSubtype.Temperate => new TemperatePlanetColorMapper(),
                Structures.Enums.RockyPlanetSubtype.Ice => new IcePlanetColorMapper(),
                Structures.Enums.RockyPlanetSubtype.Tropical => new TropicalPlanetColorMapper(),
                Structures.Enums.RockyPlanetSubtype.Ocean => new OceanPlanetColorMapper(),
                Structures.Enums.RockyPlanetSubtype.Rusted => new RustedPlanetColorMapper(),
                Structures.Enums.RockyPlanetSubtype.Volcanic => new VolcanicPlanetColorMapper(),
                _ => new TemperatePlanetColorMapper()
            };
        }
        return new TemperatePlanetColorMapper();
    }
    
    // Similar methods for other body types...
    
    private class DefaultColorMapper : IColorMapper {
        public Godot.Color GetBiomeColor(Structures.Enums.Biome.BiomeType biome, float height) {
            return Godot.Colors.Gray;
        }
    }
}
```

## Implementation Timeline

### Month 1: Core Foundation (Weeks 1-4)
**Week 1**: Constants, enums, data structures
- Add `UNITS_PER_AU` constant to `OrbitalMath`
- Create `CelestialSubtypes.cs` with all subtype enums
- Update `CelestialBody` to store subtype

**Week 2**: AU probability configuration system
- Create `Configuration/AUProbability/` directory structure
- Implement `AUProbabilityLoader.cs`
- Create sample probability files for all body types


**Week 3**: YAML validation system
- Extend `YamlValidator` with `ValidateAUProbability`
- Create comprehensive validation logic
- Test configuration loading and parsing



**Week 4**: Integration with existing systems
- Update `TemplateLoader` to support AU probability validation
- Modify `TemplateHelpers` to load subtype configurations
- Test backward compatibility with existing templates

### Month 2: Selection System (Weeks 5-8)
**Week 5**: `AUProbabilityManager` implementation
- Implement core selection algorithms
- Create weighted random selection logic
- Test probability distributions



**Week 6**: SystemGenerator integration
- Add AU distance calculation to `SystemGenerator`
- Integrate `AUProbabilityManager` into body creation
- Test subtype selection with various AU distances




**Week 7**: Satellite and belt support
- Implement satellite-specific AU probability
- Add belt subtype selection
- Test parent-relative distance calculations



**Week 8**: Error handling and fallback chains
- Implement warning system for missing configs
- Create comprehensive fallback logic
- Test error conditions and recovery

### Month 3: Biome & Resource Integration (Weeks 9-12)
**Week 9**: Subtype-specific biome system
- Create `IBiomeAssigner` interface
- Implement subtype-specific biome assigners
- Update `BiomeAssignerFactory`



**Week 10**: Resource system integration
- Update `ResourceConfigLoader` to support subtypes
- Create subtype-specific resource configurations
- Test resource generation appropriateness



**Week 11**: Color system integration
- Create `IColorMapper` interface
- Implement subtype-specific color mappers
- Update `ColorMapperFactory`



**Week 12**: System-wide integration testing
- Test full subtype selection and application
- Verify biome, resource, and color integration
- Performance and stability testing

### Month 4: UI & Final Polish (Weeks 13-16)
**Week 13**: UI integration
- Extend `SystemGeneratorUI` for type selection
- Add debug UI for body type display
- Test UI interactions and feedback



**Week 14**: Testing and validation
- Comprehensive unit testing
- Integration testing with existing systems
- Validation of AU probability configurations



**Week 15**: Performance optimization
- Cache optimization for probability calculations
- Memory usage optimization
- Load time improvements



**Week 16**: Documentation and deployment
- Create user documentation
- Update developer documentation
- Final testing and deployment

## Configuration Files to Create

### AU Probability Configuration Files
1. `Configuration/AUProbability/Star_AU.yaml`
2. `Configuration/AUProbability/RockyPlanet_AU.yaml`
3. `Configuration/AUProbability/GasGiant_AU.yaml`
4. `Configuration/AUProbability/IceGiant_AU.yaml`
5. `Configuration/AUProbability/DwarfPlanet_AU.yaml`
6. `Configuration/AUProbability/BlackHole_AU.yaml`
7. `Configuration/AUProbability/NeutronStar_AU.yaml`
8. `Configuration/AUProbability/Satellite_AU.yaml`
9. `Configuration/AUProbability/Belt_AU.yaml`

### Subtype Definition Files
1. `Configuration/planetary_types/star.yml`
2. `Configuration/planetary_types/rocky_planets.yaml`
3. `Configuration/planetary_types/gas_giants.yml`
4. `Configuration/planetary_types/ice_giants.yml`
5. `Configuration/planetary_types/dwarf_planets.yml`
6. `Configuration/planetary_types/black_holes.yml`
7. `Configuration/planetary_types/neutron_stars.yml`
8. `Configuration/planetary_types/satellites.yml`
9. `Configuration/planetary_types/belts.yml`

## Key Features

### 1. Hierarchical Type System
- `CelestialBodyType` determines broad category
- Subtype selected within same parent type
- No cross-category probability (RockyPlanet → RockyPlanet only)

### 2. AU-Based Probability
- 1000 units = 1 AU conversion
- Separate probability files per body type
- Distance ranges with weighted subtype distributions

### 3. Satellite Support
- Uses parent body distance calculations
- Different probability curves per parent type
- Belt placement with AU-based subtypes

### 4. Manual Override Priority
- Template-specified subtypes override probability
- UI integration for manual type selection
- Disables AU probability when manually specified

### 5. Comprehensive Validation
- YAML validation for AU probability files
- Range coverage validation
- Subtype distribution weight validation

### 6. Debug Integration
- Expose body types in debug UI
- Show AU distance information
- Display probability configuration status

### 7. Backward Compatibility
- All new fields optional
- Fallback chains at every level
- Existing templates work unchanged

## Success Criteria

1. **All body types** have AU probability configurations
2. **Subtype selection** works within same parent type only
3. **Manual overrides** take precedence over probability
4. **Satellites** use parent-body distance calculations
5. **Backward compatibility** maintained
6. **Performance** within acceptable limits
7. **Error handling** robust with fallbacks

## Risk Mitigation

### Technical Risks
1. **Performance Impact**: Cache configurations, pre-compute probabilities
2. **Memory Usage**: Lazy loading, efficient data structures
3. **Complexity**: Modular design, incremental implementation
4. **Integration**: Comprehensive testing, fallback chains

### Development Risks
1. **Scope Creep**: Phased implementation, clear success criteria
2. **Time Management**: Weekly deliverables, milestone tracking
3. **Quality Assurance**: Unit tests, integration tests, validation

## Dependencies

1. **Existing Systems**:
   - `SystemGenerator` for body creation
   - `CelestialBody` for type storage
   - `OrbitalMath` for distance calculations

2. **New Systems**:
   - `AUProbabilityManager` for subtype selection
   - `BiomeAssignerFactory` for biome assignment
   - `ColorMapperFactory` for color mapping

## Testing Strategy

### Unit Tests
1. AU conversion calculations
2. Subtype selection logic
3. Probability distribution validation

### Integration Tests
1. Full system generation with subtypes
2. Manual override functionality
3. Backward compatibility testing

### Performance Tests
1. Load time with configurations
2. Memory usage with caching
3. Generation speed with subtypes

## Deployment Checklist

- [ ] Core constants and enums implemented
- [ ] AU probability configuration system complete
- [ ] YAML validation system working
- [ ] Subtype selection system integrated
- [ ] Biome and resource integration complete
- [ ] Color system integration complete
- [ ] UI integration working
- [ ] Comprehensive testing completed
- [ ] Performance optimization done
- [ ] Documentation updated
- [ ] Backward compatibility verified

---

*This implementation plan provides a complete, phased approach to adding body type differentiation based on orbital distance probabilities, with unique biomes, resources, and coloration for each celestial body type.*