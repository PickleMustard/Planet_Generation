# Agent Configuration for Delaunay Triangulation Map Generation

A Godot 4.4 project using C# (.NET 8.0) for procedural celestial body and planetary system generation via spherical Delaunay triangulation, tectonic simulation, and Voronoi cell partitioning.

## Build/Lint/Test Commands

```bash
# Build the project
dotnet build

# Build with verbose output
dotnet build -v detailed

# Clean build artifacts
dotnet clean

# Format code
dotnet format

# Run all tests via Godot editor
# Open Godot and press F6 on any test scene, or use GUT/gdUnit4 runner

# Run a single test file (via Godot command line)
godot --headless --path . -s Tests/ThreadPoolTest.cs

# Run specific test via gdUnit4 inspector (in Godot editor)
# Right-click test file > Run Tests, or use gdUnit4 dock
```

## Project Structure

```
Scripts/
├── ProceduralGeneration/     # Core generation algorithms
│   ├── MeshGeneration/       # Delaunay, Voronoi, tectonics, biomes
│   ├── CelestialBody.cs      # Base celestial body class
│   ├── SystemGenerator.cs    # System-wide orchestration
│   └── PlanetGeneration/     # Planet-specific generation
├── Structures/
│   ├── MeshGeneration/       # Point, Edge, Triangle, HalfEdge, Face
│   ├── Enums/                # Biome, CelestialBodyType, VertexDistribution
│   ├── GameState/            # VoronoiCell, Continent, Octree
│   └── Resources/            # ResourceDefinition, ResourceDeposit
├── UtilityLibrary/           # GameLogger, ThreadPool, Randomizer, OrbitalMath
└── PlayerInteraction/        # Input handling, player controls
Tests/                        # gdUnit4 test suites
Configuration/
├── SystemGen/                # Body type definitions (YAML)
├── SystemTemplate/           # Pre-built system templates
└── ResourceDefinition/       # Resource configs
```

## Code Style Guidelines

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes, Methods, Properties | PascalCase | `GenerateMesh()`, `StructureDatabase` |
| Public fields | PascalCase | `MaxHeight`, `NumContinents` |
| Private fields | _camelCase or camelCase | `_bodiesList`, `totalBodiesToGenerate` |
| Local variables | camelCase | `baseMesh`, `continents` |
| Constants | PascalCase or SCREAMING_SNAKE | `GRAVITATIONAL_CONSTANT`, `BASE_SIZE` |
| Enums | PascalCase (type and values) | `BodyGenerationType.TectonicsOnly` |
| Interfaces | IPascalCase | `IPoint`, `IVoronoiCell` |
| Namespaces | PascalCase (feature-based) | `ProceduralGeneration.MeshGeneration` |

### Import Organization

Organize imports alphabetically within groups:
1. System namespaces (`System`, `System.Collections.Generic`, `System.Threading.Tasks`)
2. Godot namespaces (`Godot`, `Godot.Collections`)
3. Third-party (`GdUnit4`, `YamlDotNet`)
4. Project namespaces (`ProceduralGeneration.*`, `Structures.*`, `UtilityLibrary`)

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using GdUnit4;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using UtilityLibrary;
```

### Types and Godot Integration

- Prefer `float` over `double` for Godot API compatibility
- Use Godot math types: `Vector3`, `Vector2`, `Mathf` (not `Math`)
- Use `RandomNumberGenerator` for deterministic procedural generation
- Export fields with `[Export]` for Godot inspector visibility
- Use `[ExportCategory]` and `[ExportGroup]` to organize properties

```csharp
[ExportCategory("Planet Generation")]
[ExportGroup("Mesh Generation")]
[Export]
public int Subdivide = 1;
```

### Error Handling

- Use try/catch for configuration parsing and mesh generation
- Log errors via `GameLogger.Error()` or `GD.PrintErr()`
- Return error codes from thread pool tasks

```csharp
try
{
    await baseMesh.InitiateDeformation(cycles, aberrations, sideLength);
}
catch (Exception e)
{
    GameLogger.Error($"Deformation Error: {e.Message}\n{e.StackTrace}");
    return 1;
}
```

### Logging

Use `GameLogger` class (in UtilityLibrary namespace):

```csharp
GameLogger.Debug("Detailed diagnostic information");
GameLogger.Info("General information");
GameLogger.Warning("Warning conditions");
GameLogger.Error("Error conditions");
GameLogger.Critical("Critical failures");
GameLogger.EnterFunction("MethodName", "param1, param2");
GameLogger.ExitFunction("MethodName", "returnValue");
```

For quick debugging: `GD.Print()`, `GD.PrintErr()`

### Async and Threading

- Use `async/await` for mesh generation to avoid blocking main thread
- Use `CallDeferred()` for Godot API calls from background threads

```csharp
this.CallDeferred("set_mesh", new ArrayMesh());
```

### Testing with gdUnit4

```csharp
using GdUnit4;
using static GdUnit4.Assertions;

namespace Tests;

[TestSuite]
public class MyTest
{
    [TestCase]
    [RequireGodotRuntime]  // Use when Godot APIs are needed
    public void TestSomething()
    {
        AssertThat(actualValue).IsEqual(expectedValue);
        AssertThat(list.Count).IsGreater(0);
    }

    [TestCase]
    public async void TestAsyncOperation()
    {
        await someAsyncOperation();
        AssertThat(result).IsNotNull();
    }
}
```

### GDScript Files

- Use snake_case for functions and variables
- Use PascalCase for class names with `class_name`
- Add type hints: `var _velocity: Vector3 = Vector3.ZERO`

## Configuration System

The project uses YAML files via YamlDotNet:

- `Configuration/SystemGen/*.yaml` - Body type definitions
- `Configuration/SystemTemplate/*.yaml` - Complete system templates

Load templates using:
```csharp
var raw = TemplateLoader.Load("RockyPlanet", TemplateLoader.CelestialBodyValidator);
var defaults = TemplateHelpers.GetCelestialBodyDefaults(CelestialBodyType.RockyPlanet);
```

## Autoload Singletons

The project uses the following autoload singletons registered in `project.godot`:

| Singleton | Path | Purpose |
|-----------|------|---------|
| `RuntimeSettings` | `Scripts/UtilityLibrary/Settings/RuntimeSettings.cs` | Centralized settings management with persistence |
| `SignalBus` | `Scripts/UtilityLibrary/SignalBus.cs` | Global signal/event dispatcher |
| `ThreadPooler` | `Scripts/UtilityLibrary/TaskSystem/ThreadPooler.cs` | Background task execution |
| `TaskTimer` | `Scripts/UtilityLibrary/TaskTimer.cs` | Progress tracking and timing |
| `ResourceDatabase` | `Scripts/Structures/Resources/ResourceDatabase.cs` | Resource definitions storage |
| `CellSelectionManager` | `Scripts/PlayerInteraction/CellSelection/CellSelectionManager.cs` | Cell selection handling |
| `DebugMenu` | `UI/Debug/DebugMenu.tscn` | Debug console and database viewer |

Access singletons via their static `Instance` property:

```csharp
// Get a setting value
int threadCount = RuntimeSettings.Instance?.GetSetting<int>("threading", "manual_thread_count") ?? 0;

// Emit a signal
SignalBus.Instance?.EmitStartTimer("Generation", 5, 0, stepNames);

// Queue a background task
ThreadPooler.Instance?.EnqueuePackage(package);
```

## Key Architecture Patterns

1. **Builder Pattern**: `CelestialBody.Builder.BuildFromBodyDict()` for constructing bodies
2. **Strategy Pattern**: `BodyGenerationType` enum selects generation pipeline
3. **Two-Pass Generation**: First pass creates base mesh, second adds Voronoi/tectonics
4. **IConfigurable Pattern**: Objects implement `IConfigurable` to expose settings to `RuntimeSettings`

## OpenProject Integration

The project uses OpenProject for task tracking. Project name: **Startreprenuer**

### Work Package Hierarchy

- **EPIC**: Top-level containers for major features/initiatives
- **Feature**: Groups of related tasks
- **Task**: Smallest unit of work

### Workflow

1. Create tasks as the smallest work items
2. Group related tasks under a Feature
3. All tasks and features must be placed under an existing EPIC
4. If no suitable EPIC exists, ask the user before creating a new one

### API Rate Limiting

**IMPORTANT**: All OpenProject API requests must be separated by a 1-second delay to avoid rate limiting:

```bash
sleep 1
```

When making multiple API calls, ensure proper sequencing with delays between each request.
