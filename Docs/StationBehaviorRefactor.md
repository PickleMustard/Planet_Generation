# Station Behavior Pattern Refactor

## Overview

Introduce a behavior composition pattern for `StationSatellite` that mirrors the existing `IBuildingBehavior` system on `Building`. This flattens the station subclass hierarchy (`ConstructionYardStation`, `OrbitalArchitectStation` → deleted), moves operational ticking from `_PhysicsProcess` to `ManufactureTickEngine`, and refactors the transfer endpoint interface so both buildings and stations can participate in inter-entity transfers.

---

## Current State

### Building Behavior Pattern (existing, to be mirrored)

| Component | File | Role |
|-----------|------|------|
| `IBuildingBehavior` | `Scripts/Constructables/Buildings/IBuildingBehavior.cs` | Interface: `OnAttach`, `OnRegister`, `OnUnregister`, `OnDetach`, `OnManufactureTick(float, Building)`, `WantsTick`, `Priority` |
| `BehaviorFactory` | `Scripts/Constructables/Buildings/BehaviorFactory.cs` | Reflection loader: class name or `res://` script path → `IBuildingBehavior` |
| `Building` | `Scripts/Constructables/Buildings/Building.cs` | Holds `List<IBuildingBehavior>`, `GetBehavior<T>()`, fans tick, sleep/wake |
| `BuildingDefinition` | `Scripts/Structures/Resources/BuildingDefinition.cs` | `Godot.Collections.Array<string> BehaviorRefs` from YAML |
| `BuildingConfigLoader` | `Scripts/UtilityLibrary/DataLoading/BuildingConfigLoader.cs` | Parses `behaviors:` YAML list |
| 12 behaviors | `Scripts/Constructables/Buildings/Behaviors/*.cs` | All extend `RefCounted`, implement `IBuildingBehavior` |
| `IManufactureTickable` | `Scripts/Constructables/Tick/IManufactureTickable.cs` | `OnManufactureTick(float delta)` + `TickPriority` |
| `ManufactureTickEngine` | `Scripts/Constructables/Tick/ManufactureTickEngine.cs` | Singleton 60Hz background-thread tick driver |

### Station Current State (to be refactored)

| Component | File | Role |
|-----------|------|------|
| `StationSatellite` | `Scripts/Constructables/ArtificialSatellites/StationSatellite.cs` | Base: `Node3D`, `IArtificialSatellite`, `IConstructable`. Ticks via `_PhysicsProcess`. Has virtual `TickOperational(float)` (empty). |
| `ConstructionYardStation` | `Scripts/Constructables/ArtificialSatellites/ConstructionYardStation.cs` | Subclass. Holds `ShipBuildQueue`. Overrides `TickOperational` to tick queue. Ship enqueue/cancel/reorder API. |
| `OrbitalArchitectStation` | `Scripts/Constructables/ArtificialSatellites/OrbitalArchitectStation.cs` | Subclass. Overrides `OnConstructionComplete` → `RegisterWithBodyManager()`. Overrides `_ExitTree` → `UnregisterArchitect()`. |
| `StationDefinition` | `Scripts/Structures/Logistics/StationDefinition.cs` | Data class. Has `CanBuildShips`, `MaxParallelShipBuilds`, `CanBuildBuildings`, `BuildingWorkBudgetPerTick`, `BuildingScalingPenalty`. **No `BehaviorRefs`.** |
| `StationConfigLoader` | `Scripts/UtilityLibrary/DataLoading/StationConfigLoader.cs` | YAML parser. **No behavior parsing.** |
| `ConstructionManager.CreateStationInstance` | `Scripts/Constructables/ConstructionManager.cs` (L930-945) | Chooses subclass: `CanBuildShips` → `ConstructionYardStation`, `CanBuildBuildings` → `OrbitalArchitectStation`, else `StationSatellite`. |

### Transfer Endpoint System (to be generalized)

| Component | File | Role |
|-----------|------|------|
| `IResourceEndpoint` | `Scripts/Structures/GameState/IResourceEndpoint.cs` | Deposit/Withdraw/GetStockpile/EnqueueRequest/GetStorageFillPercentage |
| `BuildingResourceEndpoint` | `Scripts/Constructables/Buildings/BuildingResourceEndpoint.cs` | Adapter: `Building.BulkStorage` ↔ `IResourceEndpoint` |
| `IOrbitalBody` registry | `Scripts/ProceduralGeneration/IOrbitalBody.cs` (L84-128) | `RegisterTransferEndpoint(string, TransferStationDefinition, Building)` — **Building-only** |
| Implementations | `CelestialBody.cs`, `SatelliteBody.cs`, `Barycenter.cs` | Internal dict: `string → (TransferStationDefinition, Building)` |
| `TransferStationBehavior` | `Scripts/Constructables/Buildings/Behaviors/TransferStationBehavior.cs` | 888 lines, deeply coupled to `BuildingResourceEndpoint` + `IOrbitalBody` registry |

### Station Storage

Stations currently have **no storage infrastructure**. Buildings have `InputStorage`, `OutputStorage`, `BulkStorage` (all type `Storage`).

---

## Target State

1. `IStationBehavior` interface mirroring `IBuildingBehavior` with `StationSatellite` as owner
2. `StationBehaviorFactory` mirroring `BehaviorFactory`
3. `StationSatellite` implements `IManufactureTickable`, holds `List<IStationBehavior>`, `Storage BulkStorage`, `GetBehavior<T>()`, behavior lifecycle, sleep/wake
4. `StationDefinition` gets `BehaviorRefs`, `StorageCapacity`, `SlotFilters`, `TransferStation`
5. `StationConfigLoader` parses `behaviors:`, `storage_capacity:`, `slot_filters:`, `transfer_station:` YAML blocks
6. Four station behaviors: `StorageHubBehavior`, `TransferHubBehavior`, `OrbitalConstructorBehavior`, `ShipyardBehavior`
7. Shared endpoint interface: `IOrbitalBody` registry accepts `Node` (not `Building`); `StationResourceEndpoint` adapter
8. Flatten subclass hierarchy — delete `ConstructionYardStation.cs` and `OrbitalArchitectStation.cs`
9. Update `ConstructionManager.CreateStationInstance` → always `new StationSatellite`
10. Update all YAML configs with `behaviors:` lists
11. Update all code references to deleted subclasses

---

## Dependency Graph

```
T1 (IStationBehavior + Factory) ──┐
T2 (StationDefinition + YAML) ─────┤
                                    ├──→ T3 (StationSatellite core refactor)
                                          ├──→ T4 (StorageHubBehavior)
                                          ├──→ T5 (Shared endpoint refactor)
                                          │       └──→ T6 (TransferHubBehavior)
                                          ├──→ T7 (OrbitalConstructorBehavior)
                                          └──→ T8 (ShipyardBehavior)

T4, T6, T7, T8 ──→ T9 (Flatten hierarchy + update refs)
T2, T9 ──→ T10 (Update YAML configs)
```

---

## Tickets

### T1: IStationBehavior Interface + StationBehaviorFactory

**Estimate**: 1 day

**Files created**:
- `Scripts/Constructables/Stations/IStationBehavior.cs`
- `Scripts/Constructables/Stations/StationBehaviorFactory.cs`
- `Scripts/Constructables/Stations/Behaviors/` (directory, empty)

**Specification**:

`IStationBehavior` (namespace `Constructables.Stations`):
```csharp
namespace Constructables.Stations;

public interface IStationBehavior
{
    StationSatellite? Owner { get; }

    void OnAttach(StationSatellite owner);
    void OnRegister();
    void OnUnregister();
    void OnDetach();

    void OnManufactureTick(float delta, StationSatellite owner);

    bool WantsTick => true;
    int Priority => 0;
}
```

- Lifecycle mirrors `IBuildingBehavior`: `OnAttach` → `OnRegister` → tick loop → `OnUnregister` → `OnDetach`
- `Owner` nullable; set by `OnAttach`, cleared by `OnDetach`
- `WantsTick` default `true`; `Priority` default `0` (interface default implementations)

`StationBehaviorFactory` (namespace `Constructables.Stations`):
```csharp
public static IStationBehavior? Create(string nameOrPath)
```
- Null/whitespace → return `null`
- Starts with `"res://"` → `CreateFromScript` path; else → `CreateByName` reflection
- `CreateByName` searches assembly: bare name, then `Constructables.Stations.Behaviors.{name}`, then `Constructables.Stations.{name}`
- Validates implements `IStationBehavior` + has parameterless constructor
- All errors via `GameLogger.Error` (NOT `GD.PrintErr`)
- `CreateFromScript` loads `CSharpScript`, calls `.New()`, validates interface, frees Godot object if validation fails

**Error handling**:
- Type not found → `GameLogger.Error` + `null`
- Type doesn't implement `IStationBehavior` → `GameLogger.Error` + `null`
- No parameterless ctor → `GameLogger.Error` + `null`
- Script load failure → `GameLogger.Error` + `null`

**Tests**: `Tests/Constructables/Stations/StationBehaviorFactoryTest.cs`
- `Create("StorageHubBehavior")` returns non-null instance
- `Create("")` returns null
- `Create("NonexistentBehavior")` returns null + logs error
- `Create("res://nonexistent.cs")` returns null + logs error

**Dependencies**: None

---

### T2: StationDefinition BehaviorRefs + StationConfigLoader Behavior Parsing

**Estimate**: 1 day

**Files modified**:
- `Scripts/Structures/Logistics/StationDefinition.cs`
- `Scripts/UtilityLibrary/DataLoading/StationConfigLoader.cs`

**Specification**:

Add to `StationDefinition`:
```csharp
public Godot.Collections.Array<string> BehaviorRefs { get; set; } = new();
public int StorageCapacity { get; set; } = 0;
public List<SlotFilterSpec> SlotFilters { get; set; } = new();
public TransferStationDefinition? TransferStation { get; set; }
```

Add to `StationConfigLoader.ParseStationDefinition`:
```csharp
BehaviorRefs = ParseBehaviorRefs(dict),
StorageCapacity = BaseConfigLoader.ReadInt(dict, "storage_capacity", 0),
SlotFilters = ParseSlotFilters(dict),
TransferStation = ParseTransferStationDefinition(dict),
```

`ParseBehaviorRefs` mirrors `BuildingConfigLoader.ParseBehaviorRefs`:
- Missing `behaviors` key → empty `BehaviorRefs` (backward compatible)
- Each entry must be string; skip null/empty

`ParseSlotFilters` mirrors `BuildingConfigLoader.ParseSlotFilters`:
- Parse `slot_filters:` block into `List<SlotFilterSpec>`
- Missing key → empty list

`ParseTransferStationDefinition` mirrors `BuildingConfigLoader.ParseTransferStationDefinition`:
- Parse `transfer_station:` block into `TransferStationDefinition`
- Missing key → null

**Error handling**:
- Missing keys → defaults (backward compatible with existing YAML)
- Non-string behavior entries → silently skip
- Invalid transfer_station block → null + warning log

**Tests**: `Tests/UtilityLibrary/DataLoading/StationConfigLoaderBehaviorTest.cs`
- YAML with `behaviors: [StorageHubBehavior, ShipyardBehavior]` → BehaviorRefs has 2 entries
- YAML without `behaviors:` → BehaviorRefs is empty
- YAML with `storage_capacity: 20` → StorageCapacity is 20
- YAML with `transfer_station:` block → TransferStation is populated
- Existing YAML (no new fields) → all defaults, no errors

**Dependencies**: None (parallel with T1)

---

### T3: StationSatellite Core Refactoring

**Estimate**: 2-3 days

**File modified**: `Scripts/Constructables/ArtificialSatellites/StationSatellite.cs`

**Specification**:

1. **Add usings**:
   ```csharp
   using Constructables.Stations;
   using Constructables.Tick;
   ```

2. **Implement `IManufactureTickable`** on class declaration:
   ```csharp
   public partial class StationSatellite : Node3D, IArtificialSatellite, IConstructable, IManufactureTickable
   ```

3. **Add `TickPriority`** (from `IManufactureTickable`):
   ```csharp
   public int TickPriority { get; set; } = 0;
   ```

4. **Add `BulkStorage`**:
   ```csharp
   public Storage BulkStorage { get; } = new();
   ```

5. **Add `Behaviors` list**:
   ```csharp
   public List<IStationBehavior> Behaviors { get; } = new();
   ```

6. **Add `GetBehavior<T>()`** (mirrors `Building.GetBehavior<T>`):
   ```csharp
   public T? GetBehavior<T>() where T : class, IStationBehavior
   {
       foreach (var b in Behaviors)
           if (b is T match)
               return match;
       return null;
   }
   ```

7. **Modify `SetStationDefinition`** — after model installation, wire behaviors:
   ```csharp
   foreach (var refName in definition.BehaviorRefs)
   {
       var behavior = StationBehaviorFactory.Create(refName);
       if (behavior == null) continue;
       Behaviors.Add(behavior);
       behavior.OnAttach(this);
   }
   BulkStorage.StorageUpdated += OnStationStorageUpdated;
   ```

8. **Add `RegisterBehaviors()`**:
   ```csharp
   public void RegisterBehaviors()
   {
       Behaviors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
       foreach (var b in Behaviors)
           b.OnRegister();
   }
   ```

9. **Add `UnregisterBehaviors()`**:
   ```csharp
   public void UnregisterBehaviors()
   {
       for (int i = Behaviors.Count - 1; i >= 0; i--)
           Behaviors[i].OnUnregister();
   }
   ```

10. **Add `DetachBehaviors()`**:
    ```csharp
    public void DetachBehaviors()
    {
        foreach (var b in Behaviors)
            b.OnDetach();
        Behaviors.Clear();
    }
    ```

11. **Add `EvaluateTickRegistration()`**:
    ```csharp
    private void EvaluateTickRegistration()
    {
        if (_isUnderConstruction || !IsActive)
            return;
        foreach (var b in Behaviors)
        {
            if (b.WantsTick)
                return;
        }
        ManufactureTickEngine.Instance?.Unregister(this);
    }
    ```

12. **Add `OnStationStorageUpdated` handler**:
    ```csharp
    private void OnStationStorageUpdated(string resourceId, float delta)
    {
        if (_isUnderConstruction || !IsActive)
            return;
        ManufactureTickEngine.Instance?.Register(this);
    }
    ```

13. **Modify `OnConstructionComplete`** — after existing code:
    ```csharp
    RegisterBehaviors();
    ManufactureTickEngine.Instance?.Register(this);
    ```

14. **Modify `_ExitTree`** — before existing code:
    ```csharp
    UnregisterBehaviors();
    DetachBehaviors();
    ManufactureTickEngine.Instance?.Unregister(this);
    ```

15. **Implement `OnManufactureTick(float delta)`**:
    ```csharp
    public void OnManufactureTick(float delta)
    {
        if (!IsActive || _isUnderConstruction)
            return;

        foreach (var behavior in Behaviors)
        {
            try
            {
                behavior.OnManufactureTick(delta, this);
            }
            catch (System.Exception ex)
            {
                GameLogger.Error(
                    $"StationSatellite {Name}: behavior {behavior.GetType().Name} "
                    + $"threw on tick: {ex.GetType().Name}: {ex.Message}"
                );
            }
        }

        EvaluateTickRegistration();
    }
    ```

16. **Modify `_PhysicsProcess`** — remove `TickOperational(dt)` call. Keep:
    - Construction ticking (`TickConstruction` when `_isUnderConstruction`)
    - Orbital movement (angle update, position calculation, velocity calculation)
    - Mark `TickOperational` with `[Obsolete("Replaced by IStationBehavior.OnManufactureTick")]`

17. **Mark deprecated**:
    - `CanBuildShips` → `[Obsolete("Use GetBehavior<ShipyardBehavior>() != null")]`
    - `StationType` → `[Obsolete("Use GetBehavior<T>() to query capabilities")]`

**Thread safety**: `ManufactureTickEngine` runs on a dedicated background thread. Behaviors must not call Godot scene-tree APIs directly from `OnManufactureTick`. Use `CallDeferred()` for any Godot API calls, matching the existing `Building` pattern.

**Error handling**:
- Behavior factory returns null → skip, continue with remaining behaviors
- Behavior tick throws → log via `GameLogger.Error`, continue ticking other behaviors
- `ManufactureTickEngine.Instance` null → guard with `?.`

**Tests**: `Tests/Constructables/ArtificialSatellites/StationSatelliteBehaviorTest.cs`
- `OnManufactureTick` fans out to behaviors in Priority order
- `RegisterBehaviors` sorts behaviors by Priority
- `EvaluateTickRegistration` unregisters when no behavior WantsTick
- `OnStationStorageUpdated` re-registers with engine
- Behavior tick exception doesn't kill other behaviors
- `OnConstructionComplete` calls RegisterBehaviors + engine Register

**Dependencies**: T1

---

### T4: StorageHubBehavior

**Estimate**: 1 day

**File created**: `Scripts/Constructables/Stations/Behaviors/StorageHubBehavior.cs`
**Test file**: `Tests/Constructables/Stations/Behaviors/StorageHubBehaviorTest.cs`

**Specification**:

```csharp
namespace Constructables.Stations.Behaviors;

public partial class StorageHubBehavior : RefCounted, IStationBehavior
{
    private StationSatellite? _owner;
    private readonly List<StorageSlot> _addedSlots = new();

    public int StorageCapacity { get; set; }
    public List<SlotFilterSpec> SlotFilters { get; set; } = new();
    public StationSatellite? Owner => _owner;

    public void OnAttach(StationSatellite owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null) return;

        int allocated = 0;
        // Specific filters first
        foreach (var spec in SlotFilters)
        {
            for (int i = 0; i < spec.Count; i++)
            {
                if (allocated >= StorageCapacity) break;
                var slot = new StorageSlot(spec.Filter);
                _owner.BulkStorage.AddSlot(slot);
                _addedSlots.Add(slot);
                allocated++;
            }
            if (allocated >= StorageCapacity) break;
        }
        // Fill remainder with Any filter
        while (allocated < StorageCapacity)
        {
            var slot = new StorageSlot(SlotFilter.Any());
            _owner.BulkStorage.AddSlot(slot);
            _addedSlots.Add(slot);
            allocated++;
        }
    }

    public void OnUnregister()
    {
        foreach (var slot in _addedSlots)
            _owner?.BulkStorage.RemoveSlot(slot);
        _addedSlots.Clear();
    }

    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, StationSatellite owner) { }

    public bool WantsTick => false;
    public int Priority => 0;
}
```

- `StorageCapacity` and `SlotFilters` set by `StationDefinition` wiring before `OnRegister` (see T3 wiring in `SetStationDefinition`)
- `WantsTick` is `false` — storage population is one-time setup
- `Priority` is `0` — runs early, before transfer/constructor behaviors

**StationDefinition wiring**: In `SetStationDefinition` (T3 changes), after `behavior.OnAttach(this)`, inject definition values:
```csharp
if (behavior is StorageHubBehavior hub)
{
    hub.StorageCapacity = definition.StorageCapacity;
    hub.SlotFilters = definition.SlotFilters;
}
```

**Tests**:
- `OnRegister` adds `StorageCapacity` slots to `BulkStorage`
- `OnUnregister` removes all added slots
- Specific filters applied first, remainder as `Any`
- `StorageCapacity` 0 → no slots added
- `WantsTick` returns `false`
- `OnManufactureTick` is no-op

**Dependencies**: T3

---

### T5: Shared Endpoint Interface Refactoring

**Estimate**: 2 days

**Files created**:
- `Scripts/Constructables/Stations/StationResourceEndpoint.cs`

**Files modified**:
- `Scripts/Structures/GameState/IResourceEndpoint.cs`
- `Scripts/ProceduralGeneration/IOrbitalBody.cs`
- `Scripts/ProceduralGeneration/CelestialBody.cs`
- `Scripts/ProceduralGeneration/SatelliteBody.cs`
- `Scripts/ProceduralGeneration/Barycenter.cs`
- `Scripts/Constructables/Buildings/BuildingResourceEndpoint.cs`
- `Scripts/Constructables/Buildings/Behaviors/TransferStationBehavior.cs`

**Test files**:
- `Tests/Constructables/Stations/StationResourceEndpointTest.cs`
- Update existing: `Tests/ProceduralGeneration/IOrbitalBodyRegistryTest.cs`, `Tests/Constructables/Buildings/Behaviors/TransferStationBehaviorTest.cs`

**Specification**:

**1. `IResourceEndpoint` change**:

Change `GetStorageFillPercentage` signature from Building-specific to generic:
```csharp
// Before:
float GetStorageFillPercentage(Constructables.Building building, string category);

// After:
float GetStorageFillPercentage(Node? owner, string category);
```

Update `BuildingResourceEndpoint.GetStorageFillPercentage` — already takes a Building, just update the interface signature check.

**2. `IOrbitalBody` registry change**:

```csharp
// Before:
void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, Building building);
Building? GetTransferEndpointBuilding(string endpointId);

// After:
void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, Node owner);
[Obsolete("Use GetTransferEndpointOwner instead")]
Building? GetTransferEndpointBuilding(string endpointId);
Node? GetTransferEndpointOwner(string endpointId);
```

Internal dict in CelestialBody/SatelliteBody changes:
```csharp
// Before:
private readonly Dictionary<string, (TransferStationDefinition, Building)> _transferEndpoints = new();

// After:
private readonly Dictionary<string, (TransferStationDefinition, Node)> _transferEndpoints = new();
```

`GetTransferEndpointBuilding` becomes:
```csharp
[Obsolete("Use GetTransferEndpointOwner instead")]
public Building? GetTransferEndpointBuilding(string endpointId)
{
    if (!_transferEndpoints.TryGetValue(endpointId, out var entry))
        return null;
    return entry.Item2 as Building;
}
```

`GetTransferEndpointOwner` is new:
```csharp
public Node? GetTransferEndpointOwner(string endpointId)
{
    if (!_transferEndpoints.TryGetValue(endpointId, out var entry))
        return null;
    return entry.Item2;
}
```

`GetTransferEndpointsOnContinent` and `GetTotalTransferCapacityOnContinent` — update to resolve the owner's continent index. For `Building` owners, use `building.ContinentIndex`. For `StationSatellite` owners, they are not on a continent (they orbit); these methods should skip station endpoints (stations don't belong to a continent). This matches the design intent: continent-based queries are for surface buildings only.

**3. `StationResourceEndpoint`**:

```csharp
namespace Constructables.Stations;

public sealed class StationResourceEndpoint : IResourceEndpoint
{
    private readonly StationSatellite _owner;

    public StationResourceEndpoint(StationSatellite owner) => _owner = owner;

    public StationSatellite Owner => _owner;

    public float DepositResource(string resourceId, float amount)
        => _owner.BulkStorage.Deposit(resourceId, amount);

    public float WithdrawResource(string resourceId, float amount)
        => _owner.BulkStorage.Withdraw(resourceId, amount);

    public float GetStockpile(string resourceId)
        => _owner.BulkStorage.GetQuantity(resourceId);

    public IReadOnlyDictionary<string, float> GetAllStockpiles()
        => _owner.BulkStorage.GetAllQuantities();

    public void EnqueueResourceRequest(ResourceRequest request) { }

    public float GetStorageFillPercentage(Node? owner, string category)
    {
        if (owner != _owner) return 0f;
        var bulk = _owner.BulkStorage;
        if (bulk.Slots.Count == 0) return 0f;
        float used = 0f, capacity = 0f;
        foreach (var slot in bulk.Slots) { used += slot.Quantity; capacity += slot.Capacity; }
        return capacity > 0f ? used / capacity : 0f;
    }
}
```

**4. `TransferStationBehavior` updates**:

In `OnRegister`, change:
```csharp
_body.RegisterTransferEndpoint(_owner.Id, _endpointDef, _owner);
```
This already passes a `Building` which is a `Resource` which is a `Godot.Resource` which is `Godot.GodotObject` which is `Godot.Node`... wait, actually `Building : Resource` and `Resource` in Godot does NOT extend `Node`. So we need to check: does `Building` extend `Node`?

Looking at the Building class: `public partial class Building : Resource, IConstructable, IManufactureTickable`. `Resource` in Godot extends `GodotObject`, NOT `Node`. So `Building` is NOT a `Node`.

This is a problem. `StationSatellite` extends `Node3D` which IS a `Node`. So we can't unify on `Node` as the owner type for both.

**Revised approach**: Instead of `Node`, use `GodotObject` as the base type, since both `Resource` (Building's base) and `Node3D` (StationSatellite's base) ultimately extend `GodotObject`.

**Revised `IOrbitalBody` registry**:
```csharp
void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, GodotObject owner);
[Obsolete("Use GetTransferEndpointOwner instead")]
Building? GetTransferEndpointBuilding(string endpointId);
GodotObject? GetTransferEndpointOwner(string endpointId);
```

**Revised `IResourceEndpoint.GetStorageFillPercentage`**:
```csharp
float GetStorageFillPercentage(GodotObject? owner, string category);
```

**Revised `BuildingResourceEndpoint.GetStorageFillPercentage`**:
```csharp
public float GetStorageFillPercentage(GodotObject? owner, string category)
{
    if (owner != _owner) return 0f;
    // ... same logic
}
```

**5. Update all `IOrbitalBody` implementations** (CelestialBody, SatelliteBody, Barycenter, MockOrbitalBody test doubles):

- Change internal dict to `Dictionary<string, (TransferStationDefinition, GodotObject)>`
- Update `RegisterTransferEndpoint` signature
- Add `GetTransferEndpointOwner`
- Mark `GetTransferEndpointBuilding` as `[Obsolete]`, cast internally

**6. Update `TransferStationBehavior.ResolveEndpoint`**:

When resolving a destination endpoint, the method currently gets a `Building` from `GetTransferEndpointBuilding`. Update to:
1. Call `GetTransferEndpointOwner`
2. If owner is `Building`, get its `TransferStationBehavior` → `BuildingResourceEndpoint`
3. If owner is `StationSatellite`, get its `TransferHubBehavior` → `StationResourceEndpoint`
4. If neither, log warning and return null

**Error handling**:
- Null owner in Register → log warning, skip
- Unknown owner type in ResolveEndpoint → log warning, return null
- GetTransferEndpointBuilding with non-Building owner → return null (not an error, just not applicable)

**Tests**:
- `StationResourceEndpoint.Deposit` deposits into `StationSatellite.BulkStorage`
- `StationResourceEndpoint.Withdraw` withdraws from `StationSatellite.BulkStorage`
- `StationResourceEndpoint.GetStockpile` returns correct quantity
- `StationResourceEndpoint.GetStorageFillPercentage` with matching owner returns fill %
- `StationResourceEndpoint.GetStorageFillPercentage` with non-matching owner returns 0
- Update existing `IOrbitalBodyRegistryTest` to use `GodotObject` type
- Update existing `TransferStationBehaviorTest` mock registries

**Dependencies**: T3

---

### T6: TransferHubBehavior

**Estimate**: 2 days

**File created**: `Scripts/Constructables/Stations/Behaviors/TransferHubBehavior.cs`
**Test file**: `Tests/Constructables/Stations/Behaviors/TransferHubBehaviorTest.cs`

**Specification**:

```csharp
namespace Constructables.Stations.Behaviors;

public partial class TransferHubBehavior : RefCounted, IStationBehavior
{
    private StationSatellite? _owner;
    private IOrbitalBody? _body;
    private StationResourceEndpoint? _endpoint;
    private TransferStationDefinition? _endpointDef;
    private readonly Dictionary<string, ActiveTransfer> _activeTransfers = new();
    private readonly Dictionary<string, List<TransferSchedule>> _schedulesByOrigin = new();
    private double _totalTime;
```

**`OnAttach`**: Store owner reference.

**`OnRegister`**:
1. Walk scene tree upward to find `IOrbitalBody` parent.
2. Create `StationResourceEndpoint` wrapping `_owner`.
3. Read `_endpointDef` from `_owner`'s StationDefinition (set before OnRegister).
4. If `_endpointDef` is null → log warning, skip registration.
5. Call `_body.RegisterTransferEndpoint(_owner.Id, _endpointDef, _owner)`.

**`OnUnregister`**:
1. Stop all schedules for this origin.
2. Call `_body.UnregisterTransferEndpoint(_owner.Id)`.

**`OnManufactureTick(float delta, StationSatellite owner)`**:
1. `TickActiveTransfers(delta)` — advance elapsed time on `InTransit` orders, call `CompleteTransfer` when done.
2. `TickSchedules(delta)` — advance `_totalTime`, check accumulation thresholds, dispatch when ready.
3. Call `EvaluateTickRegistration` (from owner — already handled by StationSatellite.OnManufactureTick, so not needed here).

**Public API** (mirrors `TransferStationBehavior`):
- `bool DispatchOneTimeTransfer(string originBuildingId, string destinationId, Dictionary<string, float> requestedResources)`
- `TransferSchedule CreateSchedule(string destinationId, Dictionary<string, float> resources, float threshold)`
- `void StartSchedule(string scheduleId)`
- `void StopSchedule(string scheduleId)`
- `void RemoveSchedule(string scheduleId)`
- `void ReorderSchedules(string scheduleId, string? beforeId)`
- `IReadOnlyList<TransferSchedule> GetSchedulesForOrigin(string originId)`
- `IReadOnlyList<TransferSchedule> GetSchedulesForDestination(string destinationId)`
- `IReadOnlyDictionary<string, ActiveTransfer> GetActiveTransfers()`
- `IResourceEndpoint? ResourceEndpoint => _endpoint`

**`WantsTick`**: `true` when `_activeTransfers.Count > 0` or any schedule is in `Running` state.

**`Priority`**: `100` (runs after StorageHub 0, OrbitalConstructor 50).

**Key difference from `TransferStationBehavior`**: This behavior uses `StationResourceEndpoint` instead of `BuildingResourceEndpoint`. The transfer completion logic (deposit to destination) needs to handle both `BuildingResourceEndpoint` and `StationResourceEndpoint` destinations. Use `IResourceEndpoint.DepositResource` which works for both.

**StationDefinition wiring**: In `SetStationDefinition` (T3), after `behavior.OnAttach(this)`:
```csharp
if (behavior is TransferHubBehavior transfer)
{
    transfer.EndpointDef = definition.TransferStation;
}
```

**Error handling**:
- No `IOrbitalBody` found → log warning, skip registration
- No `TransferStationDefinition` → log warning, skip registration
- Destination endpoint not found → log warning, return null from DispatchOneTimeTransfer
- Max concurrent transfers reached → log warning, return null

**Tests**:
- `OnRegister` creates endpoint and registers with body
- `OnUnregister` unregisters from body
- `DispatchOneTimeTransfer` withdraws from BulkStorage, creates ActiveTransfer
- Transfer completion deposits to destination IResourceEndpoint
- Schedule accumulation tick dispatches when threshold met
- `WantsTick` returns true when active transfers exist
- `WantsTick` returns false when idle

**Dependencies**: T5

---

### T7: OrbitalConstructorBehavior

**Estimate**: 1 day

**File created**: `Scripts/Constructables/Stations/Behaviors/OrbitalConstructorBehavior.cs`
**Test file**: `Tests/Constructables/Stations/Behaviors/OrbitalConstructorBehaviorTest.cs`

**Specification**:

```csharp
namespace Constructables.Stations.Behaviors;

public partial class OrbitalConstructorBehavior : RefCounted, IStationBehavior
{
    private StationSatellite? _owner;
    private IOrbitalBody? _body;

    public float WorkBudgetPerTick { get; set; } = 1.0f;
    public float ScalingPenalty { get; set; } = 0.05f;
    public float WasteFactor { get; set; } = 1.0f;

    public StationSatellite? Owner => _owner;

    public void OnAttach(StationSatellite owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null) return;

        // Walk scene tree to find IOrbitalBody
        Node? cursor = _owner.GetParent();
        while (cursor != null)
        {
            if (cursor is IOrbitalBody body)
            {
                _body = body;
                break;
            }
            cursor = cursor.GetParent();
        }

        if (_body?.BuildingConstructionMgr == null)
        {
            GameLogger.Warning(
                $"OrbitalConstructorBehavior {_owner.Name}: "
                + "No BuildingConstructionManager found on parent body"
            );
            return;
        }

        _body.BuildingConstructionMgr.RegisterArchitect(
            _owner,
            WorkBudgetPerTick,
            ScalingPenalty,
            WasteFactor
        );

        GameLogger.Info(
            $"OrbitalConstructorBehavior {_owner.Name}: Registered with budget "
            + $"{WorkBudgetPerTick}/tick, penalty {ScalingPenalty}"
        );
    }

    public void OnUnregister()
    {
        if (_owner == null || _body?.BuildingConstructionMgr == null) return;
        _body.BuildingConstructionMgr.UnregisterArchitect(_owner);
    }

    public void OnDetach()
    {
        _owner = null;
        _body = null;
    }

    public void OnManufactureTick(float delta, StationSatellite owner) { }

    public bool WantsTick => false;
    public int Priority => 50;
}
```

- `WorkBudgetPerTick`, `ScalingPenalty`, `WasteFactor` set from `StationDefinition` values before `OnRegister`
- `WantsTick` returns `false` — architect contributes budget passively, `BuildingConstructionManager` handles per-tick work
- `Priority` is `50` — after StorageHub (0), before TransferHub (100)

**StationDefinition wiring**: In `SetStationDefinition`:
```csharp
if (behavior is OrbitalConstructorBehavior ctor)
{
    ctor.WorkBudgetPerTick = definition.BuildingWorkBudgetPerTick;
    ctor.ScalingPenalty = definition.BuildingScalingPenalty;
}
```

**Directly corresponds to existing `OrbitalArchitectStation`** (53 lines) — this is a straightforward extraction.

**Tests**:
- `OnRegister` walks tree and calls `BuildingConstructionMgr.RegisterArchitect` with correct params
- `OnUnregister` calls `BuildingConstructionMgr.UnregisterArchitect`
- Missing `IOrbitalBody` → warning log, no crash
- Missing `BuildingConstructionMgr` → warning log, no crash
- `WantsTick` returns false
- `OnManufactureTick` is no-op

**Dependencies**: T3

---

### T8: ShipyardBehavior

**Estimate**: 2 days

**File created**: `Scripts/Constructables/Stations/Behaviors/ShipyardBehavior.cs`
**Test file**: `Tests/Constructables/Stations/Behaviors/ShipyardBehaviorTest.cs`

**Specification**:

```csharp
namespace Constructables.Stations.Behaviors;

public partial class ShipyardBehavior : RefCounted, IStationBehavior
{
    private StationSatellite? _owner;
    private ShipBuildQueue? _shipBuildQueue;

    public int MaxParallelShipBuilds { get; set; } = 1;

    public StationSatellite? Owner => _owner;

    public int ActiveShipBuildCount => _shipBuildQueue?.ActiveCount ?? 0;
    public int QueuedShipBuildCount => _shipBuildQueue?.QueuedCount ?? 0;
    public int MaxParallelBuilds => _shipBuildQueue?.MaxParallelBuilds ?? 0;

    public void OnAttach(StationSatellite owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null) return;
        _shipBuildQueue = new ShipBuildQueue(_owner, MaxParallelShipBuilds);
        GameLogger.Info(
            $"ShipyardBehavior {_owner.Name}: Initialized with "
            + $"{MaxParallelShipBuilds} parallel build slot(s)"
        );
    }

    public void OnUnregister() => _shipBuildQueue = null;

    public void OnDetach()
    {
        _owner = null;
        _shipBuildQueue = null;
    }

    public void OnManufactureTick(float delta, StationSatellite owner)
        => _shipBuildQueue?.Tick(delta);

    public bool WantsTick => _shipBuildQueue != null
        && (_shipBuildQueue.ActiveCount > 0 || _shipBuildQueue.QueuedCount > 0);

    public int Priority => 200;

    // Public API — mirrors ConstructionYardStation
    public void EnqueueShipConstruction(LogisticsUnit ship)
    {
        if (_shipBuildQueue == null)
        {
            GameLogger.Warning(
                $"ShipyardBehavior {_owner?.Name}: Cannot enqueue ship — no build queue"
            );
            return;
        }
        ship.ConstructingStation = _owner;
        ship.StartConstruction(new Godot.Collections.Dictionary());
        _shipBuildQueue.Enqueue(ship);
    }

    public void CancelShipConstruction(LogisticsUnit ship)
    {
        RefundDeliveredResources(ship);
        _shipBuildQueue?.Cancel(ship);
    }

    public void SetShipPaused(LogisticsUnit ship, bool paused)
        => _shipBuildQueue?.SetManualPause(ship, paused);

    public void ReorderQueue(LogisticsUnit ship, LogisticsUnit? before)
        => _shipBuildQueue?.ReorderQueue(ship, before);

    public IReadOnlyList<LogisticsUnit> GetShipBuildQueue()
        => _shipBuildQueue?.GetQueuedShips()
            ?? (IReadOnlyList<LogisticsUnit>)new List<LogisticsUnit>();

    public IReadOnlyList<LogisticsUnit> GetActiveBuilds()
        => _shipBuildQueue?.GetActiveBuilds()
            ?? (IReadOnlyList<LogisticsUnit>)new List<LogisticsUnit>();

    private void RefundDeliveredResources(LogisticsUnit ship)
    {
        var delivered = ship.availableResources;
        if (delivered == null || delivered.Count == 0) return;
        ship.availableResources = new Godot.Collections.Dictionary<string, int>();
    }
}
```

- `MaxParallelShipBuilds` set from `StationDefinition.MaxParallelShipBuilds` before `OnRegister`
- `ShipBuildQueue` constructor already takes `StationSatellite` — **no changes needed to `ShipBuildQueue.cs`**
- `WantsTick` returns `true` when builds exist, `false` when idle — enables sleep/wake
- `Priority` is `200` — runs after all other behaviors

**StationDefinition wiring**: In `SetStationDefinition`:
```csharp
if (behavior is ShipyardBehavior shipyard)
{
    shipyard.MaxParallelShipBuilds = definition.MaxParallelShipBuilds;
}
```

**Thread safety consideration**: `ShipBuildQueue.Tick()` currently runs from `_PhysicsProcess` (main thread). After this refactor, it will be called from `ManufactureTickEngine`'s background thread. Verify that `ShipBuildQueue.Tick` and the `LogisticsUnit` methods it calls do not touch Godot scene-tree APIs. If they do, those calls must be wrapped in `CallDeferred()`. Audit `ShipBuildQueue.Tick` for Godot API usage.

**Tests**:
- `OnRegister` creates `ShipBuildQueue` with correct `MaxParallelBuilds`
- `EnqueueShipConstruction` delegates to queue, sets `ConstructingStation`
- `CancelShipConstruction` delegates to queue + refunds resources
- `WantsTick` returns true when builds active/queued
- `WantsTick` returns false when queue empty
- `OnManufactureTick` calls `_shipBuildQueue.Tick(delta)`
- `GetShipBuildQueue` and `GetActiveBuilds` return correct lists

**Dependencies**: T3

---

### T9: Flatten Hierarchy + Update All References

**Estimate**: 2-3 days

**Files deleted**:
- `Scripts/Constructables/ArtificialSatellites/ConstructionYardStation.cs`
- `Scripts/Constructables/ArtificialSatellites/OrbitalArchitectStation.cs`

**Files modified**:

| File | Line(s) | Current Code | New Code |
|------|---------|--------------|----------|
| `ConstructionManager.cs` | 853 | `parentStation is not ConstructionYardStation` | Remove check (stations don't need to be a specific subclass) |
| `ConstructionManager.cs` | 903 | `if (parentStation is ConstructionYardStation yard)` | `if (parentStation.GetBehavior<ShipyardBehavior>() is { } shipyard)` then `shipyard.EnqueueShipConstruction(unit)` |
| `ConstructionManager.cs` | 938 | `return new ConstructionYardStation { Name = name };` | `return new StationSatellite { Name = name };` |
| `ConstructionManager.cs` | 941 | `return new OrbitalArchitectStation { Name = name };` | `return new StationSatellite { Name = name };` |
| `ConstructionManager.cs` | 552 | `OrbitalArchitectStation? architectStation` | `StationSatellite? architectStation` |
| `StationTabbedPanel.cs` | 151 | `station is ConstructionYardStation` | `station.GetBehavior<ShipyardBehavior>() != null` |
| `StationTabbedPanel.cs` | 383 | `_station is not ConstructionYardStation shipyard` | `_station.GetBehavior<ShipyardBehavior>() is not { } shipyard` |
| `StationDetailsPanel.cs` | 94 | `_station is not ConstructionYardStation shipyard` | `_station.GetBehavior<ShipyardBehavior>() is not { } shipyard` |
| `StationBespokeBar.cs` | 24 | `station is ConstructionYardStation` | `station.GetBehavior<ShipyardBehavior>() != null` |
| `ShipQueueManagementState.cs` | 19 | `private ConstructionYardStation? _shipyard;` | `private ShipyardBehavior? _shipyard;` |
| `ShipQueueManagementState.cs` | 39 | `stationNode is not ConstructionYardStation shipyard` | `stationNode.GetBehavior<ShipyardBehavior>() is not { } shipyard` |
| `ShipQueueManagementState.cs` | 41 | `"Station is not a ConstructionYardStation"` | `"Station has no ShipyardBehavior"` |
| `ConstructionYardWindow.cs` | 48 | `private ConstructionYardStation? _yard;` | `private StationSatellite? _station;` + `private ShipyardBehavior? _shipyard;` |
| `ConstructionYardWindow.cs` | 79 | `public void Bind(ConstructionYardStation yard)` | `public void Bind(StationSatellite station)` — internally: `_station = station; _shipyard = station.GetBehavior<ShipyardBehavior>();` |
| `ConstructionManagerDebug.cs` | 326 | `ConstructionYardStation yard => ...` | Match on `StationSatellite s when s.GetBehavior<ShipyardBehavior>() != null` |
| `ConstructionManagerDebug.cs` | 327 | `OrbitalArchitectStation arch => ...` | Match on `StationSatellite s when s.GetBehavior<OrbitalConstructorBehavior>() != null` |
| `StationTabbedPanel.cs` | 321 | `// OrbitalArchitectStation: building construction queue.` | `// OrbitalConstructorBehavior: building construction queue.` |
| `StationSatellite.cs` | various | `[Obsolete]` marked methods | Remove `TickOperational`, `CanBuildShips`, `StationType`, and their backing field references |

**Add usings where needed**:
- `using Constructables.Stations.Behaviors;` in all modified UI files

**Error handling**:
- `GetBehavior<T>()` returning null → each call site handles gracefully (log warning, skip feature, show empty UI)

**Tests**: Update any test files that reference `ConstructionYardStation` or `OrbitalArchitectStation`:
- Search for these class names in `Tests/` directory and update

**Dependencies**: T4, T6, T7, T8 (all behaviors must exist before subclasses deleted)

---

### T10: Update Station YAML Configs

**Estimate**: 1 day

**Files modified**: All YAML in `Configuration/stations/`

**Shipyard.yaml** — add to each station entry:
```yaml
  behaviors:
    - StorageHubBehavior
    - ShipyardBehavior
  storage_capacity: 20
```

**Orbital_Architect.yaml** — add to each station entry:
```yaml
  behaviors:
    - StorageHubBehavior
    - OrbitalConstructorBehavior
  storage_capacity: 10
```

**RamshackleBuilder.yaml** — add:
```yaml
  behaviors:
    - StorageHubBehavior
    - OrbitalConstructorBehavior
  storage_capacity: 10
```

**Refinery.yaml** — add to each station entry:
```yaml
  behaviors:
    - StorageHubBehavior
    - TransferHubBehavior
  storage_capacity: 30
  transfer_station:
    cargo_capacity: 500.0
    vehicle_speed: 50.0
    max_concurrent_transfers: 2
```

**Orbital_Habitat.yaml** — add to each station entry:
```yaml
  behaviors:
    - StorageHubBehavior
  storage_capacity: 40
```

**Validation**:
1. `dotnet build` — no compilation errors
2. `./addons/gdUnit4/runtest.sh --godot_binary "/usr/bin/godot-limbo" -a "res://Tests" -c -rd "./test-reports"` — all tests pass
3. Manual Godot editor verification — each station type loads YAML without warnings

**Dependencies**: T2 (parser must support new fields), T9 (subclasses must be gone so behavior names are the canonical config)

---

## Summary

| # | Title | Est. Days | Depends On |
|---|-------|-----------|------------|
| T1 | IStationBehavior + StationBehaviorFactory | 1 | — |
| T2 | StationDefinition.BehaviorRefs + ConfigLoader parsing | 1 | — |
| T3 | StationSatellite core refactoring | 2-3 | T1 |
| T4 | StorageHubBehavior | 1 | T3 |
| T5 | Shared endpoint interface refactoring | 2 | T3 |
| T6 | TransferHubBehavior | 2 | T5 |
| T7 | OrbitalConstructorBehavior | 1 | T3 |
| T8 | ShipyardBehavior | 2 | T3 |
| T9 | Flatten hierarchy + update all references | 2-3 | T4, T6, T7, T8 |
| T10 | Update YAML configs | 1 | T2, T9 |

**Total**: 15-19 engineer-days

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| `TransferStationBehavior` is 888 lines — complex to refactor | T5 could take longer | Scope T5 narrowly: change param type only. Don't rewrite the behavior class. |
| `IOrbitalBody` interface change ripples to 4 implementations + 5 test files | T5 regression risk | Add `[Obsolete]` bridge methods. Update all implementations in single PR. Run full test suite. |
| `ShipBuildQueue.Tick()` moves from main thread (`_PhysicsProcess`) to background thread (`ManufactureTickEngine`) | Thread safety: Godot API calls from wrong thread | Audit `ShipBuildQueue.Tick` for Godot API usage. Wrap any scene-tree calls in `CallDeferred()`. Test on actual Godot runtime. |
| `Building : Resource` is NOT a `Node`; `StationSatellite : Node3D` IS a `Node` | Cannot unify on `Node` as registry owner type | Use `GodotObject` as common base (both `Resource` and `Node` extend it). |
| Save/load compatibility — existing saves don't have `BehaviorRefs` | Load failures on old saves | `StationDefinition.BehaviorRefs` defaults to empty. Old saves that don't include it will get empty list, which is fine for backward compat during migration. |
| Transfer endpoint `GetTransferEndpointsOnContinent` assumes `Building` owners | Station endpoints have no continent | Skip station endpoints in continent-based queries. Stations orbit the body, not on a continent surface. |

---

## New File Summary

| File | Ticket | Type |
|------|--------|------|
| `Scripts/Constructables/Stations/IStationBehavior.cs` | T1 | Interface |
| `Scripts/Constructables/Stations/StationBehaviorFactory.cs` | T1 | Factory |
| `Scripts/Constructables/Stations/Behaviors/` | T1 | Directory |
| `Scripts/Constructables/Stations/Behaviors/StorageHubBehavior.cs` | T4 | Behavior |
| `Scripts/Constructables/Stations/Behaviors/OrbitalConstructorBehavior.cs` | T7 | Behavior |
| `Scripts/Constructables/Stations/Behaviors/ShipyardBehavior.cs` | T8 | Behavior |
| `Scripts/Constructables/Stations/Behaviors/TransferHubBehavior.cs` | T6 | Behavior |
| `Scripts/Constructables/Stations/StationResourceEndpoint.cs` | T5 | Adapter |
| `Tests/Constructables/Stations/StationBehaviorFactoryTest.cs` | T1 | Test |
| `Tests/Constructables/Stations/Behaviors/StorageHubBehaviorTest.cs` | T4 | Test |
| `Tests/Constructables/Stations/Behaviors/OrbitalConstructorBehaviorTest.cs` | T7 | Test |
| `Tests/Constructables/Stations/Behaviors/ShipyardBehaviorTest.cs` | T8 | Test |
| `Tests/Constructables/Stations/Behaviors/TransferHubBehaviorTest.cs` | T6 | Test |
| `Tests/Constructables/Stations/StationResourceEndpointTest.cs` | T5 | Test |
| `Tests/UtilityLibrary/DataLoading/StationConfigLoaderBehaviorTest.cs` | T2 | Test |
| `Tests/Constructables/ArtificialSatellites/StationSatelliteBehaviorTest.cs` | T3 | Test |

## Deleted File Summary

| File | Ticket | Reason |
|------|--------|--------|
| `Scripts/Constructables/ArtificialSatellites/ConstructionYardStation.cs` | T9 | Replaced by `ShipyardBehavior` |
| `Scripts/Constructables/ArtificialSatellites/OrbitalArchitectStation.cs` | T9 | Replaced by `OrbitalConstructorBehavior` |
