# Delaunay Triangulation Map Generation

![Godot](https://img.shields.io/badge/Godot-4.6-%23478cbf?logo=godot-engine&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512bd4?logo=dotnet&logoColor=white)
![GDUnit4](https://img.shields.io/badge/GDUnit4-v6.1-blue)
![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green)

[![Jenkins Build](https://img.shields.io/jenkins/build?jobUrl=${JENKINS_URL}/job/${JOB_NAME})](JENKINS_URL_PLACEHOLDER)
[![Test Results](https://img.shields.io/jenkins/tests?jobUrl=${JENKINS_URL}/job/${JOB_NAME})](JENKINS_URL_PLACEHOLDER/lastCompletedBuild/testReport/)

A Godot 4.6 project using C# (.NET 8.0) for procedural celestial body and planetary system generation via spherical Delaunay triangulation, tectonic simulation, and Voronoi cell partitioning.

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

- **Spherical Delaunay Triangulation** - Generate spherical meshes from icosahedron subdivision
- **Tectonic Plate Simulation** - Realistic tectonic movement and collision
- **Voronoi Cell Partitioning** - Divide surface into discrete cells for gameplay
- **Biome Distribution** - Temperature, moisture, and elevation-based biome assignment
- **Resource Deposit Generation** - Procedural resource placement with biome affinity
- **Celestial Body Generation** - Stars, planets, moons, and other celestial objects

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
| .NET SDK | 8.0 | For C# compilation |
| GDUnit4 | 6.1+ | Included as addon |

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

### Using Command Line

```bash
# Set Godot binary path
export GODOT_BIN=/path/to/godot

# Run all tests
./addons/gdUnit4/runtest.sh -a res://Tests

# Run with continue-on-failure (run all tests even if some fail)
./addons/gdUnit4/runtest.sh -a res://Tests -c

# Run with custom report directory
./addons/gdUnit4/runtest.sh -a res://Tests -rd ./my-reports

# Run specific test file
./addons/gdUnit4/runtest.sh -a res://Tests/ThreadPoolTest.cs
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
xdg-open reports/index.html  # Linux
open reports/index.html      # macOS
start reports/index.html     # Windows
```

## Project Structure

```
Planet_Generation/
├── Scripts/
│   ├── ProceduralGeneration/   # Core generation algorithms
│   │   ├── MeshGeneration/     # Delaunay, Voronoi, tectonics
│   │   ├── CelestialBody.cs    # Base celestial body class
│   │   └── PlanetGeneration/   # Planet-specific generation
│   ├── Structures/
│   │   ├── MeshGeneration/     # Point, Edge, Triangle, Face
│   │   ├── Enums/              # Biome, CelestialBodyType
│   │   ├── GameState/          # VoronoiCell, Continent
│   │   └── Resources/          # ResourceDefinition, Deposit
│   ├── UtilityLibrary/         # GameLogger, ThreadPool, etc.
│   └── PlayerInteraction/      # Input handling, controls
├── Tests/                      # GDUnit4 test suites
│   ├── Settings/               # RuntimeSettings tests
│   ├── ResourceGeneration/     # Resource system tests
│   ├── ThreadPoolTest.cs
│   └── TaskSystemTest.cs
├── Configuration/
│   ├── SystemGen/              # Body type definitions (YAML)
│   ├── SystemTemplate/         # Pre-built system templates
│   └── ResourceDefinition/     # Resource configs
├── UI/                         # User interface scenes
├── addons/                     # Godot plugins
│   └── gdUnit4/               # Testing framework
├── docs/                       # Documentation
├── Jenkinsfile                 # CI/CD pipeline
├── AGENTS.md                   # Development guidelines
└── README.md                   # This file
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

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Run tests (`./addons/gdUnit4/runtest.sh -a res://Tests`)
5. Commit changes (`git commit -m 'Add amazing feature'`)
6. Push to branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Godot Engine](https://godotengine.org/) - Game engine
- [GDUnit4](https://github.com/godot-gdunit-labs/gdUnit4) - Testing framework
- [YamlDotNet](https://github.com/aaubry/YamlDotNet) - YAML parsing
