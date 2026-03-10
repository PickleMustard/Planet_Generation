# TOML to YAML Migration Plan

## Overview

Migrate all configuration files from TOML format to YAML format for improved flexibility in representing relationships between game elements.

**Library:** YamlDotNet 16.3.0  
**Naming Convention:** UnderscoredNamingConvention (preserves `snake_case` keys)  
**Validation:** Separate validation script called before loading

---

## Part 1: Files to Modify

### 1.1 Configuration Files to Convert (15 files)

| File Path | Type | Status |
|-----------|------|--------|
| `Configuration/SystemGen/Star.toml` | Celestial Body | Pending |
| `Configuration/SystemGen/RockyPlanet.toml` | Celestial Body | Pending |
| `Configuration/SystemGen/GasGiant.toml` | Celestial Body | Pending |
| `Configuration/SystemGen/IceGiant.toml` | Celestial Body | Pending |
| `Configuration/SystemGen/DwarfPlanet.toml` | Celestial Body | Pending |
| `Configuration/SystemGen/Moon.toml` | Satellite | Pending |
| `Configuration/SystemGen/Asteroid.toml` | Satellite | Pending |
| `Configuration/SystemGen/Comet.toml` | Satellite | Pending |
| `Configuration/SystemGen/AsteroidBelt.toml` | Satellite Group | Pending |
| `Configuration/SystemGen/BlackHole.toml` | Celestial Body | Pending |
| `Configuration/SystemTemplate/Solar System.toml` | System Template | Pending |
| `Configuration/SystemTemplate/Binary Star System.toml` | System Template | Pending |
| `Configuration/SystemTemplate/Multi-body-test.toml` | System Template | Pending |
| `Configuration/SystemTemplate/test.toml` | System Template | Pending |
| `Configuration/ResourceDefinition/ResourceDefinition.toml` | Resources | Pending |

### 1.2 C# Source Files to Modify

| File Path | Changes Required | Status |
|-----------|------------------|--------|
| `Delaunay Triangulation Map Generation.csproj` | Replace Tommy package with YamlDotNet | **Completed** |
| `Scripts/UtilityLibrary/YamlValidator.cs` | **NEW FILE** - YAML validation utility | **Completed** |
| `Scripts/UtilityLibrary/SystemGenTemplates.cs` | Major refactor - replace all TOML parsing with YAML | Pending |
| `Scripts/UtilityLibrary/ResourceConfigLoader.cs` | Replace TOML parsing with YAML | Pending |
| `UI/PlanetSystemGenerator.cs` | Update file extension checks, template loading | Pending |
| `Scripts/Structures/Resources/ResourceDatabase.cs` | Update hardcoded path (line 43) | Pending |

---

## Part 2: TOML to YAML Structure Mapping

### 2.1 Basic Types

| TOML | YAML |
|------|------|
| `key = "value"` | `key: value` |
| `key = 123` | `key: 123` |
| `key = 3.14` | `key: 3.14` |
| `key = true` | `key: true` |

### 2.2 Arrays

| TOML | YAML (block style - preferred) |
|------|-------------------------------|
| `arr = [1, 2, 3]` | `arr:`<br>`  - 1`<br>`  - 2`<br>`  - 3` |
| `arr = [[1,2], [3,4]]` | `arr:`<br>`  - [1, 2]`<br>`  - [3, 4]` |
| `position = [0, 0, 0]` | `position: [0, 0, 0]` (inline OK for short vectors) |

### 2.3 Tables (Objects)

| TOML | YAML |
|------|------|
| `[section]`<br>`key = "value"` | `section:`<br>`  key: value` |
| `[parent.child]`<br>`key = "value"` | `parent:`<br>`  child:`<br>`    key: value` |

### 2.4 Array of Tables (Critical Pattern)

| TOML | YAML |
|------|------|
| `[[bodies]]`<br>`type = "Star"`<br>`[[bodies]]`<br>`type = "Planet"` | `bodies:`<br>`  - type: Star`<br>`  - type: Planet` |

### 2.5 Nested Array of Tables

| TOML | YAML |
|------|------|
| `[[satellite.resources.main]]`<br>`resource_id = "iron"`<br>`[[satellite.resources.main]]`<br>`resource_id = "gold"` | `satellite:`<br>`  resources:`<br>`    main:`<br>`      - resource_id: iron`<br>`      - resource_id: gold` |

### 2.6 Inline Tables

| TOML | YAML |
|------|------|
| `biome_affinity = { Mountain = 2.0, Desert = 1.5 }` | `biome_affinity:`<br>`  Mountain: 2.0`<br>`  Desert: 1.5` |

---

## Part 3: Example Conversions

### 3.1 Celestial Body with Tectonics (RockyPlanet.yaml)

```yaml
categories:
  potential:
    - mythology
    - scientists
    - explorers
    - adjectives

mythology:
  names:
    - Hephaestus
    - Vulcan
    - Gaia

scientists:
  names:
    - Charles Lyell
    - James Hutton

explorers:
  names:
    - Alexander von Humboldt
    - John Wesley Powell

adjectives:
  names:
    - Basaltic
    - Ferric
    - Rugged

celestial:
  template:
    position: [150, 0, 0]
    velocity: [0, 0, 0]
    mass: 100
    size: 50

  mesh:
    base_mesh:
      subdivisions: 2
      vertices_per_edge: [[4, 6], [2, 3]]
      num_abberations: 3
      num_deformation_cycles: 3

    tectonic:
      num_continents: [13, 25]
      stress_scale: [1.1, 1.1]
      shear_scale: [1.1, 1.1]
      max_propagation_distance: [0.01, 0.01]
      propagation_falloff: [0.2, 0.2]
      inactive_stress_threshold: [0.01, 0.01]
      general_height_scale: [0.6, 0.6]
      general_shear_scale: [1.1, 1.1]
      general_compression_scale: [1.2, 1.2]
      general_transform_scale: [1.0, 1.2]
```

### 3.2 System Template (Solar System.yaml)

```yaml
bodies:
  - type: Star
    position: [0, 0, 0]
    velocity: [0, 0, 0]
    mass: 500000
    size: 75

    mesh:
      base_mesh:
        subdivisions: 1
        vertices_per_edge: [12, 17]
        num_abberations: 0
        num_deformation_cycles: 0

      tectonic:
        num_continents: [1, 2]
        stress_scale: [0.8, 3.0]
        shear_scale: [0.2, 1.7]
        max_propagation_distance: [0.01, 0.1]
        propagation_falloff: [0.8, 1.5]
        inactive_stress_threshold: [0.01, 0.1]
        general_height_scale: [0.8, 1.2]
        general_shear_scale: [0.8, 1.2]
        general_compression_scale: [1.0, 1.75]
        general_transform_scale: [1.1, 1.4]

    satellites:
      - type: Asteroid Belt
        position: [-100, 0, 0]
        velocity: [0, -2, 0]
        size: 1

        template:
          number_asteroids: [10, 15]
          grouping: balanced
          ring_velocity: [0, 2, 0]
          size_range: [1, 5]
          possible_subtypes:
            - planet
            - asteroid
```

### 3.3 Resource Definition (ResourceDefinition.yaml)

```yaml
resources:
  - id_name: iron_ore
    resource_tier: 0
    resource_type: ore
    display_color: [139, 69, 19]
    biome_affinity:
      Mountain: 2.0
      StoneDesert: 1.5
      Taiga: 1.2
      RustedMountain: 1.8
    elevation_range: [0.5, 1.0]

  - id_name: water
    resource_tier: 0
    resource_type: fuel
    display_color: [30, 144, 255]
    biome_affinity:
      Ocean: 3.0
      Coastal: 1.5
    elevation_range: [0.0, 0.4]
```

### 3.4 Satellite Group (AsteroidBelt.yaml)

```yaml
satellite_group:
  template:
    number_asteroids: [6, 15]
    grouping: Balanced
    apogee: 8000
    perigee: 250
    ring_velocity: [0, 2, 0]
    size_range: [1, 5]
    mass_range: [1, 10]
    possible_subtypes:
      - DwarfPlanet
      - Asteroid
```

---

## Part 4: Code Changes Required

### 4.1 Project File (Delaunay Triangulation Map Generation.csproj)

**Status: Completed**

```xml
<ItemGroup>
  <PackageReference Include="YamlDotNet" Version="16.3.0"/>
</ItemGroup>
```

### 4.2 YamlValidator.cs (NEW FILE)

**Status: Completed**

Location: `Scripts/UtilityLibrary/YamlValidator.cs`

Provides:
- `ValidateCelestialBodyTemplate(filePath)` - Validates body templates
- `ValidateSystemTemplate(filePath)` - Validates system templates
- `ValidateResourceDefinition(filePath)` - Validates resource definitions
- `ValidateAllConfigurations()` - Validates all config files

### 4.3 SystemGenTemplates.cs - Key Changes Required

**Status: Pending**

#### Imports Section
```csharp
// Remove:
using Tommy;

// Add:
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
```

#### Path Methods - Rename and Update
```csharp
// Rename: GetTomlPath() → GetYamlPath()
// Change extension from .toml to .yaml

private static string GetYamlPath(CelestialBodyType type)
{
    string name = type switch
    {
        CelestialBodyType.RockyPlanet => "RockyPlanet",
        CelestialBodyType.GasGiant => "GasGiant",
        CelestialBodyType.IceGiant => "IceGiant",
        CelestialBodyType.DwarfPlanet => "DwarfPlanet",
        CelestialBodyType.Star => "Star",
        CelestialBodyType.BlackHole => "BlackHole",
        _ => type.ToString(),
    };
    return $"res://Configuration/SystemGen/{name}.yaml";
}
```

#### Core Parsing Method Pattern
```csharp
private static Godot.Collections.Dictionary TryParseTemplate(
    string resPath,
    bool isSatellite = false
)
{
    // Validate first
    var validation = YamlValidator.ValidateCelestialBodyTemplate(resPath);
    if (!validation.IsValid)
    {
        GD.PrintErr($"YAML validation failed for {resPath}");
        throw new ArgumentException("YAML validation failed");
    }

    // Deserialize
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    var yamlData = deserializer.Deserialize<Dictionary<string, object>>(text);
    // ... rest of parsing logic
}
```

#### Type Mapping (Tommy → YamlDotNet)

| Tommy Type | YamlDotNet Type (when deserializing to object) |
|------------|------------------------------------------------|
| `TomlTable` | `Dictionary<string, object>` |
| `TomlArray` | `List<object>` |
| `TomlString` | `string` |
| `TomlInteger` | `long` |
| `TomlFloat` | `double` |
| `TomlBoolean` | `bool` |
| `TomlNode` | `object` |

#### Helper Methods to Update
- `ReadString()` - Update type checks
- `ReadInt()` - YamlDotNet uses `long` for integers
- `ReadFloat()` - YamlDotNet uses `double` for floats
- `ReadVector3()` - Update array access pattern (`List<object>`)
- `ReadFloatRange()` - Update array access pattern
- `ReadIntRange()` - Update array access pattern
- `ReadStringArray()` - Update sequence access pattern
- `ReadIntArray()` - Update sequence access pattern
- `NodeToFloat()` - Replace with YAML-compatible version

#### YAML Generation Method
```csharp
// Rename: GenerateTOMLContent() → GenerateYamlContent()
public static string GenerateYamlContent(Array<Dictionary> bodies)
{
    var serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    var data = new Dictionary<string, object>
    {
        ["bodies"] = ConvertBodiesToYamlStructure(bodies)
    };

    return serializer.Serialize(data);
}
```

### 4.4 ResourceConfigLoader.cs - Key Changes

**Status: Pending**

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static List<ResourceDefinition> LoadResourceDefinitions(string filePath)
{
    // Validate first
    var validation = YamlValidator.ValidateResourceDefinition(filePath);
    if (!validation.IsValid)
    {
        GD.PrintErr($"YAML validation failed");
        return definitions;
    }

    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    var yamlData = deserializer.Deserialize<Dictionary<string, object>>(text);

    if (yamlData.ContainsKey("resources") && yamlData["resources"] is List<object> resourcesList)
    {
        foreach (var resourceObj in resourcesList)
        {
            if (resourceObj is Dictionary<string, object> resourceDict)
            {
                definitions.Add(ParseResourceDefinition(resourceDict));
            }
        }
    }

    return definitions;
}
```

### 4.5 PlanetSystemGenerator.cs - Key Changes

**Status: Pending**

#### LoadTemplates() - Change extension
```csharp
if (file.EndsWith(".yaml"))  // was ".toml"
{
    button.Text = file.Replace(".yaml", "");
}
```

#### SaveSystemToFile() - Change extension and method
```csharp
if (!fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
{
    fileName += ".yaml";  // was ".toml"
}

string yamlContent = UtilityLibrary.SystemGenTemplates.GenerateYamlContent(bodies);
```

#### Remove Unused TOML Helper Methods
Delete lines 411-491 (unused `ReadString`, `ReadVector3`, `ReadFloat`, `NodeToFloat`, `ReadInt`, `ReadIntArray` methods)

### 4.6 ResourceDatabase.cs

**Status: Pending**

```csharp
// Line 43 - Change path
string configPath = "res://Configuration/ResourceDefinition/ResourceDefinition.yaml";
```

---

## Part 5: Execution Order

### Step 1: Update Project File ✅
1. Edit `.csproj` to replace Tommy with YamlDotNet
2. Run `dotnet restore`

### Step 2: Create YAML Validator ✅
1. Create `Scripts/UtilityLibrary/YamlValidator.cs`

### Step 3: Convert Configuration Files
Convert all 15 TOML files to YAML:
1. `Configuration/SystemGen/*.toml` → `*.yaml` (10 files)
2. `Configuration/SystemTemplate/*.toml` → `*.yaml` (4 files)
3. `Configuration/ResourceDefinition/ResourceDefinition.toml` → `ResourceDefinition.yaml`

### Step 4: Update C# Parsers
Update in this order:
1. `Scripts/UtilityLibrary/SystemGenTemplates.cs`
2. `Scripts/UtilityLibrary/ResourceConfigLoader.cs`
3. `Scripts/Structures/Resources/ResourceDatabase.cs`
4. `UI/PlanetSystemGenerator.cs`

### Step 5: Build and Test
1. Run `dotnet build`
2. Open in Godot
3. Test template loading
4. Test system generation
5. Test save functionality

### Step 6: Manual Cleanup (User Action)
1. Verify all YAML files work correctly
2. Delete original `.toml` files manually when ready

---

## Part 6: YamlDotNet API Quick Reference

### Deserializing (Reading)
```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var deserializer = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();

// To dictionary (dynamic)
var data = deserializer.Deserialize<Dictionary<string, object>>(yamlString);

// To typed class
var config = deserializer.Deserialize<MyConfigClass>(yamlString);
```

### Serializing (Writing)
```csharp
var serializer = new SerializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();

string yaml = serializer.Serialize(myObject);
```

### Type Mapping (YamlDotNet → C#)
| YAML Type | C# Type (when deserializing to object) |
|-----------|----------------------------------------|
| String | `string` |
| Integer | `long` |
| Float | `double` |
| Boolean | `bool` |
| List/Array | `List<object>` |
| Mapping/Dict | `Dictionary<string, object>` |
| Null | `null` |

---

## Part 7: Testing Checklist

- [ ] YamlDotNet package installed successfully
- [ ] `YamlValidator.cs` created and compiles
- [ ] All 10 SystemGen templates converted to YAML
- [ ] All 4 SystemTemplate files converted to YAML
- [ ] ResourceDefinition.yaml created
- [ ] `SystemGenTemplates.cs` updated and compiles
- [ ] `ResourceConfigLoader.cs` updated and compiles
- [ ] `PlanetSystemGenerator.cs` updated and compiles
- [ ] `ResourceDatabase.cs` path updated
- [ ] Project builds with `dotnet build`
- [ ] Godot project opens without errors
- [ ] Star template loads correctly
- [ ] RockyPlanet template loads correctly
- [ ] GasGiant template loads correctly
- [ ] Moon template loads correctly
- [ ] Asteroid template loads correctly (with resources)
- [ ] AsteroidBelt template loads correctly
- [ ] Solar System template loads correctly
- [ ] Binary Star System template loads correctly
- [ ] Resource definitions load correctly
- [ ] System generation produces expected results
- [ ] Save functionality creates valid YAML files
- [ ] Saved files can be re-loaded
- [ ] YAML validation catches errors correctly
- [ ] Old TOML files deleted manually

---

## Part 8: Files Created During Implementation

The following YAML configuration files have already been created:

### SystemGen (10 files)
- `Configuration/SystemGen/Star.yaml`
- `Configuration/SystemGen/RockyPlanet.yaml`
- `Configuration/SystemGen/GasGiant.yaml`
- `Configuration/SystemGen/IceGiant.yaml`
- `Configuration/SystemGen/DwarfPlanet.yaml`
- `Configuration/SystemGen/Moon.yaml`
- `Configuration/SystemGen/Asteroid.yaml`
- `Configuration/SystemGen/Comet.yaml`
- `Configuration/SystemGen/AsteroidBelt.yaml`
- `Configuration/SystemGen/BlackHole.yaml`

### SystemTemplate (4 files)
- `Configuration/SystemTemplate/Solar System.yaml`
- `Configuration/SystemTemplate/Binary Star System.yaml`
- `Configuration/SystemTemplate/Multi-body-test.yaml`
- `Configuration/SystemTemplate/test.yaml`

### ResourceDefinition (1 file)
- `Configuration/ResourceDefinition/ResourceDefinition.yaml`

### Code Files
- `Scripts/UtilityLibrary/YamlValidator.cs` (NEW)
