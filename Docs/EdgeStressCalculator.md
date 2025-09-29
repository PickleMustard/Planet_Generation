# EdgeStressCalculator Documentation

## Overview

The `EdgeStressCalculator` class provides static methods for calculating and propagating stress on mesh edges in a planetary generation system. This class handles boundary stress between continents, stress propagation through connected edges, and spring-based stress calculations for interior edges.

## Class: EdgeStressCalculator

```csharp
namespace MeshGeneration
{
    /// <summary>
    /// Provides static methods for calculating and propagating stress on mesh edges
    /// in a planetary generation system. This class handles boundary stress between
    /// continents, stress propagation through connected edges, and spring-based
    /// stress calculations for interior edges.
    /// </summary>
    public static class EdgeStressCalculator
    {
        // Methods documented below
    }
}
```

## Methods

### CalculateBoundaryStress

```csharp
public static float CalculateBoundaryStress(Edge edge, VoronoiCell cell1, VoronoiCell cell2, Dictionary<int, Continent> continents)
```

**Purpose:** Calculates stress on boundary edges between continents based on relative movement.

**Parameters:**
- `edge` - The edge to calculate stress for, representing the boundary between cells
- `cell1` - First Voronoi cell sharing this edge, belonging to one continent
- `cell2` - Second Voronoi cell sharing this edge, belonging to another continent
- `continents` - Dictionary mapping continent indices to Continent objects containing movement data

**Returns:** A float value representing the calculated stress magnitude. Returns 0 if cells belong to the same continent.

**Remarks:**
The stress calculation considers:
- Relative movement vectors between continents
- Distance between cell centers
- Boundary type (convergent, divergent, or transform) with appropriate modifiers

Boundary types and their stress modifiers:
- Convergent: 1.5x multiplier (higher stress)
- Divergent: 1.2x multiplier (moderate stress)
- Transform: 0.8x multiplier (lower stress)

---

### PropagateStress

```csharp
public static Dictionary<Edge, float> PropagateStress(Edge sourceEdge, List<VoronoiCell> voronoiCells, StructureDatabase db, float decayFactor = 0.7f)
```

**Purpose:** Propagates stress from a source edge to connected edges with exponential decay.

**Parameters:**
- `sourceEdge` - The edge to propagate stress from, containing the initial stress magnitude
- `voronoiCells` - List of all Voronoi cells in the mesh (currently unused but kept for API compatibility)
- `db` - StructureDatabase containing edge-to-cell mappings for connectivity information
- `decayFactor` - Factor for exponential decay (0.0 to 1.0). Lower values cause faster decay. Default: 0.7f

**Returns:** Dictionary mapping connected edges to their propagated stress values. Returns empty dictionary if source edge has no associated cells.

**Remarks:**
The propagation algorithm:
1. Finds all Voronoi cells that share the source edge
2. For each cell, examines all connected edges
3. Calculates distance between edge midpoints
4. Applies exponential decay: stress * (decayFactor ^ distance)
5. Keeps maximum stress value if multiple paths reach the same edge

This creates a realistic stress distribution pattern where nearby edges receive more stress than distant ones.

---

### CalculateSpringStress

```csharp
public static float CalculateSpringStress(Edge edge, VoronoiCell cell, float restLength = -1f)
```

**Purpose:** Calculates stress on interior edges using a spring model based on deformation.

**Parameters:**
- `edge` - The edge to calculate stress for, representing a spring element
- `cell` - The Voronoi cell containing this edge, used for calculating average edge lengths
- `restLength` - The rest length of the edge (optional). If negative or zero, calculates average edge length from the cell.

**Returns:** A float value representing the spring-based stress. Higher values indicate greater deformation.

**Remarks:**
The spring model uses Hooke's law: F = -k * (x - x0)
Where:
- k is the spring constant (currently 0.5)
- x is the current edge length
- x0 is the rest length

If no rest length is provided, the method estimates it by calculating the average length of all edges in the containing Voronoi cell. This provides a reasonable baseline for natural edge lengths within the local mesh structure.

---

### DetermineBoundaryType (Private)

```csharp
private static Continent.BOUNDARY_TYPE DetermineBoundaryType(Continent continent1, Continent continent2)
```

**Purpose:** Determines the boundary type between two continents based on their movement directions.

**Parameters:**
- `continent1` - First continent with movementDirection property
- `continent2` - Second continent with movementDirection property

**Returns:** A BOUNDARY_TYPE enum value indicating the type of tectonic boundary

**Remarks:**
Boundary classification logic:
- Transform boundary: dot product > 0.5 (continents moving in similar directions)
- Divergent boundary: dot product < -0.5 (continents moving towards each other)
- Convergent boundary: all other cases (continents moving apart or perpendicular)

This classification helps determine appropriate stress modifiers in the CalculateBoundaryStress method, simulating real-world tectonic interactions.

## Usage Examples

### Calculating Boundary Stress

```csharp
// Get edge and adjacent cells
Edge boundaryEdge = GetBoundaryEdge();
VoronoiCell cell1 = GetAdjacentCell1();
VoronoiCell cell2 = GetAdjacentCell2();
Dictionary<int, Continent> continents = GetAllContinents();

// Calculate stress
float stress = EdgeStressCalculator.CalculateBoundaryStress(boundaryEdge, cell1, cell2, continents);
```

### Propagating Stress

```csharp
// Get high-stress edge and database
Edge sourceEdge = GetHighStressEdge();
List<VoronoiCell> allCells = GetAllVoronoiCells();
StructureDatabase db = GetStructureDatabase();

// Propagate stress with default decay
Dictionary<Edge, float> stressMap = EdgeStressCalculator.PropagateStress(sourceEdge, allCells, db);

// Propagate with custom decay factor
Dictionary<Edge, float> customStressMap = EdgeStressCalculator.PropagateStress(sourceEdge, allCells, db, 0.5f);
```

### Calculating Spring Stress

```csharp
// Get edge and containing cell
Edge interiorEdge = GetInteriorEdge();
VoronoiCell containingCell = GetContainingCell();

// Calculate with automatic rest length
float stress1 = EdgeStressCalculator.CalculateSpringStress(interiorEdge, containingCell);

// Calculate with specified rest length
float stress2 = EdgeStressCalculator.CalculateSpringStress(interiorEdge, containingCell, 1.5f);
```

## Dependencies

- `System` - For basic collections and types
- `System.Collections.Generic` - For Dictionary and List collections
- `Godot` - For Vector2, Vector3, and Mathf utilities
- `Structures` - For Edge, VoronoiCell, and Continent types
- `MeshGeneration.StructureDatabase` - For database access in stress propagation

## See Also

- `Edge` class in Structures namespace
- `VoronoiCell` class in Structures namespace
- `Continent` class in Structures namespace
- `StructureDatabase` class in MeshGeneration namespace