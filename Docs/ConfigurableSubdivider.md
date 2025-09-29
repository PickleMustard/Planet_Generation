# ConfigurableSubdivider Documentation

## Overview

The `ConfigurableSubdivider` class provides flexible mesh subdivision capabilities for procedural mesh generation. It can subdivide triangular faces using different vertex distribution strategies, making it suitable for various applications in planet generation and mesh refinement.

## Class Structure

### ConfigurableSubdivider

```csharp
namespace MeshGeneration;

/// <summary>
/// A configurable mesh subdivider that can subdivide triangular faces using different vertex distribution strategies.
/// This class provides flexible subdivision capabilities for procedural mesh generation, supporting various
/// vertex distribution patterns including linear, geometric, and custom distributions.
/// </summary>
public class ConfigurableSubdivider
```

#### Fields

- **StrDb** (`StructureDatabase`): The structure database used for managing points, edges, and faces during subdivision.
- **_generators** (`Dictionary<VertexDistribution, IVertexGenerator>`): Dictionary mapping vertex distribution types to their corresponding vertex generators.

#### Constructor

```csharp
/// <summary>
/// Initializes a new instance of the ConfigurableSubdivider class.
/// </summary>
/// <param name="db">The structure database to use for managing mesh data during subdivision.</param>
public ConfigurableSubdivider(StructureDatabase db)
```

#### Public Methods

##### SubdivideFace

```csharp
/// <summary>
/// Subdivides a triangular face into smaller faces using the specified vertex distribution strategy.
/// </summary>
/// <param name="face">The triangular face to subdivide.</param>
/// <param name="verticesToGenerate">The number of vertices to generate along each edge of the face.</param>
/// <param name="distribution">The vertex distribution strategy to use (default: Linear).</param>
/// <returns>An array of new faces created from the subdivision process.</returns>
/// <remarks>
/// This method generates vertices along each edge of the face and creates interior points,
/// then constructs new triangular faces using barycentric subdivision. The number of 
/// resulting faces depends on the verticesToGenerate parameter.
/// </remarks>
public Face[] SubdivideFace(Face face, int verticesToGenerate, VertexDistribution distribution = VertexDistribution.Linear)
```

#### Private Methods

##### GenerateInteriorPoints

```csharp
/// <summary>
/// Generates interior points within a triangular face using barycentric coordinates.
/// </summary>
/// <param name="face">The triangular face in which to generate interior points.</param>
/// <param name="verticesToGenerate">The number of vertices to generate along each edge.</param>
/// <returns>A list of interior points generated within the face.</returns>
/// <remarks>
/// This method uses barycentric coordinates to distribute points evenly within the triangular face.
/// Points are generated in a grid pattern based on the resolution (verticesToGenerate + 1).
/// No interior points are generated if verticesToGenerate is 2 or less.
/// </remarks>
private List<Point> GenerateInteriorPoints(Face face, int verticesToGenerate)
```

##### CalculateBarycentricPoint

```csharp
/// <summary>
/// Calculates a point using barycentric coordinates within a triangle defined by three vertices.
/// </summary>
/// <param name="a">The first vertex of the triangle.</param>
/// <param name="b">The second vertex of the triangle.</param>
/// <param name="c">The third vertex of the triangle.</param>
/// <param name="u">The barycentric coordinate weight for vertex a.</param>
/// <param name="v">The barycentric coordinate weight for vertex b.</param>
/// <param name="w">The barycentric coordinate weight for vertex c.</param>
/// <returns>A new point calculated using the barycentric coordinates.</returns>
/// <remarks>
/// Barycentric coordinates allow for interpolation within a triangle where u + v + w = 1.
/// The resulting point is a weighted average of the three triangle vertices.
/// </remarks>
private Point CalculateBarycentricPoint(Point a, Point b, Point c, float u, float v, float w)
```

##### CreateBarycentricFaces

```csharp
/// <summary>
/// Creates new triangular faces using barycentric subdivision based on edge and interior points.
/// </summary>
/// <param name="face">The original face being subdivided.</param>
/// <param name="edgePoints">Array of three lists containing points generated along each edge.</param>
/// <param name="interiorPoints">List of points generated in the interior of the face.</param>
/// <param name="verticesToGenerate">The number of vertices generated along each edge.</param>
/// <returns>An array of new faces created from the subdivision.</returns>
/// <remarks>
/// This method handles different subdivision strategies based on the verticesToGenerate parameter:
/// - For 1 vertex: Creates 4 new triangular faces
/// - For 2 vertices: Creates 9 new triangular faces with a center point
/// - For 3+ vertices: Uses CreateTriangularGrid for more complex subdivision
/// </remarks>
private Face[] CreateBarycentricFaces(Face face, List<Point>[] edgePoints, List<Point> interiorPoints, int verticesToGenerate)
```

##### CreateTriangularGrid

```csharp
/// <summary>
/// Creates a triangular grid of faces for higher resolution subdivisions (3+ vertices per edge).
/// </summary>
/// <param name="face">The original face being subdivided.</param>
/// <param name="edgePoints">Array of three lists containing points generated along each edge.</param>
/// <param name="interiorPoints">List of points generated in the interior of the face.</param>
/// <param name="verticesToGenerate">The number of vertices generated along each edge.</param>
/// <returns>A list of new faces created from the triangular grid subdivision.</returns>
/// <remarks>
/// This method uses barycentric coordinates to map all points (vertices, edge points, and interior points)
/// to a coordinate system, then creates triangular faces by connecting adjacent points in the grid.
/// The algorithm creates two triangles for each valid grid position to ensure complete coverage.
/// </remarks>
private List<Face> CreateTriangularGrid(Face face, List<Point>[] edgePoints, List<Point> interiorPoints, int verticesToGenerate)
```

##### CalculateCentroid

```csharp
/// <summary>
/// Calculates the centroid (geometric center) of a list of vertices.
/// </summary>
/// <param name="vertices">The list of vertices for which to calculate the centroid.</param>
/// <returns>A new Point representing the centroid of the input vertices.</returns>
/// <remarks>
/// The centroid is calculated as the average position of all vertices in the list.
/// This method is useful for finding the center point of a polygon or vertex group.
/// </remarks>
private Point CalculateCentroid(List<Point> vertices)
```

## Usage Examples

### Basic Subdivision

```csharp
// Initialize the subdivider with a structure database
var subdivider = new ConfigurableSubdivider(structureDatabase);

// Subdivide a face with 2 vertices per edge using linear distribution
Face[] subdividedFaces = subdivider.SubdivideFace(originalFace, 2, VertexDistribution.Linear);
```

### Different Distribution Strategies

```csharp
// Linear distribution (default)
Face[] linearFaces = subdivider.SubdivideFace(face, 3);

// Geometric distribution
Face[] geometricFaces = subdivider.SubdivideFace(face, 3, VertexDistribution.Geometric);

// Custom distribution
Face[] customFaces = subdivider.SubdivideFace(face, 3, VertexDistribution.Custom);
```

## Subdivision Behavior

The subdivision behavior varies based on the `verticesToGenerate` parameter:

- **0 vertices**: Returns the original face unchanged
- **1 vertex**: Creates 4 new triangular faces
- **2 vertices**: Creates 9 new triangular faces with a center point
- **3+ vertices**: Creates a triangular grid with increasing complexity

## Dependencies

- `System.Collections.Generic`
- `System.Linq`
- `Godot`
- `Structures` namespace
- `UtilityLibrary` namespace
- `MeshGeneration.StructureDatabase`

## Vertex Distribution Types

The class supports three vertex distribution strategies:

1. **Linear**: Evenly distributes vertices along edges
2. **Geometric**: Uses geometric progression for vertex distribution
3. **Custom**: Currently defaults to linear distribution

## Performance Considerations

- The subdivision complexity increases quadratically with the number of vertices to generate
- Higher subdivision levels (3+ vertices) use a more complex grid-based algorithm
- The method includes logging for debugging and performance monitoring
- All points and edges are managed through the StructureDatabase for consistency