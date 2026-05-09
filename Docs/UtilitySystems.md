# Utility Systems

## Overview

The Utility Systems provide cross-cutting infrastructure used by nearly all other game systems. This includes deterministic random number generation, thread pooling for background work, runtime settings with persistence, a global signal bus, structured logging, name generation, orbital math helpers, and scene navigation.

## Key Classes

### ThreadPooler
- **Location**: `Scripts/UtilityLibrary/TaskSystem/ThreadPooler.cs`
- **Purpose**: Autoload singleton that manages a pool of worker threads for background task execution. Implements `IConfigurable`.
- **Key Responsibilities**:
  - Maintains priority-based work queues (`WorkPackage`).
  - Configurable CPU allocation percentage and manual thread count.
  - Tracks active and pending package counts.
  - Provides batch statistics for monitoring.

### WorkPackageBuilder / WorkPackage / WorkStep
- **Location**: `Scripts/UtilityLibrary/TaskSystem/WorkPackageBuilder.cs`, `WorkPackage.cs`, `WorkStep.cs`
- **Purpose**: Fluent API for building multi-step background jobs. Each `WorkStep` is a discrete unit of work with a name and delegate.

### ThreadAllocator
- **Location**: `Scripts/UtilityLibrary/TaskSystem/ThreadAllocator.cs`
- **Purpose**: Decides how many threads to allocate based on system CPU count and user settings.

### RuntimeSettings
- **Location**: `Scripts/UtilityLibrary/Settings/RuntimeSettings.cs`
- **Purpose**: Autoload singleton for persistent game settings. Saves to `res://settings.cfg`.
- **Key Responsibilities**:
  - Registers `IConfigurable` objects.
  - Validates setting ranges.
  - Emits `SettingChanged` and `SettingsLoaded` signals.

### IConfigurable / ConfigEntry
- **Location**: `Scripts/UtilityLibrary/Settings/IConfigurable.cs`, `ConfigEntry.cs`
- **Purpose**: Interface for any system that exposes runtime-tunable settings. `ConfigEntry` describes a single setting's type, default, min, max, and restart requirement.

### SignalBus
- **Location**: `Scripts/UtilityLibrary/SignalBus.cs`
- **Purpose**: Global autoload singleton for decoupled event communication. Other systems connect to its signals rather than directly to each other.
- **Key Signals**:
  - `GenerateSystemRequested`
  - `SelectedTemplate`
  - `StartTimer`, `StopTimer`

### GameLogger
- **Location**: `Scripts/UtilityLibrary/GameLogger.cs`
- **Purpose**: Structured logging utility replacing raw `GD.Print()`. Supports levels: Debug, Info, Warning, Error, Critical.
- **Key Methods**:
  - `EnterFunction`, `ExitFunction` for tracing.
  - Initialized early by `RuntimeSettings`.

### Randomizer
- **Location**: `Scripts/UtilityLibrary/Randomizer.cs`
- **Purpose**: Wrapper around `RandomNumberGenerator` that supports deterministic seeding for reproducible procedural generation.

### NameGenerator / NameFormat
- **Location**: `Scripts/UtilityLibrary/NameGeneration/NameGenerator.cs`, `NameFormat.cs`
- **Purpose**: Procedurally generates names for celestial bodies, systems, and ships based on configurable formats and syllable lists.

### OrbitalMath / KeplerianMechanics / LambertSolver
- **Location**: `Scripts/UtilityLibrary/GameMath/Orbital/OrbitalMath.cs`, `KeplerianMechanics.cs`, `LambertSolver.cs`
- **Purpose**: Mathematical utilities for orbital mechanics. `LambertSolver` solves the boundary value problem for interplanetary trajectories.

### OrbitalBodyConverter
- **Location**: `Scripts/UtilityLibrary/OrbitalBodyConverter.cs`
- **Purpose**: Converts between different orbital body representations (e.g., Godot nodes and data structures).

### NodeUtils
- **Location**: `Scripts/UtilityLibrary/NodeUtils.cs`
- **Purpose**: Helper methods for common Godot `Node` operations (safe reparenting, recursive searching, etc.).

### PlaceholderIconGenerator
- **Location**: `Scripts/UtilityLibrary/PlaceholderIconGenerator.cs`
- **Purpose**: Generates fallback icons when an asset is missing.

### PolygonRendererSDL
- **Location**: `Scripts/UtilityLibrary/PolygonRendererSDL.cs`
- **Purpose**: Software polygon rendering utility, used for texture generation and debug visualization.

### SceneNavigator
- **Location**: `Scripts/Scenes/SceneNavigator.cs`
- **Purpose**: Handles scene transitions and maintains a scene stack for modal flows.

### TaskTimer / TimerInfo
- **Location**: `Scripts/UtilityLibrary/TaskTimer.cs`, `TimerInfo.cs`
- **Purpose**: Utility for tracking elapsed time on background tasks and reporting progress.

### SignalHandlerAttribute
- **Location**: `Scripts/UtilityLibrary/SignalHandlerAttribute.cs`
- **Purpose**: Attribute used to mark methods that should automatically connect to `SignalBus` signals via reflection.

## Related Documentation
- [Debug System](DebugSystem.md)
- [Data Loading](DataLoading.md)
