# Debug Menu Implementation Plan

## Overview

A comprehensive debug menu system for developers, providing an in-game console with command execution and a database viewer for inspecting loaded data. The system is designed for extensibility to support future modules.

## Goals

- Debug Menu toggled with '`~`' key
- In-game Console with game-modifying commands
- Database Viewer to inspect loaded memory and storage
- Extensible architecture for future modules
- DEBUG build only (compiled out for RELEASE)

## Key Design Decisions

| Decision | Choice |
|----------|--------|
| Architecture | Autoload Singleton (CanvasLayer) |
| Build Mode | DEBUG only (conditional compilation) |
| Input Priority | `_UnhandledInput` consumes before game |
| Refresh Rate | Physics update rate (`_PhysicsProcess`) |
| Permission Levels | 2 levels: DEBUG (enabled) / RELEASE (compiled out) |
| Instance Namespaces | Each instance gets unique namespace: `<TypeName>.<InstanceId>` |
| UI Style | Simple/functional, matches existing patterns |

## Architecture Overview

```
DebugMenu (Autoload - CanvasLayer) [DEBUG only]
├── DebugMenuController.cs (toggle, input routing)
├── Console/
│   ├── DebugConsole.tscn/.cs (UI)
│   ├── CommandRegistry.cs (reflection-based discovery)
│   ├── AutocompleteEngine.cs (suggestions, tab completion)
│   ├── InstanceRegistry.cs (namespace management)
│   └── Commands/ (attribute-tagged methods)
├── DatabaseViewer/
│   ├── DatabaseViewer.tscn/.cs (UI)
│   ├── DataProviderRegistry.cs (tagged database discovery)
│   └── Providers/ (Godot-native + project databases)
└── Attributes/
    ├── DebugCommandAttribute.cs (tag methods as commands)
    ├── DebugDataAttribute.cs (tag database objects)
    └── DebugDataPropertyAttribute.cs (tag specific properties for display)
```

## Instance Namespace Design

```
Command format: [namespace.]command [args]

Examples:
  help                          # Global command
  list_bodies                   # Global command
  
  CelestialBody.Earth.regenerate        # Instance command (named node)
  StructureDatabase.0.regenerate        # Instance command (auto-ID)
  MeshGenerationThreadPool.status       # Singleton (no ID needed)
  
  CelestialBody.*.regenerate            # Wildcard - all instances
```

**Namespace Generation Rules:**
- Singletons: Use class name directly (e.g., `MeshGenerationThreadPool`)
- Scene Nodes: `<TypeName>.<NodeName>` (e.g., `CelestialBody.Earth`)
- Generic Objects: `<TypeName>.<SequentialId>` (e.g., `StructureDatabase.0`)

## File Structure

```
UI/Debug/
├── DebugMenu.cs + .tscn                    # Main autoload, DEBUG conditional
├── DebugMenuController.cs                  # Toggle, input routing
├── IDebugModule.cs                         # Module interface
├── BaseDebugModule.cs                      # Common module functionality
│
├── Attributes/
│   ├── DebugCommandAttribute.cs            # Tag methods as commands
│   ├── DebugDataAttribute.cs               # Tag database classes
│   └── DebugDataPropertyAttribute.cs       # Tag specific properties
│
├── Console/
│   ├── DebugConsole.cs + .tscn             # Console UI
│   ├── ICommand.cs                         # Command interface
│   ├── CommandRegistry.cs                  # Reflection-based discovery
│   ├── CommandParser.cs                    # Input parsing
│   ├── CommandContext.cs                   # Execution context
│   ├── AutocompleteEngine.cs               # Suggestions, history
│   ├── InstanceRegistry.cs                 # Tracks instance namespaces
│   └── Commands/
│       ├── BuiltInCommands.cs              # help, clear, history
│       ├── QueryCommands.cs                # get, dump, list_*, find
│       ├── StateCommands.cs                # set_log_level, toggle_*
│       ├── ModificationCommands.cs         # spawn, set_param, reload
│       └── ThreadCommands.cs               # thread_*
│
├── DatabaseViewer/
│   ├── DatabaseViewer.cs + .tscn           # Viewer UI
│   ├── IDataProvider.cs                    # Provider interface
│   ├── IDebugDataProvider.cs               # For tagged objects
│   ├── DataProviderRegistry.cs             # Discovers & registers providers
│   ├── DataTreeBuilder.cs                  # Converts data to TreeItems
│   ├── DebugDataNode.cs                    # Data container class
│   └── Providers/
│       ├── GodotNative/
│       │   ├── SceneTreeProvider.cs
│       │   ├── ResourceLoaderProvider.cs
│       │   ├── AudioServerProvider.cs
│       │   ├── InputMapProvider.cs
│       │   ├── PhysicsServerProvider.cs
│       │   ├── RenderingServerProvider.cs
│       │   ├── TranslationServerProvider.cs
│       │   ├── ProjectSettingsProvider.cs
│       │   └── PerformanceProvider.cs
│       └── Project/
│           ├── ResourceDatabaseProvider.cs
│           ├── ThreadPoolProvider.cs
│           └── StructureDatabaseProvider.cs
│
└── Modules/
    └── (Future: Profiler, Network, etc.)
```

**Total: ~40 files**

## Attribute System

### DebugCommandAttribute

Tags methods as console commands. Supports both static and instance methods.

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class DebugCommandAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public string Usage { get; }
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public bool RequiresTarget { get; set; }  // For instance methods
    public string Category { get; set; } = "General";
    
    public DebugCommandAttribute(string name, string description, string usage = "")
    {
        Name = name;
        Description = description;
        Usage = usage;
    }
}
```

**Example Usage:**
```csharp
// Static command
public static class DebugCommands
{
    [DebugCommand("help", "Show available commands", "help [command]")]
    public static int Help(CommandContext ctx, string[] args) { ... }
}

// Instance command
public partial class CelestialBody : Node3D
{
    [DebugCommand("regenerate", "Regenerate this body", "regenerate", RequiresTarget = true)]
    public int Regenerate(CommandContext ctx, string[] args)
    {
        GenerateMesh();
        return 0;
    }
}
```

### DebugDataAttribute

Tags classes as database objects for the viewer.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public class DebugDataAttribute : Attribute
{
    public string DisplayName { get; }
    public string Category { get; set; } = "General";
    public int Priority { get; set; } = 0;       // Display order
    public bool AutoRefresh { get; set; } = true; // Poll for changes
    
    public DebugDataAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}
```

### DebugDataPropertyAttribute

Tags specific properties for display control.

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class DebugDataPropertyAttribute : Attribute
{
    public string DisplayName { get; set; }  // null = use property name
    public bool IsReadOnly { get; set; } = true;
    public string Format { get; set; } = ""; // e.g., "F2" for floats
    public bool HideIfNull { get; set; } = true;
}
```

## Command System

### Command Discovery

The `CommandRegistry` uses reflection to discover all methods tagged with `[DebugCommand]`:

```
On Initialize():
  For each assembly:
    For each type:
      For each static method with [DebugCommand]:
        Register as global command
```

### Instance Registry

Tracks all registered object instances and their namespaces:

```csharp
public static class InstanceRegistry
{
    private static Dictionary<string, object> _instances = new();
    private static Dictionary<object, string> _reverseLookup = new();
    private static Dictionary<Type, int> _typeCounters = new();
    
    public static string Register(object instance);
    public static bool TryGetInstance(string namespace, out object instance);
    public static IEnumerable<string> GetNamespaces<T>();
    public static void Unregister(object instance);
}
```

**Auto-Registration:**
- Objects with `[DebugData]` attribute auto-register on creation
- Nodes in scene tree auto-register when entering tree
- Manual registration via `InstanceRegistry.Register(myObject)`

### Built-in Commands

| Command | Description | Example |
|---------|-------------|---------|
| `help [command]` | Show help for command | `help spawn` |
| `clear` | Clear console output | `clear` |
| `history` | Show command history | `history` |
| `list_commands` | List all commands | `list_commands` |
| `list_namespaces [type]` | List registered instances | `list_namespaces CelestialBody` |
| `get <path>` | Get property value | `get CelestialBody.Earth.Position` |
| `set <path> <value>` | Set property value | `set ProjectSettings.Debug true` |
| `dump <database>` | Dump database contents | `dump ResourceLoader` |
| `find <pattern>` | Search all databases | `find *iron*` |
| `set_log_level <level>` | Change log level | `set_log_level DEBUG` |
| `toggle_wireframe` | Toggle wireframe mode | `toggle_wireframe` |
| `reload_resources` | Reload resource configs | `reload_resources` |
| `spawn <type> [name]` | Spawn celestial body | `spawn RockyPlanet Mars2` |
| `thread_status` | Show thread pool status | `thread_status` |
| `thread_cancel <id>` | Cancel task | `thread_cancel 5` |
| `watch <path>` | Watch property changes | `watch CelestialBody.Earth.Position` |

## Autocomplete System

### Completion Sources

1. Command names (global + instance namespaces)
2. Command-specific argument suggestions (via `ICommand.GetSuggestions()`)
3. File paths (for commands taking file args)
4. Property paths (for `get`/`set` commands)
5. History matches

### UI Behavior

- Popup appears below cursor after 2+ characters typed
- Tab accepts first suggestion
- Arrow keys navigate suggestions
- Escape dismisses
- Up/Down in empty input navigates history

## Database Viewer

### Architecture

Each data provider implements:
```csharp
public interface IDataProvider
{
    string Name { get; }
    string Category { get; }
    bool NeedsRefresh { get; }
    DebugDataNode GetData();
    void Refresh();
    IEnumerable<string> Search(string pattern);
}
```

### Refresh Logic

Data refreshes at physics update rate:
```csharp
public override void _PhysicsProcess(double delta)
{
    if (!Visible) return;
    
    foreach (var provider in _activeProviders)
    {
        if (provider.NeedsRefresh)
            provider.Refresh();
    }
    
    UpdateTreeDisplay();
}
```

### UI Layout

```
VSplitContainer
├── HSplitContainer (top - main view)
│   ├── CategoryList (ItemList, left sidebar)
│   │   ├── Scene Tree
│   │   ├── Resources
│   │   ├── Audio
│   │   ├── Input
│   │   ├── Physics
│   │   ├── Rendering
│   │   ├── Localization
│   │   ├── Settings
│   │   ├── Performance
│   │   └── Project Databases
│   └── DataTree (Tree, right panel)
│       └── Hierarchical display of selected database
└── DetailPanel (VBoxContainer, bottom)
    ├── SearchBox (LineEdit with filter)
    ├── SelectedItemInfo (RichTextLabel)
    └── ActionButtons (HBoxContainer)
        ├── Refresh
        ├── Copy Path
        └── Watch (poll for changes)
```

### Property Display

- Primitive types → formatted string with copy button
- Collections → expandable tree nodes
- Godot types (Vector3, Color, etc.) → specialized formatters
- Custom objects → reflection + `[DebugDataProperty]` attributes
- Null values → gray italic "null" or hidden based on attribute

## Godot-Native Data Providers

| Provider | Key Data |
|----------|----------|
| `SceneTreeProvider` | Node hierarchy, groups, properties |
| `ResourceLoaderProvider` | Loaded resources by type, memory usage |
| `AudioServerProvider` | Buses, volumes, active streams |
| `InputMapProvider` | Actions and their mappings |
| `PhysicsServerProvider` | Bodies, collision shapes, contacts |
| `RenderingServerProvider` | GPU info, draw calls, materials |
| `TranslationServerProvider` | Locales, translation keys |
| `ProjectSettingsProvider` | All project settings |
| `PerformanceProvider` | FPS, memory, frame time |

## Project-Specific Providers

Tag existing classes with `[DebugData]`:

```csharp
// In MeshGenerationThreadPool.cs
[DebugData("Thread Pool", Category = "System")]
public partial class MeshGenerationThreadPool : Node, IDebugDataProvider
{
    public DebugDataNode GetData() => new DebugDataNode("Thread Pool")
        .AddProperty("Active Tasks", _activeTasks.Count)
        .AddProperty("Queued Tasks", _taskQueue.Count)
        .AddProperty("Max Threads", _maxThreads)
        .AddChild("Active Tasks", _activeTasks.Select(t => new DebugDataNode(t.Name)
            .AddProperty("Priority", t.Priority)
            .AddProperty("Progress", t.Progress)));
}

// In ResourceDatabase.cs
[DebugData("Resources", Category = "Game")]
public partial class ResourceDatabase : Node, IDebugDataProvider
{
    public DebugDataNode GetData() => new DebugDataNode("Resources")
        .AddProperty("Total Definitions", _definitions.Count)
        .AddChild("Definitions", _definitions.Select(d => 
            new DebugDataNode(d.Key).AddProperty("Type", d.Value.Type)));
}

// In StructureDatabase.cs
[DebugData("Structure Database", Category = "Mesh Generation")]
public partial class StructureDatabase : IDebugDataProvider
{
    public DebugDataNode GetData() => new DebugDataNode("Structure Database")
        .AddProperty("Vertices", BaseVertices.Count)
        .AddProperty("Edges", HalfEdgeById.Count)
        .AddProperty("Triangles", TrianglesById.Count)
        .AddProperty("Voronoi Cells", VoronoiCells.Count);
}
```

## Integration Points

### Modifications to Existing Files

1. **project.godot** - Add autoload:
```ini
[autoload]
DebugMenu="*res://UI/Debug/DebugMenu.tscn"
```

2. **InputHandler.cs** - Add early return when debug menu open:
```csharp
public override void _Input(InputEvent @event)
{
#if DEBUG
    if (DebugMenu.Instance?.IsVisible == true)
        return;
#endif
    // ... existing code
}
```

3. **MeshGenerationThreadPool.cs** - Add `[DebugData]` attribute, implement `IDebugDataProvider`

4. **ResourceDatabase.cs** - Add `[DebugData]` attribute, implement `IDebugDataProvider`

5. **StructureDatabase.cs** - Add `[DebugData]` attribute, implement `IDebugDataProvider`

### Build Configuration

```xml
<!-- In .csproj or via Godot build configuration -->
<PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
  <DefineConstants>DEBUG</DefineConstants>
</PropertyGroup>
```

## Implementation Phases

| Phase | Task | Files | Dependencies |
|-------|------|-------|--------------|
| 1 | Core infrastructure + DEBUG conditional | 4 | None |
| 2 | Attribute system | 3 | Phase 1 |
| 3 | Command registry + discovery | 5 | Phase 2 |
| 4 | Instance registry + namespacing | 2 | Phase 3 |
| 5 | Basic console UI | 2 | Phase 4 |
| 6 | Autocomplete + tab completion | 2 | Phase 5 |
| 7 | Built-in commands | 5 | Phase 5 |
| 8 | Database viewer core | 6 | Phase 1 |
| 9 | Godot-native providers | 9 | Phase 8 |
| 10 | Project-specific providers | 3 | Phase 8 |
| 11 | Integration & testing | - | All |

## Extensibility

### Adding New Commands

```csharp
public static class MyCustomCommands
{
    [DebugCommand("my_command", "Does something custom", "my_command <arg>")]
    public static int MyCommand(CommandContext ctx, string[] args)
    {
        ctx.Output.WriteLine("Executed!");
        return 0;
    }
}
```

### Adding New Data Providers

```csharp
[DebugData("My Database", Category = "Custom")]
public class MyDatabaseProvider : IDataProvider
{
    public string Name => "My Database";
    public string Category => "Custom";
    public DebugDataNode GetData() { ... }
}
```

### Adding New Modules

```csharp
public class ProfilerModule : BaseDebugModule
{
    public override string ModuleName => "Profiler";
    public override void OnModuleEnabled() { ... }
    public override void OnModuleDisabled() { ... }
}

// Registration
DebugMenu.Instance.RegisterModule(new ProfilerModule());
```
