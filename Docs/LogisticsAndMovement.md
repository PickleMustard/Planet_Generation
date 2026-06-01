# Logistics and Movement

## Overview

The Logistics and Movement system handles the movement of goods and ships between celestial bodies, stations, and surface locations. It includes trajectory planning using Lambert solvers and Keplerian mechanics, orbital schedule execution, logistics unit state machines, and transfer order management. Ships (`LogisticsUnit`) follow computed trajectories between legs, can perform gravity assists, and obey departure constraints and refuel policies.

## Key Classes

### LogisticsUnit
- **Location**: `Scripts/Constructables/Ships/LogisticsUnit.cs`
- **Purpose**: The primary spacecraft entity. Implements `IArtificialSatellite` and `IConstructable`. Can be built at stations, assigned cargo, and follow orbital trajectories.
- **Key Responsibilities**:
  - Holds `ShipDefinition` template and cargo manifest.
  - Follows `OrbitalTransferSchedule` for multi-leg journeys.
  - Transitions through `LogisticsUnitState` (Idle, Transit, Docking, etc.).
  - Integrates with `TrajectoryPlanner` for route computation.

### LogisticsMovementController
- **Location**: `Scripts/Constructables/Ships/LogisticsMovementController.cs`
- **Purpose**: Handles the physical movement and physics integration of a `LogisticsUnit` during flight.

### TrajectoryPlanner
- **Location**: `Scripts/Constructables/Ships/TrajectoryPlanner.cs`
- **Purpose**: Computes inter-body trajectories using patched conic approximation and Lambert's problem solver.
- **Key Responsibilities**:
  - Calculates delta-V requirements and travel times.
  - Evaluates launch windows and departure constraints.
  - Supports gravity assist opportunities.

### TrajectoryPreviewManager
- **Location**: `Scripts/Constructables/Ships/TrajectoryPreviewManager.cs`
- **Purpose**: Renders a visual preview of a planned trajectory in the game world for player review before confirmation.

### OrbitalScheduleExecutor
- **Location**: `Scripts/Constructables/Ships/OrbitalScheduleExecutor.cs`
- **Purpose**: Executes the steps of an `OrbitalTransferSchedule`, transitioning the logistics unit between legs and handling burns.

### ShipDatabase / StationDatabase
- **Location**: `Scripts/Logistics/Resources/ShipDatabase.cs`, `StationDatabase.cs`
- **Purpose**: Singleton databases loading ship and station definitions from YAML. Implement `ILoadableDatabase`.

### ShipDefinition / StationDefinition
- **Location**: `Scripts/Structures/Logistics/ShipDefinition.cs`, `StationDefinition.cs`
- **Purpose**: Data objects describing ship/station stats: mass, thrust, fuel capacity, storage, engine configurations.

### ClassicalOrbitalElements / OrbitalParameters / OrbitBand / OrbitConfiguration
- **Location**: `Scripts/Structures/Logistics/ClassicalOrbitalElements.cs`, `OrbitalParameters.cs`, `OrbitBand.cs`, `OrbitConfiguration.cs`
- **Purpose**: Mathematical structures representing orbits. `OrbitBand` defines allowed orbital zones around a body.

### TrajectorySolution / Leg / LegEndpoint
- **Location**: `Scripts/Structures/Logistics/TrajectorySolution.cs`, `Leg.cs`, `LegEndpoint.cs`
- **Purpose**: Represents a complete multi-leg journey. `Leg` is a single transfer segment; `LegEndpoint` is the departure or arrival point.

### BurnProfile / DepartureConstraints / RefuelInstructions
- **Location**: `Scripts/Structures/Logistics/BurnProfile.cs`, `DepartureConstraints.cs`, `RefuelInstructions.cs`
- **Purpose**: Detailed planning data for a single trajectory leg, including burn timing, allowed departure windows, and refueling directives.

### TransferSchedule / TransferOrder / TransferDestination
- **Location**: `Scripts/Structures/Transfers/TransferSchedule.cs`, `TransferOrder.cs`, `TransferDestination.cs`
- **Purpose**: High-level transfer management. `TransferOrder` is a player-initiated request to move cargo; `TransferSchedule` coordinates timing and routing.

### ResourceLink / ResourcePackage / LinkProfile / LinkProfileDatabase
- **Location**: `Scripts/Structures/Logistics/ResourceLink.cs`, `ResourcePackage.cs`, `LinkProfile.cs`, `LinkProfileDatabase.cs`
- **Purpose**: Represents physical or logical connections between resource endpoints. `LinkProfile` defines throughput and latency for a link type.

### Storage / StorageSlot
- **Location**: `Scripts/Structures/Logistics/Storage.cs`, `StorageSlot.cs`
- **Purpose**: Generic inventory system used by ships, stations, and buildings. `StorageSlot` tracks a single resource stack.

### EngineDefinition / EngineConfigDefinition / EngineModifier
- **Location**: `Scripts/Structures/Logistics/EngineDefinition.cs`, `EngineConfigDefinition.cs`, `EngineModifier.cs`
- **Purpose**: Describes ship engines and their performance characteristics.

### CargoManifest
- **Location**: `Scripts/Structures/Resources/CargoManifest.cs`
- **Purpose**: Describes what resources a logistics unit is carrying.

## Related Documentation
- [Construction System](ConstructionSystem.md)
- [Economy and Tick System](EconomyAndTickSystem.md)
