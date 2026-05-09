# Construction System

## Overview

The Construction System manages all player-driven and AI-driven construction in the game. This includes buildings on planetary surfaces, artificial satellites and stations in orbit, and ships fabricated at construction yards. The system handles construction queues, state tracking, placement validation, and signals for UI updates. Buildings are created from `BuildingDefinition` templates and run behavior scripts (extraction, manufacturing, power, storage, etc.) once completed.

## Key Classes

### ConstructionManager
- **Location**: `Scripts/Constructables/ConstructionManager.cs`
- **Purpose**: Central singleton that coordinates all construction requests. Routes UI requests to the appropriate subsystem and emits signals for progress updates and completion.
- **Key Responsibilities**:
  - Manages lists of stations, ships, and buildings under construction.
  - Provides signals: `StationConstructionInitialized`, `ShipConstructionCompleted`, `BuildingConstructionCancelled`, etc.
  - Parses GUI data and routes to `BuildingConstructionManager`, `ShipBuildQueue`, etc.

### BuildingConstructionManager
- **Location**: `Scripts/Constructables/Buildings/BuildingConstructionManager.cs`
- **Purpose**: Handles the construction lifecycle for surface buildings. Validates placement, deducts resources, and transitions buildings from "under construction" to "operational".

### Building
- **Location**: `Scripts/Constructables/Buildings/Building.cs`
- **Purpose**: Runtime instance of a constructed building. Holds state, links to its `BuildingDefinition`, and runs an `IBuildingBehavior`.
- **Key Responsibilities**:
  - Owns input/output storage slots.
  - Communicates with `ContinentEconomy` for power and resource accounting.

### BuildingNode
- **Location**: `Scripts/Constructables/Buildings/BuildingNode.cs`
- **Purpose**: Scene representation of a building on the planetary surface. Handles 3D placement and visual state.

### IBuildingBehavior / BehaviorFactory
- **Location**: `Scripts/Constructables/Buildings/IBuildingBehavior.cs`, `BehaviorFactory.cs`
- **Purpose**: Strategy pattern for building functionality. The factory creates the correct behavior based on the building type.

### Building Behaviors
- **Location**: `Scripts/Constructables/Buildings/Behaviors/`
- **Purpose**: Each behavior defines how a building operates:
  - `ExtractionBehavior`: Mines resources from the cell it occupies.
  - `ManufacturingBehavior`: Processes recipes into products.
  - `PowerProducerBehavior`: Generates power for the continent economy.
  - `StorageHubBehavior`: Provides centralized storage.
  - `TransportHubBehavior`: Facilitates resource transport between cells.
  - `HeadquartersBehavior`: Central command building.
  - `BatteryBehavior`: Stores excess power.

### IConstructable / ConstructionState
- **Location**: `Scripts/Constructables/IConstructable.cs`, `ConstructionState.cs`
- **Purpose**: Interface and state enum used by anything that can be constructed (buildings, ships, stations). Tracks construction progress and status.

### ShipBuildQueue
- **Location**: `Scripts/Constructables/Ships/ShipBuildQueue.cs`
- **Purpose**: Manages the queue of ships being built at a construction yard station. Handles multiple queued orders and progress tracking.

### StationSatellite / OrbitalArchitectStation / ConstructionYardStation
- **Location**: `Scripts/Constructables/ArtificialSatellites/`
- **Purpose**: Represent constructed stations in orbit. `ConstructionYardStation` can build ships. `OrbitalArchitectStation` specializes in station design and expansion.

### ShipSatellite / IArtificialSatellite
- **Location**: `Scripts/Constructables/ArtificialSatellites/ShipSatellite.cs`, `IArtificialSatellite.cs`
- **Purpose**: Base types for artificial satellites. `IArtificialSatellite` defines the interface for all player-constructed orbital objects.

### BodyTransferManager
- **Location**: `Scripts/Constructables/BodyTransferManager.cs`
- **Purpose**: Manages resource transfers between bodies (surface-to-orbit, orbit-to-orbit). Coordinates with `TransferSchedule` and logistics units.

## Related Documentation
- [Resource System](ResourceSystem.md)
- [Economy and Tick System](EconomyAndTickSystem.md)
- [Logistics and Movement](LogisticsAndMovement.md)
