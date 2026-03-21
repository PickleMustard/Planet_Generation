# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Godot 4.6 / C# (.NET 9.0, LangVersion 12) project for procedural celestial body and planetary system generation via spherical Delaunay triangulation, tectonic simulation, and Voronoi cell partitioning.

## Build / Lint / Test Commands

```bash
# Build
dotnet build

# Clean + rebuild
dotnet clean && dotnet build

# Format code
dotnet format

# Run ALL tests (requires GODOT_BIN env var for [RequireGodotRuntime] tests)
GODOT_BIN=/path/to/godot dotnet test

# Run a single test by exact method name
dotnet test --filter "Name=DepositCreation"

# Run multiple specific tests
dotnet test --filter "Name=DepositCreation|Name=ValidationErrorHandling"

# Run tests via gdUnit4 CLI runner (CI pipeline)
./addons/gdUnit4/runtest.sh --godot_binary "$GODOT_BIN" -a "res://Tests" -c -rd "./test-reports"
```

Tests marked `[RequireGodotRuntime]` need a running Godot engine. Without `GODOT_BIN`, only pure unit tests run. Use exact `Name=` matching — the `~` (contains) operator may fail for gdUnit4 without Godot.

## Architecture

### Generation Pipeline

Mesh generation flows through these sequential stages:

```
ConfigurableSubdivider (icosahedron)
  → SphericalDelaunayTriangulation (constrained Delaunay on sphere)
  → TectonicGeneration (plate movement & stress simulation)
  → VoronoiCellGeneration (surface partitioned into cells)
  → BiomeAssigner (temperature/moisture/elevation classification)
  → ResourceGeneration (deposit placement)
```

All pipeline stages live under `Scripts/ProceduralGeneration/MeshGeneration/`.

### Celestial Body Hierarchy

`CelestialBody` (Node3D, `IOrbitalBody`, `ISelectableBody`) owns a `UnifiedCelestialMesh`, `OrbitConfiguration`, `OrbitBand[]`, and a `SatellitesContainer` holding `SatelliteBody`, `SatelliteBeltBody`, and `LogisticsUnit` children.

`SystemGenerator` orchestrates creation: validates YAML config → builds dominant bodies via `CelestialBody.Builder.BuildFromBodyDict()` → attaches satellites.

### Logistics & Orbital Mechanics

- `LambertSolver.cs` — Izzo algorithm for interplanetary trajectory solutions
- `OrbitalMath.cs` — Keplerian orbit calculations
- `TrajectoryPlanner.cs` / `LogisticsMovementController.cs` — route planning and unit movement
- `TrajectorySolution.cs` / `BurnProfile.cs` — solver outputs and delta-v profiles
- `EngineDefinition.cs` + `EngineModifier.cs` — stackable engine modifiers (damage, upgrades)

### Autoload Singletons

Access all via nullable `Instance`:

| Singleton | Purpose |
|-----------|---------|
| `RuntimeSettings` | Persistent settings; objects register via `IConfigurable` |
| `SignalBus` | Global event dispatcher |
| `ThreadPooler` | Background task execution via `WorkPackage` queue |
| `TaskTimer` | Progress tracking |
| `ResourceDatabase` | All resource definitions |
| `CellSelectionManager` | Raycast-based Voronoi cell selection |
| `DebugMenu` | Scene-based debug console |

```csharp
RuntimeSettings.Instance?.GetSetting<int>("key") ?? defaultValue
SignalBus.Instance?.EmitStartTimer(...)
ThreadPooler.Instance?.EnqueuePackage(package)
```

### Configuration System

YAML files (via YamlDotNet) in `Configuration/SystemGen/` define body type templates; `Configuration/SystemTemplate/` holds pre-built system definitions. Load with:

```csharp
var raw = TemplateLoader.Load("RockyPlanet", TemplateLoader.CelestialBodyValidator);
var defaults = TemplateHelpers.GetCelestialBodyDefaults(CelestialBodyType.RockyPlanet);
```

### Threading

Background work is queued as `WorkPackage` objects built with the fluent `WorkPackageBuilder` API. Godot API calls from background threads must use `CallDeferred()`.

### Key Patterns

- **Builder Pattern** — `CelestialBody.Builder.BuildFromBodyDict()`
- **Strategy Pattern** — `BodyGenerationType` enum selects generation pipeline
- **IConfigurable** — exposes object settings to `RuntimeSettings`
- **Two-Pass Generation** — base mesh first, then Voronoi/tectonics overlay

## Code Style

| Element | Convention |
|---------|------------|
| Classes, Methods, Properties, Public fields | PascalCase |
| Private fields | `_camelCase` |
| Local variables | camelCase |
| Constants | `SCREAMING_SNAKE` or PascalCase |
| Interfaces | `IPascalCase` |
| Namespaces | PascalCase, feature-based (file-scoped preferred for enums/simple types) |

- Prefer `float` over `double` for Godot API compatibility
- Use `Vector3`, `Vector2`, `Mathf` (not `System.Math`)
- Use `GameLogger.Debug/Info/Warning/Error/Critical()` — not raw `GD.Print()`
- Nullable reference types are enabled; use null-conditional on singletons

**Import order:** System → Godot → third-party → project namespaces → `#if DEBUG` conditionals

## Testing

Test files live in `Tests/` mirroring `Scripts/` structure.

```csharp
[TestSuite]
public class MyTest
{
    [TestCase]
    public void PureUnitTest() { AssertThat(actual).IsEqual(expected); }

    [TestCase]
    [RequireGodotRuntime]
    public void GodotDependentTest() { ... }
}
```

## Documentation

Extended architecture docs are in `Docs/` (tectonic generation, Voronoi cells, logistics system, biome assignment, debug menu, runtime settings guide, system template specification).
