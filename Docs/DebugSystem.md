# Debug System

## Overview

The Debug System provides in-game diagnostic tools available only in DEBUG builds. It includes a tabbed debug menu (`DebugMenu`) with modules for inspecting the economy, manufacturing ticks, cell info, and a full database viewer. It also features a command console with autocomplete, an instance registry for querying live objects, and auto-registration of debug data via attributes.

## Key Classes

### DebugMenu
- **Location**: `UI/Debug/DebugMenu.cs`
- **Purpose**: Main autoload singleton (DEBUG only) that hosts all debug modules in a tabbed interface.
- **Key Responsibilities**:
  - Creates and manages module tabs.
  - Provides global visibility toggle.

### DebugMenuController
- **Location**: `UI/Debug/DebugMenuController.cs`
- **Purpose**: Handles input (e.g., tilde key) to show/hide the debug menu and manages focus.

### IDebugModule / BaseDebugModule
- **Location**: `UI/Debug/IDebugModule.cs`, `BaseDebugModule.cs`
- **Purpose**: Interface and base class for all debug modules. Modules self-register with `DebugMenu` on initialization.

### DebugConsole
- **Location**: `UI/Debug/Console/DebugConsole.cs`
- **Purpose**: In-game command-line interface for executing debug commands.
- **Key Responsibilities**:
  - Provides a scrollback log and input field.
  - Integrates with `CommandRegistry`, `AutocompleteEngine`, and `CommandParser`.

### CommandRegistry / CommandParser / AutocompleteEngine
- **Location**: `UI/Debug/Console/CommandRegistry.cs`, `CommandParser.cs`, `AutocompleteEngine.cs`
- **Purpose**: Registers available commands, parses player input, and provides tab-completion.

### ICommand / Built-in Commands
- **Location**: `UI/Debug/Console/ICommand.cs`, `Commands/`
- **Purpose**: Individual command implementations:
  - `BuiltInCommands`: Help, clear, echo.
  - `EconomyCommands`: Query and modify economy state.
  - `LogisticsCommands`: Inspect and control logistics units.
  - `ModificationCommands`: Modify game state at runtime.
  - `QueryCommands`: Query databases and scene tree.
  - `SettingsCommands`: Change runtime settings.
  - `SimulationCommands`: Control time and simulation speed.
  - `StateCommands`: Inspect HSM states.
  - `ThreadCommands`: Inspect thread pool status.

### AutoRegistrationManager / InstanceRegistry
- **Location**: `UI/Debug/Console/AutoRegistrationManager.cs`, `InstanceRegistry.cs`
- **Purpose**: Automatically discovers and registers debug-data annotated objects, allowing commands to query live instances by type or name.

### Debug Attributes
- **Location**: `UI/Debug/Attributes/`
- **Purpose**:
  - `DebugDataAttribute`: Marks a class for automatic debug registration.
  - `DebugCommandAttribute`: Marks methods as console commands.
  - `DebugDataPropertyAttribute`: Exposes specific properties to the database viewer.

### DatabaseViewer / DataTreeBuilder / DataProviderRegistry
- **Location**: `UI/Debug/DatabaseViewer/`
- **Purpose**: Hierarchical inspector for in-game data. Displays live values from registered providers.

### IDataProvider / IDebugDataProvider
- **Location**: `UI/Debug/DatabaseViewer/IDataProvider.cs`, `IDebugDataProvider.cs`
- **Purpose**: Interface for objects that can supply debug data to the viewer.

### GodotNative Providers
- **Location**: `UI/Debug/DatabaseViewer/Providers/GodotNative/`
- **Purpose**: Built-in providers that expose Godot engine internals:
  - `AudioServerProvider`, `InputMapProvider`, `PerformanceProvider`, `PhysicsServerProvider`, `ProjectSettingsProvider`, `RenderingServerProvider`, `ResourceLoaderProvider`, `SceneTreeProvider`, `TranslationServerProvider`.

### EconomyDebugModule
- **Location**: `UI/Debug/Economy/EconomyDebugModule.cs`
- **Purpose**: Real-time display of economy statistics, power grids, and production graphs.

### ManufactureTickModule
- **Location**: `UI/Debug/ManufactureTick/ManufactureTickModule.cs`
- **Purpose**: Displays telemetry from the `ManufactureTickEngine`, including tick duration, exception counts, and lag.

### CellInfo
- **Location**: `UI/Debug/CellInfo/CellInfo.cs`
- **Purpose**: Debug module showing raw data for the currently selected Voronoi cell.

### SettingsPanel / CategorySection / SettingRow
- **Location**: `UI/Debug/Settings/`
- **Purpose**: Debug UI for viewing and editing all `RuntimeSettings` entries.

## Related Documentation
- [Utility Systems](UtilitySystems.md)
- [UI and State Machine](UIAndStateMachine.md)
