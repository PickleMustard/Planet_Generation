# Resource Generation System Implementation Plan

## Overview

This document outlines the implementation plan for a data-driven resource generation system that assigns resources to celestial bodies, satellites, and continental Voronoi cells based on configurable weights, biome affinities, and topographical features.

## Goals

1. Create a centralized resource definition system with validation
2. Implement weighted resource generation for satellites (simple) and planets (complex)
3. Apply biome, elevation, and tectonic modifiers to resource weights
4. Implement soft penalty curve for global resource balancing
5. Provide visual indicators for resource-rich cells

---

## Current State

| Component | Status |
|-----------|--------|
| ResourceDefinition.toml | Exists with 20 resources, no loader |
| ResourceDefinition.cs | Stub class (3 properties), unused |
| Body config resource pattern | Asteroid.toml has preliminary main/secondary structure |
| VoronoiCell/Point storage | No resource properties |
| Continent system | Full tectonic/biome system exists |
| TOML parsing utilities | Tommy package, helpers in SystemGenTemplates.cs |

---

## Phase 1: Resource Definition System

### 1.1 Enhance ResourceDefinition.cs

**Location**: `Scripts/Structures/Resources/ResourceDefinition.cs`

**Current**:
```csharp
namespace Structures;

public class ResourceDefinition
{
    public string IdName { get; set; }
    public int ResourceTier { get; set; }
    public string ResourceType { get; set; }
}
```

**Enhanced**:
```csharp
namespace Structures.Resources;

public class ResourceDefinition
{
    public string IdName { get; set; }
    public int ResourceTier { get; set; }
    public string ResourceType { get; set; }
    public Color DisplayColor { get; set; }
    public Dictionary<BiomeType, float> BiomeAffinity { get; set; }
    public float MinElevation { get; set; }
    public float MaxElevation { get; set; }
}
```

### 1.2 Create ResourceDatabase.cs (Autoload)

**Location**: `Scripts/Structures/Resources/ResourceDatabase.cs`

**Responsibilities**:
- Load `ResourceDefinition.toml` on startup
- Provide `GetResource(id_name)` lookup
- Validate all body configs reference valid resources (throws error on startup if invalid)
- Store loaded resources in `Dictionary<string, ResourceDefinition>`
- Provide color lookup for visualization

**Key Methods**:
```csharp
public static ResourceDatabase Instance { get; }
public bool TryGetResource(string id, out ResourceDefinition resource);
public IEnumerable<ResourceDefinition> GetAllResources();
public Color GetResourceColor(string id);
public bool ValidateResourceExists(string id);
public void ValidateAllBodyConfigResources();
```

### 1.3 Create ResourceConfigLoader.cs

**Location**: `Scripts/UtilityLibrary/ResourceConfigLoader.cs`

**Responsibilities**:
- Parse ResourceDefinition.toml using Tommy
- Follow existing patterns from SystemGenTemplates.cs
- Handle biome affinity parsing from TOML

### 1.4 Update ResourceDefinition.toml Format

**Location**: `Configuration/ResourceDefinition/ResourceDefinition.toml`

**Enhanced Format**:
```toml
[[resources]]
id_name = "iron_ore"
resource_tier = 0
resource_type = "ore"
display_color = [0.6, 0.4, 0.3]
biome_affinity = { "Mountain" = 1.5, "RustedMountain" = 2.0, "Tundra" = 0.8 }
elevation_range = [-0.2, 0.9]

[[resources]]
id_name = "water_ice"
resource_tier = 0
resource_type = "fuel"
display_color = [0.5, 0.7, 0.9]
biome_affinity = { "Glacier" = 2.0, "Icecap" = 2.5, "Tundra" = 1.2 }
elevation_range = [-1.0, 0.3]
```

---

## Phase 2: Satellite Resource Generation

### 2.1 Satellite Resource Config Format

**Location**: Body TOML files (e.g., `Configuration/SystemGen/Asteroid.toml`, `Moon.toml`)

```toml
[satellite.resources]
min_total = 1
max_total = 3

[[satellite.resources.primary]]
id = "iron_ore"
weight = 1.0

[[satellite.resources.primary]]
id = "nickel_ore"
weight = 0.7

[[satellite.resources.secondary]]
id = "gold_ore"
weight = 0.3

[[satellite.resources.secondary]]
id = "cobalt_ore"
weight = 0.4
```

### 2.2 Create ResourceDeposit.cs

**Location**: `Scripts/Structures/Resources/ResourceDeposit.cs`

```csharp
namespace Structures.Resources;

public class ResourceDeposit
{
    public string ResourceId { get; set; }
    public float Abundance { get; set; }  // 0-1 normalized
    public float Accessibility { get; set; }  // How easy to extract
}
```

### 2.3 Create SatelliteResourceGenerator.cs

**Location**: `Scripts/ProceduralGeneration/MeshGeneration/ResourceGeneration/SatelliteResourceGenerator.cs`

**Algorithm**:
```
1. Load resource config from body definition
2. Select 1 primary resource (weighted random from primary list)
3. Select 0-2 secondary resources (weighted random, can pick none)
4. Calculate abundance for each selected resource
5. Return Dictionary<string, ResourceDeposit>
```

### 2.4 Update SatelliteBody.cs

**Location**: `Scripts/ProceduralGeneration/SatelliteBody.cs`

**Add**:
```csharp
public Dictionary<string, ResourceDeposit> Resources { get; set; }
```

---

## Phase 3: Planet/Continent Resource Generation

### 3.1 Continent Resource Config Format

**Location**: `Configuration/SystemGen/RockyPlanet.toml`, `DwarfPlanet.toml`

```toml
[celestial.resources]
primary_count = [1, 3]
secondary_count = [0, 5]
balance_penalty_factor = 0.8
balance_threshold = 0.15

[[celestial.resources.primary]]
id = "iron_ore"
base_weight = 1.0
elevation_weight_curve = [0.0, 0.3, 0.9, 1.0, 0.7]

[[celestial.resources.primary]]
id = "copper_ore"
base_weight = 0.8
elevation_weight_curve = [0.1, 0.5, 0.8, 0.6, 0.2]

[[celestial.resources.secondary]]
id = "gold_ore"
base_weight = 0.2
elevation_weight_curve = [0.0, 0.1, 0.4, 0.8, 0.3]

[[celestial.resources.secondary]]
id = "uranium_ore"
base_weight = 0.1
elevation_weight_curve = [0.0, 0.2, 0.5, 0.9, 0.4]
```

### 3.2 Create ContinentResourceGenerator.cs

**Location**: `Scripts/ProceduralGeneration/MeshGeneration/ResourceGeneration/ContinentResourceGenerator.cs`

**Algorithm**:
```
For each continent:
    1. Calculate weighted resource pool:
       - Base weight from config
       - × BiomeAffinity from ResourceDefinition (averaged over continent's cells)
       - × ElevationWeight from config curve (based on avg continent elevation)
       - × StressProximityFactor (bonus for cells near tectonic boundaries)
       
    2. Select 1-3 primary resources (weighted random)
    
    3. Select 0-5 secondary resources (weighted random with soft penalty)
    
    4. Apply global balancing:
       - Track total abundance per resource across all continents
       - Apply soft penalty curve if resource exceeds threshold
       
    5. Distribute to VoronoiCells:
       - Each cell gets subset of continent's resources
       - Abundance varies per cell based on local conditions
```

**Soft Penalty Curve Implementation**:
```csharp
float CalculateAdjustedWeight(string resourceId, float baseWeight, 
    Dictionary<string, float> currentGlobalTotals, float threshold, float penaltyFactor)
{
    float currentFraction = currentGlobalTotals.GetValueOrDefault(resourceId, 0) 
                           / totalGlobalResources;
    
    if (currentFraction > threshold)
    {
        float excess = currentFraction - threshold;
        return baseWeight * Mathf.Pow(penaltyFactor, excess * 10f);
    }
    return baseWeight;
}
```

### 3.3 Update VoronoiCell.cs

**Location**: `Scripts/Structures/GameState/VoronoiCell.cs`

**Add**:
```csharp
public Dictionary<string, float> Resources { get; set; }  // resource_id -> abundance
```

### 3.4 Update Continent.cs

**Location**: `Scripts/Structures/GameState/Continent.cs`

**Add**:
```csharp
public Dictionary<string, float> ContinentalResources { get; set; }  // Available resource types
public Dictionary<string, float> ResourceAbundance { get; set; }    // Total amounts
```

---

## Phase 4: Visualization & Integration

### 4.1 Create ResourceVisualizer.cs

**Location**: `Scripts/ProceduralGeneration/MeshGeneration/ResourceGeneration/ResourceVisualizer.cs`

**Responsibilities**:
- Apply color tinting to VoronoiCells based on dominant resource
- Blend with existing biome colors

**Implementation**:
```csharp
public static Color ApplyResourceTint(Color baseColor, Dictionary<string, float> resources)
{
    if (resources == null || resources.Count == 0)
        return baseColor;
    
    var dominantResource = resources.OrderByDescending(r => r.Value).First();
    var resourceColor = ResourceDatabase.Instance.GetResourceColor(dominantResource.Key);
    float tintStrength = Mathf.Clamp(dominantResource.Value * 0.5f, 0f, 0.35f);
    
    return baseColor.Lerp(resourceColor, tintStrength);
}
```

### 4.2 Update UnifiedCelestialMesh.cs

**Location**: `Scripts/ProceduralGeneration/MeshGeneration/UnifiedCelestialMesh.cs`

**Integration Points**:
- After `AssignBiomes()` call, add `AssignResources()`
- In `GenerateSurfaceMesh()`, apply resource tinting after biome coloring

### 4.3 Update SystemGenTemplates.cs

**Location**: `Scripts/UtilityLibrary/SystemGenTemplates.cs`

**Add parsing for**:
- `[satellite.resources]` section
- `[celestial.resources]` section
- Resource weight tables

---

## Weight Modifier System

### Topography Modifiers

| Feature | Effect |
|---------|--------|
| High elevation (mountains) | +metal ores, +rare minerals |
| Low elevation (ocean) | +fuel resources, -most ores |
| Tectonic boundary proximity | +rare minerals, +tier 2-3 |
| High stress magnitude | +rare resource chance |

### Biome Modifiers

From ResourceDefinition.BiomeAffinity:
```csharp
float GetBiomeWeight(ResourceDefinition resource, BiomeType biome)
{
    if (resource.BiomeAffinity.TryGetValue(biome, out float affinity))
        return affinity;
    return 1.0f;  // Default neutral
}
```

### Elevation Weight Curve

5-point curve mapped to elevation range [-1, 1]:
- Point 0: Deep ocean (-1.0)
- Point 1: Shallow ocean (-0.3)
- Point 2: Coastal/plains (0.0)
- Point 3: Hills (0.4)
- Point 4: Mountains (0.8+)

---

## File Structure

```
Scripts/
├── Structures/
│   └── Resources/
│       ├── ResourceDefinition.cs      (enhanced)
│       ├── ResourceDeposit.cs         (new)
│       └── ResourceDatabase.cs        (new - autoload)
├── ProceduralGeneration/
│   ├── SatelliteBody.cs               (modified)
│   └── MeshGeneration/
│       ├── UnifiedCelestialMesh.cs    (modified)
│       └── ResourceGeneration/
│           ├── SatelliteResourceGenerator.cs   (new)
│           ├── ContinentResourceGenerator.cs   (new)
│           └── ResourceVisualizer.cs           (new)
├── Structures/
│   └── GameState/
│       ├── VoronoiCell.cs             (modified)
│       └── Continent.cs               (modified)
└── UtilityLibrary/
    ├── SystemGenTemplates.cs          (modified)
    └── ResourceConfigLoader.cs        (new)

Configuration/
├── ResourceDefinition/
│   └── ResourceDefinition.toml        (enhanced)
└── SystemGen/
    ├── Asteroid.toml                  (enhanced)
    ├── Moon.toml                      (enhanced)
    ├── RockyPlanet.toml               (enhanced)
    └── DwarfPlanet.toml               (enhanced)
```

---

## Error Handling

### Startup Validation

1. ResourceDatabase loads all resources from RDF
2. For each body config file, validate referenced resource IDs exist
3. Throw descriptive error if invalid reference found:
   ```
   ResourceValidationError: Body config 'Asteroid.toml' references 
   unknown resource 'unobtainium_ore'. Valid resources: iron_ore, 
   copper_ore, gold_ore, ...
   ```

### Runtime Handling

- Missing biome affinity defaults to 1.0 (neutral)
- Out-of-range elevation defaults to 0.0 weight
- Empty resource config results in no resources (valid state)

---

## Testing Strategy

1. **Unit Tests**: ResourceDatabase validation, weight calculations
2. **Integration Tests**: End-to-end satellite/planet generation
3. **Visual Tests**: Verify resource tinting in Godot editor
4. **Config Tests**: Invalid resource references throw expected errors

---

## Estimated Effort

| Phase | Effort | Files |
|-------|--------|-------|
| Phase 1 (Database) | Medium | 4 files |
| Phase 2 (Satellites) | Low | 3 files |
| Phase 3 (Continents) | High | 4 files |
| Phase 4 (Visualization) | Low | 2 files |

**Total**: ~13 files, 4 new classes, modifications to 6 existing files

---

## Dependencies

- Tommy (TOML parsing) - already in project
- Godot 4.4 Color APIs
- Existing biome and tectonic systems
