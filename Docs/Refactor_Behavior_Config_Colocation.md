# Refactor: Behavior Config Colocation

## Problem

Building YAML configs scatter behavior-specific settings across disconnected top-level sections. The `behaviors:` list contains bare ID strings, while their configuration lives in separate sections (`production:`, `power:`, `extraction:`, `transfer_station:`, `storage_capacity:`, `slot_filters:`, `starting_stockpiles:`). Mismatches between behavior IDs and their config sections cause silent bugs. `BuildingDefinition.ApplyPowerToBehavior()` switch-hammers values from sub-definitions onto behavior instances — adding a new behavior requires editing the loader, the switch statement, and the YAML schema.

## Solution

Move all behavior-specific config inline under `behaviors:`. Each entry becomes a mapping table: first key is `behavior_id`, remaining keys are that behavior's config. A new generic `IBehaviorConfigurable` interface lets each behavior parse its own config dict, eliminating the central switch statement entirely. Top-level sub-definition types on `BuildingDefinition` are removed.

---

## YAML Format Change

### Before (Wind Turbine)

```yaml
production:
  default_recipe: "wind_analysis"
  alternative_recipes: ["turbine_maintenance", "grid_synchronization"]
  production_speed: 2
power:
  grid_radius: 4
  output: 75
  is_renewable: true
  min_atmosphere: 0.1
  max_atmosphere: 5.0
  reference_atmosphere: 1.0
behaviors:
  - ManufacturingBehavior
  - WindPowerProducerBehavior
```

### After

```yaml
behaviors:
  - behavior_id: ManufacturingBehavior
    default_recipe: "wind_analysis"
    alternative_recipes: ["turbine_maintenance", "grid_synchronization"]
    production_speed: 2
  - behavior_id: WindPowerProducerBehavior
    grid_radius: 4
    output: 75
    is_renewable: true
    reference_atmosphere: 1.0

placement_requirements:
  biomes: [category:flat, category:ocean, category:mountain]
  min_elevation: 0.3
  max_elevation: 0.9
  max_slope: 25.0
  cell_count: 1
  requires_adjacent: false
  configurable_behavior:
    behavior_class: AtmospherePlacementBehavior
    min_atmosphere: 0.1
    max_atmosphere: 5.0
```

### Before (Company Headquarters)

```yaml
production:
  default_recipe: "hq_all_in_one_operation"
  alternative_recipes: ["hq_power_focus", "hq_extraction_focus", "hq_fabrication_focus"]
  production_speed: 1.0
starting_stockpiles:
  concrete: 100
  iron: 50
  copper: 30
  water: 200
  grain: 50
storage_capacity: 30
slot_filters:
  any: 12
  category:ore: 3
  category:raw_material: 3
  category:fuel: 3
  category:food: 3
  category:construction: 3
  category:industrial: 3
transfer_station:
  cargo_capacity: 500.0
  vehicle_speed: 50.0
  max_concurrent_transfers: 2
behaviors:
  - StorageHubBehavior
  - TransferStationBehavior
  - TransportHubBehavior
  - InitialStockpileBehavior
  - ManufacturingBehavior
  - GameStartBehavior
```

### After

```yaml
behaviors:
  - behavior_id: StorageHubBehavior
    storage_capacity: 30
    slot_filters:
      any: 12
      category:ore: 3
      category:raw_material: 3
      category:fuel: 3
      category:food: 3
      category:construction: 3
      category:industrial: 3
  - behavior_id: TransferStationBehavior
    cargo_capacity: 500.0
    vehicle_speed: 50.0
    max_concurrent_transfers: 2
  - behavior_id: TransportHubBehavior
  - behavior_id: InitialStockpileBehavior
    stockpiles:
      concrete: 100
      iron: 50
      copper: 30
      water: 200
      grain: 50
  - behavior_id: ManufacturingBehavior
    default_recipe: "hq_all_in_one_operation"
    alternative_recipes: ["hq_power_focus", "hq_extraction_focus", "hq_fabrication_focus"]
    production_speed: 1.0
  - behavior_id: GameStartBehavior
```

---

## Config Mapping: Old Sections → New Inline Behavior Entries

| Old Section | Old Key(s) | New `behavior_id` | New Inline Key(s) |
|---|---|---|---|
| `production` | `default_recipe` | `ManufacturingBehavior` | `default_recipe` |
| `production` | `alternative_recipes` | `ManufacturingBehavior` | `alternative_recipes` |
| `production` | `production_speed` | `ManufacturingBehavior` | `production_speed` |
| `power` | `grid_radius`, `output`, `is_renewable` | `PowerProducerBehavior` | same names |
| `power` | `+ reference_distance` | `SolarPowerProducerBehavior` | same |
| `power` | `+ reference_atmosphere` | `WindPowerProducerBehavior` | same |
| `power` | `base_draw` | `PowerConsumerBehavior` | `base_draw` |
| `power` | `battery_capacity`, `grid_radius` | `BatteryBehavior` | `capacity`, `grid_radius` |
| `extraction` | `extract_types`, `rate_per_tick`, `work_per_cycle` | `ExtractionBehavior` | same names |
| `storage_capacity` + `slot_filters` | both | `StorageHubBehavior` | `storage_capacity`, `slot_filters` |
| `transfer_station` | `cargo_capacity`, `vehicle_speed`, `max_concurrent_transfers` | `TransferStationBehavior` | same names |
| `starting_stockpiles` | dict | `InitialStockpileBehavior` | `stockpiles` |
| — | — | `GameStartBehavior` | no config |
| — | — | `TransportHubBehavior` | no config |
| — | — | `BulkStorageRoutingBehavior` | no config (auto-attached) |

### Atmosphere Placement Bounds

| Old Location | Old Key | New Location | New Key |
|---|---|---|---|
| `power.min_atmosphere` | `min_atmosphere` | `placement_requirements.configurable_behavior` | `min_atmosphere` |
| `power.max_atmosphere` | `max_atmosphere` | `placement_requirements.configurable_behavior` | `max_atmosphere` |

The `configurable_behavior` field in `placement_requirements` also changes from a bare string to an inline config table:

```yaml
placement_requirements:
  configurable_behavior:
    behavior_class: AtmospherePlacementBehavior
    min_atmosphere: 0.1
    max_atmosphere: 5.0
```

---

## Architecture: Generic Behavior Config System

### New Interface: `IBehaviorConfigurable`

```csharp
// File: Scripts/Constructables/Buildings/IBehaviorConfigurable.cs
namespace Constructables.Buildings;

/// <summary>
/// Marks a behavior as accepting YAML-driven configuration. The loader
/// calls Configure() after instantiation but before OnAttach(), giving
/// the behavior a chance to read its inline config dict from the
/// behaviors: section of the building YAML.
/// Adding a new configurable behavior only requires implementing this
/// interface — no loader or factory changes needed.
/// </summary>
public interface IBehaviorConfigurable
{
    void Configure(Dictionary<string, object> config);
}
```

### New Type: `BehaviorConfigEntry`

```csharp
// Added to BuildingDefinition.cs
public class BehaviorConfigEntry
{
    public string BehaviorId { get; set; } = "";
    public Dictionary<string, object> Config { get; set; } = new();
}
```

### Updated `BehaviorFactory`

```csharp
// File: Scripts/Constructables/Buildings/BehaviorFactory.cs
public static IBuildingBehavior? Create(
    string nameOrPath,
    Dictionary<string, object>? config = null)
{
    if (string.IsNullOrWhiteSpace(nameOrPath))
        return null;

    try
    {
        IBuildingBehavior? behavior;
        if (nameOrPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            behavior = CreateFromScript(nameOrPath);
        else
            behavior = CreateByName(nameOrPath);

        if (behavior is IBehaviorConfigurable configurable && config != null)
            configurable.Configure(config);

        return behavior;
    }
    catch (Exception ex)
    {
        GD.PrintErr($"BehaviorFactory: Failed to instantiate behavior '{nameOrPath}': {ex.Message}");
        return null;
    }
}
```

The parameterless `Create(string)` overload remains for StationSatellite and other callers that don't have config dicts.

---

## Tickets

### Ticket 1: Add `IBehaviorConfigurable` Interface + `BehaviorConfigEntry` Type

**Files to modify:**
- `Scripts/Constructables/Buildings/IBehaviorConfigurable.cs` (NEW)
- `Scripts/Structures/Resources/BuildingDefinition.cs`

**What:**
1. Create `IBehaviorConfigurable` interface (see architecture section above)
2. Add `BehaviorConfigEntry` class to `BuildingDefinition`
3. Change `BehaviorRefs` property type from `Godot.Collections.Array<string>` to `Godot.Collections.Array<BehaviorConfigEntry>`
4. Add `using System.Collections.Generic;` if not already present (for `Dictionary<string, object>`)

**Validation:**
- `dotnet build` compiles
- No behavior changes yet (entry Config dicts will be empty until behaviors implement Configure)

---

### Ticket 2: Implement `IBehaviorConfigurable` on All Configurable Behaviors

**Files to modify:**

| File | Config Keys to Parse in `Configure()` |
|---|---|
| `Scripts/Constructables/Buildings/Behaviors/ManufacturingBehavior.cs` | `default_recipe` (string), `alternative_recipes` (string list), `production_speed` (float) |
| `Scripts/Constructables/Buildings/Behaviors/PowerProducerBehavior.cs` | `grid_radius` (int), `output` (float), `is_renewable` (bool) |
| `Scripts/Constructables/Buildings/Behaviors/SolarPowerProducerBehavior.cs` | inherits PowerProducerBehavior.Configure() + `reference_distance` (float) |
| `Scripts/Constructables/Buildings/Behaviors/WindPowerProducerBehavior.cs` | inherits PowerProducerBehavior.Configure() + `reference_atmosphere` (float) |
| `Scripts/Constructables/Buildings/Behaviors/GeothermalPowerProducerBehavior.cs` | inherits PowerProducerBehavior.Configure() (no extra keys) |
| `Scripts/Constructables/Buildings/Behaviors/PowerConsumerBehavior.cs` | `base_draw` (float) |
| `Scripts/Constructables/Buildings/Behaviors/BatteryBehavior.cs` | `capacity` (float), `grid_radius` (int) |
| `Scripts/Constructables/Buildings/Behaviors/ExtractionBehavior.cs` | `extract_types` (int), `rate_per_tick` (float), `work_per_cycle` (float) |
| `Scripts/Constructables/Buildings/Behaviors/StorageHubBehavior.cs` | `storage_capacity` (int), `slot_filters` (dict → List<SlotFilterSpec>) |
| `Scripts/Constructables/Buildings/Behaviors/TransferStationBehavior.cs` | `cargo_capacity` (float), `vehicle_speed` (float), `max_concurrent_transfers` (int) |
| `Scripts/Constructables/Buildings/Behaviors/InitialStockpileBehavior.cs` | `stockpiles` (dict string→int) |

**Implementation pattern (ManufacturingBehavior example):**

```csharp
public partial class ManufacturingBehavior : RefCounted, IBuildingBehavior, IBehaviorConfigurable
{
    // ... existing fields ...

    public string? DefaultRecipe { get; private set; }
    public IReadOnlyList<string> AlternativeRecipes => _alternativeRecipes;
    private List<string> _alternativeRecipes = new();
    public float ProductionSpeed { get; private set; } = 1.0f;

    public void Configure(Dictionary<string, object> config)
    {
        DefaultRecipe = ReadString(config, "default_recipe", null);
        _alternativeRecipes = ReadStringList(config, "alternative_recipes");
        ProductionSpeed = ReadFloat(config, "production_speed", 1.0f);
    }

    // Helper methods duplicated from BuildingConfigLoader or extracted to a shared util class:
    private static string? ReadString(Dictionary<string, object> d, string key, string? fallback)
    {
        if (!d.TryGetValue(key, out var val)) return fallback;
        return val?.ToString() ?? fallback;
    }

    private static float ReadFloat(Dictionary<string, object> d, string key, float fallback)
    {
        if (!d.TryGetValue(key, out var val)) return fallback;
        return NodeToFloat(val, fallback);
    }

    private static List<string> ReadStringList(Dictionary<string, object> d, string key)
    {
        var list = new List<string>();
        if (!d.TryGetValue(key, out var val)) return list;
        if (val is not List<object> items) return list;
        foreach (var item in items)
            if (item is string s) list.Add(s);
        return list;
    }

    private static int ReadInt(Dictionary<string, object> d, string key, int fallback) { ... }
    private static bool ReadBool(Dictionary<string, object> d, string key, bool fallback) { ... }
    private static float NodeToFloat(object node, float fallback) { ... copy from BuildingConfigLoader }
    private static int NodeToInt(object node, int fallback) { ... copy from BuildingConfigLoader }
}
```

**Shared helper extraction:** To avoid duplicating ReadString/ReadFloat/etc. across 11 behaviors, extract a static utility class:

```csharp
// File: Scripts/Constructables/Buildings/BehaviorConfigHelper.cs (NEW)
namespace Constructables.Buildings;

/// <summary>
/// Shared type-safe readers for behavior Configure() dicts.
/// Mirrors BuildingConfigLoader's NodeToXxx helpers but works on
/// Dictionary<string, object> instead of Dictionary<object, object>.
/// </summary>
public static class BehaviorConfigHelper
{
    public static string? ReadString(Dictionary<string, object> d, string key, string? fallback) { ... }
    public static float ReadFloat(Dictionary<string, object> d, string key, float fallback) { ... }
    public static int ReadInt(Dictionary<string, object> d, string key, int fallback) { ... }
    public static bool ReadBool(Dictionary<string, object> d, string key, bool fallback) { ... }
    public static List<string> ReadStringList(Dictionary<string, object> d, string key) { ... }
    public static Dictionary<string, int> ReadStringIntDict(Dictionary<string, object> d, string key) { ... }
    public static float NodeToFloat(object node, float fallback) { ... }
    public static int NodeToInt(object node, int fallback) { ... }
}
```

**Behaviors with no config (GameStartBehavior, TransportHubBehavior, BulkStorageRoutingBehavior):** Do not implement `IBehaviorConfigurable`. No changes needed beyond what's already there.

**New accessors on ManufacturingBehavior for Building.SwapRecipe:**

Currently `Building.SwapRecipe` reads `Definition.Production.DefaultRecipe` and `Definition.Production.AlternativeRecipes`. After this ticket, those reads must come from the behavior:

```csharp
// ManufacturingBehavior.cs — new public accessors
public string? DefaultRecipe { get; private set; }
public IReadOnlyList<string> AlternativeRecipes => _alternativeRecipes;
```

**Validation:**
- `dotnet build` compiles
- Each behavior's `Configure()` method can be unit-tested with a simple dict (no Godot runtime needed for most)

---

### Ticket 3: Update `BehaviorFactory.Create` to Accept Config

**File:** `Scripts/Constructables/Buildings/BehaviorFactory.cs`

**What:**
1. Add new overload:
   ```csharp
   public static IBuildingBehavior? Create(
       string nameOrPath,
       Dictionary<string, object>? config = null)
   ```
2. After instantiation (CreateFromScript or CreateByName), check `is IBehaviorConfigurable` and call `Configure(config)` if config is non-null
3. Keep existing `Create(string nameOrPath)` overload for callers without config (StationSatellite, tests) — delegates to new overload with `config = null`

**Validation:**
- `dotnet build` compiles
- Existing callers unchanged

---

### Ticket 4: Rewrite `BuildingConfigLoader` — Parse Inline Behavior Entries

**File:** `Scripts/UtilityLibrary/DataLoading/BuildingConfigLoader.cs`

**What:**

#### 4a. Replace `ParseBehaviorRefs` with `ParseBehaviorEntries`

New method parses `behaviors:` list where each entry is either:
- A bare string → `BehaviorConfigEntry` with `BehaviorId = s`, `Config = empty`
- A mapping dict → `BehaviorConfigEntry` with `BehaviorId = dict["behavior_id"]`, `Config = dict minus "behavior_id" key`

```csharp
private static void ParseBehaviorEntries(
    Dictionary<object, object> dict,
    BuildingDefinition definition)
{
    if (!dict.ContainsKey("behaviors"))
        return;

    if (dict["behaviors"] is not List<object> behaviorList)
        return;

    foreach (var entry in behaviorList)
    {
        if (entry is string s && !string.IsNullOrEmpty(s))
        {
            definition.BehaviorEntries.Add(new BehaviorConfigEntry
            {
                BehaviorId = s,
                Config = new Dictionary<string, object>()
            });
        }
        else if (entry is Dictionary<object, object> entryDict)
        {
            string behaviorId = ReadString(entryDict, "behavior_id", "");
            if (string.IsNullOrEmpty(behaviorId))
            {
                GD.PrintErr("BuildingConfigLoader: behavior entry missing 'behavior_id' key");
                continue;
            }

            var config = new Dictionary<string, object>();
            foreach (var kvp in entryDict)
            {
                string key = kvp.Key?.ToString() ?? "";
                if (key == "behavior_id") continue;
                config[key] = kvp.Value;
            }

            definition.BehaviorEntries.Add(new BehaviorConfigEntry
            {
                BehaviorId = behaviorId,
                Config = config
            });
        }
    }
}
```

#### 4b. Remove obsolete parsing methods

Delete these methods entirely:
- `ParseProductionDefinition`
- `ParsePowerDefinition`
- `ParseExtractionDefinition`
- `ParseTransferStationDefinition`
- `ParseStartingStockpiles`
- `ParseStorageCapacity`
- `ParseSlotFilters`

#### 4c. Update `ParseBuildingDefinition`

Remove assignments to deleted sub-definitions:
```csharp
// REMOVE these lines from ParseBuildingDefinition:
Production = ParseProductionDefinition(dict),
Power = ParsePowerDefinition(dict),
Extraction = ParseExtractionDefinition(dict),
TransferStation = ParseTransferStationDefinition(dict),
StartingStockpiles = ParseStartingStockpiles(dict),
StorageCapacity = ParseStorageCapacity(dict),
SlotFilters = ParseSlotFilters(dict),
```

Replace `ParseBehaviorRefs(dict, definition)` call with `ParseBehaviorEntries(dict, definition)`.

Remove the AtmospherePlacement cross-reference block (lines 122-126):
```csharp
// REMOVE:
if (definition.Placement.ConfigurableBehavior is AtmospherePlacementBehavior atm)
{
    atm.MinAtmosphere = definition.Power.MinAtmosphere;
    atm.MaxAtmosphere = definition.Power.MaxAtmosphere;
}
```

#### 4d. Update `LoadPlacementBehavior` for inline config

Change `configurable_behavior` parsing in `ParsePlacementRequirements` to accept a mapping dict instead of a bare string:

```yaml
# New format:
configurable_behavior:
  behavior_class: AtmospherePlacementBehavior
  min_atmosphere: 0.1
  max_atmosphere: 5.0
```

```csharp
private static IPlacementBehavior? LoadPlacementBehavior(
    Dictionary<object, object> placementDict,
    BuildingDefinition.PlacementRequirements requirements)
{
    if (!placementDict.ContainsKey("configurable_behavior"))
        return null;

    var behaviorValue = placementDict["configurable_behavior"];
    if (behaviorValue == null)
        return null;

    // Support legacy bare string format (warn)
    if (behaviorValue is string bareString)
    {
        GD.PushWarning(
            "BuildingConfigLoader: Bare string 'configurable_behavior' is deprecated. " +
            "Use mapping with 'behavior_class' key and inline config.");
        return InstantiatePlacementBehavior(bareString, requirements);
    }

    if (behaviorValue is not Dictionary<object, object> behaviorDict)
        return null;

    string className = ReadString(behaviorDict, "behavior_class", "");
    if (string.IsNullOrWhiteSpace(className))
    {
        GD.PrintErr("BuildingConfigLoader: configurable_behavior mapping missing 'behavior_class' key");
        return null;
    }

    // Extract config keys (everything except behavior_class)
    var config = new Dictionary<string, object>();
    foreach (var kvp in behaviorDict)
    {
        string key = kvp.Key?.ToString() ?? "";
        if (key == "behavior_class") continue;
        config[key] = kvp.Value;
    }

    var behavior = InstantiatePlacementBehavior(className, requirements);

    // Apply inline config (e.g., min_atmosphere, max_atmosphere)
    if (behavior is AtmospherePlacementBehavior atm)
    {
        if (config.TryGetValue("min_atmosphere", out var minAtm))
            atm.MinAtmosphere = NodeToFloat(minAtm, atm.MinAtmosphere);
        if (config.TryGetValue("max_atmosphere", out var maxAtm))
            atm.MaxAtmosphere = NodeToFloat(maxAtm, atm.MaxAtmosphere);
    }

    return behavior;
}
```

**Validation:**
- `dotnet build` compiles
- Loading a YAML file with new format produces `BehaviorConfigEntry` items with populated `Config` dicts
- Loading a YAML file without `behaviors:` key doesn't crash (empty list)

---

### Ticket 5: Remove Sub-Definition Types from `BuildingDefinition`

**File:** `Scripts/Structures/Resources/BuildingDefinition.cs`

**Remove these properties:**
```csharp
public ProductionDefinition Production { get; set; } = new();
public PowerDefinition Power { get; set; } = new();
public ExtractionDefinition Extraction { get; set; } = new();
public TransferStationDefinition? TransferStation { get; set; }
public Dictionary<string, int> StartingStockpiles { get; set; } = new();
public int StorageCapacity { get; set; } = 0;
public List<SlotFilterSpec> SlotFilters { get; set; } = new();
```

**Remove these nested classes:**
```csharp
public class ProductionDefinition { ... }
public class PowerDefinition { ... }
public class ExtractionDefinition { ... }
```

**Remove method:** `ApplyPowerToBehavior()` (lines 100-143) — all config now flows through `IBehaviorConfigurable.Configure()`.

**Update `Instantiate()` method:**

```csharp
public Building Instantiate()
{
    var building = new Building();
    building.Id = System.Guid.NewGuid().ToString();
    building.ApplyDefinition(this);

    // Attach behaviors from entries (config flows through IBehaviorConfigurable)
    foreach (var entry in BehaviorEntries)
    {
        var behavior = Constructables.Buildings.BehaviorFactory.Create(
            entry.BehaviorId, entry.Config);
        if (behavior == null)
            continue;
        building.Behaviors.Add(behavior);
        behavior.OnAttach(building);
    }

    // Set initial active recipe from ManufacturingBehavior
    var mfg = building.GetBehavior<Constructables.Buildings.Behaviors.ManufacturingBehavior>();
    if (mfg != null && !string.IsNullOrEmpty(mfg.DefaultRecipe))
        building.ActiveRecipeId = mfg.DefaultRecipe;

    // Build resource nodes from NodeLayout
    foreach (var spec in NodeLayout)
        building.Nodes.Add(spec.Build(building));

    // Auto-attach BulkStorageRoutingBehavior if StorageHubBehavior was configured
    var hub = building.GetBehavior<Constructables.Buildings.Behaviors.StorageHubBehavior>();
    if (hub != null && hub.StorageCapacity > 0
        && building.GetBehavior<Constructables.Buildings.Behaviors.BulkStorageRoutingBehavior>() == null)
    {
        var routing = new Constructables.Buildings.Behaviors.BulkStorageRoutingBehavior();
        building.Behaviors.Add(routing);
        routing.OnAttach(building);
    }

    return building;
}
```

**Also remove:** `SoundDefinition` nested class and `Sound` property — this is out of scope for this refactor but they're not consumed anywhere meaningfully. Actually, leave `Sound` alone — it's not part of the behavior config issue. Only remove the types listed above.

**Validation:**
- `dotnet build` — will fail because consumers still reference removed properties. That's expected; those get fixed in tickets 6-12.
- After all consumer tickets are done, `dotnet build` must compile clean.

---

### Ticket 6: Update `Building.cs` Consumers

**File:** `Scripts/Constructables/Buildings/Building.cs`

**Changes:**

#### 6a. `SwapRecipe()` (lines 406-449)

```csharp
// BEFORE:
if (Definition?.Production == null)
    return false;
var production = Definition.Production;
bool isDefault = !string.IsNullOrEmpty(production.DefaultRecipe)
    && production.DefaultRecipe == recipeId;
bool isAlternative = production.AlternativeRecipes.Contains(recipeId);

// AFTER:
var mfg = GetBehavior<ManufacturingBehavior>();
if (mfg == null)
    return false;
bool isDefault = !string.IsNullOrEmpty(mfg.DefaultRecipe)
    && mfg.DefaultRecipe == recipeId;
bool isAlternative = mfg.AlternativeRecipes.Contains(recipeId);
```

#### 6b. `TryStartManufacturingCycleFromRegistration()` (lines 487-516)

```csharp
// BEFORE:
if (Definition?.Production == null)
    return;
string? recipeId = ActiveRecipeId ?? Definition.Production.DefaultRecipe;

// AFTER:
var mfg = GetBehavior<ManufacturingBehavior>();
if (mfg == null)
    return;
string? recipeId = ActiveRecipeId ?? mfg.DefaultRecipe;
```

#### 6c. `ApplyDefinition()` (lines 283-313)

```csharp
// BEFORE:
if (definition.StorageCapacity > 0 && GetBehavior<BulkStorageRoutingBehavior>() == null)

// AFTER:
var hub = GetBehavior<StorageHubBehavior>();
if (hub != null && hub.StorageCapacity > 0 && GetBehavior<BulkStorageRoutingBehavior>() == null)
```

Note: Since `ApplyDefinition` is called before behaviors are added by `Instantiate()`, the auto-attach of `BulkStorageRoutingBehavior` must happen in `Instantiate()` instead (see Ticket 5). Remove the auto-attach block from `ApplyDefinition` entirely.

**Validation:**
- `dotnet build` compiles
- SwapRecipe rejects unknown recipe IDs
- SwapRecipe accepts valid default and alternative recipe IDs

---

### Ticket 7: Update `TransferStationBehavior.cs`

**File:** `Scripts/Constructables/Buildings/Behaviors/TransferStationBehavior.cs`

**What:**

#### 7a. Implement `IBehaviorConfigurable`

```csharp
public partial class TransferStationBehavior : RefCounted, IBuildingBehavior, IBehaviorConfigurable
{
    // Store config locally instead of reading from Definition
    private float _cargoCapacity;
    private float _vehicleSpeed;
    private int _maxConcurrentTransfers;

    public void Configure(Dictionary<string, object> config)
    {
        _cargoCapacity = BehaviorConfigHelper.ReadFloat(config, "cargo_capacity", 500.0f);
        _vehicleSpeed = BehaviorConfigHelper.ReadFloat(config, "vehicle_speed", 50.0f);
        _maxConcurrentTransfers = BehaviorConfigHelper.ReadInt(config, "max_concurrent_transfers", 2);
    }
```

#### 7b. Replace `Definition.TransferStation` reads with local fields

In `OnRegister()`:
```csharp
// BEFORE:
if (_owner.Definition?.TransferStation == null) { ... skip ... }
_endpointDef = _owner.Definition.TransferStation;
_body.RegisterTransferEndpoint(_owner.Id, _endpointDef, _owner);

// AFTER:
if (_cargoCapacity <= 0f) { ... skip ... }
_endpointDef = new TransferStationDefinition
{
    CargoCapacity = _cargoCapacity,
    VehicleSpeed = _vehicleSpeed,
    MaxConcurrentTransfers = _maxConcurrentTransfers,
};
_body.RegisterTransferEndpoint(_owner.Id, _endpointDef, _owner);
```

In `GetCapacity`, `GetMaxConcurrentTransfers`, `GetVehicleSpeed` — return local fields instead of `_endpointDef` reads.

**Validation:**
- `dotnet build` compiles
- TransferStationBehavior works without `Definition.TransferStation`

---

### Ticket 8: Update `InitialStockpileBehavior.cs`

**File:** `Scripts/Constructables/Buildings/Behaviors/InitialStockpileBehavior.cs`

**What:**

```csharp
public partial class InitialStockpileBehavior : IBuildingBehavior, IBehaviorConfigurable
{
    private Dictionary<string, int> _stockpiles = new();

    public void Configure(Dictionary<string, object> config)
    {
        _stockpiles = BehaviorConfigHelper.ReadStringIntDict(config, "stockpiles");
    }

    public void OnRegister()
    {
        if (_owner == null) return;
        if (_stockpiles.Count == 0) return;

        foreach (var kvp in _stockpiles)
        {
            // ... same deposit logic, reading from _stockpiles instead of definition
        }
    }
```

**Validation:**
- `dotnet build` compiles
- InitialStockpileBehavior works without `Definition.StartingStockpiles`

---

### Ticket 9: Update `AtmospherePlacementBehavior.cs`

**File:** `Scripts/Structures/Resources/AtmospherePlacementBehavior.cs`

**What:**

The atmosphere bounds now come from the `configurable_behavior` inline config in `placement_requirements`, not from `BuildingDefinition.Power`. The `BuildingConfigLoader.LoadPlacementBehavior` change (Ticket 4d) already applies them. No further changes needed in this file — its `MinAtmosphere`/`MaxAtmosphere` properties are already settable. Just verify the loader sets them correctly.

**Validation:**
- `dotnet build` compiles
- AtmospherePlacementBehavior gets bounds from placement_requirements config, not Power

---

### Ticket 10: Migrate All YAML Files to New Format

**Files to modify:**

| File | Changes |
|---|---|
| `Configuration/Buildings/example_building.yaml` | Remove `production:`, `visual:`, `sound:`. Move `production` keys into ManufacturingBehavior entry. Update example format docs. |
| `Configuration/Buildings/Power/Wind.yaml` | Remove `production:`, `power:`. Move keys into ManufacturingBehavior and WindPowerProducerBehavior entries. Move `min_atmosphere`/`max_atmosphere` to `configurable_behavior` inline config. |
| `Configuration/Buildings/Power/Solar.yaml` | Remove `production:`, `power:`. Move keys into ManufacturingBehavior and SolarPowerProducerBehavior entries. |
| `Configuration/Buildings/Power/Geothermal.yaml` | Remove `production:`, `power:`. Move keys into ManufacturingBehavior and GeothermalPowerProducerBehavior entries. |
| `Configuration/Buildings/Power/PowerPlant.yaml` | Remove `production:`, `power:`. Move keys into ManufacturingBehavior and PowerProducerBehavior entries (x3 buildings). |
| `Configuration/Buildings/Extraction/Mine.yaml` | Remove `production:`, `extraction:`, `power:`. Move keys into ManufacturingBehavior, ExtractionBehavior, PowerConsumerBehavior entries (x2 buildings). |
| `Configuration/Buildings/Extraction/DeepSeaMine.yaml` | No `behaviors:` block. Add one with entries from `production:`. |
| `Configuration/Buildings/Agriculture/Farm.yaml` | Remove `production:`. Move keys into ManufacturingBehavior entries (x2 buildings). |
| `Configuration/Buildings/Logistics/TransferStation.yaml` | Remove `production:`, `transfer_station:`, `storage_capacity:`, `slot_filters:`. Move into StorageHubBehavior and TransferStationBehavior entries (x2 buildings). |
| `Configuration/Buildings/Administration/CompanyHeadquarters.yaml` | Remove `production:`, `starting_stockpiles:`, `storage_capacity:`, `slot_filters:`, `transfer_station:`. Move into StorageHubBehavior, TransferStationBehavior, InitialStockpileBehavior, ManufacturingBehavior entries. |
| `Configuration/Buildings/Administration/BusinessAdmin.yaml` | No `behaviors:` block. No `production:`. No changes needed (unless we want to add an empty behaviors list for consistency). |

**Detailed migration per file:**

#### Wind.yaml

```yaml
buildings:
  - id_name: wind_turbine
    display_name: Wind Turbine Farm
    description: Cluster of high-efficiency wind turbines for harnessing atmospheric energy flows.
    category: power
    building_time: 5.0
    work_required: 4.0

    placement_requirements:
      biomes: [category:flat, category:ocean, category:mountain]
      min_elevation: 0.3
      max_elevation: 0.9
      max_slope: 25.0
      cell_count: 1
      requires_adjacent: false
      configurable_behavior:
        behavior_class: AtmospherePlacementBehavior
        min_atmosphere: 0.1
        max_atmosphere: 5.0

    required_resources:
      aluminum: 20
      concrete: 5

    behaviors:
      - behavior_id: ManufacturingBehavior
        default_recipe: "wind_analysis"
        alternative_recipes: ["turbine_maintenance", "grid_synchronization"]
        production_speed: 2
      - behavior_id: WindPowerProducerBehavior
        grid_radius: 4
        output: 75
        is_renewable: true
        reference_atmosphere: 1.0

    visual:
      model_path: "res://Models/Buildings/wind_turbine.glb"
      scale: 1.0
      rotation_offset: [0, 0, 0]

    icon:
      base_path: "res://Assets/Icons/Buildings/power/wind_turbine"
```

#### Mine.yaml

```yaml
buildings:
  - id_name: mountain_extractor
    display_name: Mountain Mineral Extractor
    description: Heavy-duty mining facility built into mountain sides for extracting high-value ores and minerals.
    category: extraction
    building_time: 600.0
    work_required: 120.0

    placement_requirements:
      biomes: [category:mountain, category:rocky, VolcanicPlain]
      min_elevation: 0.6
      max_elevation: 1.0
      max_slope: 30.0
      cell_count: 2
      requires_adjacent: false

    required_resources:
      steel: 400
      tungsten: 100
      concrete: 300
      explosives: 50
      water: 200

    behaviors:
      - behavior_id: ManufacturingBehavior
        default_recipe: "tunnel_boring"
        alternative_recipes: ["ore_processing", "smelting"]
        production_speed: 3
      - behavior_id: ExtractionBehavior
        extract_types: 2
        rate_per_tick: 8
        work_per_cycle: 5
      - behavior_id: PowerConsumerBehavior
        base_draw: 80

    nodes:
      - side: north
        kind: export
      - side: south
        kind: export

    visual:
      model_path: "res://Models/Buildings/mountain_extractor.glb"
      scale: 1.3
      rotation_offset: [0, 45, 0]

    icon:
      base_path: "res://Assets/Icons/Buildings/extraction/mountain_extractor"

  - id_name: surface_quarry
    # ... similar pattern ...
```

#### CompanyHeadquarters.yaml

```yaml
buildings:
  - id_name: company_headquarters
    display_name: Company Headquarters
    description: The nerve center of your interstellar enterprise.
    category: administration
    building_limit: 1
    building_time: 0
    work_required: 0
    demolishable: false
    allowed_recipe_category: headquarters

    placement_requirements:
      biomes: ["*"]
      min_elevation: 0.0
      max_elevation: 1.0
      max_slope: 90.0
      cell_count: 4
      requires_adjacent: true

    required_resources: {}

    behaviors:
      - behavior_id: StorageHubBehavior
        storage_capacity: 30
        slot_filters:
          any: 12
          category:ore: 3
          category:raw_material: 3
          category:fuel: 3
          category:food: 3
          category:construction: 3
          category:industrial: 3
      - behavior_id: TransferStationBehavior
        cargo_capacity: 500.0
        vehicle_speed: 50.0
        max_concurrent_transfers: 2
      - behavior_id: TransportHubBehavior
      - behavior_id: InitialStockpileBehavior
        stockpiles:
          concrete: 100
          iron: 50
          copper: 30
          water: 200
          grain: 50
      - behavior_id: ManufacturingBehavior
        default_recipe: "hq_all_in_one_operation"
        alternative_recipes: ["hq_power_focus", "hq_extraction_focus", "hq_fabrication_focus"]
        production_speed: 1.0
      - behavior_id: GameStartBehavior

    nodes:
      - side: north
        kind: flex
      - side: south
        kind: flex
      - side: east
        kind: flex
      - side: west
        kind: flex

    visual:
      model_path: "res://Models/Buildings/headquarters.glb"
      scale: 2.0
      rotation_offset: [0, 0, 0]

    icon:
      base_path: "res://Assets/Icons/Buildings/headquarters/company_headquarters"
```

**Validation:**
- Each YAML file loads without YamlValidator errors
- Each building instantiates without errors
- All behavior instances have correct config values

---

### Ticket 11: Update UI Consumers

**Files to modify:**

#### `Scripts/UI/Construction/BuildingPlacementMode.cs`

Currently reads `_definition.Power` for grid preview. After refactor, power info lives in behavior entries.

```csharp
// BEFORE:
var power = _definition.Power;
_previewWanted = power != null
    && power.GridRadius >= 0
    && (power.Output > 0f || power.BatteryCapacity > 0f);

// AFTER:
// Scan behavior entries for power contributor info
var powerEntry = _definition.BehaviorEntries
    .FirstOrDefault(e => e.BehaviorId is "PowerProducerBehavior"
        or "SolarPowerProducerBehavior"
        or "WindPowerProducerBehavior"
        or "GeothermalPowerProducerBehavior"
        or "BatteryBehavior");
int gridRadius = powerEntry != null
    ? BehaviorConfigHelper.ReadInt(powerEntry.Config, "grid_radius", -1)
    : -1;
float output = powerEntry != null
    ? BehaviorConfigHelper.ReadFloat(powerEntry.Config, "output", 0f)
    : 0f;
float batteryCap = powerEntry != null
    ? BehaviorConfigHelper.ReadFloat(powerEntry.Config, "capacity", 0f)
    : 0f;
_previewWanted = gridRadius >= 0 && (output > 0f || batteryCap > 0f);
```

Similarly for `UpdateGridPreview()`.

#### `Scripts/UI/BuildingInfo/BuildingInfoWindow.cs` (line 160)

```csharp
// BEFORE:
var production = _currentBuilding.Definition?.Production;

// AFTER:
var mfg = _currentBuilding.GetBehavior<ManufacturingBehavior>();
```

#### `Scripts/UI/BuildingInfo/BaseBuildingDetails.cs` (lines 51, 136, 138)

```csharp
// BEFORE:
string? recipeId = _building.ActiveRecipeId ?? _building.Definition?.Production?.DefaultRecipe;
float speed = definition.Production.ProductionSpeed;

// AFTER:
var mfg = _building.GetBehavior<ManufacturingBehavior>();
string? recipeId = _building.ActiveRecipeId ?? mfg?.DefaultRecipe;
float speed = mfg?.ProductionSpeed ?? 1f;
```

#### `Scripts/UI/BuildingInfo/BuildingPanelDetails.cs` (line 163)

```csharp
// BEFORE:
float speed = _building?.Definition?.Production?.ProductionSpeed ?? 1f;

// AFTER:
var mfg = _building?.GetBehavior<ManufacturingBehavior>();
float speed = mfg?.ProductionSpeed ?? 1f;
```

#### `Scripts/UI/BuildingInfo/HubPanelDetails.cs` (lines 124, 377-379)

```csharp
// BEFORE:
=> building.Definition?.TransferStation != null;
// ...
if (definition?.SlotFilters != null && definition.SlotFilters.Count > 0)
    foreach (var spec in definition.SlotFilters)

// AFTER:
=> building.GetBehavior<TransferStationBehavior>() != null;
// ...
var hub = building.GetBehavior<StorageHubBehavior>();
if (hub != null && hub.SlotFilters.Count > 0)
    foreach (var spec in hub.SlotFilters)
```

#### `Scripts/UI/BuildingInfo/RecipeSelectionPopup.cs` (line 64)

```csharp
// BEFORE:
var production = building.Definition?.Production;

// AFTER:
var mfg = building.GetBehavior<ManufacturingBehavior>();
```

#### `Scripts/UI/BuildingInfo/Administration/AdministrationTabbedPanel.cs` (line 295)

```csharp
// BEFORE:
string? recipeId = _building?.ActiveRecipeId ?? def?.Production?.DefaultRecipe;

// AFTER:
var mfg = _building?.GetBehavior<ManufacturingBehavior>();
string? recipeId = _building?.ActiveRecipeId ?? mfg?.DefaultRecipe;
```

#### `Scripts/UI/TestScenes/TestPlanetBoardScene.cs` (lines 80-81)

```csharp
// BEFORE:
if (b.Definition?.TransferStation != null)
    _body.RegisterTransferEndpoint(b.Id, b.Definition.TransferStation, b);

// AFTER:
var tsb = b.GetBehavior<TransferStationBehavior>();
if (tsb != null)
{
    var def = new TransferStationDefinition
    {
        CargoCapacity = tsb.CargoCapacity,
        VehicleSpeed = tsb.VehicleSpeed,
        MaxConcurrentTransfers = tsb.MaxConcurrentTransfers,
    };
    _body.RegisterTransferEndpoint(b.Id, def, b);
}
```

**Validation:**
- `dotnet build` compiles
- UI panels display recipe info, power info, storage info correctly at runtime

---

### Ticket 12: Update `BuildingDatabaseDebug.cs`

**File:** `Scripts/Structures/Resources/BuildingDatabaseDebug.cs`

**What:**

Replace reads of removed `BuildingDefinition` sub-properties with reads from behavior entries:

```csharp
// BEFORE:
.AddProperty("Default Recipe", building.Production.DefaultRecipe ?? "")
.AddProperty("Alt Recipes", building.Production.AlternativeRecipes.Count > 0
    ? string.Join(", ", building.Production.AlternativeRecipes) : "(none)")
.AddProperty("Production Speed", building.Production.ProductionSpeed)
.AddProperty("Storage Capacity (slots)", building.StorageCapacity)

// AFTER:
var mfgEntry = building.BehaviorEntries
    .FirstOrDefault(e => e.BehaviorId == "ManufacturingBehavior");
var mfgDefault = mfgEntry != null
    ? BehaviorConfigHelper.ReadString(mfgEntry.Config, "default_recipe", "") ?? ""
    : "";
var mfgAlts = mfgEntry != null
    ? BehaviorConfigHelper.ReadStringList(mfgEntry.Config, "alternative_recipes")
    : new List<string>();
var mfgSpeed = mfgEntry != null
    ? BehaviorConfigHelper.ReadFloat(mfgEntry.Config, "production_speed", 1.0f)
    : 1.0f;
var storageEntry = building.BehaviorEntries
    .FirstOrDefault(e => e.BehaviorId == "StorageHubBehavior");
var storageCap = storageEntry != null
    ? BehaviorConfigHelper.ReadInt(storageEntry.Config, "storage_capacity", 0)
    : 0;
```

**Validation:**
- `dotnet build` compiles
- Debug viewer shows correct values

---

### Ticket 13: Update `YamlValidator`

**File:** `Scripts/UtilityLibrary/DataLoading/YamlValidator.cs`

**What:**

#### 13a. Add validation for new `behaviors:` format

Inside `ValidateBuildingDefinition`, after required-field checks:

```csharp
// Validate behaviors structure if present
if (building.Children.ContainsKey("behaviors"))
{
    var behaviorsNode = building.Children["behaviors"];
    if (behaviorsNode is YamlSequenceNode behaviorsSeq)
    {
        int behaviorIndex = 0;
        foreach (var entryNode in behaviorsSeq.Children)
        {
            if (entryNode is YamlScalarNode)
            {
                // Bare string — allowed but not preferred
                // (backward compat if we ever support both formats)
            }
            else if (entryNode is YamlMappingNode entryMap)
            {
                if (!entryMap.Children.ContainsKey("behavior_id"))
                {
                    result.AddError(
                        $"Building at index {buildingIndex}: behavior entry at index {behaviorIndex} missing required 'behavior_id' key"
                    );
                }
                else
                {
                    var idNode = entryMap.Children["behavior_id"];
                    if (idNode is YamlScalarNode idScalar)
                    {
                        string behaviorId = idScalar.Value ?? "";
                        if (!IsValidBehaviorId(behaviorId))
                        {
                            result.AddWarning(
                                $"Building at index {buildingIndex}: behavior_id '{behaviorId}' may not be a valid behavior class name"
                            );
                        }
                    }
                }
            }
            else
            {
                result.AddError(
                    $"Building at index {buildingIndex}: behavior entry at index {behaviorIndex} must be a string or mapping"
                );
            }
            behaviorIndex++;
        }
    }
}
```

#### 13b. Add `IsValidBehaviorId` helper

```csharp
private static readonly HashSet<string> KnownBehaviorIds = new()
{
    "ManufacturingBehavior",
    "PowerProducerBehavior",
    "SolarPowerProducerBehavior",
    "WindPowerProducerBehavior",
    "GeothermalPowerProducerBehavior",
    "PowerConsumerBehavior",
    "BatteryBehavior",
    "ExtractionBehavior",
    "StorageHubBehavior",
    "TransferStationBehavior",
    "TransportHubBehavior",
    "InitialStockpileBehavior",
    "GameStartBehavior",
    "BulkStorageRoutingBehavior",
};

private static bool IsValidBehaviorId(string id)
{
    return KnownBehaviorIds.Contains(id);
}
```

#### 13c. Remove validation for removed sections

Remove or disable validation for `production:`, `power:`, `extraction:` sections. These are no longer valid top-level keys.

#### 13d. Validate `configurable_behavior` inline config

Update the `configurable_behavior` validation to accept a mapping (with `behavior_class` key) in addition to bare strings.

**Validation:**
- YamlValidator accepts new format
- YamlValidator rejects entries missing `behavior_id`
- YamlValidator warns on unknown behavior IDs

---

### Ticket 14: Update Tests

**Files to modify:**

#### `Tests/ResourceGeneration/BuildingDatabaseTest.cs`

Replace assertions that read `Production`, `StorageCapacity`, etc.:

```csharp
// BEFORE:
AssertThat(exampleBuilding.Production.DefaultRecipe).IsEqual("recipe_id");
AssertThat(exampleBuilding.Production.AlternativeRecipes.Count).IsEqual(1);
AssertThat(exampleBuilding.Production.ProductionSpeed).IsEqual(5.0f);

// AFTER:
var mfgEntry = exampleBuilding.BehaviorEntries
    .FirstOrDefault(e => e.BehaviorId == "ManufacturingBehavior");
AssertThat(mfgEntry).IsNotNull();
AssertThat(BehaviorConfigHelper.ReadString(mfgEntry!.Config, "default_recipe", "")).IsEqual("recipe_id");
AssertThat(BehaviorConfigHelper.ReadStringList(mfgEntry.Config, "alternative_recipes").Count).IsEqual(1);
AssertThat(BehaviorConfigHelper.ReadFloat(mfgEntry.Config, "production_speed", 1.0f)).IsEqual(5.0f);
```

Similarly for `universalBuilding.Production.*` and any `StorageCapacity` assertions.

#### `Tests/ResourceGeneration/PlacementBehaviorTest.cs`

Update AtmospherePlacementBehavior tests to verify bounds come from inline placement config, not Power.

#### `Tests/Constructables/Buildings/BuildingTest.cs`

Update `OnManufactureTick` and `SwapRecipe` tests to use `ManufacturingBehavior.DefaultRecipe` instead of `Definition.Production.DefaultRecipe`.

**Validation:**
- All existing tests pass with new assertions
- `dotnet build` compiles

---

### Ticket 15: Clean Up Orphaned Types

**Files to remove or simplify:**

| File | Action |
|---|---|
| `Scripts/Structures/Transfers/TransferStationDefinition.cs` | Keep for now — `TransferStationBehavior` still uses it internally for endpoint registration. Consider inlining in a follow-up. |
| `BuildingDefinition.ProductionDefinition` (nested) | Remove (part of Ticket 5) |
| `BuildingDefinition.PowerDefinition` (nested) | Remove (part of Ticket 5) |
| `BuildingDefinition.ExtractionDefinition` (nested) | Remove (part of Ticket 5) |
| `BuildingDefinition.SoundDefinition` (nested) | Keep — unrelated to behavior config refactor |

**Additional cleanup:**
- Remove any unused `using` statements introduced during refactor
- Verify no dead code paths reference removed types

**Validation:**
- `dotnet build` compiles with zero warnings related to removed types
- `grep -r "ProductionDefinition\|PowerDefinition\|ExtractionDefinition" Scripts/` returns zero hits

---

## Execution Order

```
Ticket 1  (types) ──────────────────────────────┐
                                                │
Ticket 2  (IBehaviorConfigurable on behaviors) ─┤
                                                │
Ticket 3  (BehaviorFactory update) ─────────────┤
                                                │
Ticket 4  (BuildingConfigLoader rewrite) ───────┤  ← Sequential
                                                │
Ticket 5  (BuildingDefinition cleanup) ─────────┤
                                                │
Ticket 6  (Building.cs consumers) ──────────────┘
                                               
Ticket 7  (TransferStationBehavior)  ──────┐
Ticket 8  (InitialStockpileBehavior)  ──────┤  ← Parallel
Ticket 9  (AtmospherePlacementBehavior) ────┘
                                            
Ticket 10 (YAML migration) ──────────────── ← After 7-9
                                            
Ticket 11 (UI consumers) ──────────────────┐
Ticket 12 (Debug viewer) ─────────────────┤  ← Parallel
Ticket 13 (YamlValidator) ────────────────┘
                                            
Ticket 14 (Tests) ──────────────────────── ← After 11-13
Ticket 15 (Cleanup) ────────────────────── ← After 14
```

---

## Scope Boundary

**In scope:**
- Building YAML behavior config colocation
- `IBehaviorConfigurable` interface + `BehaviorConfigHelper` utility
- Removal of `ProductionDefinition`, `PowerDefinition`, `ExtractionDefinition` from `BuildingDefinition`
- Removal of `ApplyPowerToBehavior()` switch
- All building YAML files migrated
- All C# consumers updated
- Tests updated
- YamlValidator updated

**Out of scope:**
- StationSatellite parallel refactor (same pattern, different codebase — follow-up)
- `TransferStationDefinition` type inlining (used by both buildings and stations — follow-up)
- `SoundDefinition` changes (unrelated)
- `VisualDefinition` changes (unrelated)
- Recipe/Resource YAML formats (unrelated)

---

## Future Follow-ups

1. **Apply same pattern to StationSatellite**: Station definition has the same scattered config problem. Apply `IBehaviorConfigurable` to station behaviors, inline config into behavior entries.

2. **Inline `TransferStationDefinition`**: Once both Building and Station behaviors store their own cargo/speed/concurrency values, the shared `TransferStationDefinition` type becomes unnecessary. Each behavior can store the 3 floats directly.

3. **Declarative behavior registration**: Instead of `KnownBehaviorIds` hardcoded set in YamlValidator, auto-discover `IBehaviorBehavior` implementations via reflection. Eliminates manual list maintenance.

4. **Schema generator**: Generate a JSON/YAML schema from `IBehaviorConfigurable` implementations' expected keys, enabling IDE autocomplete for building configs.
