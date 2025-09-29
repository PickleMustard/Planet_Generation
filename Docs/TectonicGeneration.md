# TectonicGeneration Class Documentation

## Overview

The `TectonicGeneration` class is responsible for simulating tectonic plate interactions and calculating stress distributions across planetary terrain. It implements realistic geological processes including plate boundary classification, stress propagation, and terrain deformation.

## Class Structure

```csharp
public class TectonicGeneration
```

### Purpose

This class simulates the interaction between continental plates, calculates boundary stresses, and applies the resulting deformations to create realistic terrain features such as mountains, valleys, trenches, and rift zones.

## Fields

### Private Fields

| Field | Type | Description |
|-------|------|-------------|
| `StrDb` | `StructureDatabase` | Database containing all structural data for the mesh including vertices, edges, and cells |
| `rand` | `RandomNumberGenerator` | Random number generator for procedural generation and random sampling |
| `StressScale` | `float` | Scaling factor for compression stress calculations |
| `ShearScale` | `float` | Scaling factor for shear stress calculations |
| `MaxPropagationDistance` | `float` | Maximum distance that stress can propagate from its source edge |
| `PropagationFalloff` | `float` | Rate at which stress magnitude decreases with distance from source |
| `InactiveStressThreshold` | `float` | Threshold below which stress is considered inactive and doesn't affect terrain |
| `GeneralHeightScale` | `float` | General scaling factor for height modifications due to inactive stress |
| `GeneralShearScale` | `float` | Scaling factor for height modifications due to shear stress |
| `GeneralCompressionScale` | `float` | Scaling factor for height modifications due to compression stress |

## Constructors

### TectonicGeneration

```csharp
public TectonicGeneration(
    StructureDatabase strDb,
    RandomNumberGenerator rng,
    float stressScale,
    float shearScale,
    float maxPropagationDistance,
    float propagationFalloff,
    float inactiveStressThreshold,
    float generalHeightScale,
    float generalShearScale,
    float generalCompressionScale)
```

**Description:** Initializes a new instance of the TectonicGeneration class with specified parameters.

**Parameters:**
- `strDb` - Structure database containing mesh data
- `rng` - Random number generator for procedural generation
- `stressScale` - Scaling factor for compression stress calculations
- `shearScale` - Scaling factor for shear stress calculations
- `maxPropagationDistance` - Maximum distance stress can propagate from source
- `propagationFalloff` - Rate at which stress decreases with distance
- `inactiveStressThreshold` - Threshold below which stress is considered inactive
- `generalHeightScale` - Scaling factor for height modifications from inactive stress
- `generalShearScale` - Scaling factor for height modifications from shear stress
- `generalCompressionScale` - Scaling factor for height modifications from compression stress

## Methods

### Public Methods

#### CalculateBoundaryStress

```csharp
public void CalculateBoundaryStress(
    IReadOnlyDictionary<Edge, HashSet<VoronoiCell>> edgeMap,
    HashSet<Point> points,
    Dictionary<int, Continent> continents,
    GenericPercent percent)
```

**Description:** Calculates stress at boundaries between continental plates and propagates stress throughout the mesh. This method analyzes the interaction between neighboring continents, calculates compression and shear stresses at their boundaries, and propagates these stresses through the mesh structure.

**Parameters:**
- `edgeMap` - Dictionary mapping edges to their adjacent Voronoi cells
- `points` - Collection of all points in the mesh
- `continents` - Dictionary of continents with their movement properties
- `percent` - Progress tracking object for reporting completion status

**Remarks:** The method performs the following steps:
1. For each continent, calculates local coordinate system based on random point pairs
2. For each boundary cell, analyzes edges that border different continents
3. Calculates compression and shear stress based on relative plate movement
4. Classifies boundary type (convergent, divergent, transform, or inactive)
5. Propagates stress from boundary edges to surrounding mesh using priority queue

#### ApplyStressToTerrain

```csharp
public void ApplyStressToTerrain(Dictionary<int, Continent> continents, List<VoronoiCell> cells)
```

**Description:** Applies calculated tectonic stresses to terrain vertices, modifying their heights. This method processes all vertices in the mesh and adjusts their heights based on the stress values of their incident edges, creating realistic terrain features like mountains, valleys, and trenches.

**Parameters:**
- `continents` - Dictionary of continents (not directly used but kept for interface consistency)
- `cells` - List of Voronoi cells (not directly used but kept for interface consistency)

**Remarks:** The method applies different height modifications based on edge types:
- **Inactive edges**: General height scaling based on stress magnitude
- **Transform edges**: Height modification based on shear stress (creates strike-slip features)
- **Divergent edges**: Height reduction based on compression stress (creates rifts/trenches)
- **Convergent edges**: Height increase based on compression stress (creates mountains)

### Private Methods

#### ClassifyBoundaryType

```csharp
private EdgeType ClassifyBoundaryType(EdgeStress es)
```

**Description:** Classifies the type of tectonic boundary based on calculated stress values. This method analyzes compression and shear stress components to determine whether a boundary is convergent, divergent, transform, or inactive.

**Parameters:**
- `es` - EdgeStress object containing compression and shear stress values

**Returns:** `EdgeType` enum value indicating the classification of the boundary

**Remarks:** Classification logic:
- If total stress is below threshold: inactive
- If compression factor > 56%: convergent (positive compression) or divergent (negative compression)
- If shear factor > 70%: transform boundary
- Otherwise: classify based on dominant stress type

#### CalculateStressAtDistance

```csharp
private float CalculateStressAtDistance(EdgeStress edgeStress, float distance, Edge current, Edge origin)
```

**Description:** Calculates the stress magnitude at a given distance from the source edge. This method models how tectonic stress propagates through the crust with exponential decay and directional attenuation.

**Parameters:**
- `edgeStress` - The original stress values at the source edge
- `distance` - Distance from the source edge to the current edge
- `current` - The edge receiving the propagated stress
- `origin` - The source edge from which stress originates

**Returns:** Calculated stress magnitude at the current edge location

**Remarks:** The calculation considers:
- Exponential decay based on distance and propagation falloff rate
- Combined compression and shear stress (with shear weighted at 50%)
- Directional factor based on alignment with stress direction

## Usage Example

```csharp
// Initialize tectonic generation
var tectonicGen = new TectonicGeneration(
    structureDatabase,
    randomGenerator,
    stressScale: 1.0f,
    shearScale: 0.8f,
    maxPropagationDistance: 10.0f,
    propagationFalloff: 5.0f,
    inactiveStressThreshold: 0.1f,
    generalHeightScale: 0.5f,
    generalShearScale: 0.3f,
    generalCompressionScale: 0.7f
);

// Calculate boundary stresses
tectonicGen.CalculateBoundaryStress(edgeMap, points, continents, progressTracker);

// Apply stresses to terrain
tectonicGen.ApplyStressToTerrain(continents, voronoiCells);
```

## Key Features

1. **Realistic Plate Tectonics**: Simulates convergent, divergent, and transform boundaries
2. **Stress Propagation**: Models how stress spreads through the crust with distance-based decay
3. **Terrain Deformation**: Creates realistic geological features based on stress patterns
4. **Configurable Parameters**: Allows fine-tuning of tectonic behavior through scaling factors
5. **Progress Tracking**: Supports progress reporting for long-running calculations

## Dependencies

- `Godot` - Core game engine framework
- `Structures` - Custom data structures for mesh representation
- `UtilityLibrary` - Utility functions and helpers
- `MeshGeneration` - Mesh generation related classes

## Edge Types

The system classifies tectonic boundaries into four types:

- **Convergent**: Plates moving toward each other, creating mountains
- **Divergent**: Plates moving apart, creating rifts and trenches
- **Transform**: Plates sliding past each other, creating fault lines
- **Inactive**: Boundaries with insufficient stress to affect terrain

Each type produces different terrain features and stress propagation patterns.