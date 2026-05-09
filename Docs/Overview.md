# Game Systems Overview

This document provides a high-level summary of all major systems implemented in the project. Each system has a dedicated documentation file with detailed breakdowns of the classes and their responsibilities.

---

## Systems

### [Procedural Generation](ProceduralGeneration.md)
Creates entire planetary systems from configuration templates. Generates celestial bodies (stars, planets, gas giants, black holes, etc.), positions them in realistic orbits, and assigns physical properties. Uses a two-pass pipeline (base mesh, then overlays) with optional background threading.

**Key classes**: `SystemGenerator`, `CelestialBody`, `SatelliteBody`, `SatelliteBeltBody`, `NBodyCoordinator`, `OrbitalDistanceCalculator`, `AUProbabilityManager`

---

### [Mesh Generation](MeshGeneration.md)
Builds spherical geometry for celestial bodies using spherical Delaunay triangulation, constrained triangulation, and Voronoi cell partitioning. Supports vertex distribution strategies, configurable subdivision, tectonic plate simulation, spherical harmonics deformation, and biome/color assignment.

**Key classes**: `UnifiedCelestialMesh`, `BaseMeshGeneration`, `SphericalDelaunayTriangulation`, `ConstrainedDelauneyTriangulation`, `VoronoiCellGeneration`, `TectonicGeneration`, `EdgeStressCalculator`, `BiomeAssigner`, `ColorMapperFactory`

---

### [Resource System](ResourceSystem.md)
Defines all in-game resources via YAML configuration, loads them into a central database, and procedurally places deposits on celestial bodies based on biome affinity and elevation. Also manages building definitions, manufacturing recipes, and surface visualizers.

**Key classes**: `ResourceDatabase`, `ResourceDefinition`, `BuildingDatabase`, `RecipeDatabase`, `CellResourceGenerator`, `ContinentResourceGenerator`, `ResourceVisualizer`, `ResourceNode`

---

### [Construction System](ConstructionSystem.md)
Manages player and AI construction of surface buildings, orbital stations, and ships. Handles construction queues, state tracking, placement validation, and UI signals. Buildings run behavior scripts (extraction, manufacturing, power, etc.) once operational.

**Key classes**: `ConstructionManager`, `BuildingConstructionManager`, `Building`, `IBuildingBehavior`, `BehaviorFactory`, `ShipBuildQueue`, `StationSatellite`, `IArtificialSatellite`

---

### [Economy and Tick System](EconomyAndTickSystem.md)
Drives production, consumption, and power management on a dedicated 60Hz background thread. Aggregates continent and station economies per body. Individual buildings implement `IManufactureTickable` to receive regular simulation ticks without impacting rendering.

**Key classes**: `ManufactureTickEngine`, `IManufactureTickable`, `BodyEconomyManager`, `ContinentEconomy`, `StationEconomy`, `SystemData`

---

### [Logistics and Movement](LogisticsAndMovement.md)
Handles ship movement and cargo transfer between bodies and stations. Includes trajectory planning using Lambert solvers, orbital schedule execution, logistics unit state machines, and transfer order management.

**Key classes**: `LogisticsUnit`, `TrajectoryPlanner`, `TrajectoryPreviewManager`, `OrbitalScheduleExecutor`, `ShipDatabase`, `TrajectorySolution`, `TransferSchedule`, `ResourceLink`, `Storage`

---

### [UI and State Machine](UIAndStateMachine.md)
Manages all in-game UI through a Hierarchical State Machine (HSM) using LimboHSM. Top-level states include HUD, planet selection, cell inspection, continent view, orbital inspection, construction, station management, and transfer planning.

**Key classes**: `GUIControllerHSM`, `GUIManager`, `MainGameUI`, `HUD`, `ToastSystem`, `VoronoiCellInfoWindow`, `ContinentInfoWindow`, `OrbitalBodyWindow`, `TransferPlanningWindow`

---

### [Debug System](DebugSystem.md)
In-game diagnostic tools available in DEBUG builds. Includes a tabbed debug menu, command console with autocomplete, database viewer, and auto-registration of annotated objects.

**Key classes**: `DebugMenu`, `DebugConsole`, `CommandRegistry`, `AutocompleteEngine`, `DatabaseViewer`, `EconomyDebugModule`, `ManufactureTickModule`, `AutoRegistrationManager`

---

### [Data Loading](DataLoading.md)
Parses YAML configuration files and loads them into singleton databases. Supports concurrent loading, validation, and template-based configuration. Each database implements `ILoadableDatabase` and reports progress for loading screens.

**Key classes**: `DatabaseLoadManager`, `ILoadableDatabase`, `TemplateLoader`, `BaseConfigLoader`, `ResourceConfigLoader`, `BuildingConfigLoader`, `RecipeConfigLoader`, `YamlValidator`

---

### [Player Interaction](PlayerInteraction.md)
Handles direct player input: camera control, Voronoi cell selection with shader highlighting, building placement, ship movement commands, and body selection.

**Key classes**: `CellSelectionManager`, `InputHandler`, `PlayerController`, `ShipMovement`

---

### [Utility Systems](UtilitySystems.md)
Cross-cutting infrastructure including deterministic random generation, thread pooling, runtime settings, global signal bus, structured logging, name generation, orbital math, and scene navigation.

**Key classes**: `ThreadPooler`, `WorkPackageBuilder`, `RuntimeSettings`, `SignalBus`, `GameLogger`, `Randomizer`, `NameGenerator`, `OrbitalMath`, `LambertSolver`

---

### [Rendering](Rendering.md)
Visual effects separate from core mesh generation, including GPU-based orbital body indicators and procedural texture generation for celestial surfaces.

**Key classes**: `OrbitalIndicatorCoordinator`, `OrbitalIndicatorEffect`, `TextureGeneratorFactory`, `RockyPlanetTextureGenerator`, `GasGiantTextureGenerator`, `MeshRasterizer`

---

## File Structure

All system documentation lives under the `Docs/` directory:

```
Docs/
├── Overview.md
├── ProceduralGeneration.md
├── MeshGeneration.md
├── ResourceSystem.md
├── ConstructionSystem.md
├── EconomyAndTickSystem.md
├── LogisticsAndMovement.md
├── UIAndStateMachine.md
├── DebugSystem.md
├── DataLoading.md
├── PlayerInteraction.md
├── UtilitySystems.md
└── Rendering.md
```
