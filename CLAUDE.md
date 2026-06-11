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

# Build for release (CI)
dotnet build --configuration Release --no-restore

# Run ALL tests via gdUnit4 CLI runner
# See https://godot-gdunit-labs.github.io/gdUnit4/latest/advanced_testing/cmd/
./addons/gdUnit4/runtest.sh --godot_binary "/usr/bin/godot-limbo" -a "res://Tests" -c -rd "./test-reports"

# Run a specific test suite/folder
./addons/gdUnit4/runtest.sh --godot_binary "/usr/bin/godot-limbo" -a "res://Tests/{test-suite-name}" -c -rd "./test-reports"

# Run tests defined in GdUnitRunner.cfg (curated test-suite list)
./addons/gdUnit4/runtest.sh --godot_binary "/usr/bin/godot-limbo" -conf GdUnitRunner.cfg -c -rd "./test-reports"
```

Reports land in `./test-reports/` by default. Replace `/usr/bin/godot-limbo` with the local Godot binary path if different.

Tests marked `[RequireGodotRuntime]` need a running Godot engine — the gdUnit4 runner provides this via `--godot_binary`.

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

### GUI Authoring Convention (layout in `.tscn`, logic in `.cs`)

All NEW UI panels MUST separate layout from logic. Do NOT build Control trees in C#
(`new VBoxContainer()` + `AddChild`) for static layout.

- **Layout** lives in a `.tscn` under `UI/<area>/` (in-game) or colocated in
  `DeveloperTools/<area>/` (debug editors). The script lives under `Scripts/UI/<area>/`
  (in-game) and references nodes through `[Export]` fields. The `.tscn` root declares
  `node_paths=PackedStringArray(...)` and assigns each export a `NodePath(...)`.
  Reference: `UI/BuildingInfo/BuildingInfoWindow.tscn` + `Scripts/UI/BuildingInfo/BuildingInfoWindow.cs`.
- **Per-item cards** use a `static Create(...)` PackedScene factory (`GD.Load<PackedScene>` →
  `Instantiate<T>()` → `Initialize()`), never `new <Card>()` — a `new`'d instance has null
  `[Export]` fields. Reference: `DeveloperTools/ShipEditor/ShipCard.cs`.
- **Per-data-item rows** get their own small item `.tscn` instantiated + `Bind()`-ed in a loop.
  Shared in-game rows already exist: `UI/Components/DetailRow.tscn` (via `DetailRowBuilder`),
  `UI/Components/ResourceCostRow.tscn`, `UI/Components/LabeledFieldRow.tscn`. Reference:
  `UI/SatelliteItem.tscn`.
- **Styling** (colors, fonts, styleboxes) comes from `UI/Theme/wireframe_paper/wireframe_paper.tres`
  via `theme_type_variation` (set in the `.tscn`, or `ThemeTypeVariation` in code for dynamic
  rows). Do NOT call `AddThemeColorOverride`/`AddThemeStyleboxOverride`/`AddThemeFontSizeOverride`
  for static styling, and do NOT use `WireColors` constants for theme overrides — those are
  reserved for custom `_Draw()` / `Modulate` / `ColorRect.Color` only.

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
