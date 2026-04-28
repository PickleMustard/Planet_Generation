# Station/Ship/Engine Loading System Refactoring Plan

## Overview
Refactor the station, ship, and engine loading systems to use dedicated loader classes in the DataLoading directory. **No backward compatibility required** - all callers will be updated to use the new loaders directly.

## Current State

### Files Using LogisticsConfigLoader
Only 2 files currently use `LogisticsConfigLoader`:
1. `Scripts/Logistics/Resources/StationDatabase.cs` - uses `LoadAllStations()`, `StationDefinition`
2. `Scripts/Logistics/Resources/ShipDatabase.cs` - uses `LoadAllShips()`, `ShipDefinition`

### Current Rigid Pattern
1. **Category Registry Required**: `StationTemplates.yaml`, `ShipTemplates.yaml`, `EngineTypes.yaml` must define categories first
2. **Filename Coupling**: Each category MUST have a matching file (e.g., `Shipyard.yaml` for "Shipyard" category)
3. **Monolithic**: All loading logic in `LogisticsConfigLoader.cs` (750+ lines)
4. **No Subdirectory Support**: All files must be in flat directory structure

## Target State

### New Architecture
```
Scripts/
├── UtilityLibrary/
│   └── DataLoading/
│       ├── BaseConfigLoader.cs           # NEW - Shared utilities
│       ├── BuildingConfigLoader.cs       # Existing
│       ├── ResourceConfigLoader.cs       # Existing
│       ├── StationConfigLoader.cs        # NEW
│       ├── ShipConfigLoader.cs           # NEW
│       └── EngineConfigLoader.cs         # NEW
├── Structures/
│   └── Logistics/
│       ├── StationDefinition.cs          # MOVED from LogisticsConfigLoader
│       ├── ShipDefinition.cs             # MOVED from LogisticsConfigLoader
│       ├── EngineDefinition.cs           # MOVED from LogisticsConfigLoader
│       ├── StationTemplateCategory.cs    # MOVED from LogisticsConfigLoader
│       ├── ShipTemplateCategory.cs       # MOVED from LogisticsConfigLoader
│       └── EngineTypeCategory.cs         # MOVED from LogisticsConfigLoader
└── Logistics/
    └── Resources/
        ├── StationDatabase.cs            # UPDATED - uses StationConfigLoader
        └── ShipDatabase.cs               # UPDATED - uses ShipConfigLoader
```

### Flexible Loading Pattern
1. **Directory Scanning**: Scan `Configuration/stations/`, `Configuration/ships/`, `Configuration/engines/` recursively
2. **Load All YAML Files**: Any `.yaml` or `.yml` file found is loaded
3. **Category Inference**: Categories determined from entity fields (`station_type`, `ship_template`, `engine_category`)
4. **Organized Structure**: Support subdirectories for logical organization
5. **Separated Concerns**: Dedicated loader classes with shared utilities

---

## Implementation Plan

---

## Phase 1: Create Shared Infrastructure

### Ticket 1: Create BaseConfigLoader

**New File**: `Scripts/UtilityLibrary/DataLoading/BaseConfigLoader.cs`

**Purpose**: Share common YAML loading utilities across all loaders

**Content**:
```csharp
public static class BaseConfigLoader
{
    // Directory scanning
    public static List<string> GetYamlFilesRecursive(string directory)
    
    // YAML parsing helpers
    public static string ReadString(Dictionary<object, object> dict, string key, string fallback)
    public static int ReadInt(Dictionary<object, object> dict, string key, int fallback)
    public static float ReadFloat(Dictionary<object, object> dict, string key, float fallback)
    public static bool ReadBool(Dictionary<object, object> dict, string key, bool fallback)
    public static Dictionary<string, int> ReadResourceDict(Dictionary<object, object> dict, string key)
    public static List<string> ReadStringList(Dictionary<object, object> dict, string key)
    
    // Type conversion helpers
    public static int NodeToInt(object node, int fallback)
    public static float NodeToFloat(object node, float fallback)
}
```

**Benefits**:
- Eliminates code duplication across loaders
- Consistent parsing behavior
- Easier maintenance
- Reduces loader file sizes

---

## Phase 2: Move Definition Classes

### Ticket 2: Create Structures/Logistics Directory and Move Definitions

**New Directory**: `Scripts/Structures/Logistics/`

**Files to Create**:

1. **StationDefinition.cs**
```csharp
namespace Structures.Logistics;

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
}
```

2. **ShipDefinition.cs**
```csharp
namespace Structures.Logistics;

public class ShipDefinition
{
    public string Name { get; set; } = string.Empty;
    public float DryMass { get; set; }
    public float CargoCapacity { get; set; }
    public float FuelCapacity { get; set; }
    public string EngineCategory { get; set; } = string.Empty;
    public float ConstructionTime { get; set; }
    public Dictionary<string, int> RequiredResources { get; set; } = new();
}
```

3. **EngineDefinition.cs**
```csharp
namespace Structures.Logistics;

public class EngineDefinition
{
    public string Name { get; set; } = string.Empty;
    public float SpecificImpulse { get; set; }
    public float Thrust { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

4. **StationTemplateCategory.cs**
```csharp
namespace Structures.Logistics;

public class StationTemplateCategory
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

5. **ShipTemplateCategory.cs**
```csharp
namespace Structures.Logistics;

public class ShipTemplateCategory
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

6. **EngineTypeCategory.cs**
```csharp
namespace Structures.Logistics;

public class EngineTypeCategory
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
```

**Acceptance Criteria**:
- All 6 definition classes created in `Structures/Logistics/`
- Proper namespace declarations
- XML documentation comments on public members
- No dependencies on loader classes

---

## Phase 3: Create Loader Classes

### Ticket 3: Create StationConfigLoader

**New File**: `Scripts/UtilityLibrary/DataLoading/StationConfigLoader.cs`

**Structure**:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Logistics;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

public static class StationConfigLoader
{
    private const string StationsDirectory = "res://Configuration/stations/";
    private const string StationTemplatesPath = "res://Configuration/stations/StationTemplates.yaml";
    
    private static List<StationDefinition>? _allStations;
    private static HashSet<string>? _inferredCategories;
    private static Dictionary<string, string>? _stationSourceFiles;
    private static List<StationTemplateCategory>? _templateCategories;
    
    public static List<StationDefinition> LoadAllStations()
    public static List<StationDefinition> LoadStationsFromFile(string filePath)
    public static List<StationTemplateCategory> LoadStationTemplates()
    public static List<string> GetStationCategories()
    public static StationDefinition? GetStationByName(string name)
    public static void ClearCache()
    
    private static void InferCategories(List<StationDefinition> stations)
    private static void ValidateStationTypes(List<StationDefinition> stations)
    private static StationDefinition ParseStationDefinition(Dictionary<object, object> dict)
}
```

**Features**:
- Directory scanning for all `.yaml` and `.yml` files
- Parse stations from any file regardless of name
- Infer categories from `station_type` field
- Detect duplicates across files with source file tracking
- Warn on unknown `station_type` values (doesn't prevent loading)
- Logging of loading progress

**Acceptance Criteria**:
- Loads all stations from `Configuration/stations/` directory
- Supports subdirectories
- Skips `StationTemplates.yaml` (used only for validation)
- Detects and logs duplicate station names with file paths
- Infers categories from loaded stations
- Validates station types against templates (warns only)
- Proper error handling and logging

---

### Ticket 4: Create ShipConfigLoader

**New File**: `Scripts/UtilityLibrary/DataLoading/ShipConfigLoader.cs`

**Structure** (same pattern as StationConfigLoader):
```csharp
namespace UtilityLibrary.DataLoading;

public static class ShipConfigLoader
{
    private const string ShipsDirectory = "res://Configuration/ships/";
    private const string ShipTemplatesPath = "res://Configuration/ships/ShipTemplates.yaml";
    
    private static List<ShipDefinition>? _allShips;
    private static HashSet<string>? _inferredCategories;
    private static Dictionary<string, string>? _shipSourceFiles;
    
    public static List<ShipDefinition> LoadAllShips()
    public static List<ShipDefinition> LoadShipsFromFile(string filePath)
    public static List<ShipTemplateCategory> LoadShipTemplates()
    public static List<string> GetShipCategories()
    public static ShipDefinition? GetShipByName(string name)
    public static void ClearCache()
    
    private static void InferCategories(List<ShipDefinition> ships)
    private static void ValidateShipTemplates(List<ShipDefinition> ships)
    private static ShipDefinition ParseShipDefinition(Dictionary<object, object> dict)
}
```

**Acceptance Criteria**:
- Loads all ships from `Configuration/ships/` directory
- Supports subdirectories
- Infers categories from `ship_template` field
- Validates ship templates (warns only)

---

### Ticket 5: Create EngineConfigLoader

**New File**: `Scripts/UtilityLibrary/DataLoading/EngineConfigLoader.cs`

**Structure** (same pattern):
```csharp
namespace UtilityLibrary.DataLoading;

public static class EngineConfigLoader
{
    private const string EnginesDirectory = "res://Configuration/engines/";
    private const string EngineTypesPath = "res://Configuration/engines/EngineTypes.yaml";
    
    private static List<EngineDefinition>? _allEngines;
    private static HashSet<string>? _inferredCategories;
    private static Dictionary<string, string>? _engineSourceFiles;
    
    public static List<EngineDefinition> LoadAllEngines()
    public static List<EngineDefinition> LoadEnginesFromFile(string filePath)
    public static List<EngineTypeCategory> LoadEngineTypes()
    public static List<string> GetEngineCategories()
    public static EngineDefinition? GetEngineByName(string name)
    public static void ClearCache()
    
    private static void InferCategories(List<EngineDefinition> engines)
    private static void ValidateEngineTypes(List<EngineDefinition> engines)
    private static EngineDefinition ParseEngineDefinition(Dictionary<object, object> dict)
}
```

**Acceptance Criteria**:
- Loads all engines from `Configuration/engines/` directory
- Supports subdirectories
- Infers categories from engine references
- Validates engine types (warns only)

---

## Phase 4: Update Dependent Code

### Ticket 6: Update StationDatabase

**File**: `Scripts/Logistics/Resources/StationDatabase.cs`

**Changes**:
1. **Update imports**:
   ```csharp
   // Remove:
   using LogisticsConfigLoader = UtilityLibrary.LogisticsConfigLoader;
   using StationDefinition = UtilityLibrary.StationDefinition;
   
   // Add:
   using UtilityLibrary.DataLoading;
   using Structures.Logistics;
   ```

2. **Update LoadData() method**:
   ```csharp
   // Change:
   var allStations = LogisticsConfigLoader.LoadAllStations();
   
   // To:
   var allStations = StationConfigLoader.LoadAllStations();
   ```

3. **Update CreateLoadPackage() step names** (optional but recommended):
   - "Parse_Station_Templates" → "Load_Station_Configurations"

**Acceptance Criteria**:
- Uses `StationConfigLoader.LoadAllStations()`
- Uses `Structures.Logistics.StationDefinition`
- Compiles without errors
- All functionality preserved

---

### Ticket 7: Update ShipDatabase

**File**: `Scripts/Logistics/Resources/ShipDatabase.cs`

**Changes**:
1. **Update imports**:
   ```csharp
   // Remove:
   using LogisticsConfigLoader = UtilityLibrary.LogisticsConfigLoader;
   using ShipDefinition = UtilityLibrary.ShipDefinition;
   
   // Add:
   using UtilityLibrary.DataLoading;
   using Structures.Logistics;
   ```

2. **Update LoadData() method**:
   ```csharp
   // Change:
   var allShips = LogisticsConfigLoader.LoadAllShips();
   
   // To:
   var allShips = ShipConfigLoader.LoadAllShips();
   ```

**Acceptance Criteria**:
- Uses `ShipConfigLoader.LoadAllShips()`
- Uses `Structures.Logistics.ShipDefinition`
- Compiles without errors
- All functionality preserved

---

## Phase 5: Remove Old Code

### Ticket 8: Delete LogisticsConfigLoader

**File to Delete**: `Scripts/UtilityLibrary/LogisticsConfigLoader.cs`

**Rationale**: 
- No longer needed - all functionality moved to dedicated loaders
- No backward compatibility requirement
- All callers updated

**Action**: Delete the file entirely.

**Verification**:
- Ensure no compilation errors
- Verify no other files reference `LogisticsConfigLoader`
- Run all tests to confirm no regressions

---

## Phase 6: Testing

### Ticket 9: Create Unit Tests for StationConfigLoader

**New File**: `Tests/Logistics/StationConfigLoaderTest.cs`

**Test Cases**:
1. `LoadAllStations_ScansDirectory()`
   - Verify all `.yaml` and `.yml` files are found
   - Verify subdirectories are scanned

2. `LoadAllStations_LoadsFromMultipleFiles()`
   - Create multiple test YAML files
   - Verify stations from all files are loaded

3. `LoadAllStations_InfersCategories()`
   - Load stations with different `station_type` values
   - Verify categories are correctly inferred

4. `LoadAllStations_DetectsDuplicates()`
   - Create duplicate station names across files
   - Verify duplicate detection and error logging

5. `LoadAllStations_WarnsOnUnknownType()`
   - Create station with unknown `station_type`
   - Verify warning is logged
   - Verify station is still loaded

6. `LoadAllStations_SupportsSubdirectories()`
   - Create stations in subdirectories
   - Verify they are loaded correctly

7. `LoadStationsFromFile_LoadsSingleFile()`
   - Test loading from specific file path
   - Verify only that file's stations are loaded

8. `ClearCache_ResetsState()`
   - Load stations, clear cache, load again
   - Verify fresh load occurs

9. `GetStationByName_ReturnsCorrectStation()`
   - Test lookup by name
   - Test case sensitivity
   - Test not found returns null

10. `GetStationCategories_ReturnsInferredCategories()`
    - Load stations with various types
    - Verify distinct categories returned

---

### Ticket 10: Create Unit Tests for ShipConfigLoader

**New File**: `Tests/Logistics/ShipConfigLoaderTest.cs`

**Test Cases**:
- Same pattern as StationConfigLoader tests (8-10 tests)
- Focus on ship-specific fields (`EngineCategory`, etc.)

---

### Ticket 11: Create Unit Tests for EngineConfigLoader

**New File**: `Tests/Logistics/EngineConfigLoaderTest.cs`

**Test Cases**:
- Same pattern as StationConfigLoader tests (8-10 tests)
- Focus on engine-specific fields (`SpecificImpulse`, `Thrust`)

---

### Ticket 12: Create Integration Tests

**New File**: `Tests/Logistics/LogisticsLoadingIntegrationTest.cs`

**Test Cases**:
1. `StationDatabase_UsesNewLoader()`
   - Full integration test with StationDatabase
   - Verify all stations loaded
   - Verify category queries work

2. `ShipDatabase_UsesNewLoader()`
   - Full integration test with ShipDatabase
   - Verify all ships loaded
   - Verify category queries work

3. `AllLoaders_ConsistentBehavior()`
   - Test that all three loaders have consistent behavior
   - Similar error handling
   - Similar caching behavior

4. `DefinitionClasses_CorrectNamespace()`
   - Verify StationDefinition is in Structures.Logistics
   - Verify ShipDefinition is in Structures.Logistics
   - Verify EngineDefinition is in Structures.Logistics

---

## Phase 7: Documentation

### Ticket 13: Update AGENTS.md

**File**: `AGENTS.md`

**Changes**:
1. Remove `LogisticsConfigLoader` references
2. Add new loader classes to DataLoading section
3. Document `Structures.Logistics` namespace
4. Update loading documentation section
5. Document flexible loading approach
6. Document category inference
7. Document subdirectory support

---

### Ticket 14: Create Loader Documentation

**New Files**:
- `Docs/Configuration/StationLoading.md`
- `Docs/Configuration/ShipLoading.md`
- `Docs/Configuration/EngineLoading.md`

**Each document includes**:
1. How to add new entities
2. File organization recommendations (including subdirectories)
3. YAML format reference
4. Category inference explanation
5. Validation and error messages
6. API reference for the loader class

---

## Detailed Implementation

### BaseConfigLoader Implementation

```csharp
// Scripts/UtilityLibrary/DataLoading/BaseConfigLoader.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace UtilityLibrary.DataLoading;

public static class BaseConfigLoader
{
    public static List<string> GetYamlFilesRecursive(string directory)
    {
        var files = new List<string>();
        
        if (!DirAccess.DirExistsAbsolute(directory))
        {
            GameLogger.Warning($"BaseConfigLoader: Directory not found: {directory}");
            return files;
        }

        var currentFiles = DirAccess.GetFilesAt(directory);
        foreach (var file in currentFiles)
        {
            if (file.EndsWith(".yaml") || file.EndsWith(".yml"))
                files.Add(directory + file);
        }

        var subdirs = DirAccess.GetDirectoriesAt(directory);
        foreach (var subdir in subdirs)
            files.AddRange(GetYamlFilesRecursive(directory + subdir + "/"));

        return files;
    }

    public static string ReadString(Dictionary<object, object> dict, string key, string fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;
        
        var value = dict[key];
        return value is string s ? s : value?.ToString() ?? fallback;
    }

    public static int ReadInt(Dictionary<object, object> dict, string key, int fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;
        
        return NodeToInt(dict[key], fallback);
    }

    public static float ReadFloat(Dictionary<object, object> dict, string key, float fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;
        
        return NodeToFloat(dict[key], fallback);
    }

    public static bool ReadBool(Dictionary<object, object> dict, string key, bool fallback)
    {
        if (!dict.ContainsKey(key))
            return fallback;
        
        var value = dict[key];
        if (value is bool b)
            return b;
        
        if (value is string s)
            return bool.TryParse(s, out bool result) && result;
        
        return fallback;
    }

    public static Dictionary<string, int> ReadResourceDict(Dictionary<object, object> dict, string key)
    {
        var result = new Dictionary<string, int>();
        
        if (!dict.ContainsKey(key))
            return result;
        
        if (dict[key] is not Dictionary<object, object> resourceDict)
            return result;
        
        foreach (var kvp in resourceDict)
        {
            string resourceName = kvp.Key?.ToString() ?? "";
            int amount = NodeToInt(kvp.Value, 0);
            
            if (!string.IsNullOrEmpty(resourceName) && amount > 0)
                result[resourceName] = amount;
        }
        
        return result;
    }

    public static List<string> ReadStringList(Dictionary<object, object> dict, string key)
    {
        var list = new List<string>();
        
        if (!dict.ContainsKey(key))
            return list;
        
        if (dict[key] is not List<object> items)
            return list;
        
        foreach (var item in items)
        {
            if (item is string s)
                list.Add(s);
        }
        
        return list;
    }

    public static int NodeToInt(object node, int fallback)
    {
        try
        {
            if (node is long l) return (int)l;
            if (node is int i) return i;
            if (node is double d) return (int)d;
            if (node is float f) return (int)f;
            
            var s = node?.ToString();
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch (Exception e)
        {
            GD.PrintErr($"BaseConfigLoader: Error parsing node to int: {e.Message}");
        }
        
        return fallback;
    }

    public static float NodeToFloat(object node, float fallback)
    {
        try
        {
            if (node is long l) return (float)l;
            if (node is double d) return (float)d;
            if (node is float f) return f;
            if (node is int i) return (float)i;
            
            var s = node?.ToString();
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        catch (Exception e)
        {
            GD.PrintErr($"BaseConfigLoader: Error parsing node to float: {e.Message}");
        }
        
        return fallback;
    }
}
```

### StationConfigLoader Implementation

```csharp
// Scripts/UtilityLibrary/DataLoading/StationConfigLoader.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Logistics;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<string, object>;

namespace UtilityLibrary.DataLoading;

public static class StationConfigLoader
{
    private const string StationsDirectory = "res://Configuration/stations/";
    private const string StationTemplatesPath = "res://Configuration/stations/StationTemplates.yaml";

    private static List<StationDefinition>? _allStations;
    private static HashSet<string>? _inferredCategories;
    private static Dictionary<string, string>? _stationSourceFiles;
    private static List<StationTemplateCategory>? _templateCategories;

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

    public static List<string> GetStationCategories()
    {
        LoadAllStations(); // Ensure stations are loaded
        return _inferredCategories?.ToList() ?? new List<string>();
    }

    public static StationDefinition? GetStationByName(string name)
    {
        var stations = LoadAllStations();
        return stations.FirstOrDefault(s => 
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

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
        };
    }
}
```

---

## File Summary

### New Files (10)
1. `Scripts/UtilityLibrary/DataLoading/BaseConfigLoader.cs`
2. `Scripts/UtilityLibrary/DataLoading/StationConfigLoader.cs`
3. `Scripts/UtilityLibrary/DataLoading/ShipConfigLoader.cs`
4. `Scripts/UtilityLibrary/DataLoading/EngineConfigLoader.cs`
5. `Scripts/Structures/Logistics/StationDefinition.cs`
6. `Scripts/Structures/Logistics/ShipDefinition.cs`
7. `Scripts/Structures/Logistics/EngineDefinition.cs`
8. `Scripts/Structures/Logistics/StationTemplateCategory.cs`
9. `Scripts/Structures/Logistics/ShipTemplateCategory.cs`
10. `Scripts/Structures/Logistics/EngineTypeCategory.cs`

### Modified Files (2)
1. `Scripts/Logistics/Resources/StationDatabase.cs` - Use new loader
2. `Scripts/Logistics/Resources/ShipDatabase.cs` - Use new loader

### Deleted Files (1)
1. `Scripts/UtilityLibrary/LogisticsConfigLoader.cs`

### New Test Files (4)
1. `Tests/Logistics/StationConfigLoaderTest.cs`
2. `Tests/Logistics/ShipConfigLoaderTest.cs`
3. `Tests/Logistics/EngineConfigLoaderTest.cs`
4. `Tests/Logistics/LogisticsLoadingIntegrationTest.cs`

### Updated Documentation (1)
1. `AGENTS.md`

### New Documentation (3)
1. `Docs/Configuration/StationLoading.md`
2. `Docs/Configuration/ShipLoading.md`
3. `Docs/Configuration/EngineLoading.md`

---

## Breaking Changes

Since backward compatibility is not required, this is a complete breaking change:

### API Changes
- `LogisticsConfigLoader` class **deleted entirely**
- `StationDefinition`, `ShipDefinition`, `EngineDefinition` moved to `Structures.Logistics` namespace
- All callers must update imports and method calls

### Migration Required For
1. `StationDatabase.cs` - Update imports and loader call
2. `ShipDatabase.cs` - Update imports and loader call

### Migration Path
1. Update imports from `UtilityLibrary` to `Structures.Logistics` for definition classes
2. Update loader calls from `LogisticsConfigLoader.*` to `StationConfigLoader.*`, etc.
3. Delete `LogisticsConfigLoader.cs`

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Import/namespacing issues | Medium | Medium | Carefully update all using statements; compile frequently |
| Missing references after deletion | Low | High | Verify all references removed before deleting file |
| Cache behavior differences | Low | Medium | Thorough testing of cache clear/reload |
| Test failures | Medium | Low | Update tests to use new loaders and namespaces |

---

## Success Criteria

1. ✅ `LogisticsConfigLoader.cs` deleted
2. ✅ All 3 new loader classes created in `DataLoading/` directory
3. ✅ `BaseConfigLoader` created with shared utilities
4. ✅ All definition classes moved to `Structures/Logistics/`
5. ✅ `StationDatabase` and `ShipDatabase` updated to use new loaders
6. ✅ Directory scanning works for all `.yaml` and `.yml` files
7. ✅ Subdirectory organization is supported
8. ✅ Categories are inferred from entity definitions
9. ✅ Duplicate detection works across files
10. ✅ Validation warnings for unknown types
11. ✅ All existing tests pass (after updates)
12. ✅ New tests created for each loader
13. ✅ Documentation updated

---

## Estimates

| Phase | Tickets | Estimated Effort |
|-------|---------|------------------|
| Phase 1: Shared Infrastructure | 1 ticket | 0.5 day |
| Phase 2: Move Definitions | 1 ticket | 0.5 day |
| Phase 3: Create Loaders | 3 tickets | 2-3 days |
| Phase 4: Update Dependent Code | 2 tickets | 0.5-1 day |
| Phase 5: Remove Old Code | 1 ticket | 0.5 day |
| Phase 6: Testing | 4 tickets | 3-4 days |
| Phase 7: Documentation | 2 tickets | 1 day |
| **Total** | **14 tickets** | **8-11 days** |

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-04-06 | Refactor all three systems | User wants architectural consistency |
| 2026-04-06 | Warning-only for unknown types | User preference for non-blocking validation |
| 2026-04-06 | Keep template files for validation | Provides value for typo detection |
| 2026-04-06 | Support `.yaml` and `.yml` | Common convention |
| 2026-04-06 | Separate loader classes | User request for better organization |
| 2026-04-06 | Place in DataLoading directory | Follows existing pattern |
| 2026-04-06 | Create BaseConfigLoader | Share common utilities, reduce duplication |
| 2026-04-06 | Move definitions to Structures/Logistics | Better separation of data and loading |
| 2026-04-06 | **No backward compatibility** | User explicitly stated; simplifies design |
| 2026-04-06 | Delete LogisticsConfigLoader entirely | No longer needed without backward compatibility |

---

## Appendix: Example Migrations

### StationDatabase Migration

**Before**:
```csharp
using LogisticsConfigLoader = UtilityLibrary.LogisticsConfigLoader;
using StationDefinition = UtilityLibrary.StationDefinition;
// ...
var allStations = LogisticsConfigLoader.LoadAllStations();
```

**After**:
```csharp
using UtilityLibrary.DataLoading;
using Structures.Logistics;
// ...
var allStations = StationConfigLoader.LoadAllStations();
```

### Adding a New Station

**Before** (old system):
1. Add category to `StationTemplates.yaml`
2. Create file matching category name exactly
3. Add station to that file

**After** (new system):
1. Create any `.yaml` file in `Configuration/stations/` (or subdirectory)
2. Add station definition with `station_type` field
3. Done! Category is inferred automatically

```yaml
# Configuration/stations/industrial/custom_stations.yaml
stations:
  - name: Custom_Station
    station_type: Industrial
    construction_time: 100.0
    can_build_ships: false
    can_build_buildings: true
    required_resources:
      Steel: 500
```
