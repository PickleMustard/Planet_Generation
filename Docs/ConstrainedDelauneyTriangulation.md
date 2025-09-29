# ConstrainedDelauneyTriangulation Documentation

## Overview

The `ConstrainedDelauneyTriangulation` class implements a constrained Delaunay triangulation algorithm for generating triangular meshes from polygon boundaries. This algorithm is particularly useful for mesh generation in planetary surface modeling and other geometric applications where certain edges must be preserved while maintaining optimal triangle properties.

## Algorithm Overview

The constrained Delaunay triangulation follows these main steps:

1. **Initialization**: Create a polygon boundary and build a super-triangle that encompasses all points
2. **Vertex Insertion**: Incrementally insert vertices while maintaining Delaunay properties
3. **Edge Recovery**: Recover constrained edges (polygon boundaries) through edge flipping
4. **Cleanup**: Remove the super-triangle and perform flood fill to identify interior triangles

The implementation uses efficient data structures including edge lookup tables and neighbor tracking to optimize performance during triangulation operations.

## Public Interface

### Constructor

```csharp
public ConstrainedDelauneyTriangulation(StructureDatabase db, List<Point> points)
```

**Parameters:**
- `db`: Reference to the structure database for accessing legacy circumcenters
- `points`: List of points forming the polygon boundary to be triangulated

**Remarks:**
The constructor validates the input points and ensures they are in counter-clockwise order. If the polygon has negative area (clockwise order), the points are automatically reversed.

### Main Method

```csharp
public Structures.Triangle[] Triangulate()
```

**Returns:** An array of Triangle structures representing the triangulated mesh.

**Remarks:**
This method executes the full triangulation algorithm:
1. Builds a super-triangle that encompasses all input points
2. Incrementally inserts all vertices while maintaining Delaunay properties
3. Recovers all constrained edges (polygon boundaries) through edge flipping
4. Removes the super-triangle and performs flood fill to identify interior triangles
5. Converts internal triangle representations to the public Triangle structure

The resulting triangulation preserves all polygon boundary edges as constrained while maintaining Delaunay properties for the interior mesh.

### Static Utility Methods

```csharp
public static bool PointInTriangle(Point a, Point b, Point c, Point p)
```

**Parameters:**
- `a`: First vertex of the triangle
- `b`: Second vertex of the triangle
- `c`: Third vertex of the triangle
- `p`: The point to test

**Returns:** True if the point lies inside or on the edge of the triangle, false otherwise.

## Internal Data Structures

### EdgeKey (private struct)

Represents an edge between two vertices using sorted indices for consistent hashing.

**Fields:**
- `a`: The first vertex index (always the smaller of the two)
- `b`: The second vertex index (always the larger of the two)

### EdgeRecord (private struct)

Stores information about an edge's location within a triangle.

**Fields:**
- `tri`: The index of the triangle containing this edge
- `edge`: The edge index within the triangle (0, 1, or 2)

### Triangle (private class)

Represents a triangle in the triangulation with vertex indices, neighbor information, and constraint flags.

**Properties:**
- `vertices`: Array of three vertex indices that form this triangle
- `neighbors`: Array of three neighbor triangle indices (-1 if no neighbor exists)
- `constrained`: Array indicating which edges are constrained (true = constrained, false = unconstrained)
- `alive`: Flag indicating whether this triangle is still active in the triangulation

### WalkResult (private struct)

Stores the result of a triangle walking operation for point location.

**Properties:**
- `triangle`: The index of the located triangle
- `edge`: The edge index within the triangle

## Private Helper Methods

### Geometric Computations

```csharp
private static float Orient2D(Point a, Point b, Point c)
```
Computes the 2D orientation test for three points. Returns positive value if counter-clockwise, negative if clockwise, zero if collinear.

```csharp
private static bool IsCounterClockwise(Point a, Point b, Point c)
```
Determines if three points form a counter-clockwise triangle.

```csharp
private bool InCircle(Point a, Point b, Point c, Point d)
```
Tests if a point lies inside the circumcircle of a triangle.

```csharp
private static bool SegmentsIntersectProperly(Point a, Point b, Point c, Point d)
```
Tests if two line segments intersect properly (not at endpoints).

### Polygon Operations

```csharp
private static double PolygonArea(List<Point> points)
```
Computes the signed area of a polygon using the shoelace formula.

```csharp
private static Point PolygonCentroid(List<Point> points)
```
Computes the centroid (geometric center) of a polygon.

```csharp
private static bool PointInPolygon(List<Point> points, Point p)
```
Tests if a point lies inside a polygon using the ray casting algorithm.

### Triangulation Core Methods

```csharp
private void InsertVertex(int idx)
```
Inserts a vertex into the triangulation while maintaining Delaunay properties.

```csharp
private void AddTriangle(int ia, int ib, int ic)
```
Adds a new triangle to the triangulation and updates neighbor relationships.

```csharp
private void LegalizeEdge(int startTri, int startEdge)
```
Legalizes edges by flipping to maintain Delaunay properties.

```csharp
private void FlipEdge(int triIndex, int edge)
```
Performs an edge flip operation between two adjacent triangles.

```csharp
private void RecoverConstrainedEdge(int a, int b)
```
Recovers a constrained edge by flipping intersecting edges until the constraint is satisfied.

### Utility Methods

```csharp
private void BuildSuperTriangle()
```
Builds a super-triangle that encompasses all input points.

```csharp
private void PurgeSuperTriangle()
```
Removes all triangles that contain super-triangle vertices.

```csharp
private void FloodFill(int seedTriangle, List<bool> insideList)
```
Performs flood fill to identify all triangles inside the polygon boundary.

```csharp
private int LocateTriangle(Point p)
```
Finds the triangle containing a given point.

```csharp
private Structures.Triangle ConvertTriangle(Triangle t, int index)
```
Converts an internal Triangle representation to the public Triangle structure.

## Usage Example

```csharp
// Create a polygon boundary
List<Point> polygonPoints = new List<Point>
{
    new Point { Position = new Vector3(0, 0, 0), Index = 0 },
    new Point { Position = new Vector3(10, 0, 0), Index = 1 },
    new Point { Position = new Vector3(10, 10, 0), Index = 2 },
    new Point { Position = new Vector3(0, 10, 0), Index = 3 }
};

// Initialize triangulation with structure database
StructureDatabase db = GetStructureDatabase();
ConstrainedDelauneyTriangulation triangulation = new ConstrainedDelauneyTriangulation(db, polygonPoints);

// Perform triangulation
Structures.Triangle[] triangles = triangulation.Triangulate();

// Use the resulting triangles for mesh generation
foreach (var triangle in triangles)
{
    // Process triangle...
}
```

## Performance Considerations

- The algorithm uses efficient data structures (dictionaries, lists) for O(1) edge lookups
- Edge legalization uses a stack-based approach to avoid recursion depth issues
- Safety counters prevent infinite loops during edge recovery
- The flood fill algorithm efficiently identifies interior triangles
- Memory usage is optimized by reusing triangle structures and marking dead triangles

## Error Handling

The class includes comprehensive logging through the Logger utility and handles edge cases such as:
- Insufficient points for triangulation (minimum 3 required)
- Clockwise polygon orientation (automatically corrected)
- Invalid triangle states during edge operations
- Missing seed triangles for flood fill operations

## Dependencies

- `Godot.Vector3` for 3D vector operations
- `Structures.Point` and `Structures.Triangle` for geometric data structures
- `UtilityLibrary.Logger` for debug and information logging
- `StructureDatabase` for accessing legacy circumcenter data