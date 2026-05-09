# Resource System

## Overview

The Resource System defines all in-game resources (ores, fuels, food, electronics, etc.) via YAML configuration files, loads them into a central `ResourceDatabase`, and procedurally places resource deposits on celestial bodies based on biome affinity, elevation ranges, and planetary type. It also manages building definitions, recipes, and the visual representation of resources on the planetary surface.

## Key Classes

### ResourceDatabase
- **Location**: `Scripts/Structures/Resources/ResourceDatabase.cs`
- **Purpose**: Singleton database that holds all `ResourceDefinition` instances loaded from YAML category files. Implements `ILoadableDatabase` for managed async loading.
- **Key Responsibilities**:
  - Loads resources from `Configuration/ResourceDefinition/categories/*.yaml`.
  - Infers `resource_type` from the filename (e.g., `ore.yaml` -> `"ore"`).
  - Indexes resources by `id_name` for fast lookup.
  - Identifies "generatable" resources (those with biome/color/elevation data).

### ResourceDefinition
- **Location**: `Scripts/Structures/Resources/ResourceDefinition.cs`
- **Purpose**: Data object representing a single resource type. Contains fields for tier, display color, biome affinity, elevation range, and generation flags.

### ResourceGenerationConfigDatabase / ResourceGenerationConfigDatabaseDebug
- **Location**: `Scripts/Structures/Resources/ResourceGenerationConfigDatabase.cs`
- **Purpose**: Stores the rules that govern how resources spawn on different body types and biomes.

### BuildingDatabase / BuildingDatabaseDebug
- **Location**: `Scripts/Structures/Resources/BuildingDatabase.cs`
- **Purpose**: Stores all building definitions loaded from YAML. Buildings define behavior, cost, power usage, and placement rules.

### BuildingDefinition
- **Location**: `Scripts/Structures/Resources/BuildingDefinition.cs`
- **Purpose**: Data object describing a constructable building. Links to behaviors, recipes, and visual definitions.

### RecipeDatabase / RecipeDatabaseDebug
- **Location**: `Scripts/Structures/Resources/RecipeDatabase.cs`
- **Purpose**: Stores crafting/manufacturing recipes that convert input resources into output resources.

### RecipeDefinition
- **Location**: `Scripts/Structures/Resources/RecipeDefinition.cs`
- **Purpose**: Data object describing a single recipe with inputs, outputs, processing time, and building requirements.

### CellResourceGenerator
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/ResourceGeneration/CellResourceGenerator.cs`
- **Purpose**: Assigns resources to individual Voronoi cells based on cell biome, elevation, and planetary type.

### ContinentResourceGenerator
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/ResourceGeneration/ContinentResourceGenerator.cs`
- **Purpose**: Generates resource deposits at the continent level, distributing major deposits across landmasses.

### SatelliteResourceGenerator
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/ResourceGeneration/SatelliteResourceGenerator.cs`
- **Purpose**: Handles resource placement on satellite bodies and belts.

### ResourceVisualizer
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/ResourceGeneration/ResourceVisualizer.cs`
- **Purpose**: Updates the celestial body mesh to visually indicate where resources are located (e.g., tinting cells or spawning decals).

### ResourceNode / ResourceDeposit
- **Location**: `Scripts/Structures/Resources/ResourceNode.cs`, `ResourceDeposit.cs`
- **Purpose**: Runtime representation of a resource deposit on a body. Tracks quantity, extraction rate, and depletion.

### PlanetaryTypeTagConfig / BiomeTagConfig / BiomeCategoryConfig
- **Location**: `Scripts/Structures/Resources/PlanetaryTypeTagConfig.cs`, `BiomeTagConfig.cs`, `BiomeCategoryConfig.cs`
- **Purpose**: Configuration data linking planetary types and biomes to allowable resources.

### IPlacementBehavior / DefaultPlacementBehavior / GeothermalVentPlacementBehavior
- **Location**: `Scripts/Structures/Resources/IPlacementBehavior.cs`, `DefaultPlacementBehavior.cs`, `GeothermalVentPlacementBehavior.cs`
- **Purpose**: Strategy objects that determine whether a building or resource can be placed at a specific cell.

### VisualDefinition / IconDefinition
- **Location**: `Scripts/Structures/Resources/VisualDefinition.cs`, `IconDefinition.cs`
- **Purpose**: Defines visual appearance and iconography for resources and buildings.

### DefaultModelRegistry
- **Location**: `Scripts/Structures/Resources/DefaultModelRegistry.cs`
- **Purpose**: Provides fallback 3D models when a specific building or resource does not define its own.

## Related Documentation
- [Data Loading](DataLoading.md)
- [Mesh Generation](MeshGeneration.md)
- [Construction System](ConstructionSystem.md)
