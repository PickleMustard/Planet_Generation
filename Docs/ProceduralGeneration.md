# Procedural Generation System

## Overview

The Procedural Generation system is responsible for creating entire planetary systems from configuration templates. It generates celestial bodies of various types (stars, rocky planets, gas giants, black holes, etc.), positions them in realistic orbits, and assigns physical properties such as mass, radius, and velocity. The system uses a two-pass generation pipeline: first creating base meshes and then applying biome, tectonic, and Voronoi overlays. Generation can be performed synchronously or dispatched to a background thread pool to keep the UI responsive.

## Key Classes

### SystemGenerator
- **Location**: `Scripts/ProceduralGeneration/SystemGenerator.cs`
- **Purpose**: Entry point for generating entire star systems. Receives template data from the UI, coordinates the creation of dominant bodies, satellite belts, and planetary bodies. Tracks generation progress and can dispatch work to the `ThreadPooler` for asynchronous processing.
- **Key Responsibilities**:
  - Listens for `GenerateSystemRequested` signals from the `SignalBus`.
  - Manages `NBodyCoordinator` for physics integration when multiple bodies exist.
  - Calculates orbital parameters using `OrbitalMath` and `OrbitalDistanceCalculator`.
  - Supports both threaded and non-threaded generation paths.

### CelestialBody
- **Location**: `Scripts/ProceduralGeneration/CelestialBody.cs`
- **Purpose**: The core representation of any major celestial object (star, planet, etc.). Implements `IOrbitalBody` and `ISelectableBody`. Holds physical properties (mass, radius, velocity, position) and hosts mesh generation components.
- **Key Responsibilities**:
  - Integrates physics via `_PhysicsProcess` or delegates to `NBodyCoordinator`.
  - Manages its own `UnifiedCelestialMesh` for surface geometry.
  - Stores continent and Voronoi cell data for surface interaction.
  - Uses a Builder pattern (`CelestialBody.Builder`) for complex construction from dictionaries/templates.

### SatelliteBody
- **Location**: `Scripts/ProceduralGeneration/SatelliteBody.cs`
- **Purpose**: Represents natural moons or minor bodies orbiting a parent `CelestialBody`. Shares many traits with `CelestialBody` but is scaled for satellite roles.
- **Key Responsibilities**:
  - Inherits orbital parameters from its parent.
  - Can have its own simplified mesh and resource data.

### SatelliteBeltBody
- **Location**: `Scripts/ProceduralGeneration/SatelliteBeltBody.cs`
- **Purpose**: Represents a belt of minor bodies (like an asteroid belt) orbiting a dominant body.
- **Key Responsibilities**:
  - Renders as a distributed collection rather than a single mesh.
  - Supports resource generation across the belt.

### Barycenter
- **Location**: `Scripts/ProceduralGeneration/Barycenter.cs`
- **Purpose**: Represents the center of mass for a multi-body system. Used by `NBodyCoordinator` when `CoordinatorActive` is true.
- **Key Responsibilities**:
  - Provides a shared gravitational reference point.

### NBodyCoordinator
- **Location**: `Scripts/ProceduralGeneration/NBodyCoordinator.cs`
- **Purpose**: When active, takes over physics integration for all celestial bodies to ensure stable n-body simulation. Disables per-body `_PhysicsProcess` to avoid duplicate work.
- **Key Responsibilities**:
  - Centralized gravitational force calculations.
  - Stable integration for multi-body systems.

### OrbitalDistanceCalculator
- **Location**: `Scripts/ProceduralGeneration/OrbitalDistanceCalculator.cs`
- **Purpose**: Computes semi-major axes and orbital distances based on probability distributions and body classification.
- **Key Responsibilities**:
  - Uses `AUProbabilityManager` to determine realistic orbital spacing.

### AUProbabilityManager
- **Location**: `Scripts/ProceduralGeneration/AUProbabilityManager.cs`
- **Purpose**: Loads and queries AU (astronomical unit) probability data to guide where planets form in a system.
- **Key Responsibilities**:
  - Reads `AUProbabilityData` from configuration.

### BodyClassification
- **Location**: `Scripts/ProceduralGeneration/BodyClassification.cs`
- **Purpose**: Determines the type and subtype of a body (e.g., RockyPlanet, GasGiant, IceGiant) based on template data and orbital position.
- **Key Responsibilities**:
  - Maps generation parameters to `CelestialBodyType` and `CelestialSubtype`.

### SubtypeParser
- **Location**: `Scripts/ProceduralGeneration/SubtypeParser.cs`
- **Purpose**: Parses string-based subtype identifiers from templates into strongly typed enums.

## Related Documentation
- [Mesh Generation](MeshGeneration.md)
- [Resource System](ResourceSystem.md)
- [Data Loading](DataLoading.md)
