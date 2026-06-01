# Data Loading

## Overview

The Data Loading system is responsible for parsing YAML configuration files and loading them into singleton databases at runtime. It supports concurrent loading via the `DatabaseLoadManager`, validation through `YamlValidator`, and template-based configuration for celestial bodies. Each database implements `ILoadableDatabase` and emits progress events for loading screens.

## Key Classes

### DatabaseLoadManager
- **Location**: `Scripts/UtilityLibrary/DataLoading/DatabaseLoadManager.cs`
- **Purpose**: Central coordinator for loading all game databases. Implements `IConfigurable`.
- **Key Responsibilities**:
  - Registers `ILoadableDatabase` instances.
  - Loads databases concurrently up to a configurable limit (`max_concurrent_loads`).
  - Tracks per-database and overall load progress.
  - Integrates with `ThreadPooler` for background loading.

### ILoadableDatabase
- **Location**: `Scripts/UtilityLibrary/DataLoading/ILoadableDatabase.cs`
- **Purpose**: Interface for all loadable databases. Defines `LoadData()`, progress events, and load state.

### TemplateLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/TemplateLoader.cs`
- **Purpose**: Loads YAML templates for celestial body generation and validates them.
- **Key Responsibilities**:
  - Reads from `Configuration/SystemTemplate/`.
  - Validates templates against expected schemas.

### TemplateHelpers
- **Location**: `Scripts/UtilityLibrary/DataLoading/TemplateHelpers.cs`
- **Purpose**: Provides default values for celestial body properties based on `CelestialBodyType`.

### BaseConfigLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/BaseConfigLoader.cs`
- **Purpose**: Base class for YAML config loaders. Handles file I/O, parsing, and error reporting.

### ResourceConfigLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/ResourceConfigLoader.cs`
- **Purpose**: Loads resource definitions from `Configuration/ResourceDefinition/categories/*.yaml`. Infers category from filename.

### BuildingConfigLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/BuildingConfigLoader.cs`
- **Purpose**: Loads building definitions from YAML.

### RecipeConfigLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/RecipeConfigLoader.cs`
- **Purpose**: Loads manufacturing recipes from YAML.

### ShipConfigLoader / StationConfigLoader / EngineConfigLoader / LinkConfigLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/`
- **Purpose**: Load ship, station, engine, and link profile definitions from YAML.

### PlanetaryTypeLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/PlanetaryTypeLoader.cs`
- **Purpose**: Loads planetary type and biome tag configurations.

### IconDataLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/IconDataLoader.cs`
- **Purpose**: Loads icon definitions and sprite data.

### AUProbabilityLoader
- **Location**: `Scripts/UtilityLibrary/DataLoading/AUProbabilityLoader.cs`
- **Purpose**: Loads AU probability tables used by `AUProbabilityManager`.

### DatabaseAccess
- **Location**: `Scripts/UtilityLibrary/DataLoading/DatabaseAccess.cs`
- **Purpose**: Provides uniform access patterns for querying loaded databases.

### YamlValidator
- **Location**: `Scripts/UtilityLibrary/DataLoading/YamlValidator.cs`
- **Purpose**: Validates YAML structure and required fields before parsing into objects.

### TestDatabase
- **Location**: `Scripts/UtilityLibrary/DataLoading/TestDatabase.cs`
- **Purpose**: In-memory database used for unit tests.

### DatabaseNotLoadedException
- **Location**: `Scripts/UtilityLibrary/DataLoading/DatabaseNotLoadedException.cs`
- **Purpose**: Exception thrown when code attempts to access a database before it has finished loading.

### DataLoadingScene
- **Location**: `Scripts/UtilityLibrary/DataLoading/DataLoadingScene.cs`
- **Purpose**: Dedicated scene for the initial loading screen that waits for all databases to load before transitioning to the main menu or game.

## Related Documentation
- [Resource System](ResourceSystem.md)
- [Procedural Generation](ProceduralGeneration.md)
- [Utility Systems](UtilitySystems.md)
