# Agent Configuration for Delaunay Triangulation Map Generation

Godot 4.6 / C# (.NET 9.0, LangVersion 12) project for procedural celestial body and planetary system generation via spherical Delaunay triangulation, tectonic simulation, and Voronoi cell partitioning.

## Build/Lint/Test Commands

```bash
# Build
dotnet build

# Clean + rebuild
dotnet clean && dotnet build

# Run tests via gdUnit4 CLI runner (CI pipeline)
# See https://godot-gdunit-labs.github.io/gdUnit4/latest/advanced_testing/cmd/
./addons/gdUnit4/runtest.sh --godot_binary "/usr/bin/godot-limbo" -a "res://Tests" -c -rd "./test-reports"

# Run specific tests via gdUnit4 CLI runner (CI pipeline)
./addons/gdUnit4/runtest.sh --godot_binary "/usr/bin/godot-limbo" -a "res://Tests/{test-suite-name} -c -rd "./test-reports"

# Run tests using GdUnitRunner.cfg (contains references to specific test suites)
./addons/gdUnit4/runtest.sh --godot_binary "/usr/bin/godot-limbo" -conf GdUnitRunner.cfg -c -rd "./test-reports"

Tests output reports to `./test-reports/` by default.

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

## Resource Configuration System

Resources are defined in category-based YAML files located in `Configuration/ResourceDefinition/categories/`.

### File Structure

```
Configuration/ResourceDefinition/categories/
├── ore.yaml              # All ore resources (resource_type inferred from filename)
├── raw_material.yaml     # All raw material resources
├── fuel.yaml            # All fuel resources
├── food.yaml            # All food resources
├── electronic.yaml      # All electronic resources
├── industrial.yaml      # All industrial resources
└── construction.yaml    # All construction resources
```

### File Format

Each category file contains a `resources` list. Resource type is inferred from the filename (e.g., `ore.yaml` → `resource_type: "ore"`).

```yaml
# File: categories/ore.yaml
resources:
  - id_name: iron_ore
    resource_tier: 0
    display_color: [139, 69, 19]  # Optional - defaults to white
    biome_affinity:                # Optional - defaults to empty dict
      Mountain: 2.0
      StoneDesert: 1.5
    elevation_range: [0.5, 1.0]   # Optional - defaults to [0.0, 1.0]
  
  - id_name: uranium_ore
    resource_tier: 1
    # No generation fields = not generatable (used only in recipes/buildings)
```

### Key Concepts

1. **Category Inference**: Resource type is automatically set based on filename. Do NOT include `resource_type` field in YAML.

2. **Generatable Resources**: A resource is considered "generatable" (can spawn on celestial bodies) if it defines ANY of:
   - `display_color` (not default white)
   - `biome_affinity` (non-empty)
   - `elevation_range` (not default [0.0, 1.0])

3. **Default Values**:
   - `display_color`: `Colors.White` (RGB: 255, 255, 255)
   - `biome_affinity`: Empty dictionary `{}`
   - `elevation_range`: `[0.0, 1.0]`

4. **Resource Types**:
   - `ore`: Naturally occurring deposits (iron_ore, copper_ore, etc.)
   - `raw_material`: Processed materials (iron, copper, steel, etc.)
   - `fuel`: Energy sources (water, hydrogen, antimatter, etc.)
   - `food`: Agricultural products (grain, vegetable, protein, etc.)
   - `electronic`: Electronics components (pcb, microchip, quantum_chip, etc.)
   - `industrial`: Industrial materials (ceramics, silicon, etc.)
   - `construction`: Construction materials (concrete, clay, etc.)

### Loading Resources

Resources are loaded via `ResourceDatabase.Instance.LoadData()` which calls `ResourceConfigLoader.LoadResourceDefinitionsFromCategories()`.

### Adding New Resources

1. Determine the appropriate category file based on resource type
2. Add the resource definition to the corresponding YAML file
3. Ensure `id_name` is unique across all categories
4. Include generation fields only if the resource should spawn on celestial bodies

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
