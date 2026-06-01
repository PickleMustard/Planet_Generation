# Transfer Architecture Refactor: BodyTransferManager → TransferStationBehavior

## Goal

Remove `BodyTransferManager` as a separate object. Each `TransferStationBehavior`
instance absorbs responsibility for tracking and updating transfers that originate
from its owning building. The body becomes a simple endpoint registry (via
`IOrbitalBody`), with no central transfer state.

---

## Before (centralized per-body manager)

```
Body (CelestialBody / SatelliteBody)
  └── TransferMgr : BodyTransferManager (Node child)
         ├── _activeTransfers (Dictionary<string, ActiveTransfer>)
         ├── _endpoints (Dictionary<string, IResourceEndpoint>)
         ├── _endpointDefs (Dictionary<string, TransferStationDefinition>)
         ├── _endpointBuildings (Dictionary<string, Building>)
         ├── _schedulesByOrigin (Dictionary<string, List<TransferSchedule>>)
         ├── _totalTime (double)
         └── _PhysicsProcess() → ticks all transfers + schedules globally
```

## After (per-building ownership)

```
Body (CelestialBody / SatelliteBody) : IOrbitalBody
  ├── endpoint registry (endpoint defs + buildings, no transfer state)
  └── IOrbitalBody.GetAllEndpoints(), HasTransferEndpoint(), etc.

      Each Building
        └── TransferStationBehavior
              ├── _endpoint (BuildingResourceEndpoint — already owned)
              ├── _activeTransfers (originates from THIS building only)
              ├── _schedulesByOrigin (originates from THIS building only)
              ├── _endpointDefs (copied at registration time)
              ├── _totalTime (double — accumulated game time for schedule gating)
              └── OnManufactureTick(delta, owner) → ticks THIS building's transfers + schedules
```

---

## Key Design Decisions

1. **Tick via ManufactureTickEngine** — `TransferStationBehavior.OnManufactureTick(float delta, Building owner)`
   drives the transfer tick, consistent with all other building-level simulation
   behaviors (`StorageHubBehavior`, `ExtractionBehavior`, etc.).

2. **Per-building state isolation** — each `TransferStationBehavior` owns transfers
   and schedules it originated. Destination endpoints on other buildings are looked
   up via the body's endpoint registry, but state is not shared.

3. **Body = endpoint registry** — `IOrbitalBody` provides lightweight methods for
   querying endpoints: `RegisterTransferEndpoint`, `UnregisterTransferEndpoint`,
   `GetAllEndpoints`, `GetEndpointsOnContinent`, `GetTotalCapacityOnContinent`,
   `HasTransferEndpoint`. No transfer logic lives here.

4. **SignalBus unchanged** — `TransferStationBehavior` emits the same signals
   (`TransferDispatched`, `TransferArrived`, `TransferReverted`,
   `TransferScheduleStateChanged`, `ContinentTransferCapacityChanged`) so existing
   UI listeners in `DispatchSlipsWindow` and `HubPanelDetails` require no changes
   to the signal layer.

---

## File Changes

### Delete

- `Scripts/Constructables/BodyTransferManager.cs`

### Modify: Interface + Bodies

#### `Scripts/ProceduralGeneration/IOrbitalBody.cs`

- Remove: `BodyTransferManager TransferMgr { get; }`
- Add methods (all bodies must implement):
  ```csharp
  void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, Building building);
  void UnregisterTransferEndpoint(string endpointId);
  bool HasTransferEndpoint(string endpointId);
  TransferStationDefinition? GetTransferEndpointDef(string endpointId);
  Building? GetTransferEndpointBuilding(string endpointId);
  IReadOnlyList<string> GetAllTransferEndpoints();
  IReadOnlyList<string> GetTransferEndpointsOnContinent(int continentIndex);
  float GetTotalTransferCapacityOnContinent(int continentIndex);
  ```

#### `Scripts/ProceduralGeneration/CelestialBody.cs`

- Remove: `public BodyTransferManager? TransferMgr` field (line 139)
- Remove: creation of `TransferMgr` in `InitializeOrbitSystem()` (lines 340–341)
- Implement: all `IOrbitalBody` transfer endpoint methods above
- Implement: signal emission — `RegisterTransferEndpoint` and `UnregisterTransferEndpoint`
  must call `SignalBus.Instance?.EmitContinentTransferCapacityChanged(...)` after
  updating the registry (the body is the aggregate authority for continent-level
  capacity changes).
- Keep: `BuildingConstructionManager`, `BodyEconomyManager`, `PowerGridMgr` — no change

#### `Scripts/ProceduralGeneration/SatelliteBody.cs`

- Same changes as `CelestialBody.cs`

#### `Scripts/ProceduralGeneration/Barycenter.cs`

- Remove `TransferMgr` property (or let interface removal make it disappear)
- Add stubs for all new `IOrbitalBody` endpoint-registry methods. Barycenters
  never have surface buildings, so all implementations return empty / false / 0f.

---

### Modify: TransferStationBehavior (main target)

#### `Scripts/Constructables/Buildings/Behaviors/TransferStationBehavior.cs`

Absorb ALL logic from `BodyTransferManager`. The file will grow substantially.
Core additions:

**Critical — Tick Registration:**
```csharp
public bool WantsTick => true;   // MUST be true; previously false
```
Without this change, `ManufactureTickEngine` will skip the behavior and no
in-flight transfers or schedules will ever advance.

**New fields (replacing BodyTransferManager state):**
```csharp
private Building? _owner;
private BuildingResourceEndpoint? _endpoint;
private TransferStationDefinition? _endpointDef;
private IOrbitalBody? _body;                         // weak ref to owning body
private readonly Dictionary<string, ActiveTransfer> _activeTransfers = new();
private readonly Dictionary<string, List<TransferSchedule>> _schedulesByOrigin = new();
private double _totalTime;                           // accumulated game time
```

**New IBuildingBehavior overrides:**
- `OnAttach(Building owner)` — set `_owner`, find `IOrbitalBody` parent via
  `VisualNode` parent chain, cache in `_body`
- `OnRegister()` — call `_body?.RegisterTransferEndpoint(...)` instead of
  `mgr.RegisterEndpoint(...)`
- `OnUnregister()` — call `_body?.UnregisterTransferEndpoint(...)`, stop all
  schedules for this origin, clear `_endpoint` / `_endpointDef`
- `OnDetach()` — clear `_owner`, `_body`, `_endpoint`, `_activeTransfers`, `_schedulesByOrigin`
- `OnManufactureTick(float delta, Building owner)` — call `TickActiveTransfers(dt)`
  and `TickSchedules(dt)`. Note: signature matches `IBuildingBehavior`;
  `owner` is redundant because `_owner` is already cached.

**All public methods ported from BodyTransferManager (scoped to this behavior's state):**
```
RegisterEndpoint(...)        — removed; replaced by OnRegister → body registration
UnregisterEndpoint(...)      — removed; replaced by OnUnregister → body unregistration
HasEndpoint(id)              → public bool HasEndpoint(string id)
GetEndpointBuilding(id)      → public Building? GetEndpointBuilding(string id)
GetCapacity(id)              → public float GetCapacity(string id)  [reads _endpointDef]
GetMaxConcurrentTransfers(id)→ public int GetMaxConcurrentTransfers(string id)
GetVehicleSpeed(id)          → public float GetVehicleSpeed(string id)
GetActiveTransferCountForOrigin(id) → public int GetActiveTransferCountForOrigin(string id)
GetEndpointsOnContinent(idx) → delegated to _body
GetTotalCapacityOnContinent(idx) → delegated to _body
DispatchOneTimeTransfer(...) → public string? DispatchOneTimeTransfer(...)
ComputeTravelTime(...)       → public float ComputeTravelTime(...)
CreateSchedule(...)          → public string? CreateSchedule(...)
ReorderSchedules(...)       → public bool ReorderSchedules(...)
GetSchedulesForDestination() → public IReadOnlyList<TransferSchedule> GetSchedulesForDestination(TransferDestination dest)
StartSchedule(id)            → public bool StartSchedule(string scheduleId)
StopSchedule(id)             → public bool StopSchedule(string scheduleId)
RemoveSchedule(id)           → public bool RemoveSchedule(string scheduleId)
GetSchedulesForOrigin(id)    → public IReadOnlyList<TransferSchedule> GetSchedulesForOrigin(string originBuildingId)
IsTransferActive(id)         → public bool IsTransferActive(string orderId)
GetActiveTransfers()         → public IReadOnlyCollection<ActiveTransfer> GetActiveTransfers()
GetAllSchedules()            → public IReadOnlyList<TransferSchedule> GetAllSchedules()
```

**Private helpers (ported from BodyTransferManager):**
```
TickActiveTransfers(float delta)
CompleteTransfer(ActiveTransfer transfer)
TickSchedules(float delta)
TickSchedule(TransferSchedule schedule, string originId)
TickScheduleAccumulating(TransferSchedule schedule, string originId)
TickScheduleDispatched(TransferSchedule schedule)
ResolveEndpoint(TransferDestination destination)  — looks up via _body
GetTransportWeight(string resourceId)
StopAllSchedulesForOrigin(string originBuildingId)
FindSchedule(string scheduleId)
ComputeDistance(string originBuildingId, TransferDestination destination)
```

**Nested class:**
```csharp
public class ActiveTransfer
{
    public TransferOrder Order { get; set; } = null!;
}
```

**Key differences from BodyTransferManager:**
- `RegisterEndpoint` / `UnregisterEndpoint` are GONE — replaced by `OnRegister` /
  `OnUnregister` which delegate to the body
- `TickActiveTransfers` / `TickSchedules` called from `OnManufactureTick`, not
  `_PhysicsProcess`
- Endpoint data (`_endpointDef`, `_endpointBuildings`) copied at registration time
  so the behavior is self-contained after unregistration
- `_body` reference used for `ResolveEndpoint` (destination lookup) and body-level
  capacity queries

---

### Body-Level Aggregation Strategy

Several UI panels show **aggregate** body-level metrics (total active transfers,
schedules per continent, etc.) that were previously cheap because
`BodyTransferManager` held all state centrally. Once state is fragmented across
per-building `TransferStationBehavior` instances, the body must aggregate on demand.

Recommended approach:
1. `IOrbitalBody` endpoint registry tracks only `Building` references and
   `TransferStationDefinition` instances.
2. Body-level queries (`GetTotalActiveTransfers`, `GetSchedulesForOrigin` across
   all buildings, etc.) are implemented as extension methods or lightweight
   `IOrbitalBody` helpers that iterate `GetAllTransferEndpoints()`, resolve each
   endpoint’s owning `Building`, call `building.GetBehavior<TransferStationBehavior>()`
   if needed, and sum results.
3. UI panels (`OrbitalBodyDetailsPanel`, `OrbitalBodyHeaderPanel`,
   `OrbitalBodyTabbedPanel`, `ContinentTabbedPanel`) must switch from
   `_body.TransferMgr?.Property` calls to these body-level aggregation helpers
   (or, where per-building data is correct, query the specific building’s behavior
   directly).

This preserves state decentralization while allowing UI rollups.

#### `GetSchedulesForDestination` Semantics

The old `BodyTransferManager.GetSchedulesForDestination` scanned **all origins**
and returned every schedule targeting that destination, regardless of which hub
initiated it.

The per-behavior `TransferStationBehavior.GetSchedulesForDestination` returns
only schedules **from this building** to that destination.

- In `PickDestinationView` showing “X routes already filed”, per-origin counts are
  acceptable UX.
- If a true body-wide count is required, add a body-level aggregator that iterates
  all endpoint buildings and sums their behavior counts.

---

### Modify: UI Components

#### `Scripts/UI/BuildingInfo/HubPanelDetails.cs`

- `FindBodyTransferManager()` tree-walk — **delete entirely**. Replace with:
  - Resolve the building’s own `TransferStationBehavior` via
    `_building?.GetBehavior<TransferStationBehavior>()`.
  - `UpdateTransfers(bool isTransport)` now reads from that behavior’s
    `GetActiveTransfers()` and `GetAllSchedules()` (per-building view, which is
    the correct scope for a per-building panel).
- Update comment at line 342 to reference `TransferStationBehavior` and `IOrbitalBody`.

#### `Scripts/UI/TransferPlanning/DispatchSlipsWindow.cs`

- `_mgr : BodyTransferManager?` field → `_behavior : TransferStationBehavior?`
- `ShowWindow(...)` — after resolving `_originBuilding`, get its
  `TransferStationBehavior` via `originBuilding.GetBehavior<TransferStationBehavior>()`
  ```csharp
  _behavior = originBuilding?.GetBehavior<TransferStationBehavior>();
  if (_behavior == null) { ToastSystem.Instance?.ShowError("..."); return; }
  ```
- All `_mgr?.Method(...)` calls → `_behavior?.Method(...)`
- Remove `body switch { CelestialBody/SatelliteBody }` that extracts `TransferMgr`

#### `Scripts/UI/TransferPlanning/SlipsListView.cs`

- `_mgr : BodyTransferManager?` → `_behavior : TransferStationBehavior?`
- `Bind(...)` signature: `void Bind(TransferStationBehavior? behavior, string originBuildingId)`
- `Refresh()`: replace `_mgr.GetSchedulesForOrigin(_originBuildingId)` with
  `_behavior?.GetSchedulesForOrigin(_originBuildingId)`

#### `Scripts/UI/TransferPlanning/PickDestinationView.cs`

- `_mgr : BodyTransferManager?` → `_behavior : TransferStationBehavior?`
- `_body : CelestialBody?` stays (for board rendering)
- `Bind(...)` signature: `void Bind(TransferStationBehavior? behavior, string originBuildingId, Node3D body)`
- `UpdateCard()`: replace `_mgr.ComputeTravelTime(...)` with
  `_behavior?.ComputeTravelTime(...)`; replace `_mgr.GetSchedulesForDestination(dest)`
  with `_behavior?.GetSchedulesForDestination(dest)`

#### `Scripts/UI/TransferPlanning/ManifestEditorView.cs`

- `_mgr : BodyTransferManager?` → `_behavior : TransferStationBehavior?`
- `Bind(...)` signature: `void Bind(TransferStationBehavior? behavior, string originBuildingId, Theme? _)`
- `UpdateScale()`: replace `_mgr.GetCapacity(_originBuildingId)` with
  `_behavior?.GetCapacity(_originBuildingId)`
- `OnFinish()`: replace `_mgr.CreateSchedule(...)`, `_mgr.StartSchedule(...)`,
  `_mgr.RemoveSchedule(...)` with `_behavior` equivalents

#### `Scripts/UI/TransferPlanning/PriorityEditView.cs`

- `_mgr : BodyTransferManager?` → `_behavior : TransferStationBehavior?`
- `Bind(...)` signature: `void Bind(TransferStationBehavior? behavior, string originBuildingId)`
- `Refresh()`: replace `_mgr.GetSchedulesForOrigin(_originBuildingId)` with
  `_behavior?.GetSchedulesForOrigin(_originBuildingId)`
- `OnCardSwapRequested()`: replace `_mgr.ReorderSchedules(...)` with
  `_behavior?.ReorderSchedules(...)`

#### `Scripts/UI/PlanetBoard/Modes/TransferRoutePlanningMode.cs`

- `DrawOverlay()`: replace `_body?.TransferMgr` with body endpoint query
  ```csharp
  var body = _body as IOrbitalBody;
  if (body == null) return;
  foreach (var bv in _view.BuildingViews) {
      string id = bv.Building?.Id ?? "";
      if (string.IsNullOrEmpty(id) || !body.HasTransferEndpoint(id)) continue;
      ...
  }
  ```
- `GetTooltip()`: same replacement
- `TryPickFromBuilding()`: same replacement
- `HasEndpoint(id)` calls → `body.HasTransferEndpoint(id)`

#### `Scripts/UI/TransferPlanning/SlipDataBuilder.cs`

- `BuildFromSchedule(...)` signature unchanged — `BodyTransferManager? mgr` is only
  used for `ComputeTravelTime` and `GetCapacity`. After refactor, callers pass
  `null` for `mgr` and supply a `(float travelTime, float capacity)` tuple instead,
  OR a new overload accepts `TransferStationBehavior`.
- Simpler fix: add a new overload:
  ```csharp
  public static SlipCardData BuildFromSchedule(
      TransferSchedule schedule,
      TransferStationBehavior? behavior,
      ResourceDatabase? resources)
  ```
  This reads travel time and capacity from the behavior directly.
  Deprecate / remove the `BodyTransferManager` overload once all callers are updated.

---

### Modify: Additional UI Files (Previously Missing from Plan)

#### `Scripts/UI/StateMachine/States/TransferPlanningState.cs`

- Remove `body switch { CelestialBody/SatelliteBody } => cb.TransferMgr` logic.
- Use `IOrbitalBody.GetTransferEndpointsOnContinent(continentIndex)` to find the
  first hub building on the continent.
- Use `IOrbitalBody.GetTransferEndpointBuilding(hubId)` to get the `Building`.
- Pass that `Building` to `DispatchSlipsWindow.ShowWindow(...)`; the window itself
  will resolve the behavior.

#### `Scripts/UI/ContinentInfo/ContinentTabbedPanel.cs`

- Remove references to `BodyTransferManager.ActiveTransfer` as a public type name.
  `ActiveTransfer` will now live under `TransferStationBehavior` or in
  `Structures.Transfers`. Update the snapshot lists to the new type.
- For transfer tab population, replace `BodyTransferManager` calls with body-level
  aggregation helpers (or iterate endpoint buildings and query behaviors).

#### `Scripts/UI/OrbitalBodyWindow/OrbitalBodyDetailsPanel.cs`

- Replace all `_body.TransferMgr` usage:
  - Use body-level aggregation helpers for total active transfer counts and
    continent-level schedule / transfer rollups.

#### `Scripts/UI/OrbitalBodyWindow/OrbitalBodyHeaderPanel.cs`

- Replace `body.TransferMgr?.ActiveTransferCount` with a body-level aggregation
  helper (e.g., sum all endpoint behaviors’ active transfer counts).

#### `Scripts/UI/OrbitalBodyWindow/OrbitalBodyTabbedPanel.cs`

- In `PopulateTransfers` (Transfers tab), replace `transferMgr` references with
  body-level aggregation over endpoint buildings and their behaviors.

---

### Modify: Building (small)

#### `Scripts/Constructables/Buildings/Building.cs`

- Comment at line 342 (`BodyTransferManager`) — update to reference
  `TransferStationBehavior` and `IOrbitalBody`

#### `Scripts/Constructables/Buildings/BuildingResourceEndpoint.cs`

- Update XML doc comments that mention `BodyTransferManager` to reference
  `TransferStationBehavior`.

---

### Type Relocation: `ActiveTransfer`

`BodyTransferManager.ActiveTransfer` is currently referenced by
`ContinentTabbedPanel` as a public nested type.

- Move `ActiveTransfer` to a top-level class under `Structures.Transfers`
  (`Structures/GameState/ActiveTransfer.cs`) **OR** keep it as a nested class
  under `TransferStationBehavior`.
- Update `ContinentTabbedPanel` type references accordingly.
- Ensure the nested class in `TransferStationBehavior` is `public` so it matches
  the visibility of the old `BodyTransferManager.ActiveTransfer`.

---

## Tests

### Delete

- `Tests/Constructables/BodyTransferManagerTest.cs`

### New/Expanded

#### `Tests/Constructables/Buildings/Behaviors/TransferStationBehaviorTest.cs`

Port all `BodyTransferManager` tests and add new coverage for the per-behavior
lifecycle. Minimum required test cases:

```csharp
[TestSuite]
public class TransferStationBehaviorTest
{
    // --- Endpoint registration / query ---
    [TestCase] public void RegisterBehavior_ExposesEndpointForDispatch();
    [TestCase] public void HasEndpoint_ReturnsTrueAfterRegister();
    [TestCase] public void GetCapacity_ReturnsDefValue();
    [TestCase] public void GetMaxConcurrentTransfers_ReturnsDefValue();
    [TestCase] public void GetVehicleSpeed_ReturnsDefValue();

    // --- One-time transfer dispatch & completion ---
    [TestCase] public void DispatchOneTimeTransfer_DeliversToDestinationAfterTravelTime();
    [TestCase] public void DispatchOneTimeTransfer_RejectedWhenOriginNotRegistered(); // should still guard
    [TestCase] public void DispatchOneTimeTransfer_RejectedWhenMaxConcurrentReached();
    [TestCase] public void TransferArrival_DestinationFull_RevertsToOrigin();
    [TestCase] public void TransferArrival_DestinationGone_OriginReverts();
    [TestCase] public void TransferArrival_BothGone_LogsLostCargo();

    // --- Schedule CRUD ---
    [TestCase] public void CreateSchedule_ReturnsIdAndIsIdle();
    [TestCase] public void CreateSchedule_WithoutRegisteredOrigin_ReturnsNull();
    [TestCase] public void StartSchedule_ChangesStateToAccumulating();
    [TestCase] public void StopSchedule_ChangesStateToStopped();
    [TestCase] public void RemoveSchedule_RemovesFromList();
    [TestCase] public void ReorderSchedules_UpdatesPriority();

    // --- Schedule threshold & tick logic ---
    [TestCase] public void CreateSchedule_AnyResourceThreshold_DepartsWhenOneResourceReady();
    [TestCase] public void CreateSchedule_AllResourcesThreshold_WaitsForAll();
    [TestCase] public void CreateSchedule_WaitTimer_DepartsAfterElapsedTime();
    [TestCase] public void CreateSchedule_InsufficientStockpile_DoesNotDepart();
    [TestCase] public void CreateSchedule_ThenStartSchedule_DispatchesWhenThresholdMet();

    // --- Tick / in-flight state ---
    [TestCase] public void OnManufactureTick_AdvancesInFlightTransfers();
    [TestCase] public void OnManufactureTick_MultipleSteps_AdvancesBothTransfersAndSchedules();

    // --- Unregister / cleanup ---
    [TestCase] public void OnUnregister_StopsSchedulesFromThatOrigin();
    [TestCase] public void OnUnregister_ClearsActiveTransfers();
    [TestCase] public void OnDetach_ClearsAllState();
}
```

#### `Tests/ProceduralGeneration/IOrbitalBodyRegistryTest.cs` (Suggested)

Add a dedicated test file verifying the endpoint registry methods on
`CelestialBody` and `SatelliteBody`:

```csharp
[TestSuite]
public class CelestialBodyTransferRegistryTest
{
    [TestCase] public void RegisterEndpoint_AddsToAllEndpointsList();
    [TestCase] public void RegisterEndpoint_SetsDefinitionAndBuildingRefs();
    [TestCase] public void UnregisterEndpoint_RemovesFromAllLists();
    [TestCase] public void HasTransferEndpoint_KnownId_ReturnsTrue();
    [TestCase] public void GetTransferEndpointsOnContinent_FiltersByContinentIndex();
    [TestCase] public void GetTotalTransferCapacityOnContinent_SumsDefinitions();
    [TestCase] public void UnregisterEndpoint_EmitsContinentCapacityChanged();
}
```

---

## Implementation Order

1. Move `ActiveTransfer` to `TransferStationBehavior` or `Structures.Transfers`
   and update `ContinentTabbedPanel` type references.
2. Modify `IOrbitalBody` interface — add endpoint registry methods, remove `TransferMgr`
3. Implement endpoint registry in `CelestialBody` (including signal emission)
4. Implement endpoint registry in `SatelliteBody` (including signal emission)
5. Add stubs in `Barycenter.cs` for new `IOrbitalBody` methods
6. Delete `BodyTransferManager.cs`
7. Expand `TransferStationBehavior` — inline all transfer logic from `BodyTransferManager`
8. Update `HubPanelDetails` — replace `FindBodyTransferManager` tree-walk
9. Update `SlipsListView`, `PickDestinationView`, `ManifestEditorView`, `PriorityEditView`
10. Update `DispatchSlipsWindow` — use `GetBehavior<TransferStationBehavior>()` instead of body switch
11. Update `TransferRoutePlanningMode`
12. Update `SlipDataBuilder`
13. Update `TransferPlanningState`
14. Update `ContinentTabbedPanel`
15. Update `OrbitalBodyDetailsPanel`, `OrbitalBodyHeaderPanel`, `OrbitalBodyTabbedPanel`
16. Update `Building.cs` and `BuildingResourceEndpoint.cs` doc comments
17. Delete old test file; write new `TransferStationBehaviorTest` + registry tests
18. Build and verify

(End of file)
