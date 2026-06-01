# Logistics Unit Movement System - Implementation Plan

## Executive Summary

This document outlines the implementation plan for a comprehensive logistics unit movement system in the Planet Generation game. The system enables space-based cargo transport using realistic orbital mechanics, specifically the Izzo Lambert solver for trajectory calculation.

### Key Features
- **Trajectory Planning**: Multiple transfer options with different time-of-flight/delta-v tradeoffs
- **Runtime Engine Modifiers**: Stackable upgrades, damage, and environmental effects
- **Gravity Assist Suggestions**: Auto-detected opportunities for fuel-efficient transfers
- **Hybrid Simulation**: Real-time when observed, simplified when off-screen
- **Instant Burn Model**: Delta-v applied instantly, matching Lambert solver assumptions

---

## System Architecture

### Directory Structure

```
Scripts/
├── Logistics/                              # NEW TOP-LEVEL DIRECTORY
│   ├── LogisticsUnit.cs                    # Main unit class
│   ├── LogisticsMovementController.cs      # Movement simulation
│   ├── TrajectoryPlanner.cs                # Route planning
│   └── ThrustPerformanceCalculator.cs      # Payload/range calculations
├── Structures/
│   └── Logistics/
│       ├── EngineDefinition.cs             # Engine with runtime modifiers
│       ├── EngineModifier.cs               # Modifier struct with factory methods
│       ├── AppliedModifier.cs              # Modifier tracking types
│       ├── CargoManifest.cs                # Cargo management
│       ├── TrajectorySolution.cs           # Lambert output wrapper
│       └── LogisticsUnitState.cs           # Unit state enum
└── UtilityLibrary/
    ├── OrbitalMath.cs                      # Extended with Lambert methods
    ├── LambertSolver.cs                    # Izzo algorithm implementation
    └── GravityAssistCalculator.cs          # Phase 2: Gravity assists

Configuration/
└── Logistics/
    ├── EngineTypes.toml                    # Engine definitions
    └── ShipTemplates.toml                  # Ship class templates
```

---

## Phase 1: Core Data Structures

### 1.1 EngineDefinition.cs

**Purpose**: Define engine properties with runtime-modifiable stats.

**Key Properties**:
- `BaseSpecificImpulse` - Base Isp in seconds
- `BaseThrust` - Base thrust in Newtons
- `EffectiveSpecificImpulse` - Computed after modifiers
- `EffectiveThrust` - Computed after modifiers
- `ExhaustVelocity` - Derived from effective Isp

**Modifier Stacking Order**:
```
Final Value = (Base + Σ(AdditiveModifiers)) × Π(MultiplicativeModifiers)
```

**Example**:
```
Base Isp = 3000 s
Additive: +200, +50 = +250 total
Multiplicative: ×1.1, ×0.95 = ×1.045

Step 1: 3000 + 250 = 3250
Step 2: 3250 × 1.045 = 3396.25 s (final)
```

**Key Methods**:
- `ApplyModifier(EngineModifier)` - Add and track modifier
- `RemoveModifier(string source)` - Remove by source ID
- `GetDetailedBreakdown()` - Full step-by-step calculation display
- `GetModifierHistory()` - Access to all applied modifiers

### 1.2 EngineModifier.cs

**Purpose**: Define individual modifiers with type distinction.

**Modifier Types**:
- `Additive` - Adds/subtracts flat value
- `Multiplicative` - Multiplies/divides by percentage

**Factory Methods**:
```csharp
// Additive modifiers
EngineModifier.Additive(string source, float ispBonus, float thrustBonus)
EngineModifier.AdditiveIsp(string source, float ispBonus)
EngineModifier.AdditiveThrust(string source, float thrustBonus)

// Multiplicative modifiers
EngineModifier.Multiplicative(string source, float efficiencyPercent, float thrustPercent)
EngineModifier.MultiplicativeIsp(string source, float efficiencyPercent)
EngineModifier.MultiplicativeThrust(string source, float thrustPercent)

// Convenience methods
EngineModifier.Upgrade(string source, float ispBonus)
EngineModifier.Damage(string source, float percentReduction)
EngineModifier.ThrustBoost(string source, float percentIncrease)
EngineModifier.Wear(string source, float efficiencyLoss, float thrustLoss)
```

### 1.3 AppliedModifier.cs

**Purpose**: Track modifier application with before/after state.

**Types**:
- `EngineState` - Snapshot of engine stats
- `AppliedModifier` - Modifier + timestamp + state change

### 1.4 CargoManifest.cs

**Purpose**: Manage cargo loadout.

**Key Properties**:
- `Dictionary<string, float> Resources` - Resource quantities
- `TotalCargoMass` - Sum of all resource weights

**Key Methods**:
- `LoadResource(string id, float quantity)`
- `UnloadResource(string id, float quantity)`
- `GetResourceQuantity(string id)`

### 1.5 TrajectorySolution.cs

**Purpose**: Wrap Lambert solver output.

**Key Properties**:
- `InitialVelocity` (Vector3) - Required velocity at start
- `FinalVelocity` (Vector3) - Velocity at arrival
- `TimeOfFlight` (float) - Transfer duration
- `DeltaVRequired` (float) - Total delta-v needed
- `SemiMajorAxis`, `Eccentricity` - Orbital parameters
- `Revolutions` (int) - For multi-rev transfers
- `TransferType` (enum) - Direct, MultiRev, GravityAssist

### 1.6 LogisticsUnitState.cs

**Enum Values**:
- `Idle` - No active trajectory
- `Planning` - Calculating route options
- `InTransit` - Executing transfer
- `Arriving` - Approaching destination
- `Disabled` - Cannot move

---

## Phase 2: Orbital Math Extensions

### 2.1 OrbitalMath.cs Extensions

Add to existing utility class:

```csharp
// Lambert Solver wrapper
public static List<TrajectorySolution> SolveLambert(
    Vector3 r1,           // Start position
    Vector3 r2,           // End position
    float tof,            // Time of flight
    float mu,             // Gravitational parameter (G * M_central)
    int maxRevolutions = 0,
    bool retrograde = false
)

// Delta-v calculation
public static float CalculateDeltaV(Vector3 currentVelocity, Vector3 requiredVelocity)

// Generate multiple trajectory options
public static List<TrajectorySolution> GetTrajectoryOptions(
    Vector3 startPosition,
    Vector3 endPosition,
    float centralBodyMass,
    float[] timeOfFlightOptions
)

// Position along Kepler orbit at time t
public static Vector3 GetPositionOnOrbit(
    Vector3 positionAtT0,
    Vector3 velocityAtT0,
    float mu,
    float deltaTime
)

// Gravity assist detection
public static List<GravityAssistOpportunity> DetectGravityAssists(
    Vector3 startPos,
    Vector3 endPos,
    List<CelestialBody> bodies,
    float maxTimeOfFlight
)
```

### 2.2 LambertSolver.cs

**Purpose**: Implement Izzo algorithm (port from pykep C++).

**Algorithm Overview**:
1. Calculate chord and semiperimeter
2. Compute lambda parameter
3. Use Householder iterations to find x values
4. Reconstruct terminal velocities
5. Handle multiple revolutions (0-rev, 1-rev left/right, etc.)

**Key Implementation Details**:
- Uses `boost::math::acosh` and `asinh` - need C# equivalents
- Hypergeometric function for Battin series
- Time-of-flight expressions: Battin, Lagrange, Lancaster

---

## Phase 3: Logistics Unit Class

### 3.1 LogisticsUnit.cs

**Purpose**: Main unit class with physics and navigation.

**Physical Properties**:
```csharp
public float DryMass;              // kg - empty ship mass
public float CargoCapacity;        // kg - max cargo
public float CurrentFuelMass;      // kg - remaining fuel
public float MaxFuelMass;          // kg - fuel tank capacity
public float TotalMass => DryMass + CargoManifest.TotalCargoMass + CurrentFuelMass;
```

**Engine Integration**:
```csharp
public EngineDefinition Engine { get; private set; }

// Tsiolkovsky rocket equation
private float CalculateRemainingDeltaV()
{
    float exhaustVelocity = Engine.ExhaustVelocity;
    float wetMass = TotalMass;
    float dryMassWithCargo = DryMass + CargoManifest.TotalCargoMass;
    return exhaustVelocity * Mathf.Log(wetMass / dryMassWithCargo);
}

// Fuel consumption for a burn
private float CalculateFuelForDeltaV(float deltaV)
{
    float exhaustVelocity = Engine.ExhaustVelocity;
    return TotalMass * (1f - Mathf.Exp(-deltaV / exhaustVelocity));
}
```

**Navigation Methods**:
```csharp
public List<TrajectoryOption> PlanRoute(CelestialBody destination);
public void ExecuteTrajectory(TrajectorySolution trajectory);
public void ApplyBurn(Vector3 deltaV);
public void ApplyEngineModifier(EngineModifier modifier);
```

**Performance Queries**:
```csharp
public float GetMaxDeliverableCargo(CelestialBody destination);
public float GetRangeForCurrentCargo();
public string GetDetailedEngineStatus();
```

---

## Phase 4: Trajectory Planning System

### 4.1 TrajectoryPlanner.cs

**Purpose**: Generate and rank trajectory options.

**Responsibilities**:
1. Get future positions of origin and destination (moving planets)
2. Calculate Lambert solutions for various TOFs
3. Check gravity assist opportunities
4. Calculate required delta-v for each option
5. Filter by unit's available delta-v
6. Rank by efficiency (fuel) or time

**Key Method**:
```csharp
public List<TrajectoryOption> GetOptions(
    LogisticsUnit unit,
    CelestialBody origin,
    CelestialBody destination,
    float earliestDeparture = 0f
)
```

---

## Phase 5: Movement Simulation

### 5.1 LogisticsMovementController.cs

**Purpose**: Hybrid simulation of unit movement.

**Hybrid Approach**:
- Real-time Kepler propagation when observed
- On-demand position calculation when off-screen
- Warp capability for time-skip scenarios

**Key Methods**:
```csharp
public override void _PhysicsProcess(double delta)
{
    if (_isObserved)
    {
        UpdatePositionRealTime((float)delta);
    }
}

private void UpdatePositionRealTime(float delta)
{
    // Use Kepler propagation for ballistic flight
    // Position = OrbitalMath.GetPositionOnOrbit(...)
    
    ElapsedFlightTime += delta;
    
    if (ElapsedFlightTime >= CurrentTrajectory.TimeOfFlight)
    {
        ExecuteArrivalBurn();
    }
}

public void WarpToTime(float targetTime)
{
    // Skip ahead - calculate final position directly
}
```

---

## Phase 6: Thrust Performance Calculator

### 6.1 ThrustPerformanceCalculator.cs

**Purpose**: Calculate payload capacity and range based on thrust.

**Key Methods**:

```csharp
// How much cargo can be delivered?
public static float CalculateMaxPayload(
    EngineDefinition engine,
    float dryMass,
    float fuelMass,
    float requiredDeltaV,
    float marginFactor = 0.9f
)

// How far can this ship travel with given cargo?
public static float CalculateMaxDeltaV(
    EngineDefinition engine,
    float dryMass,
    float fuelMass,
    float payloadMass
)

// Range calculation with reserve fuel
public static float CalculateRange(
    EngineDefinition engine,
    float dryMass,
    float fuelMass,
    float cargoMass,
    float reserveFuelFraction = 0.1f
)

// Is this transfer realistic?
public static TransferFeasibility AssessTransferFeasibility(
    EngineDefinition engine,
    float totalMass,
    float requiredDeltaV,
    float transferTime,
    float timeTolerance = 0.1f
)
```

---

## Phase 7: Configuration Files

### 7.1 EngineTypes.toml

```toml
[[engine]]
name = "Ion Drive"
specific_impulse = 3000  # seconds
thrust = 0.1  # N
description = "Efficient but slow electric propulsion"

[[engine]]
name = "Chemical Rocket"
specific_impulse = 350
thrust = 10000
description = "High thrust, lower efficiency"

[[engine]]
name = "Nuclear Thermal"
specific_impulse = 900
thrust = 5000
description = "Balanced efficiency and thrust"

[[engine]]
name = "Fusion Torch"
specific_impulse = 5000
thrust = 1000
description = "Advanced high-efficiency engine"
```

### 7.2 ShipTemplates.toml

```toml
[[ship]]
name = "Cargo Freighter"
dry_mass = 50000  # kg
cargo_capacity = 200000
fuel_capacity = 30000
default_engine = "Ion Drive"

[[ship]]
name = "Fast Courier"
dry_mass = 10000
cargo_capacity = 5000
fuel_capacity = 15000
default_engine = "Chemical Rocket"

[[ship]]
name = "Heavy Transport"
dry_mass = 100000
cargo_capacity = 500000
fuel_capacity = 80000
default_engine = "Nuclear Thermal"
```

---

## Phase 8: Gravity Assist System (Future Enhancement)

### 8.1 GravityAssistCalculator.cs

**Purpose**: Detect and calculate gravity assist opportunities.

**Key Types**:
```csharp
public struct GravityAssistOpportunity
{
    public CelestialBody AssistBody;
    public float ApproachTime;
    public float DeltaVSavings;
    public float DeflectionAngle;
    public Vector3 ExitVelocity;
}
```

**Key Methods**:
```csharp
public static GravityAssistOpportunity? FindAssistOpportunity(
    Vector3 currentVelocity,
    Vector3 targetDirection,
    CelestialBody body,
    float maxDeviation
)
```

---

## Logging and Debugging

### Modifier Application Log

When a modifier is applied, detailed logging occurs:

```
[INFO] [Ion Drive] Modifier applied: 'Solar Panel Damage'
[DEBUG]   Timestamp: 2026-02-24 14:32:15.123
[DEBUG]   Type: Multiplicative
[DEBUG]   --- Efficiency (Isp) ---
[DEBUG]     Applied: ×0.8500 (-15.0%)
[DEBUG]     Additive sum: 200.00 → 200.00
[DEBUG]     Multiplicative product: 1.0000 → 0.8500
[DEBUG]     Calculation: (3000.00 + 200.00) × 0.8500
[DEBUG]     Result: 3200.00 → 2720.00 s
[DEBUG]   --- Thrust ---
[DEBUG]     Applied: ×1.0000 (0.0%)
[DEBUG]     Additive sum: 0.05 → 0.05
[DEBUG]     Multiplicative product: 1.0000 → 1.0000
[DEBUG]     Calculation: (0.10 + 0.05) × 1.0000
[DEBUG]     Result: 0.15 → 0.15 N
[DEBUG]   Total modifiers: 3
```

### Flexible Debugger Output

The `GetDetailedBreakdown()` method produces a formatted display that grows/shrinks with modifier count:

```
╔══════════════════════════════════════════════════════════════╗
║ Engine: Ion Drive                                            ║
╠══════════════════════════════════════════════════════════════╣
║ SPECIFIC IMPULSE                                            ║
╠══════════════════════════════════════════════════════════════╣
║ Base Value:                                      3000.00 s   ║
║ ─── Additive Modifiers ───                                  ║
║   Tech Level 2                                    +200.00 s  ║
║   Nozzle Tuning                                    +50.00 s  ║
║   SUBTOTAL                                        +250.00 s  ║
║   Base + Additives                                3250.00 s  ║
║ ─── Multiplicative Modifiers ───                            ║
║   Solar Panel Damage                              0.8500 (-15.0%)
║   Overclock                                       1.1000 (+10.0%)
║   PRODUCT                                         0.9350     ║
╠══════════════════════════════════════════════════════════════╣
║ FINAL: (3000.00 + 250.00) × 0.9350                           ║
║        =                                          3038.75 s  ║
╚══════════════════════════════════════════════════════════════╝
```

---

## Implementation Order

| Phase | Description | Dependencies | Priority |
|-------|-------------|--------------|----------|
| 1 | Core Data Structures | None | High |
| 2 | Orbital Math Extensions | Phase 1 | High |
| 3 | Logistics Unit Class | Phase 1, 2 | High |
| 4 | Trajectory Planning | Phase 2, 3 | High |
| 5 | Movement Simulation | Phase 3, 4 | High |
| 6 | Thrust Performance Calculator | Phase 1, 3 | Medium |
| 7 | Configuration Files | Phase 1, 3 | Medium |
| 8 | Gravity Assist System | Phase 2, 4 | Low (Future) |

---

## Integration Points

### Existing Codebase Integration

| New Component | Existing Component | Integration |
|---------------|-------------------|-------------|
| LogisticsUnit | CelestialBody | Query positions at specific times |
| TrajectoryPlanner | CelestialBody | Get future orbital positions |
| LogisticsMovementController | OrbitalMath | Use existing gravity constant |
| TrajectoryPlanner | Octree | Spatial queries for nearby bodies |
| TrajectoryPlanner | MeshGenerationThreadPool | Async Lambert calculations |

### Godot Integration

- LogisticsUnit extends `Node3D`
- Uses `_PhysicsProcess` for movement updates
- Signals for UI updates on trajectory changes
- Export attributes for inspector configuration

---

## Testing Strategy

### Unit Tests

1. **EngineModifier Tests**
   - Additive stacking
   - Multiplicative stacking
   - Mixed modifier order of operations
   - Modifier removal and recalculation

2. **LambertSolver Tests**
   - Known trajectory solutions
   - Multi-revolution transfers
   - Edge cases (collinear positions, 180° transfers)

3. **ThrustPerformanceCalculator Tests**
   - Max payload calculations
   - Range calculations
   - Feasibility assessments

### Integration Tests

1. **End-to-end Transfer**
   - Plan route from Earth to Mars
   - Execute trajectory
   - Verify arrival

2. **Modifier Effects on Transfer**
   - Apply damage modifier
   - Re-plan route
   - Verify reduced options

---

## Success Criteria

1. Logistics units can plan routes between any two celestial bodies
2. Multiple trajectory options are presented with different TOF/delta-v tradeoffs
3. Engine modifiers correctly affect performance metrics
4. Detailed logging shows all modifier effects
5. Thrust-based payload/range calculations are accurate
6. Movement simulation handles both real-time and warp scenarios
7. Configuration files allow easy balancing of engines and ships
