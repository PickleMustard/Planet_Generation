# Logistics System Documentation

The Logistics system provides a complete framework for managing interplanetary transportation, including spacecraft with engines, trajectory planning using Lambert solvers, cargo management, and fuel calculations using real physics-based models.

## Table of Contents

- [Overview](#overview)
- [Core Components](#core-components)
- [Engine System](#engine-system)
- [Trajectory Planning](#trajectory-planning)
- [Logistics Units](#logistics-units)
- [Cargo Management](#cargo-management)
- [State Machine](#state-machine)
- [Physics Equations](#physics-equations)
- [Configuration](#configuration)

---

## Overview

The Logistics system enables spacecraft (LogisticsUnits) to:

- Orbit celestial bodies
- Plan and execute transfers between bodies using Lambert solutions
- Manage cargo and fuel
- Utilize different engine types with performance modifiers
- Calculate delta-v budgets and fuel requirements

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Logistics System Architecture                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────┐    ┌──────────────────┐    ┌────────────────┐ │
│  │ LogisticsUnit │───▶│ TrajectoryPlanner │───▶│ LambertSolver  │ │
│  └──────────────┘    └──────────────────┘    └────────────────┘ │
│         │                     │                                       │
│         ▼                     ▼                                       │
│  ┌──────────────┐    ┌──────────────────┐                          │
│  │ EngineDef +  │    │TrajectorySolution│                          │
│  │ Modifiers    │    └──────────────────┘                          │
│  └──────────────┘                                                   │
│         │                                                            │
│         ▼                                                            │
│  ┌──────────────┐    ┌──────────────────┐                          │
│  │ CargoManifest│    │ Fuel Management │                          │
│  └──────────────┘    └──────────────────┘                          │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Core Components

### Directory Structure

```
Scripts/Structures/Logistics/
├── EngineDefinition.cs       # Engine performance with modifiers
├── EngineModifier.cs         # Additive/multiplicative modifiers
├── AppliedModifier.cs        # Modifier history tracking
├── TrajectorySolution.cs     # Lambert transfer solution
├── GravityAssistOpportunity.cs # Gravity assist opportunities
└── TransferType.cs           # Enum: Direct, MultiRev, GravityAssist

Scripts/Constructables/ArtificialSatellites/
├── LogisticsUnit.cs          # Main spacecraft entity
├── TrajectoryPlanner.cs      # Route generation and ranking
├── LogisticsMovementController.cs # Transfer execution
└── ThrustPerformanceCalculator.cs # Delta-v calculations

Scripts/UtilityLibrary/
├── LambertSolver.cs          # Orbital mechanics solver
├── OrbitalMath.cs            # Keplerian orbital calculations
└── LogisticsConfigLoader.cs  # YAML configuration loading
```

---

## Engine System

### EngineDefinition

The `EngineDefinition` class defines engine performance characteristics with support for modifiers. It maintains base values and calculates effective performance using a stacking formula.

#### Key Properties

| Property | Description |
|----------|-------------|
| `BaseSpecificImpulse` | Base Isp in seconds (without modifiers) |
| `BaseThrust` | Base thrust in Newtons |
| `EffectiveSpecificImpulse` | Current Isp after applying all modifiers |
| `EffectiveThrust` | Current thrust after applying all modifiers |
| `ExhaustVelocity` | Calculated exhaust velocity: Isp × g₀ |

#### Stacking Formula

The system uses the formula: **Final = (Base + ΣAdditive) × ΠMultiplicative**

```csharp
// Example: Base Isp 300s, +50s additive, ×1.1 multiplicative
// Effective = (300 + 50) × 1.1 = 385s
EffectiveSpecificImpulse = (BaseSpecificImpulse + additiveIsp) * multiplicativeIsp;
```

### EngineModifier

Modifiers can be applied to engines to represent upgrades, damage, wear, or other effects.

#### Modifier Types

| Type | Effect | Example |
|------|--------|---------|
| `Additive` | Flat value bonus | +50s Isp, +500N thrust |
| `Multiplicative` | Percentage multiplier | 1.1 = +10%, 0.9 = -10% |

#### Factory Methods

```csharp
// Additive modifiers - flat bonuses
EngineModifier.Additive("upgrade", ispBonus: 50f, thrustBonus: 500f);
EngineModifier.AdditiveIsp("nozzle_upgrade", 30f);
EngineModifier.AdditiveThrust("boost_pump", 200f);

// Multiplicative modifiers - percentage changes
EngineModifier.Multiplicative("efficiency", efficiencyPercent: 1.1f, thrustPercent: 1.05f);
EngineModifier.MultiplicativeIsp("tech_upgrade", 1.15f);
EngineModifier.MultiplicativeThrust("afterburner", 1.25f);

// Convenience methods
EngineModifier.Upgrade("mark_ii", 50f);           // +50s Isp additive
EngineModifier.Damage("collision", 0.8f);         // 80% effectiveness
EngineModifier.ThrustBoost("overdrive", 1.5f);    // +50% thrust
EngineModifier.Wear("age", 0.95f, 0.9f);          // 5% Isp, 10% thrust degradation
```

#### Applying Modifiers

```csharp
var engine = new EngineDefinition(baseIsp: 300f, baseThrust: 1000f);

// Apply an upgrade
engine.ApplyModifier(EngineModifier.Upgrade("mark_ii", 50f));

// Apply damage (e.g., from collision)
engine.ApplyModifier(EngineModifier.Damage("hull_dent", 0.9f));

// Check if modifier exists
if (engine.HasModifier("mark_ii"))
{
    // Get detailed breakdown
    var breakdown = engine.GetDetailedBreakdown();
    Console.WriteLine($"Effective Isp: {breakdown["EffectiveSpecificImpulse"]}");
}

// Remove modifier (e.g., after repairs)
engine.RemoveModifier("hull_dent");

// Clear all modifiers
engine.ClearAllModifiers();
```

---

## Trajectory Planning

### TrajectoryPlanner

The `TrajectoryPlanner` generates optimal transfer trajectories between celestial bodies using Lambert solver calculations.

#### Key Configuration

```csharp
TrajectoryPlanner.Instance.DefaultNumOptions = 5;      // Number of options to generate
TrajectoryPlanner.Instance.MinTOF = 100f;              // Minimum time of flight (seconds)
TrajectoryPlanner.Instance.MaxTOF = 86400f;            // Maximum time of flight (1 day)
TrajectoryPlanner.Instance.SafetyMargin = 0.9f;       // 90% of max usable delta-v
TrajectoryPlanner.Instance.IncludeRetrograde = false; // Include retrograde options
TrajectoryPlanner.Instance.MaxRevolutions = 0;        // Max orbit revolutions
```

#### Generating Trajectories

```csharp
// Get multiple trajectory options
var options = TrajectoryPlanner.Instance.GetOptions(
    unit: logisticsUnit,
    origin: earth,
    destination: mars,
    departureTime: 0f,              // Depart now
    numOptions: 5,
    rankingCriteria: TrajectorySolution.RankingCriteria.MostEfficient
);

// Quick single trajectory estimate
var quick = TrajectoryPlanner.Instance.GetQuickTrajectory(
    logisticsUnit, earth, mars
);
```

### TrajectorySolution

Represents a calculated orbital transfer with all necessary parameters.

#### Properties

| Property | Description |
|----------|-------------|
| `InitialVelocity` | Velocity required at departure (m/s) |
| `FinalVelocity` | Velocity at arrival (m/s) |
| `TimeOfFlight` | Transfer duration (seconds) |
| `DeltaVRequired` | Total delta-v needed (m/s) |
| `SemiMajorAxis` | Transfer orbit semi-major axis (m) |
| `Eccentricity` | Transfer orbit eccentricity (0-1) |
| `TransferType` | Direct, MultiRev, or GravityAssist |
| `FuelRequired` | Fuel needed for this trajectory (kg) |

#### Ranking and Filtering

```csharp
// Calculate normalized scores (0-1)
TrajectorySolution.CalculateScores(options);

// Rank by different criteria
var efficient = TrajectorySolution.RankBy(options, RankingCriteria.MostEfficient);
var balanced = TrajectorySolution.RankBy(options, RankingCriteria.Balanced);
var quickest = TrajectorySolution.RankBy(options, RankingCriteria.Quickest);

// Filter by delta-v budget
var withinBudget = TrajectorySolution.FilterByDeltaV(options, availableDeltaV);

// Get top N options
var top3 = TrajectorySolution.GetTopOptions(options, 3);
```

#### Delta-V Calculation

The system correctly accounts for orbital velocities:

```csharp
// Delta-V = |v_lambert_departure - v_orbital_origin| + |v_lambert_arrival - v_orbital_destination|
solution.OriginOrbitalVelocity = origin.Velocity;
solution.DestinationOrbitalVelocity = destination.Velocity;
solution.RecalculateDeltaV();
```

---

## Logistics Units

### LogisticsUnit

The main spacecraft entity that orbits bodies and executes transfers.

#### Initialization

```csharp
var ship = new LogisticsUnit();
ship.Initialize(parentBody: planetEarth, bandIndex: 2);
ship.SetDryMass(1000f);                    // Ship empty mass (kg)
ship.SetFuelCapacity(500f);                // Maximum fuel capacity
ship.Refuel(500f);                         // Fill tank
```

#### Engine Setup

```csharp
// Set engine
var engine = new EngineDefinition(baseIsp: 350f, baseThrust: 1500f);
ship.SetEngine(engine);

// Apply upgrades
ship.ApplyEngineModifier(EngineModifier.Upgrade("ion_thruster", 100f));
```

#### Cargo Management

```csharp
// Initialize cargo manifest
ship.InitializeCargo();

// Load cargo
ship.LoadCargo("Iron", 500f);
ship.LoadCargo("Copper", 250f);

// Check cargo mass
float cargoMass = ship.GetCargoMass();  // Total cargo in kg

// Unload cargo
ship.UnloadCargo("Iron", 200f);

// Clear all cargo
ship.ClearCargo();
```

#### Route Planning and Execution

```csharp
// Get available routes to destination
var routes = ship.GetRouteOptions(destination: mars, numOptions: 5);

// Plan the most efficient route
ship.PlanRoute(mars);

// Or use a specific trajectory option
ship.PlanRoute(selectedTrajectory);

// Execute the transfer
ship.ExecuteTrajectory();

// Or warp directly to destination (if transfer time allows)
ship.ExecuteWarp();
```

#### Orbital Movement

Ships automatically orbit their parent body when not in transit:

```csharp
// Ship orbits at its assigned band index
// Inner bands orbit faster than outer bands
ship.HandleOrbit(delta);
```

---

## Cargo Management

### CargoManifest

Tracks resources being transported with mass calculations.

```csharp
var manifest = new CargoManifest();

// Load resources
manifest.LoadResource("Iron", 500f);
manifest.LoadResource("Copper", 300f);

// Check quantities
float ironQty = manifest.GetResourceQuantity("Iron");
int resourceTypes = manifest.ResourceCount;

// Total mass calculation (uses ResourceDatabase for mass per unit)
float totalMass = manifest.TotalCargoMass;

// Unload resources
manifest.UnloadResource("Iron", 200f);

// Clear all
manifest.Clear();
```

---

## State Machine

Logistics units operate with a state machine:

```
        ┌─────────┐
        │  Idle   │
        └────┬────┘
             │ PlanRoute()
             ▼
        ┌─────────┐
        │Planning │ ◀──────────────┐
        └────┬────┘                │
             │ ExecuteTrajectory() │
             ▼                     │
        ┌───────────┐               │
        │ InTransit │               │
        └─────┬─────┘               │
               │ ApplyBurn()        │
               │ (reaches target)   │
               ▼                    │
        ┌───────────┐               │
        │ Arriving  │───────────────┘
        └─────┬─────┘
              │ HandleArrival()
              ▼
        ┌─────────┐
        │  Idle   │
        └─────────┘

Disabled: Out of fuel or failed state
```

#### State Transitions

```csharp
// Check if transition is valid
if (ship.CanTransitionTo(LogisticsUnitState.InTransit))
{
    ship.TransitionTo(LogisticsUnitState.InTransit);
}

// Check if ship can perform operations
if (ship.IsStateValidForOperation())
{
    // Can plan routes, load cargo, etc.
}
```

---

## Physics Equations

### Tsiolkovsky Rocket Equation

The fundamental equation for delta-v calculations:

$$\Delta v = v_e \cdot \ln\left(\frac{m_0}{m_1}\right)$$

Where:
- $\Delta v$ = Delta-v capability (m/s)
- $v_e$ = Exhaust velocity (m/s)
- $m_0$ = Initial mass (dry + fuel + cargo)
- $m_1$ = Final mass (dry + cargo)

```csharp
// Calculate remaining delta-v
float deltaV = exhaustVelocity * Mathf.Log(totalMass / dryMass);
```

### Fuel Required

Reverse Tsiolkovsky to find fuel needed for a given delta-v:

$$m_{fuel} = m_1 \cdot (e^{\Delta v / v_e} - 1)$$

```csharp
float massRatio = Mathf.Exp(deltaV / exhaustVelocity);
float initialMass = dryMass * massRatio;
float fuelRequired = initialMass - dryMass;
```

### Exhaust Velocity from Isp

$$v_e = I_{sp} \cdot g_0$$

```csharp
// g₀ = 9.81 m/s²
float exhaustVelocity = specificImpulse * 9.81f;
```

### Lambert's Problem

Solving for the initial velocity required to transfer between two positions in a given time:

```csharp
var solutions = OrbitalMath.SolveLambert(
    r1: originPosition,
    r2: destinationPosition,
    tof: timeOfFlight,
    mu: gravitationalParameter,  // μ = GM
    maxRevolutions: 0,
    includeRetrograde: false
);
```

---

## Configuration

### Engine Types (YAML)

Configuration files define engine characteristics:

```yaml
# Configuration/engines/Fusion.yaml
name: "Fusion Drive"
base_isp: 5000
base_thrust: 50000
fuel_type: "Hydrogen"
description: "High-efficiency fusion drive"
```

### Ship Templates (YAML)

```yaml
# Configuration/ships/Cargo_Freighter.yaml
name: "Cargo Freighter"
dry_mass: 5000
max_fuel: 2000
cargo_capacity: 50000
engine: "Fusion"
max_speed: 500
```

### Loading Configuration

```csharp
// Load engine definitions
var engines = LogisticsConfigLoader.LoadEngines();

// Load ship templates
var ships = LogisticsConfigLoader.LoadShips();

// Create ship from template
var ship = LogisticsUnit.CreateFromTemplate("Cargo_Freighter");
```

---

## Usage Example

Complete workflow for interplanetary transport:

```csharp
// 1. Initialize a logistics unit orbiting Earth
var transport = new LogisticsUnit();
transport.Initialize(parentBody: earth, bandIndex: 1);
transport.SetEngine(new EngineDefinition(isp: 400f, thrust: 2000f));
transport.Refuel(1000f);

// 2. Load cargo
transport.InitializeCargo();
transport.LoadCargo("Iron", 1000f);
transport.LoadCargo("Silicon", 500f);

// 3. Plan route to Mars
var mars = systemGenerator.FindBody("Mars");
var trajectoryOptions = transport.GetRouteOptions(mars, numOptions: 10);

// 4. Select most efficient option
var bestTrajectory = trajectoryOptions[0];

// 5. Check if we have enough fuel
float fuelNeeded = transport.CalculateFuelForDeltaV(bestTrajectory.DeltaVRequired);
if (fuelNeeded <= transport.Fuel)
{
    // 6. Plan and execute
    transport.PlanRoute(bestTrajectory);
    transport.ExecuteTrajectory();
}

// Ship will now transfer to Mars
// On arrival, it re-orbits the destination body
```

---

## Related Systems

- **CelestialBody**: Bodies that ships orbit and travel between
- **OrbitalMath**: Mathematical utilities for orbital mechanics
- **LambertSolver**: Problem solver for interplanetary transfers
- **ResourceDatabase**: Resource definitions and mass per unit values
- **SignalBus**: Events for ship arrival/departure notifications
