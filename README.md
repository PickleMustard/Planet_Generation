# Delaunay Triangulation Map Generation

![Godot](https://img.shields.io/badge/Godot-4.6-%23478cbf?logo=godot-engine&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9.0-512bd4?logo=dotnet&logoColor=white)
![GDUnit4](https://img.shields.io/badge/GDUnit4-v5.1-blue)
![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green)

A Godot 4.6 project using C# (.NET 9.0) for procedural celestial body and planetary system generation via spherical Delaunay triangulation, tectonic simulation, and Voronoi cell partitioning.

## Table of Contents

- [Features](#features)
- [CI/CD](#cicd)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Running Tests](#running-tests)
- [Project Structure](#project-structure)
- [Development](#development)
- [License](#license)

## Features

- **Spherical Delaunay Triangulation** - Generate spherical meshes from icosahedron subdivision with configurable vertex distribution
- **Tectonic Plate Simulation** - Realistic tectonic movement and collision modeling with edge stress calculations
- **Voronoi Cell Partitioning** - Divide surface into discrete cells for gameplay and resource management
- **Biome Distribution** - Temperature, moisture, and elevation-based biome assignment
- **Resource Deposit Generation** - Procedural resource placement with biome affinity
- **Celestial Body Generation** - Stars, planets, moons, asteroids, and satellite belts
- **Orbital Mechanics** - Lambert solver for interplanetary transfers, orbital configuration
- **Logistics System** - Ships, stations, trajectory planning, and cargo management with engine modifiers

## CI/CD

This project uses **Jenkins** for continuous integration with automated test execution via **GDUnit4**.

### Pipeline Status

| Status | Description |
|--------|-------------|
| ![Build](https://img.shields.io/badge/Build-Jenkins-blue) | Overall build status |
| ![Tests](https://img.shields.io/badge/Tests-GDUnit4-green) | Automated test suite |

### Pipeline Stages

1. **Checkout** - Clone repository and pull LFS assets
2. **Setup Environment** - Prepare build tools
3. **Download Godot** - Fetch Godot 4.6 .NET edition
4. **Restore Dependencies** - Restore NuGet packages
5. **Build** - Compile C# project in Release mode
6. **Run Tests** - Execute GDUnit4 test suite
7. **Archive Reports** - Save JUnit XML and HTML reports

### Test Reports

After each build, test reports are available:

- **JUnit XML:** `test-reports/**/results.xml` - For CI/CD integration
- **HTML Report:** `test-reports/index.html` - Detailed failure analysis

### Exit Codes

| Code | Meaning | Jenkins Behavior |
|------|---------|------------------|
| `0` | All tests passed | ✅ Build success |
| `100` | Tests have failures | ❌ Build failure |
| `101` | Tests with warnings | ⚠️ Build unstable |

## Requirements

| Dependency | Version | Notes |
|------------|---------|-------|
| Godot | 4.6+ | .NET/Mono edition required |
| .NET SDK | 9.0 | For C# compilation |
| GDUnit4 | 5.1+ | Included as addon |

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/YOUR_USERNAME/Planet_Generation.git
cd Planet_Generation
```

### 2. Install Godot

Download Godot 4.6 .NET edition from [godotengine.org](https://godotengine.org/download/).

### 3. Open in Godot

```bash
# Launch Godot and open the project
godot4 --path .
```

### 4. Build C# Project

```bash
dotnet build
```

## Running Tests

For full details on the GDUnit4 command line tool, see the [official documentation](https://godot-gdunit-labs.github.io/gdUnit4/latest/advanced_testing/cmd/).

### Using Command Line

```bash
# Set Godot binary path
export GODOT_BIN=/usr/bin/godot-limbo

# Run all tests via dotnet CLI (pure C# tests only without GODOT_BIN)
dotnet test

# Run a specific test by exact name
dotnet test --filter "Name=DepositCreation"

# Run with GDUnit4 CLI runner (recommended for CI)
./addons/gdUnit4/runtest.sh --godot_binary "$GODOT_BIN" -a "res://Tests" -c -rd "./test-reports"

# Run tests using GdUnitRunner.cfg (contains references to specific test suites)
./addons/gdUnit4/runtest.sh --godot_binary "$GODOT_BIN" -conf GdUnitRunner.cfg -c -rd "./test-reports"
```

### Using Godot Editor

1. Open the project in Godot
2. Navigate to **GDUnit4** dock (right panel)
3. Click **Discover Tests**
4. Select tests to run
5. Click **Run**

### View Test Reports

After running tests:

```bash
# Open HTML report in browser
xdg-open test-reports/index.html  # Linux
open test-reports/index.html      # macOS
start test-reports/index.html     # Windows
```

## Project Structure

```
Planet_Generation/
├── Scripts/
│   ├── ProceduralGeneration/      # Core generation algorithms
│   │   ├── MeshGeneration/        # Delaunay, Voronoi, tectonics
│   │   │   ├── ResourceGeneration/# Resource deposit placement
│   │   │   ├── SphericalDelaunayTriangulation.cs
│   │   │   ├── TectonicGeneration.cs
│   │   │   ├── VoronoiCellGeneration.cs
│   │   │   └── BiomeAssigner.cs
│   │   ├── CelestialBody.cs       # Base celestial body class
│   │   ├── SatelliteBody.cs      # Satellite/moon generation
│   │   ├── SatelliteBeltBody.cs  # Asteroid belt generation
│   │   └── SystemGenerator.cs    # System-wide generation
│   ├── Structures/
│   │   ├── MeshGeneration/        # Point, Edge, Triangle, Face
│   │   ├── Enums/                 # Biome, CelestialBodyType
│   │   ├── GameState/              # VoronoiCell, Continent, Orbit
│   │   ├── Resources/              # ResourceDefinition, Deposit
│   │   └── Logistics/             # Engine, Trajectory, Cargo
│   ├── Constructables/
│   │   └── ArtificialSatellites/  # Ships, stations
│   ├── UtilityLibrary/            # Logger, ThreadPool, Settings
│   └── PlayerInteraction/          # Input, cell selection
├── Tests/                         # GDUnit4 test suites
│   ├── Settings/                  # RuntimeSettings tests
│   ├── ResourceGeneration/        # Resource system tests
│   ├── UtilityLibrary/            # ThreadPool, Lambert solver
│   └── ThreadPoolTest.cs
├── Configuration/
│   ├── SystemGen/                 # Body type definitions (YAML)
│   ├── SystemTemplate/            # Pre-built system templates
│   ├── ResourceDefinition/        # Resource configs
│   ├── engines/                   # Engine type definitions
│   └── ships/                     # Ship templates
├── UI/                            # User interface scenes
├── addons/                        # Godot plugins
│   └── gdUnit4/                   # Testing framework
├── docs/                          # Documentation
│   └── LogisticsSystem.md         # Logistics system deep dive
├── Jenkinsfile                    # CI/CD pipeline
├── AGENTS.md                      # Development guidelines
└── README.md                      # This file
```

## Development

### Code Style

See [AGENTS.md](./AGENTS.md) for detailed development guidelines including:

- Naming conventions (PascalCase, camelCase, etc.)
- Import organization
- Godot integration patterns
- Testing with GDUnit4
- Error handling and logging

### Key Patterns

| Pattern | Usage |
|---------|-------|
| Builder | `CelestialBody.Builder.BuildFromBodyDict()` |
| Strategy | `BodyGenerationType` enum selects pipeline |
| Two-Pass Generation | Base mesh → Voronoi/tectonics |
| IConfigurable | Expose settings to `RuntimeSettings` |

### Autoload Singletons

| Singleton | Purpose |
|-----------|---------|
| `RuntimeSettings` | Centralized settings management |
| `SignalBus` | Global signal/event dispatcher |
| `ThreadPooler` | Background task execution |
| `TaskTimer` | Progress tracking |
| `ResourceDatabase` | Resource definitions storage |
| `CellSelectionManager` | Cell selection handling |

### Core Systems

- **Mesh Generation Pipeline**: ConfigurableSubdivider → SphericalDelaunayTriangulation → TectonicGeneration → VoronoiCellGeneration → BiomeAssigner
- **Orbital Mechanics**: LambertSolver for transfer calculations, OrbitalMath for Keplerian orbits
- **Logistics**: EngineDefinition with modifiers, TrajectoryPlanner for pathfinding, CargoManifest for resource tracking ([Detailed Docs](./docs/LogisticsSystem.md))

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Run tests (`dotnet test`)
5. Commit changes (`git commit -m 'Add amazing feature'`)
6. Push to branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Godot Engine](https://godotengine.org/) - Game engine
- [GDUnit4](https://github.com/godot-gdunit-labs/gdUnit4) - Testing framework
- [YamlDotNet](https://github.com/aaubry/YamlDotNet) - YAML parsing
