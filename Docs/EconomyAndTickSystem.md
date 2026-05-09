# Economy and Tick System

## Overview

The Economy and Tick System drives all simulation logic for production, consumption, and power management across planetary surfaces and orbital stations. It operates on a dedicated 60Hz background thread via the `ManufactureTickEngine`, ensuring that large factories cannot starve the main rendering or physics threads. Each body maintains a `BodyEconomyManager` that aggregates continent and station economies, while individual buildings implement `IManufactureTickable` to receive regular tick callbacks.

## Key Classes

### ManufactureTickEngine
- **Location**: `Scripts/Constructables/Tick/ManufactureTickEngine.cs`
- **Purpose**: A dedicated-thread, fixed-rate (60Hz) tick driver for all manufacturing simulation. Created at game start and torn down at game end.
- **Key Responsibilities**:
  - Runs a single worker thread that ticks all registered `IManufactureTickable` instances sequentially.
  - Provides thread-safe registration and unregistration via a concurrent queue.
  - Collects telemetry: tick duration, exception counts, and lag metrics.
  - Supports pause/resume via a `ManualResetEventSlim`.

### IManufactureTickable
- **Location**: `Scripts/Constructables/Tick/IManufactureTickable.cs`
- **Purpose**: Interface for any object that needs to run simulation logic every tick. Implemented by economies, buildings, and other producers/consumers.
- **Key Method**: `void ManufactureTick(float delta);`

### BodyEconomyManager
- **Location**: `Scripts/Constructables/BodyEconomyManager.cs`
- **Purpose**: Per-body registry of all active `ContinentEconomy` and `StationEconomy` instances. Added as a child of `CelestialBody` or `SatelliteBody`.
- **Key Responsibilities**:
  - Aggregates total power generation and consumption.
  - Provides read-only lists of economies for UI display.
  - Does NOT drive ticks itself; ticking is handled by `ManufactureTickEngine`.

### ContinentEconomy
- **Location**: `Scripts/Structures/GameState/ContinentEconomy.cs`
- **Purpose**: Manages the economy for a single continent on a body. Tracks power balance, building lists, and resource flows.
- **Key Responsibilities**:
  - Receives tick calls from `ManufactureTickEngine`.
  - Distributes power to buildings.
  - Reports production/consumption statistics.

### StationEconomy
- **Location**: `Scripts/Structures/GameState/StationEconomy.cs`
- **Purpose**: Economy manager for orbital stations. Similar to `ContinentEconomy` but for zero-gravity constructs.

### Continent
- **Location**: `Scripts/Structures/GameState/Continent.cs`
- **Purpose**: Data object representing a landmass on a celestial body. Holds its associated `ContinentEconomy`, cell indices, and boundary data.

### IResourceEndpoint
- **Location**: `Scripts/Structures/GameState/IResourceEndpoint.cs`
- **Purpose**: Interface for any entity that can send or receive resources (buildings, stations, logistics units).

### ResourceRequest
- **Location**: `Scripts/Structures/GameState/ResourceRequest.cs`
- **Purpose**: Represents a request for resources from one endpoint to another. Used by economies to request inputs or push outputs.

### SystemData
- **Location**: `Scripts/Structures/GameState/SystemData.cs`
- **Purpose**: Holds global game-state for the current system. Owns the `ManufactureTickEngine` lifecycle (`StartGame` / `EndGame`).

## Related Documentation
- [Construction System](ConstructionSystem.md)
- [Logistics and Movement](LogisticsAndMovement.md)
