# System Template Documentation

This document describes the TOML configuration files used for defining celestial system templates in the Planet Generation project. These templates specify the composition and properties of star systems, including stars, planets, moons, and other celestial bodies.

## Overview

The system templates are located in `Configuration/SystemTemplate/` and include:
- `Binary Star System.toml`: Defines a system with two stars and a rocky planet.
- `Solar System.toml`: Defines a more complex system resembling our solar system, with a central star, an asteroid belt, rocky planets, and a gas giant.

Each template is structured as an array of `bodies`, where each body represents a celestial object with its properties and optional satellites.

## Body Structure

Each body in the `[[bodies]]` array has the following top-level properties:

- **type** (string): The type of celestial body. Possible values pulled from enums under CelestialBody.

- **position** (array of 3 floats): The 3D position of the body in space, e.g., `[x, y, z]`.

- **velocity** (array of 3 floats): The initial velocity vector of the body, e.g., `[vx, vy, vz]`.

- **mass** (float): The mass of the body, influencing gravitational interactions.

- **size** (float): The radius or scale of the body.

### Mesh Configuration

Each body includes a `mesh` section with sub-sections for base mesh and tectonic generation.

#### Base Mesh (`[[bodies.mesh.base_mesh]]`)

- **subdivisions** (integer): Number of subdivision levels for the mesh.
- **vertices_per_edge** (array of 2 integers): Range for the number of vertices per edge, e.g., `[min, max]`.
- **num_abberations** (integer): Number of aberrations applied to the mesh.
- **num_deformation_cycles** (integer): Number of deformation cycles for mesh shaping.

#### Tectonic (`[[bodies.mesh.tectonic]]`)

Parameters for simulating tectonic activity and surface features. All arrays defined here refer to ranges for generation:

- **num_continents** (array of 2 integers): Range for the number of continents, e.g., `[min, max]`.
- **stress_scale** (array of 2 floats): Scaling factor for stress calculations.
- **shear_scale** (array of 2 floats): Scaling factor for shear forces.
- **max_propagation_distance** (array of 2 floats): Maximum distance for stress propagation.
- **propagation_falloff** (array of 2 floats): Rate at which propagation decreases with distance.
- **inactive_stress_threshold** (array of 2 floats): Threshold below which stress is considered inactive.
- **general_height_scale** (array of 2 floats): Overall scaling for height variations.
- **general_shear_scale** (array of 2 floats): General scaling for shear effects.
- **general_compression_scale** (array of 2 floats): Scaling for compression forces.
- **general_transform_scale** (array of 2 floats): Overall transformation scaling.

### Satellites

Some bodies can have satellites (e.g., moons or asteroid belts). Defined under `[[bodies.satellites]]`.

- **type** (string): Type of satellite, e.g., `"Moon"` or `"Asteroid Belt"`. Pulled from enums under SatelliteBody
- **position** (array of 3 floats): Position relative to the parent body.
- **velocity** (array of 3 floats): Velocity relative to the parent body.
- **size** (float): Size of the satellite.

Additional sections:

- **base_mesh**: Similar to the main body's base mesh.
 - Defined under `[[bodies.satellites.base_mesh]]`
- **scaling**: Scaling factors for the moon's dimensions.
  - Defined under `[[bodies.satellites.scaling]]`
  - **scaling_range_x**, **scaling_range_y**, **scaling_range_z** (arrays of 2 floats): Scaling ranges for each axis.
- **noise_settings**: Noise parameters for surface generation.
  - Defined under `[[bodies.satellites.noise_settings]]`
  - **amplitude_range** (array of 2 floats): Range for noise amplitude.
  - **scaling_range** (array of 2 floats): Range for noise scaling.
  - **octave_range** (array of 2 integers): Range for noise octaves.

#### For Satellite Groups

Central Celestial Bodies (Suns, Blackholes) will have Satellite groups instead of individual satellites, these are defined as the following:

- **template**: Configuration for the asteroid belt.
  - **number_asteroids** (array of 2 integers): Range for the number of asteroids.
  - **grouping** (array of strings): Possible grouping patterns, e.g., `["balanced", "clustered", "dual grouping"]`.
  - **ring_velocity** (array of 3 floats): Velocity of the ring.
  - **size_range** (array of 2 floats): Size range for individual asteroids.
  - **possible_subtypes** (array of strings): Possible subtypes for asteroids.

## Example Usage

These templates are used by the `SystemGenerator` to instantiate celestial systems in the game or simulation. Parameters with ranges (e.g., arrays of two values) allow for procedural variation, ensuring each generated system is unique.

For modifications, adjust the values in the TOML files to influence system composition, body properties, and visual generation.

## Usage in Scripts

The TOML templates are utilized in the Scripts directory to instantiate and manage celestial bodies and their satellites in the game engine (Godot).

### CelestialBody.cs

- **Purpose**: Defines the `CelestialBody` class, which represents individual celestial objects (e.g., stars, planets) with physical properties and behaviors.
- **Key Features**:
  - Handles physics simulation, including gravitational forces and orbital mechanics via `_PhysicsProcess`.
  - Supports mesh generation using `CelestialBodyMesh` for visual representation.
  - Body types are defined by the `CelestialBodyType` enum: `BlackHole`, `Star`, `RockyPlanet`, `GasGiant`, `IceGiant`, `DwarfPlanet`.
  - Constructor takes type, mass, velocity, size, and mesh, and adds special features like light sources for stars.
  - Integrates with `SystemGenTemplates` to configure meshes based on type and seed.
- **Relation to TOML**: The `type`, `mass`, `velocity`, `size`, and mesh parameters from the TOML are directly used to initialize `CelestialBody` instances.

### SatelliteBody.cs

- **Purpose**: Defines the `SatelliteBody` class for satellites (e.g., moons, asteroids) orbiting parent celestial bodies.
- **Key Features**:
  - Simulates orbital physics around a parent body, calculating gravitational forces in `_PhysicsProcess`.
  - Differentiates between satellite groups (e.g., `AsteroidBelt`) for stars/black holes and individual satellites (e.g., `Moon`) for planets.
  - Enums: `SatelliteGroupTypes` (AsteroidBelt, IceBelt, Comet) and `SatelliteBodyType` (Asteroid, Moon, Planet, Satellite, Rings).
  - Does not affect other bodies' gravity, focusing on parent-child orbital dynamics.
- **Relation to TOML**: Satellite properties like `type`, `position`, `velocity`, `mass`, `size`, and mesh configurations are parsed from the TOML's `satellites` sections to create `SatelliteBody` instances.

### SystemGenerator.cs

- **Purpose**: Likely orchestrates the creation of entire systems from TOML templates.
- **Usage**: Reads TOML files to instantiate `CelestialBody` and `SatelliteBody` objects, applying procedural variations within specified ranges.

## Usage in UI

The UI components in the `UI/` directory allow users to interactively edit and configure celestial bodies and satellites, reflecting changes back to the TOML-like data structures.

### BodyItem.cs

- **Purpose**: A UI control (`VBoxContainer`) for editing a single celestial body's properties.
- **Key Features**:
  - Provides input fields for `position` (X, Y, Z), `velocity` (velX, velY, velZ), `mass`, and `size`.
  - Dropdown (`OptionButton`) for selecting body `type` from `CelestialBodyType`.
  - Manages a list of satellites via `SatellitesList`, with buttons to add/remove satellites.
  - Applies constraints (e.g., position/velocity clamped to ±10,000) and templates from `SystemGenTemplates`.
  - Emits signals for updates, propagating changes to satellites.
  - Converts UI values to a dictionary (`ToParams()`) mirroring TOML structure for export or application.
- **Relation to TOML**: UI fields directly map to TOML parameters, allowing real-time editing of templates.

### SatelliteItem.cs

- **Purpose**: A UI control for editing individual satellite properties within a `BodyItem`.
- **Key Features**:
  - Input fields for `position`, `velocity`, `mass`, `size`.
  - Dropdown for satellite `type`, switching between `SatelliteGroupTypes` (for stars) and `SatelliteBodyType` (for planets).
  - Special handling for asteroid belts with `numInBeltLower` and `numInBeltUpper` for range-based generation.
  - Visibility of belt-specific UI toggles based on parent body type.
  - Applies constraints and templates, emitting updates.
  - Outputs parameters as a dictionary for integration with parent body.
- **Relation to TOML**: Mirrors satellite sections in TOML, enabling configuration of moons, belts, etc.

### PlanetSystemGenerator.cs

- **Purpose**: Main UI scene for generating and managing planetary systems.
- **Usage**: Integrates `BodyItem` instances, allowing users to build systems interactively, which can then be saved or used to generate scenes.

## Notes

- All parameters support randomization within specified ranges where applicable.
- Ensure consistency in units (e.g., mass, size) across related bodies for realistic simulations.
- Refer to related documentation in `Docs/` for mesh generation and tectonic simulation details.
- UI components enforce constraints to prevent invalid values, aligning with TOML range specifications.
