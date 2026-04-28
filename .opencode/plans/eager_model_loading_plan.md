# Implementation Plan: Eager Model Loading with Prototype Duplication

## Overview

Transition from lazy loading (loading models on-demand) to eager loading (loading all models at startup), using `PackedScene` prototypes that can be quickly instantiated via `Instantiate()` instead of `GD.Load()`.

This eliminates frame hiccups during gameplay at the cost of increased startup time and memory usage.

---

## Goals

1. **Eliminate runtime hiccups** - All model loading happens during startup
2. **Fast instantiation** - Use `PackedScene.Instantiate()` instead of `GD.Load()` + `Instantiate()`
3. **Unified visual system** - Shared `VisualDefinition` class for Buildings, Ships, and Stations
4. **Graceful fallback** - Missing/corrupt models use fallback cube meshes
5. **Full observability** - Console logging of model loading statistics

---

## Phase 1: Extract Shared VisualDefinition

### 1.1 Create VisualDefinition.cs

**File**: `Scripts/Structures/Resources/VisualDefinition.cs` (NEW)

Create a standalone shared class that will be used by BuildingDefinition, ShipDefinition, and StationDefinition.

**Implementation**:
```csharp
using Godot;
using UtilityLibrary;

namespace Structures.Resources;

/// <summary>
/// Defines visual representation settings for game entities.
/// Shared between Buildings, Ships, and Stations.
/// </summary>
public class VisualDefinition
{
    /// <summary>Path to 3D model resource (for reference/debugging).</summary>
    public string? ModelPath { get; set; }

    /// <summary>Pre-loaded PackedScene prototype (loaded during configuration).</summary>
    public PackedScene? ModelPrototype { get; set; }

    /// <summary>Path to material resource.</summary>
    public string? ModelMaterial { get; set; }

    /// <summary>Path to animation resource.</summary>
    public string? AnimationPath { get; set; }

    /// <summary>Scale factor for the model.</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Rotation offset in degrees (Euler angles).</summary>
    public Vector3 RotationOffset { get; set; } = Vector3.Zero;

    /// <summary>
    /// Returns true if a valid model prototype is available.
    /// </summary>
    public bool HasValidPrototype => ModelPrototype != null && ModelPrototype.CanInstantiate();

    /// <summary>
    /// Creates a new instance of the model from the prototype.
    /// Returns null if no prototype is available.
    /// Caller must add the returned node to the scene tree.
    /// </summary>
    public Node3D? CreateModelInstance()
    {
        if (!HasValidPrototype)
        {
            GameLogger.Debug($"VisualDefinition: No valid prototype available for model '{ModelPath}'");
            return null;
        }

        try
        {
            var instance = ModelPrototype!.Instantiate<Node3D>();
            instance.Scale = Vector3.One * Scale;
            instance.RotationDegrees = RotationOffset;

            GameLogger.Debug($"VisualDefinition: Created model instance from prototype '{ModelPath}'");
            return instance;
        }
        catch (System.Exception ex)
        {
            GameLogger.Error($"VisualDefinition: Failed to instantiate model '{ModelPath}': {ex.Message}");
            return null;
        }
    }
}
```

---

## Phase 2: Update BuildingDefinition

### 2.1 Remove Nested VisualDefinition Class

**File**: `Scripts/Structures/Resources/BuildingDefinition.cs`

**Changes**:
1. Add `using Structures.Resources;` at top (if not already present)
2. Remove the nested `VisualDefinition` class (lines 179-205)
3. Keep the `Visual` property but change type to shared `VisualDefinition`

**Before**:
```csharp
public class BuildingDefinition
{
    // ... other properties ...
    public VisualDefinition Visual { get; set; } = new();
    
    public class VisualDefinition { /* nested class */ }
}
```

**After**:
```csharp
public class BuildingDefinition
{
    // ... other properties ...
    public VisualDefinition Visual { get; set; } = new();
    // Nested class removed - using shared VisualDefinition
}
```

---

## Phase 3: Update BuildingConfigLoader

### 3.1 Add Loading Statistics and Eager Loading

**File**: `Scripts/UtilityLibrary/DataLoading/BuildingConfigLoader.cs`

**Changes**:

1. Add static counters at top of class:
```csharp
public static int ModelsLoadedCount { get; private set; }
public static int ModelsFailedCount { get; private set; }

public static void ResetLoadingStats()
{
    ModelsLoadedCount = 0;
    ModelsFailedCount = 0;
}
```

2. Modify `ParseVisualDefinition()` to eagerly load `PackedScene`:

**Before**:
```csharp
visual.ModelPath = ValidateFilePath(
    ReadString(visualDict, "model_path", ""),
    "visual.model_path"
);
```

**After**:
```csharp
string? modelPath = ValidateFilePath(
    ReadString(visualDict, "model_path", ""),
    "visual.model_path"
);
visual.ModelPath = modelPath;

// Eagerly load the PackedScene prototype
if (!string.IsNullOrEmpty(modelPath))
{
    try
    {
        visual.ModelPrototype = GD.Load<PackedScene>(modelPath);
        if (visual.ModelPrototype != null)
        {
            GameLogger.Info($"BuildingConfigLoader: Loaded model prototype '{modelPath}'");
            ModelsLoadedCount++;
        }
        else
        {
            GameLogger.Error($"BuildingConfigLoader: Failed to load model at '{modelPath}'");
            ModelsFailedCount++;
        }
    }
    catch (System.Exception ex)
    {
        GameLogger.Error($"BuildingConfigLoader: Exception loading model '{modelPath}': {ex.Message}");
        visual.ModelPrototype = null;
        ModelsFailedCount++;
    }
}
```

3. Update the return type hint if needed (should already return `BuildingDefinition.VisualDefinition` which now aliases to shared `VisualDefinition`)

---

## Phase 4: Update BuildingConstruction

### 4.1 Use CreateModelInstance() Instead of GD.Load

**File**: `Scripts/Constructables/Buildings/BuildingConstruction.cs`

**Changes**:

Replace the lazy loading logic in `SetBuildingDefinition()`:

**Before** (lines 186-234):
```csharp
if (!string.IsNullOrEmpty(definition.Visual?.ModelPath))
{
    try
    {
        var scene = GD.Load<PackedScene>(definition.Visual.ModelPath);
        if (scene != null)
        {
            var model = scene.Instantiate<Node3D>();
            model.Scale = Vector3.One * definition.Visual.Scale;
            model.RotationDegrees = definition.Visual.RotationOffset;
            AddChild(model);
            _meshInstance = FindMeshInstanceRecursive(model);
            // ... logging ...
        }
    }
    catch (System.Exception e)
    {
        // ... error handling ...
    }
}

if (_meshInstance == null)
{
    _meshInstance = new MeshInstance3D
    {
        Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.5f, 0.5f) },
        Name = "FallbackMesh"
    };
    AddChild(_meshInstance);
}
```

**After**:
```csharp
// Create model from pre-loaded prototype
Node3D? model = definition.Visual?.CreateModelInstance();

if (model != null)
{
    AddChild(model);
    _meshInstance = FindMeshInstanceRecursive(model);

    if (_meshInstance == null)
    {
        GameLogger.Warning($"BuildingConstruction: No MeshInstance3D found in model for '{definition.IdName}'. Using fallback.");
    }
    else
    {
        GameLogger.Debug($"BuildingConstruction: Created building model for '{definition.IdName}' from prototype");
    }
}
else
{
    GameLogger.Warning($"BuildingConstruction: No model prototype for '{definition.IdName}'. Using fallback.");
}

// Fallback: create box mesh if no valid model
if (_meshInstance == null)
{
    _meshInstance = new MeshInstance3D
    {
        Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.5f, 0.5f) },
        Name = "FallbackMesh"
    };
    AddChild(_meshInstance);
    GameLogger.Debug($"BuildingConstruction: Created fallback mesh for '{definition.IdName}'");
}
```

---

## Phase 5: Update BuildingPlacementMode

### 5.1 Use CreateModelInstance() for Ghost Model

**File**: `Scripts/UI/Construction/BuildingPlacementMode.cs`

**Changes**:

Replace ghost model creation:

**Before** (lines 285-313):
```csharp
private void CreateGhostModel()
{
    if (!string.IsNullOrEmpty(_definition.Visual?.ModelPath))
    {
        var scene = GD.Load<PackedScene>(_definition.Visual.ModelPath);
        if (scene != null)
        {
            _ghostNode = scene.Instantiate<Node3D>();
            _ghostNode.Scale = Vector3.One * _definition.Visual.Scale;
            _ghostNode.RotationDegrees = _definition.Visual.RotationOffset;
        }
    }

    // Fallback: create a simple box placeholder
    if (_ghostNode == null)
    {
        var meshInstance = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.3f, 0.3f) },
        };
        _ghostNode = meshInstance;
    }
    // ... rest of method ...
}
```

**After**:
```csharp
private void CreateGhostModel()
{
    // Create ghost from pre-loaded prototype
    _ghostNode = _definition.Visual?.CreateModelInstance();

    // Fallback: create a simple box placeholder
    if (_ghostNode == null)
    {
        _ghostNode = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.3f, 0.3f) },
            Name = "GhostFallbackMesh"
        };
        GameLogger.Debug($"BuildingPlacementMode: Using fallback ghost model for '{_definition.IdName}'");
    }
    else
    {
        GameLogger.Debug($"BuildingPlacementMode: Created ghost model from prototype for '{_definition.IdName}'");
    }

    // Apply semi-transparent material to all mesh instances
    ApplyGhostMaterial(_ghostNode);

    _ghostNode.Visible = false;
    AddChild(_ghostNode);
}
```

---

## Phase 6: Update ShipDefinition

### 6.1 Add Visual Property

**File**: `Scripts/Structures/Logistics/ShipDefinition.cs`

**Changes**:

1. Add using statement:
```csharp
using Structures.Resources;
```

2. Add Visual property at end of class:
```csharp
public class ShipDefinition
{
    public string Name { get; set; } = string.Empty;
    public float DryMass { get; set; }
    public float CargoCapacity { get; set; }
    public float FuelCapacity { get; set; }
    public string EngineCategory { get; set; } = string.Empty;
    public float ConstructionTime { get; set; }
    public Dictionary<string, int> RequiredResources { get; set; } = new();

    /// <summary>Visual representation settings.</summary>
    public VisualDefinition Visual { get; set; } = new();
}
```

---

## Phase 7: Update ShipConfigLoader

### 7.1 Add Visual Parsing and Model Loading

**File**: `Scripts/UtilityLibrary/DataLoading/ShipConfigLoader.cs`

**Changes**:

1. Add static counters:
```csharp
public static int ModelsLoadedCount { get; private set; }
public static int ModelsFailedCount { get; private set; }

public static void ResetLoadingStats()
{
    ModelsLoadedCount = 0;
    ModelsFailedCount = 0;
}
```

2. Modify `ParseShipDefinition()` to parse visual:
```csharp
private static ShipDefinition ParseShipDefinition(Dictionary<object, object> dict)
{
    return new ShipDefinition
    {
        Name = BaseConfigLoader.ReadString(dict, "name", ""),
        DryMass = BaseConfigLoader.ReadFloat(dict, "dry_mass", 0f),
        CargoCapacity = BaseConfigLoader.ReadFloat(dict, "cargo_capacity", 0f),
        FuelCapacity = BaseConfigLoader.ReadFloat(dict, "fuel_capacity", 0f),
        EngineCategory = BaseConfigLoader.ReadString(dict, "engine_category", ""),
        ConstructionTime = BaseConfigLoader.ReadFloat(dict, "construction_time", 0f),
        RequiredResources = BaseConfigLoader.ReadResourceDict(dict, "required_resources"),
        Visual = ParseVisualDefinition(dict),
    };
}
```

3. Add `ParseVisualDefinition()` method:
```csharp
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
                GameLogger.Info($"ShipConfigLoader: Loaded model prototype '{modelPath}'");
                ModelsLoadedCount++;
            }
            else
            {
                GameLogger.Error($"ShipConfigLoader: Failed to load model at '{modelPath}'");
                ModelsFailedCount++;
            }
        }
        catch (System.Exception ex)
        {
            GameLogger.Error($"ShipConfigLoader: Exception loading model '{modelPath}': {ex.Message}");
            ModelsFailedCount++;
        }
    }

    visual.ModelMaterial = BaseConfigLoader.ReadString(visualDict, "model_material", "");
    visual.AnimationPath = BaseConfigLoader.ReadString(visualDict, "animation_path", "");
    visual.Scale = BaseConfigLoader.ReadFloat(visualDict, "scale", 1.0f);
    visual.RotationOffset = BaseConfigLoader.ReadVector3(visualDict, "rotation_offset", Vector3.Zero);

    return visual;
}
```

---

## Phase 8: Update StationDefinition

### 8.1 Add Visual Property

**File**: `Scripts/Structures/Logistics/StationDefinition.cs`

**Changes**:

1. Add using statement:
```csharp
using Structures.Resources;
```

2. Add Visual property at end of class:
```csharp
public class StationDefinition
{
    public string Name { get; set; } = string.Empty;
    public string StationType { get; set; } = string.Empty;
    public float ConstructionTime { get; set; }
    public bool CanBuildShips { get; set; }
    public int MaxParallelShipBuilds { get; set; } = 1;
    public bool CanBuildBuildings { get; set; }
    public float BuildingWorkBudgetPerTick { get; set; } = 1.0f;
    public float BuildingScalingPenalty { get; set; } = 0.05f;
    public Dictionary<string, int> RequiredResources { get; set; } = new();

    /// <summary>Visual representation settings.</summary>
    public VisualDefinition Visual { get; set; } = new();
}
```

---

## Phase 9: Update StationConfigLoader

### 9.1 Add Visual Parsing and Model Loading

**File**: `Scripts/UtilityLibrary/DataLoading/StationConfigLoader.cs`

**Changes**:

1. Add static counters:
```csharp
public static int ModelsLoadedCount { get; private set; }
public static int ModelsFailedCount { get; private set; }

public static void ResetLoadingStats()
{
    ModelsLoadedCount = 0;
    ModelsFailedCount = 0;
}
```

2. Modify `ParseStationDefinition()` to parse visual:
```csharp
private static StationDefinition ParseStationDefinition(Dictionary<object, object> dict)
{
    return new StationDefinition
    {
        Name = BaseConfigLoader.ReadString(dict, "name", ""),
        StationType = BaseConfigLoader.ReadString(dict, "station_type", ""),
        ConstructionTime = BaseConfigLoader.ReadFloat(dict, "construction_time", 0f),
        CanBuildShips = BaseConfigLoader.ReadBool(dict, "can_build_ships", false),
        MaxParallelShipBuilds = BaseConfigLoader.ReadInt(dict, "max_parallel_ship_builds", 1),
        CanBuildBuildings = BaseConfigLoader.ReadBool(dict, "can_build_buildings", false),
        BuildingWorkBudgetPerTick = BaseConfigLoader.ReadFloat(dict, "building_work_budget_per_tick", 1.0f),
        BuildingScalingPenalty = BaseConfigLoader.ReadFloat(dict, "building_scaling_penalty", 0.05f),
        RequiredResources = BaseConfigLoader.ReadResourceDict(dict, "required_resources"),
        Visual = ParseVisualDefinition(dict),
    };
}
```

3. Add `ParseVisualDefinition()` method (identical to ShipConfigLoader):
```csharp
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

    return visual;
}
```

---

## Phase 10: Add Loading Summary Logging

### 10.1 Update Database Classes

**Files**:
- `Scripts/Structures/Resources/BuildingDatabase.cs`
- `Scripts/Logistics/Resources/ShipDatabase.cs`
- `Scripts/Logistics/Resources/StationDatabase.cs`

**Changes**: Add summary logging after loading completes.

**For BuildingDatabase.cs** (in `LoadData()`, after loading loop):
```csharp
GameLogger.Info($"BuildingDatabase: '{DatabaseName}' loaded successfully with {_buildings.Count} buildings, " +
                $"{BuildingConfigLoader.ModelsLoadedCount} models loaded, " +
                $"{BuildingConfigLoader.ModelsFailedCount} models failed");
```

**For ShipDatabase.cs** (in `LoadData()`, after loading loop):
```csharp
GameLogger.Info($"ShipDatabase: '{DatabaseName}' loaded successfully with {_ships.Count} ships, " +
                $"{ShipConfigLoader.ModelsLoadedCount} models loaded, " +
                $"{ShipConfigLoader.ModelsFailedCount} models failed");
```

**For StationDatabase.cs** (in `LoadData()`, after loading loop):
```csharp
GameLogger.Info($"StationDatabase: '{DatabaseName}' loaded successfully with {_stations.Count} stations, " +
                $"{StationConfigLoader.ModelsLoadedCount} models loaded, " +
                $"{StationConfigLoader.ModelsFailedCount} models failed");
```

---

## Phase 11: Future Ship/Station Instantiation

When ships and stations need to be instantiated in the scene tree (future work), use the same pattern:

```csharp
// Example ship instantiation
public void CreateShipFromDefinition(ShipDefinition definition)
{
    Node3D? shipModel = definition.Visual?.CreateModelInstance();
    
    if (shipModel == null)
    {
        // Use fallback
        shipModel = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.0f, 0.5f, 2.0f) },
            Name = "ShipFallbackMesh"
        };
    }
    
    AddChild(shipModel);
    // ... configure ship properties ...
}
```

---

## File Summary

### New Files
| File | Purpose |
|------|---------|
| `Scripts/Structures/Resources/VisualDefinition.cs` | Shared visual definition class |

### Modified Files
| File | Changes |
|------|---------|
| `Scripts/Structures/Resources/BuildingDefinition.cs` | Remove nested VisualDefinition, use shared class |
| `Scripts/UtilityLibrary/DataLoading/BuildingConfigLoader.cs` | Add eager loading, statistics |
| `Scripts/Constructables/Buildings/BuildingConstruction.cs` | Use CreateModelInstance() |
| `Scripts/UI/Construction/BuildingPlacementMode.cs` | Use CreateModelInstance() |
| `Scripts/Structures/Resources/BuildingDatabase.cs` | Add loading summary logging |
| `Scripts/Structures/Logistics/ShipDefinition.cs` | Add Visual property |
| `Scripts/UtilityLibrary/DataLoading/ShipConfigLoader.cs` | Add visual parsing, eager loading |
| `Scripts/Logistics/Resources/ShipDatabase.cs` | Add loading summary logging |
| `Scripts/Structures/Logistics/StationDefinition.cs` | Add Visual property |
| `Scripts/UtilityLibrary/DataLoading/StationConfigLoader.cs` | Add visual parsing, eager loading |
| `Scripts/Logistics/Resources/StationDatabase.cs` | Add loading summary logging |

### No Changes Needed
- YAML configuration files (visual sections are optional)
- `DatabaseLoadManager.cs` (existing flow already supports this)
- Model files (no changes to actual 3D assets)

---

## Testing Checklist

1. **Build Project**: Verify no compilation errors after changes
2. **Startup Loading**: Verify all building models load without errors
3. **Building Placement**: Place buildings and confirm no frame hiccups
4. **Ghost Preview**: Enter placement mode and confirm smooth ghost movement
5. **Fallback Behavior**: Temporarily rename a model file and verify fallback cube appears
6. **Loading Statistics**: Check console output shows model counts
7. **Ship/Station Loading**: Verify they load successfully (even without visual sections)

---

## Expected Console Output

On startup, expect to see:
```
BuildingConfigLoader: Loaded model prototype 'res://Models/Buildings/wind_turbine.glb'
BuildingConfigLoader: Loaded model prototype 'res://Models/Buildings/geothermal_plant.glb'
...
BuildingDatabase: 'BuildingDatabase' loaded successfully with 8 buildings, 8 models loaded, 0 models failed
ShipDatabase: 'ShipDatabase' loaded successfully with 4 ships, 0 models loaded, 0 models failed
StationDatabase: 'StationDatabase' loaded successfully with 3 stations, 0 models loaded, 0 models failed
```

During building placement:
```
BuildingPlacementMode: Created ghost model from prototype for 'wind_turbine'
BuildingConstruction: Created building model for 'wind_turbine' from prototype
```

---

## Rollback Plan

If issues occur, the changes are isolated and can be reverted:

1. Revert to lazy loading by restoring the original `SetBuildingDefinition()` and `CreateGhostModel()` methods
2. The `ModelPrototype` property can remain in `VisualDefinition` without being used
3. Remove the eager loading code from ConfigLoaders if desired

The architecture supports both patterns - the `CreateModelInstance()` method gracefully falls back to null if no prototype is loaded, triggering the fallback mesh behavior.
