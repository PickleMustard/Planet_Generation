# Debug Menu Usage Guide

## Overview

The Debug Menu is a developer tool available in DEBUG builds that provides an in-game console for command execution and a database viewer for inspecting loaded data.

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `` ` `` (backtick/grave) | Toggle debug menu |
| `Tab` | Accept autocomplete suggestion |
| `Up/Down` (empty input) | Navigate command history |
| `Up/Down` (suggestions visible) | Navigate autocomplete suggestions |
| `Escape` | Dismiss autocomplete popup |

## Console Commands

### Built-in Commands

| Command | Description | Example |
|---------|-------------|---------|
| `help [command]` | Show available commands or help for a specific command | `help spawn` |
| `clear` | Clear console output | `clear` |
| `history` | Show command history | `history` |
| `list_commands [category]` | List all registered commands | `list_commands Query` |
| `list_namespaces [type]` | List registered instances | `list_namespaces CelestialBody` |

### Query Commands

| Command | Description | Example |
|---------|-------------|---------|
| `get <namespace>.<property>` | Get a property value | `get CelestialBody.Earth.Position` |
| `dump <namespace>` | Dump all properties of an instance | `dump MeshGenerationThreadPool` |
| `find <pattern>` | Search all registered instances (supports `*` wildcard) | `find *Earth*` |

### State Commands

| Command | Description | Example |
|---------|-------------|---------|
| `set_log_level <level>` | Change Logger log level | `set_log_level DEBUG` |
| `toggle_wireframe` | Toggle wireframe rendering mode | `toggle_wireframe` |
| `set <path> <value>` | Set a property value | `set CelestialBody.Earth.Mass 1000` |

**Log Levels:** DEBUG, INFO, WARNING, ERROR, CRITICAL, PROD

### Modification Commands

| Command | Description | Example |
|---------|-------------|---------|
| `spawn <type> [name] [position]` | Spawn a celestial body | `spawn RockyPlanet NewPlanet (100,0,50)` |
| `reload_resources` | Reload resource configurations | `reload_resources` |
| `set_param <namespace> <param> <value>` | Set a generation parameter | `set_param CelestialBody.Earth mass 1000` |

**Celestial Body Types:** Star, RockyPlanet, GasGiant, Moon, Asteroid, Comet, BlackHole

### Thread Commands

| Command | Description | Example |
|---------|-------------|---------|
| `thread_status` | Show thread pool status and active tasks | `thread_status` |
| `thread_cancel <task_id>` | Cancel a specific task | `thread_cancel 5` |
| `watch <path> [interval_ms]` | Watch a property for changes | `watch CelestialBody.Earth.Position 1000` |
| `watch_stop [path\|all]` | Stop watching a property | `watch_stop all` |
| `watch_list` | List all active watches | `watch_list` |

## Instance Namespace Format

Commands use dot-notation to reference instances:

```
[namespace.]command [args]
```

**Namespace Examples:**
- `CelestialBody.Earth` - Named scene node
- `MeshGenerationThreadPool` - Singleton (no ID needed)
- `StructureDatabase.0` - Generic object with auto-ID

**Wildcard Support:**
- `CelestialBody.*.regenerate` - Apply to all CelestialBody instances

## Database Viewer

The Database Viewer tab provides inspection of:

### Godot-Native Providers

| Provider | Data |
|----------|------|
| Scene Tree | Node hierarchy, groups, properties |
| Resource Loader | Loaded resources by type, memory usage |
| Audio Server | Buses, volumes, active streams |
| Input Map | Actions and their mappings |
| Physics Server | Bodies, collision shapes, contacts |
| Rendering Server | GPU info, draw calls, materials |
| Translation Server | Locales, translation keys |
| Project Settings | All project settings |
| Performance | FPS, memory, frame time |

### Project-Specific Providers

| Provider | Data |
|----------|------|
| Thread Pool | Active/queued tasks, progress |
| Resource Database | Loaded resource definitions |
| Structure Database | Vertices, edges, triangles, Voronoi cells |

## Adding Custom Commands

Add commands by creating a static method with the `[DebugCommand]` attribute:

```csharp
using UI.Debug;
using UI.Debug.Console;

public static class MyCommands
{
    [DebugCommand("my_command", "Description of what it does", "my_command <arg>", Category = "Custom")]
    public static int MyCommand(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
        {
            ctx.WriteError("Usage: my_command <arg>");
            return 1;
        }

        ctx.WriteLine($"Executed with arg: {args[0]}");
        return 0;
    }
}
```

**Instance Commands (requires target):**

```csharp
public partial class CelestialBody : Node3D
{
    [DebugCommand("regenerate", "Regenerate this body", "regenerate", 
                  RequiresTarget = true, Category = "Celestial")]
    public int Regenerate(CommandContext ctx, string[] args)
    {
        GenerateMesh();
        ctx.WriteLine("Mesh regenerated");
        return 0;
    }
}
```

## Adding Custom Data Providers

Implement `IDataProvider` and tag with `[DebugData]`:

```csharp
using UI.Debug;
using UI.Debug.DatabaseViewer;

[DebugData("My Database", Category = "Custom")]
public class MyDatabaseProvider : IDataProvider
{
    public string Name => "My Database";
    public string Category => "Custom";
    public bool NeedsRefresh => true;

    public DebugDataNode GetData()
    {
        return new DebugDataNode("My Database")
            .AddProperty("Total Items", _items.Count)
            .AddChild("Items", _items.Select(item => 
                new DebugDataNode(item.Name)
                    .AddProperty("Value", item.Value)));
    }

    public void Refresh()
    {
        // Refresh data from source
    }

    public IEnumerable<string> Search(string pattern)
    {
        return _items.Where(i => i.Name.Contains(pattern))
                     .Select(i => i.Name);
    }
}
```

## Integration with Existing Classes

To expose a class to the database viewer, implement `IDebugDataProvider`:

```csharp
using UI.Debug;
using UI.Debug.DatabaseViewer;

[DebugData("Thread Pool", Category = "System")]
public partial class MeshGenerationThreadPool : Node, IDebugDataProvider
{
    public DebugDataNode GetData()
    {
        return new DebugDataNode("Thread Pool")
            .AddProperty("Active Tasks", _activeTasks.Count)
            .AddProperty("Queued Tasks", _taskQueue.Count)
            .AddProperty("Max Threads", _maxThreads);
    }
}
```

## Build Configuration

The debug menu is only compiled in DEBUG builds. Ensure your `.csproj` has:

```xml
<PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
  <DefineConstants>DEBUG</DefineConstants>
</PropertyGroup>
```

Release builds will have no debug menu references.
