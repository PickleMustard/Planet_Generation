# Agent Configuration for Delaunay Triangulation Map Generation

Godot 4.6 / C# (.NET 9.0, LangVersion 12) project for procedural celestial body and planetary system generation via spherical Delaunay triangulation, tectonic simulation, and Voronoi cell partitioning.

## Build/Lint/Test Commands

```bash
# Build
dotnet build

# Clean + rebuild
dotnet clean && dotnet build

# Format code
dotnet format

# Run ALL tests (requires GODOT_BIN env var for [RequireGodotRuntime] tests)
GODOT_BIN=/path/to/godot dotnet test

# Run a single test by exact method name (works without Godot for pure unit tests)
dotnet test --filter "Name=DepositCreation"

# Run multiple specific tests
dotnet test --filter "Name=DepositCreation|Name=ValidationErrorHandling"

# Run tests via gdUnit4 CLI runner (CI pipeline)
./addons/gdUnit4/runtest.sh --godot_binary "$GODOT_BIN" -a "res://Tests" -c -rd "./test-reports"

# Build for release (CI)
dotnet build --configuration Release --no-restore
```

**Note:** Tests marked `[RequireGodotRuntime]` need a running Godot engine. Without `GODOT_BIN` set, only pure unit tests (no `[RequireGodotRuntime]` attribute) can be discovered and run. The `--filter` `~` (contains) operator may fail for gdUnit4 tests without Godot; use exact `Name=` matching instead.

## Project Structure

`Scripts/ProceduralGeneration/` — Core generation (MeshGeneration/, ResourceGeneration/, CelestialBody, SystemGenerator, SatelliteBody)
`Scripts/Structures/` — Data types (MeshGeneration/, Enums/, GameState/, Resources/, Logistics/)
`Scripts/Constructables/` — Ships, stations, logistics units
`Scripts/UtilityLibrary/` — GameLogger, Randomizer, OrbitalMath, Settings/, TaskSystem/
`Scripts/PlayerInteraction/` — Input handling, cell selection
`Tests/` — gdUnit4 test suites (mirrors Scripts/ structure)
`Configuration/` — YAML configs (SystemGen/, SystemTemplate/, ResourceDefinition/)

## Code Style Guidelines

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes, Methods, Properties | PascalCase | `GenerateMesh()`, `StructureDatabase` |
| Public fields | PascalCase | `MaxHeight`, `NumContinents` |
| Private fields | `_camelCase` | `_bodiesList`, `_isInitialized` |
| Local variables | camelCase | `baseMesh`, `continents` |
| Constants | PascalCase or SCREAMING_SNAKE | `GRAVITATIONAL_CONSTANT`, `BASE_SIZE` |
| Enums | PascalCase (type and values) | `BodyGenerationType.TectonicsOnly` |
| Interfaces | `IPascalCase` | `IPoint`, `IVoronoiCell`, `IConfigurable` |
| Namespaces | PascalCase, feature-based | `ProceduralGeneration.MeshGeneration` |
| File-scoped namespaces | Preferred for enums/simple types | `namespace Structures.Enums;` |

### Import Organization

Alphabetical within groups, separated by blank lines:
1. `System` namespaces
2. `Godot` namespaces
3. Third-party (`GdUnit4`, `YamlDotNet`)
4. Project namespaces (`ProceduralGeneration.*`, `Structures.*`, `UtilityLibrary`)
5. `#if DEBUG` conditional imports last

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using UtilityLibrary;
#if DEBUG
using UI.Debug;
#endif
```

### Types and Godot Integration

- Prefer `float` over `double` for Godot API compatibility
- Use Godot math types: `Vector3`, `Vector2`, `Mathf` (not `System.Math`)
- Use `RandomNumberGenerator` for deterministic procedural generation
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`)
- Use `[Export]` for inspector visibility; group with `[ExportCategory]`/`[ExportGroup]`
- Singleton autoloads expose `public static T? Instance { get; private set; }` set in `_Ready()`

### Error Handling

- Use try/catch for configuration parsing and mesh generation
- Log errors via `GameLogger.Error()` or `GD.PrintErr()`
- Return error codes (int) from thread pool work steps
- Use `ArgumentNullException` for null guard checks in constructors
- Use null-conditional access for singletons: `RuntimeSettings.Instance?.GetSetting<int>(...) ?? default`

### Logging

Use `GameLogger` (UtilityLibrary namespace), not raw `GD.Print()`:

```csharp
GameLogger.Debug("Detailed diagnostic info");
GameLogger.Info("General information");
GameLogger.Warning("Warning conditions");
GameLogger.Error("Error conditions");
GameLogger.Critical("Critical failures");
GameLogger.EnterFunction("MethodName", "param1, param2");
GameLogger.ExitFunction("MethodName", "returnValue");
```

### Async and Threading

- Use `async/await` for mesh generation pipelines
- Use `CallDeferred()` for Godot API calls from background threads
- Queue background work via `ThreadPooler.Instance?.EnqueuePackage(package)`
- Build work packages with `WorkPackageBuilder`: name, steps, priority, batch ID

### Testing with gdUnit4

```csharp
using GdUnit4;
using static GdUnit4.Assertions;
namespace Tests;

[TestSuite]
public class MyTest
{
    [TestCase]
    public void PureUnitTest()  // Runs without Godot
    {
        AssertThat(actualValue).IsEqual(expectedValue);
    }

    [TestCase]
    [RequireGodotRuntime]  // Needs GODOT_BIN env var
    public void GodotDependentTest()
    {
        var node = new Node3D();
        AssertThat(node).IsNotNull();
    }
}
```

Test files live in `Tests/` mirroring the `Scripts/` folder structure. Test lookup folder is configured as `Tests` in `project.godot`.

## Autoload Singletons

| Singleton | Path | Purpose |
|-----------|------|---------|
| `RuntimeSettings` | `Scripts/UtilityLibrary/Settings/RuntimeSettings.cs` | Settings management with persistence |
| `SignalBus` | `Scripts/UtilityLibrary/SignalBus.cs` | Global signal/event dispatcher |
| `ThreadPooler` | `Scripts/UtilityLibrary/TaskSystem/ThreadPooler.cs` | Background task execution |
| `TaskTimer` | `Scripts/UtilityLibrary/TaskTimer.cs` | Progress tracking and timing |
| `ResourceDatabase` | `Scripts/Structures/Resources/ResourceDatabase.cs` | Resource definitions storage |
| `CellSelectionManager` | `Scripts/PlayerInteraction/CellSelection/CellSelectionManager.cs` | Cell selection handling |
| `DebugMenu` | `UI/Debug/DebugMenu.tscn` | Debug console (scene-based) |

Access via `Instance`: `SignalBus.Instance?.EmitStartTimer(...)`, `ThreadPooler.Instance?.EnqueuePackage(...)`

## Key Architecture Patterns

- **IConfigurable**: Objects expose settings via `IConfigurable` and register with `RuntimeSettings`
- **Builder Pattern**: `CelestialBody.Builder.BuildFromBodyDict()` constructs bodies
- **WorkPackageBuilder**: Fluent API for building background task pipelines
- **Strategy Pattern**: `BodyGenerationType` enum selects generation pipeline
- **Two-Pass Generation**: Base mesh first, then Voronoi/tectonics overlay
- **YAML Configuration**: `TemplateLoader.Load()` + `TemplateHelpers` for body templates

## Configuration System

YAML files via YamlDotNet, loaded with validation:
```csharp
var raw = TemplateLoader.Load("RockyPlanet", TemplateLoader.CelestialBodyValidator);
var defaults = TemplateHelpers.GetCelestialBodyDefaults(CelestialBodyType.RockyPlanet);
```

## OpenProject Integration

Project name: **Startreprenuer**. Hierarchy: EPIC > Feature > Task. All tasks/features must be placed under an existing EPIC. **All OpenProject API requests must be separated by a 1-second delay** to avoid rate limiting.

