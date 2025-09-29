# VoronoiCellGeneration Documentation

## Overview

The `VoronoiCellGeneration` class is responsible for generating Voronoi diagrams on a spherical surface from a Delaunay triangulation. It processes site points, computes circumcenters of incident triangles, and organizes these circumcenters into Voronoi cells through triangulation.

## Class Structure

### VoronoiCellGeneration

```csharp
namespace MeshGeneration;

/// <summary>
/// Handles the generation of Voronoi cells from a spherical Delaunay triangulation.
/// This class is responsible for creating Voronoi diagrams on a sphere by computing
/// circumcenters of triangles and organizing them into cells around each site point.
/// </summary>
public class VoronoiCellGeneration
```

#### Fields

- **StrDb** (`StructureDatabase`): Reference to the structure database containing all mesh data and relationships.
- **sphericalTriangulator** (`SphericalDelaunayTriangulation`): Spherical triangulator used for triangulating projected points.

#### Constructor

```csharp
/// <summary>
/// Initializes a new instance of the VoronoiCellGeneration class.
/// </summary>
/// <param name="db">The structure database containing vertex, edge, and triangle data.</param>
public VoronoiCellGeneration(StructureDatabase db)
```

#### Public Methods

##### GenerateVoronoiCells

```csharp
/// <summary>
/// Generates Voronoi cells for all sites in the structure database.
/// This method processes each site point, finds incident triangles, computes circumcenters,
/// and creates Voronoi cells by triangulating the projected circumcenters.
/// </summary>
/// <param name="percent">Progress tracking object for monitoring generation progress.</param>
public void GenerateVoronoiCells(GenericPercent percent)
```

**Process:**
1. Iterates through all legacy vertex points as Voronoi sites
2. For each site, finds all incident triangles using half-edge maps
3. Computes spherical circumcenters for each incident triangle
4. Projects circumcenters onto a 2D plane using a computed normal vector
5. Triangulates the projected points to form a Voronoi cell
6. Registers the cell with the structure database

##### TriangulatePoints

```csharp
/// <summary>
/// Triangulates a set of circumcenter points to form a Voronoi cell.
/// Projects 3D points onto a 2D plane using the provided normal vector,
/// then performs Delaunay triangulation to create the cell structure.
/// </summary>
/// <param name="unitNorm">The unit normal vector for the projection plane.</param>
/// <param name="TriCircumcenters">List of circumcenter points to triangulate.</param>
/// <param name="index">Index to assign to the generated Voronoi cell.</param>
/// <returns>A new VoronoiCell containing the triangulated structure.</returns>
public VoronoiCell TriangulatePoints(Vector3 unitNorm, List<Point> TriCircumcenters, int index)
```

**Process:**
1. Creates an orthonormal basis for projection using the unit normal
2. Projects all 3D circumcenter points onto a 2D plane
3. Performs Delaunay triangulation on the projected points
4. Creates a VoronoiCell from the triangulation results
5. Registers the cell with vertex and edge mappings in the database

##### ReorderPoints

```csharp
/// <summary>
/// Reorders points in a list based on their angular position relative to the centroid.
/// This method calculates the average position of all points and sorts them by angle
/// to create a consistent ordering for polygon formation.
/// </summary>
/// <param name="points">List of points to reorder.</param>
/// <returns>List of points reordered by angular position.</returns>
public List<Point> ReorderPoints(List<Point> points)
```

##### IsPointInTriangle

```csharp
/// <summary>
/// Determines if a 2D point lies inside a triangle defined by three vertices.
/// Uses cross product calculations to check the point's position relative to each edge.
/// </summary>
/// <param name="p">The point to test.</param>
/// <param name="a">First vertex of the triangle.</param>
/// <param name="b">Second vertex of the triangle.</param>
/// <param name="c">Third vertex of the triangle.</param>
/// <param name="reversed">Whether to use reversed winding order for the test.</param>
/// <returns>True if the point is inside the triangle, false otherwise.</returns>
public bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, bool reversed)
```

##### GetOrderedPoint

```csharp
/// <summary>
/// Gets a point from a list using modular arithmetic to handle out-of-bounds indices.
/// This method wraps around the list boundaries, allowing negative indices and
/// indices larger than the list size.
/// </summary>
/// <param name="points">List of points to access.</param>
/// <param name="index">Index of the point to retrieve (can be negative or out of bounds).</param>
/// <returns>The point at the specified index (with wrap-around behavior).</returns>
public Point GetOrderedPoint(List<Point> points, int index)
```

##### less

```csharp
/// <summary>
/// Calculates the angle in degrees between a center point and another point.
/// This method computes the arctangent of the relative position and converts
/// it to degrees, normalized to the range [0, 360).
/// </summary>
/// <param name="center">The center reference point.</param>
/// <param name="a">The point to calculate the angle for.</param>
/// <returns>The angle in degrees from the center to point a.</returns>
public float less(Vector2 center, Vector2 a)
```

#### Private Methods

##### MonotoneChainTriangulation

```csharp
/// <summary>
/// Performs fan triangulation on a set of ordered points to create triangles.
/// This method uses a simple fan triangulation approach suitable for convex polygons,
/// creating triangles from the first point to consecutive pairs of points.
/// </summary>
/// <param name="orderedPoints">List of points ordered in convex polygon formation.</param>
/// <returns>List of triangle point arrays representing the triangulation.</returns>
private List<Point[]> MonotoneChainTriangulation(List<Point> orderedPoints)
```

##### RemoveCollinearPoints

```csharp
/// <summary>
/// Removes collinear points from a list while preserving the polygon shape.
/// This method iterates through consecutive triplets of points and removes
/// the middle point if it lies on the line segment between the other two.
/// </summary>
/// <param name="points">List of points to process.</param>
/// <returns>List of points with collinear points removed.</returns>
private List<Point> RemoveCollinearPoints(List<Point> points)
```

## Key Algorithms

### Spherical Voronoi Cell Generation

1. **Site Processing**: Each vertex point from the original mesh is treated as a Voronoi site
2. **Triangle Discovery**: For each site, find all triangles that include that vertex
3. **Circumcenter Calculation**: Compute the spherical circumcenter of each incident triangle
4. **Projection**: Project 3D circumcenters onto a 2D plane using a computed normal vector
5. **Triangulation**: Perform Delaunay triangulation on the projected 2D points
6. **Cell Creation**: Create a VoronoiCell from the triangulation results

### Circumcenter Calculation

The circumcenter of a spherical triangle is calculated using vector mathematics:

```csharp
var v3 = Point.ToVectors3(tri.Points);
var ac = v3[2] - v3[0];
var ab = v3[1] - v3[0];
var abXac = ab.Cross(ac);
var vToCircumsphereCenter = (abXac.Cross(ab) * ac.LengthSquared() + ac.Cross(abXac) * ab.LengthSquared()) / (2.0f * abXac.LengthSquared());
Point cc = new Point(v3[0] + vToCircumsphereCenter);
```

### Point Projection

Points are projected onto a 2D plane using an orthonormal basis derived from the unit normal:

```csharp
var u = new Vector3(0, 0, 0);
if (!Mathf.Equals(unitNorm.X, 0.0f))
{
    u = new Vector3(-unitNorm.Y, unitNorm.X, 0.0f);
}
else if (!Mathf.Equals(unitNorm.Y, 0.0f))
{
    u = new Vector3(-unitNorm.Z, 0, unitNorm.Y);
}
else
{
    u = new Vector3(1, 0, 0);
}
u = u.Normalized();
var v = unitNorm.Cross(u);
```

## Dependencies

- `StructureDatabase`: Stores and manages all mesh data structures
- `SphericalDelaunayTriangulation`: Performs triangulation operations
- `Point`, `Triangle`, `Edge`, `VoronoiCell`: Core geometric data structures
- `Logger`: Provides logging functionality for debugging and monitoring
- `GenericPercent`: Progress tracking utility

## Usage Example

```csharp
// Initialize with structure database
var voronoiGenerator = new VoronoiCellGeneration(structureDatabase);

// Create progress tracker
var progress = new GenericPercent { PercentTotal = structureDatabase.LegacyVertexPoints.Count };

// Generate Voronoi cells
voronoiGenerator.GenerateVoronoiCells(progress);
```

## Error Handling

The class includes comprehensive error handling with try-catch blocks in the main generation method and extensive logging throughout all operations to facilitate debugging and monitoring of the generation process.