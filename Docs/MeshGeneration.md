# Mesh Generation System

## Overview

The Mesh Generation system creates the spherical geometry for celestial bodies using spherical Delaunay triangulation, constrained triangulation for continents, and Voronoi cell partitioning. It supports multiple vertex distribution strategies (geometric, linear), configurable subdivision levels, tectonic plate simulation for continent formation, and spherical harmonics deformation for surface detail. The end result is a `UnifiedCelestialMesh` that combines base geometry, biome data, and color mapping into a single renderable mesh.

## Key Classes

### UnifiedCelestialMesh
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/UnifiedCelestialMesh.cs`
- **Purpose**: The final combined mesh for a celestial body. Integrates base mesh geometry, continent data, biome colors, and resource visualization into a single `ArrayMesh`.
- **Key Responsibilities**:
  - Merges base mesh with continent and Voronoi overlays.
  - Applies color mapping from `IColorMapper` implementations.
  - Provides mesh data to the `CelestialBody` for rendering.

### BaseMeshGeneration
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/BaseMeshGeneration.cs`
- **Purpose**: Generates the initial spherical mesh using an icosahedron or other platonic solid as a starting point, then subdivides it to the desired resolution.
- **Key Responsibilities**:
  - Creates the base topology (vertices, edges, faces).
  - Delegates vertex placement to `IVertexGenerator` implementations.

### SphericalDelaunayTriangulation
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/SphericalDelaunayTriangulation.cs`
- **Purpose**: Computes a Delaunay triangulation on the surface of a sphere. This ensures that no vertex lies inside the circumcircle of any triangle, producing well-formed meshes.
- **Key Responsibilities**:
  - Uses `HalfEdge` data structures for efficient topology traversal.
  - Supports incremental insertion of vertices.

### ConstrainedDelauneyTriangulation
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/ConstrainedDelauneyTriangulation.cs`
- **Purpose**: Extends Delaunay triangulation to respect boundary edges (e.g., continent coastlines). Ensures that constrained edges appear in the final triangulation even if they violate the Delaunay criterion.
- **Key Responsibilities**:
  - Preserves continent boundary edges during mesh refinement.

### SphericalTriangulation
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/SphericalTriangulation.cs`
- **Purpose**: General spherical triangulation utilities and helpers used by both Delaunay and constrained variants.

### VoronoiCellGeneration
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/VoronoiCellGeneration.cs`
- **Purpose**: Generates Voronoi cells from the triangulated mesh. Each cell corresponds to a region of the surface closest to a particular vertex. These cells are the primary units of surface interaction (selection, resource placement, building placement).
- **Key Responsibilities**:
  - Computes cell boundaries, neighbors, and centroids.
  - Associates cells with continents.

### TectonicGeneration
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/TectonicGeneration.cs`
- **Purpose**: Simulates tectonic plate movement to generate continents and elevation data. Uses stress calculations along mesh edges to create realistic landmass distributions.
- **Key Responsibilities**:
  - Generates continent shapes and elevations.
  - Drives the `Continent` data structures stored on `CelestialBody`.

### TectonicGenerationHelpers
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/TectonicGenerationHelpers.cs`
- **Purpose**: Utility functions and data structures supporting the tectonic simulation.

### EdgeStressCalculator
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/EdgeStressCalculator.cs`
- **Purpose**: Calculates compressional and tensional stress along mesh edges. Used by `TectonicGeneration` to determine where mountains, rifts, and coastlines form.

### ConfigurableSubdivider
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/ConfigurableSubdivider.cs`
- **Purpose**: Controls how many times the base mesh is subdivided. Higher subdivision yields more vertices and finer detail at the cost of performance.

### IVertexGenerator / GeometricVertexGenerator / LinearVertexGenerator
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/IVertexGenerator.cs`, `GeometricVertexGenerator.cs`, `LinearVertexGenerator.cs`
- **Purpose**: Strategies for placing vertices during subdivision.
  - `GeometricVertexGenerator`: Places new vertices using geometric interpolation.
  - `LinearVertexGenerator`: Uses linear interpolation for faster, less uniform distribution.

### SphericalHarmonicsDeformer
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/SphericalHarmonicsDeformer.cs`
- **Purpose**: Deforms the spherical mesh using spherical harmonics to create non-uniform surface detail (craters, bumps) without increasing vertex count.

### BiomeAssigner / BiomeAssignerFactory / RockyPlanetBiomeAssigners
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/BiomeAssigner.cs`, `Biome/BiomeAssignerFactory.cs`, `Biome/RockyPlanetBiomeAssigners.cs`
- **Purpose**: Assigns biome types (e.g., Ocean, Mountain, Desert) to mesh regions based on elevation, latitude, and other factors. The factory selects the appropriate assigner for the body type.

### ColorMapperFactory / RockyPlanetColorMappers
- **Location**: `Scripts/ProceduralGeneration/Color/ColorMapperFactory.cs`, `Color/RockyPlanetColorMappers.cs`
- **Purpose**: Maps biomes to colors for mesh vertex coloring. The factory provides color mappers per body type.

### StructureDatabase / StructureDatabaseDebug
- **Location**: `Scripts/ProceduralGeneration/MeshGeneration/StructureDatabase.cs`, `StructureDatabaseDebug.cs`
- **Purpose**: Stores and queries mesh topology structures (edges, faces, half-edges) for a given body.

## Related Documentation
- [Procedural Generation](ProceduralGeneration.md)
- [Resource System](ResourceSystem.md)
