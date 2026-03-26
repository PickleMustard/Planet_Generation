# Refactor: Physics-Based Orbital Parameters for Artificial Satellites

## Context

The current artificial satellite system has several problems:
1. **Velocity is abstracted away** — position is calculated from angle + trig functions, so velocity is never tracked. This makes transfer calculations harder and prevents flexible placement.
2. **Duplicated logic** — `CalculateOrbitalParameters()` and `GetOrbitBandsFromParent()` are copy-pasted across ShipSatellite, StationSatellite, and LogisticsUnit.
3. **Hardcoded speed formula** — `baseSpeed / (1 + bandIndex * 0.5)` instead of physics-based `sqrt(G*M/r^3)`.
4. **OrbitBands don't store velocity** — each satellite recalculates speed independently.
5. **No continuous placement** — all satellites are locked to discrete orbital bands.

The refactor centralizes orbital parameter provisioning in the host body, uses physics-based velocity, and introduces continuous placement for large bodies (Stars, BlackHoles, NeutronStars).

---

## Phase 1: Foundation (no breaking changes)

### 1.1 Create `OrbitalParameters` struct
**New file:** `Scripts/Structures/GameState/OrbitalParameters.cs`

```csharp
public readonly struct OrbitalParameters
{
    public float Radius { get; init; }
    public float AngularSpeed { get; init; }
    public float LinearSpeed { get; init; }
    public Vector3 InitialPosition { get; init; }
    public Vector3 InitialVelocity { get; init; }
    public float HostMass { get; init; }
    public int BandIndex { get; init; }  // -1 for continuous placement
}
```

### 1.2 Add velocity fields to `OrbitBand`
**File:** `Scripts/Structures/GameState/OrbitBand.cs`

- Add `[Export] float AngularSpeed` and `[Export] float LinearSpeed` properties
- Update constructors and `ToString()`

### 1.3 Update `OrbitConfiguration` to compute physics-based speeds
**File:** `Scripts/Structures/GameState/OrbitConfiguration.cs`

- Modify `CreateOrbitBand(int index, float bodyRadius)` → `CreateOrbitBand(int index, float bodyRadius, float hostMass)`
  - Compute: `angularSpeed = sqrt(G * M / r^3)` using `OrbitalMath.GRAVITATIONAL_CONSTANT`
  - Compute: `linearSpeed = radius * angularSpeed`
- Modify `CreateAllOrbitBands(float bodyRadius)` → `CreateAllOrbitBands(float bodyRadius, float hostMass)`
- Remove `BaseOrbitalSpeed` property and `DefaultBaseOrbitalSpeed` constant

---

## Phase 2: IOrbitalBody and Implementations

### 2.1 Extend `IOrbitalBody` interface
**File:** `Scripts/ProceduralGeneration/IOrbitalBody.cs`

Add inside a `#region OrbitalParameters`:
```csharp
bool UsesBandPlacement { get; }
OrbitalParameters GetOrbitalParametersForBand(int bandIndex, float startingAngle);
OrbitalParameters GetOrbitalParametersAtRadius(float radius, float startingAngle);
```

### 2.2 Update `CelestialBody`
**File:** `Scripts/ProceduralGeneration/CelestialBody.cs`

- Add `UsesBandPlacement` property:
  ```csharp
  public bool UsesBandPlacement => Type switch
  {
      CelestialBodyType.Star => false,
      CelestialBodyType.BlackHole => false,
      CelestialBodyType.NeutronStar => false,
      _ => true,
  };
  ```
- Modify `InitializeOrbitSystem()`:
  - Band-based: pass `Mass` to `CreateAllOrbitBands(bodyRadius, Mass)`
  - Continuous: set `OrbitBands` to empty, skip band creation, still create `SatellitesContainer`
- Implement `GetOrbitalParametersForBand(bandIndex, startingAngle)`:
  - Read radius/speed from `OrbitBands[bandIndex]`
  - Compute position = `(cos(angle)*r, 0, sin(angle)*r)`
  - Compute velocity = `(-sin(angle)*linearSpeed, 0, cos(angle)*linearSpeed)`
- Implement `GetOrbitalParametersAtRadius(radius, startingAngle)`:
  - Compute `angularSpeed = sqrt(G*M/r^3)`, `linearSpeed = r * angularSpeed`
  - Same position/velocity formulas
- Update `GetOrbitalSpeedForBand()` to return `OrbitBands[bandIndex].AngularSpeed`
- Wrap all orbital methods in `#region OrbitalParameters` / `#endregion`

### 2.3 Update `SatelliteBody`
**File:** `Scripts/ProceduralGeneration/SatelliteBody.cs`

- Add `UsesBandPlacement => true` (satellite bodies always use bands)
- Same `InitializeOrbitSystem` change: pass `Mass` to `CreateAllOrbitBands`
- Implement `GetOrbitalParametersForBand` and `GetOrbitalParametersAtRadius` (same as CelestialBody)
- Wrap orbital methods in `#region OrbitalParameters`

---

## Phase 3: Satellite Consumers

### 3.1 Update `IArtificialSatellite` interface
**File:** `Scripts/Constructables/ArtificialSatellites/IArtificialSatellite.cs`

Add inside `#region OrbitalParameters`:
```csharp
float OrbitalAngle { get; }
float OrbitalRadius { get; }
float OrbitalSpeed { get; }
void InitializeOrbit(IOrbitalBody hostBody, int bandIndex);
void InitializeOrbitAtRadius(IOrbitalBody hostBody, float radius);
```

### 3.2 Refactor `StationSatellite`
**File:** `Scripts/Constructables/ArtificialSatellites/StationSatellite.cs`

**Remove:**
- `CalculateOrbitalParameters()` method
- `GetOrbitBandsFromParent()` method (dynamic cast)
- `DefaultOrbitalSpeed` constant, `_bodyRadius` field

**Add** `#region OrbitalParameters` containing:
- `_orbitalAngle`, `_orbitalRadius`, `_orbitalSpeed`, `_hostMass` fields
- `InitializeOrbit(IOrbitalBody hostBody, int bandIndex)`:
  - Get random starting angle
  - Call `hostBody.GetOrbitalParametersForBand(bandIndex, angle)` or `GetOrbitalParametersAtRadius` based on `hostBody.UsesBandPlacement`
  - Set `_orbitalRadius`, `_orbitalSpeed`, `_orbitalAngle`, `Velocity` from returned params
- `InitializeOrbitAtRadius(IOrbitalBody hostBody, float radius)`: Same but for continuous
- `Initialize(Node3D parentBody, int bandIndex)`: Backward-compat wrapper that casts to `IOrbitalBody` and calls `InitializeOrbit`

**Update `_PhysicsProcess`** to compute velocity each frame:
```csharp
_orbitalAngle += _orbitalSpeed * (float)delta;
float cos = Mathf.Cos(_orbitalAngle);
float sin = Mathf.Sin(_orbitalAngle);
GlobalPosition = parentBody.GlobalPosition + new Vector3(cos * _orbitalRadius, 0, sin * _orbitalRadius);
float linearSpeed = _orbitalRadius * _orbitalSpeed;
Velocity = new Vector3(-sin * linearSpeed, 0f, cos * linearSpeed);
```

**Update `_EnterTree`**: Remove `CalculateOrbitalParameters()` call. Defer init to `_Ready` or explicit `Initialize` call.

### 3.3 Refactor `ShipSatellite`
**File:** `Scripts/Constructables/ArtificialSatellites/ShipSatellite.cs`

Same pattern as StationSatellite:
- Remove `CalculateOrbitalParameters()`, `GetOrbitBandsFromParent()`, `DefaultOrbitalSpeed`, `_bodyRadius`
- Add `InitializeOrbit`, `InitializeOrbitAtRadius`
- Update `HandleOrbit()` to track velocity
- Update `Initialize(Node3D, int)` to delegate to `InitializeOrbit`
- Update `HandleTravel` arrival: cast destination to `IOrbitalBody`, call `InitializeOrbit`
- Wrap in `#region OrbitalParameters`

### 3.4 Refactor `LogisticsUnit`
**File:** `Scripts/Constructables/ArtificialSatellites/LogisticsUnit.cs`

Same pattern, but more call sites to update:
- Remove `CalculateOrbitalParameters()` (line 680), `DefaultOrbitalSpeed`, `_bodyRadius`
- Add `InitializeOrbit`, `InitializeOrbitAtRadius`
- Update all 5 call sites of `CalculateOrbitalParameters()`:
  - `_EnterTree` (line 147) → defer to `_Ready` or `Initialize`
  - `_Ready` (line 188) → call `InitializeOrbit` with parent as `IOrbitalBody`
  - `HandleTravel` arrival (line 982) → cast destination, call `InitializeOrbit`
  - `OnTransferComplete` (line 1500) → cast `newParentBody`, call `InitializeOrbit`
  - Transfer arrival method (line 1450) → cast `targetBody`, call `InitializeOrbit`
- `GetOrbitalVelocity()` can now return `Velocity` directly (maintained each frame)
- Wrap orbital fields/methods in `#region OrbitalParameters`

---

## Phase 4: Callers

### 4.1 Update `ConstructionManager`
**File:** `Scripts/Constructables/ConstructionManager.cs`

- Existing `CreateStation(IOrbitalBody, int, string?)` and `CreateLogisticsUnit(IOrbitalBody, int, string?)` work unchanged — the satellite's `Initialize` method internally delegates to `InitializeOrbit`
- Add new overloads for continuous placement:
  - `CreateStationAtRadius(IOrbitalBody targetBody, float radius, string? name)`
  - `CreateLogisticsUnitAtRadius(IOrbitalBody targetBody, float radius, string? name)`

### 4.2 Update `ConstructionManagerDebug`
**File:** `Scripts/Constructables/ConstructionManagerDebug.cs`

- Check `UsesBandPlacement` on target body
- Band-based: existing behavior (require band index)
- Continuous: accept radius parameter instead

### 4.3 Update `LogisticsMovementController`
**File:** `Scripts/Constructables/ArtificialSatellites/LogisticsMovementController.cs`

- `InitializeOrbitalState()` (line 400): Currently sets `_orbitalVelocity = Vector3.Zero`. Now can read `_logisticsUnit.Velocity` which is maintained each frame.
- `ProcessOrbit()` (line 419): Currently reads orbital params from LogisticsUnit getters and updates position. Add velocity update: `_logisticsUnit.Velocity = new Vector3(-sin * linearSpeed, 0, cos * linearSpeed)`

---

## Phase 5: Cleanup

- Remove `BaseOrbitalSpeed` from `OrbitConfiguration` constructors
- Remove `DefaultOrbitalSpeed` constants from all satellite types
- Remove `GetOrbitBandsFromParent()` dynamic-cast helpers from Ship/StationSatellite
- Run `dotnet build` and fix compilation errors
- Run `dotnet format`

---

## Files Modified (in order)

| # | File | Change |
|---|------|--------|
| 1 | `Scripts/Structures/GameState/OrbitalParameters.cs` | **NEW** — readonly struct |
| 2 | `Scripts/Structures/GameState/OrbitBand.cs` | Add AngularSpeed, LinearSpeed |
| 3 | `Scripts/Structures/GameState/OrbitConfiguration.cs` | Physics-based speed calc, remove BaseOrbitalSpeed |
| 4 | `Scripts/ProceduralGeneration/IOrbitalBody.cs` | Add UsesBandPlacement, GetOrbitalParametersForBand/AtRadius |
| 5 | `Scripts/ProceduralGeneration/CelestialBody.cs` | Implement new interface methods, update InitializeOrbitSystem, regions |
| 6 | `Scripts/ProceduralGeneration/SatelliteBody.cs` | Same as CelestialBody |
| 7 | `Scripts/Constructables/ArtificialSatellites/IArtificialSatellite.cs` | Add orbital properties and init methods |
| 8 | `Scripts/Constructables/ArtificialSatellites/StationSatellite.cs` | Remove dup logic, use host params, track velocity |
| 9 | `Scripts/Constructables/ArtificialSatellites/ShipSatellite.cs` | Same pattern |
| 10 | `Scripts/Constructables/ArtificialSatellites/LogisticsUnit.cs` | Same pattern, 5 call sites |
| 11 | `Scripts/Constructables/ConstructionManager.cs` | Add continuous-placement overloads |
| 12 | `Scripts/Constructables/ConstructionManagerDebug.cs` | Handle continuous vs band |
| 13 | `Scripts/Constructables/ArtificialSatellites/LogisticsMovementController.cs` | Update orbit velocity tracking |

## Existing Utilities to Reuse

- `OrbitalMath.GRAVITATIONAL_CONSTANT` (`Scripts/UtilityLibrary/OrbitalMath.cs`) — for `sqrt(G*M/r^3)` speed calculation
- `OrbitalMath.CalculateEccentricity()`, `CalculateOrbitalPosition()`, `CalculateEllipticalOrbitalVelocity()` — already used by SatelliteBody, not needed for circular artificial satellite orbits but available
- `Randomizer.GetRandomNumberGenerator()` (`Scripts/UtilityLibrary/Randomizer.cs`) — for random starting angles

## Verification

1. `dotnet build` — must compile cleanly
2. `dotnet format` — code style compliance
3. `dotnet test` — existing tests pass
4. Manual in-engine verification:
   - Spawn a station via debug console on a planet (band-based) → orbits at correct radius with velocity tracked
   - Spawn a station via debug console on a star (continuous) → orbits at specified radius
   - Spawn a logistics unit → verify velocity is non-zero during orbit
   - Execute a transfer between two planets → verify arrival re-initializes orbit from host body params
   - Verify `GetOrbitalVelocityRelativeTo` returns correct values for trajectory planning
