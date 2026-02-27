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

# Format code (if dotnet format is available)
dotnet format

# Run tests (via Godot editor)
# Open Tests/test_thread_pool.tscn in Godot and run the scene (F6)
# Tests auto-exit on completion

# Run Godot headless for testing
godot --headless --script Tests/test_thread_pool.cs
```

## Project Structure

```
├── Scripts/
│   ├── ProceduralGeneration/     # Core generation algorithms
│   │   ├── MeshGeneration/       # Delaunay, Voronoi, tectonics, biomes
│   │   ├── CelestialBody.cs      # Base celestial body class
│   │   ├── SystemGenerator.cs    # System-wide generation orchestration
│   │   └── PlanetGeneration/     # Planet-specific generation
│   ├── Structures/
│   │   ├── MeshGeneration/       # Point, Edge, Triangle, HalfEdge, Face
│   │   ├── Enums/                # Biome, CelestialBodyType, VertexDistribution
│   │   ├── GameState/            # VoronoiCell, Continent, Octree
│   │   └── Resources/            # ResourceDefinition
│   ├── UtilityLibrary/           # Logger, ThreadPool, Randomizer, OrbitalMath
│   └── PlayerInteraction/        # Input handling, player controls
├── UI/                           # Godot UI controls (C# + .tscn)
├── Tests/                        # Godot test scenes
├── Configuration/
│   ├── SystemGen/                # Body type definitions (TOML)
│   ├── SystemTemplate/           # Pre-built system templates
│   └── ResourceDefinition/       # Resource configs
├── Shaders/                      # GDShader files
└── Docs/                         # Architecture documentation
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

Organize imports in this order, alphabetically within each group:
1. System namespaces (`System`, `System.Collections.Generic`, `System.Threading.Tasks`)
2. Godot namespaces (`Godot`, `Godot.Collections`)
3. Project namespaces (`ProceduralGeneration.*`, `Structures.*`, `UtilityLibrary`, `UI`)

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using UtilityLibrary;
```

### Types and Godot Integration

- Prefer `float` over `double` for performance with Godot APIs
- Use Godot math types: `Vector3`, `Vector2`, `Mathf` (not `Math`)
- Use `RandomNumberGenerator` for deterministic procedural generation
- Export fields with `[Export]` attribute for Godot inspector visibility
- Use `[ExportCategory]` and `[ExportGroup]` to organize inspector properties
- Define signals with `[Signal]` attribute and `EventHandler` suffix

```csharp
[ExportCategory("Planet Generation")]
[ExportGroup("Mesh Generation")]
[Export]
public int subdivide = 1;

[Signal]
public delegate void GeneratedVoronoiCellsEventHandler();
```

### Documentation

- Include XML documentation for all public classes, methods, and properties
- Use `<summary>`, `<param>`, `<returns>`, and `<remarks>` tags
- Document the "why" not just the "what" in remarks

```csharp
/// <summary>
/// Dynamically detects the generation type based on configuration parameters.
/// </summary>
/// <param name="meshParams">Dictionary containing mesh generation parameters.</param>
/// <returns>The detected BodyGenerationType.</returns>
/// <remarks>
/// The detection logic prioritizes: Tectonics > Scaling > Noise
/// </remarks>
```

### Error Handling

- Use try/catch blocks for configuration parsing and mesh generation
- Log errors using `Logger.Error()` or `GD.PrintErr()`
- For mesh generation, wrap operations and return error codes when using thread pool

```csharp
try
{
    await baseMesh.InitiateDeformation(cycles, aberrations, sideLength);
}
catch (Exception e)
{
    Logger.Error($"Deformation Error: {e.Message}\n{e.StackTrace}");
    return 1;
}
```

- Silent catches are acceptable for optional configuration parsing:

```csharp
if (meshParams.ContainsKey("size"))
{
    try { size = meshParams["size"].As<float>(); } 
    catch { GD.PrintErr("Couldn't find size in meshParams"); }
}
```

### Logging

Use the custom `Logger` class for structured logging:

```csharp
Logger.Debug("Detailed diagnostic information");
Logger.Info("General information");
Logger.Warning("Warning conditions");
Logger.Error("Error conditions");
Logger.Critical("Critical failures");
Logger.EnterFunction("MethodName", "param1, param2");
Logger.ExitFunction("MethodName", "returnValue");
```

For quick debugging, use Godot's built-in:
```csharp
GD.Print("Debug message");
GD.PrintErr("Error message");
GD.PrintRaw("Raw output without newline");
```

### Async and Threading

- Use `async/await` pattern for mesh generation to avoid blocking main thread
- The `MeshGenerationThreadPool` autoload manages concurrent generation tasks
- Always check `UseThreadPool` flag before enqueueing tasks

```csharp
if (UseThreadPool && MeshGenerationThreadPool.Instance != null)
{
    await MeshGenerationThreadPool.Instance.EnqueueTask(
        () => { GenerateFirstPass(); return 0; },
        $"{Name}_firstpass",
        TaskPriority.High,
        Name
    );
}
else
{
    Task.Run(() => GenerateFirstPass());
}
```

- Use `CallDeferred()` for Godot API calls from background threads:

```csharp
this.CallDeferred("set_mesh", new ArrayMesh());
this.CallDeferred("set_name", name + "_mesh");
```

### GDScript Files

For the few GDScript files in the project:
- Use snake_case for functions and variables
- Use PascalCase for class names with `class_name`
- Add type hints where possible: `var _velocity: Vector3 = Vector3.ZERO`

## Configuration System

The project uses YAML files for configuration via YamlDotNet:

- `Configuration/SystemGen/*.yaml` - Body type definitions (Star, GasGiant, RockyPlanet, etc.)
- `Configuration/SystemTemplate/*.yaml` - Complete system templates (Solar System, Binary Star)

### Loading Templates

Use `TemplateLoader` for raw YAML access with validation:
```csharp
var raw = TemplateLoader.Load("RockyPlanet", TemplateLoader.CelestialBodyValidator);
```

Use `TemplateHelpers` for transformed data matching expected consumer format:
```csharp
var defaults = TemplateHelpers.GetCelestialBodyDefaults(CelestialBodyType.RockyPlanet);
var bodies = TemplateHelpers.LoadSystemTemplate("Solar System.yaml");
var yamlContent = TemplateHelpers.GenerateYamlContent(bodies);
```

### Validation

YAML files are validated by `YamlValidator`:
- `ValidateCelestialBodyTemplate(path)` - Validates body type templates
- `ValidateSystemTemplate(path)` - Validates system templates
- `ValidateResourceDefinition(path)` - Validates resource definitions

## Key Architecture Patterns

1. **Builder Pattern**: `CelestialBody.Builder.BuildFromBodyDict()` for constructing bodies from dictionaries
2. **Strategy Pattern**: `BodyGenerationType` enum selects generation pipeline
3. **Two-Pass Generation**: First pass creates base mesh, second pass adds Voronoi/tectonics
4. **Autoload Singletons**: `MeshGenerationThreadPool` registered in project.godot

## Running in Godot Editor

1. Open project in Godot 4.4+
2. Ensure .NET SDK 8.0 is installed
3. Build first: `dotnet build` or use Godot's Build button
4. Run main scene (F5) or test scenes (F6)
5. Debug with VS Code using the "Launch Godot Editor" configuration
