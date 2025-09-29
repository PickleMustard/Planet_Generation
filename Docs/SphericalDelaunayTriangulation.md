# SphericalDelaunayTriangulation

## Overview

The `SphericalDelaunayTriangulation` class performs Delaunay triangulation on convex hulls that have been projected from a sphere onto a 2D plane. This implementation is specifically designed for spherical mesh generation and uses different triangulation strategies based on the number of points.

## Class Summary

```csharp
public class SphericalDelaunayTriangulation
```

Performs Delaunay triangulation on a convex hull projected from a sphere. This implementation is specifically designed for triangulating spherical convex hulls that have been projected onto a 2D plane.

### Remarks

The class uses two different triangulation strategies based on the number of points:
- For small convex hulls (≤ 6 points): Fan triangulation from centroid
- For larger convex hulls: Incremental Delaunay triangulation with edge flipping

The algorithm ensures that the resulting triangulation satisfies the Delaunay property, which means no point lies inside the circumcircle of any triangle.

## Fields

### originalPoints
```csharp
private List<Point> originalPoints;
```
Original 3D points from the sphere surface

### projectedPoints
```csharp
private List<Point> projectedPoints;
```
Points projected onto a 2D plane for triangulation

### triangles
```csharp
private List<Triangle> triangles;
```
Generated triangles forming the triangulation

### pointMap
```csharp
private Dictionary<int, Point> pointMap;
```
Mapping from index to original point for quick lookup

### StrDb
```csharp
private StructureDatabase StrDb;
```
Structure database containing circumcenters and other geometric data

## Constructors

### SphericalDelaunayTriangulation
```csharp
public SphericalDelaunayTriangulation(StructureDatabase db)
```
Initializes a new instance of the SphericalDelaunayTriangulation class

#### Parameters
- `db` - Structure database containing geometric data and circumcenters

## Methods

### Triangulate
```csharp
public Triangle[] Triangulate(List<Point> projectedPoints, List<Point> originalPoints)
```
Triangulates a set of points that represent a convex hull on a sphere. The points should already be projected onto a 2D plane.

#### Parameters
- `projectedPoints` - Points projected onto a 2D plane for triangulation
- `originalPoints` - Original 3D points from the sphere surface corresponding to the projected points

#### Returns
Array of triangles forming the Delaunay triangulation, or empty array if triangulation fails

#### Remarks
This method performs validation to ensure:
- Projected and original point counts match
- At least 3 points are provided (minimum for triangulation)

For 3 points, creates a single triangle. For 4-6 points, uses fan triangulation. For more than 6 points, uses incremental Delaunay triangulation with edge flipping.

### PerformFanTriangulation
```csharp
private void PerformFanTriangulation()
```
Performs fan triangulation from the centroid for small convex hulls

#### Remarks
This method is used for convex hulls with 6 or fewer points. It works by:
1. Sorting points by angle from the centroid
2. Creating triangles by connecting the first point to all other consecutive point pairs

This approach is efficient for small convex polygons and ensures proper triangulation without the need for complex Delaunay checks.

### PerformIncrementalDelaunay
```csharp
private void PerformIncrementalDelaunay()
```
Performs incremental Delaunay triangulation for larger convex hulls

#### Remarks
This method is used for convex hulls with more than 6 points. The algorithm:
1. Sorts points by angle for numerical stability
2. Creates an initial triangle with the first three points
3. Incrementally inserts remaining points into the triangulation
4. Performs edge flipping to ensure the Delaunay property is maintained

The edge flipping process iteratively checks adjacent triangles and flips edges when a point lies inside the circumcircle of a triangle, ensuring optimal triangle quality.

### InsertPointIntoTriangulation
```csharp
private void InsertPointIntoTriangulation(int pointIndex)
```
Inserts a new point into the existing triangulation

#### Parameters
- `pointIndex` - Index of the point to insert into the triangulation

#### Remarks
This method implements the point insertion step of incremental Delaunay triangulation:
1. Finds all triangles visible from the new point (forming the horizon)
2. Identifies horizon edges (boundary edges of visible triangles)
3. Removes visible triangles from the triangulation
4. Creates new triangles connecting the new point to each horizon edge

For convex hulls, the new point should always be visible from some triangles, as it lies on the convex hull boundary.

### IsPointVisibleFromTriangle
```csharp
private bool IsPointVisibleFromTriangle(int pointIndex, Triangle tri)
```
Checks if a point is visible from a triangle (on the correct side)

#### Parameters
- `pointIndex` - Index of the point to check visibility for
- `tri` - Triangle to check visibility against

#### Returns
True if the point is visible from the triangle, false otherwise

#### Remarks
Visibility is determined using the 2D orientation test. A point is considered visible from a triangle if it lies on the same side of the triangle's plane as the triangle's normal. This is crucial for determining which triangles should be removed during point insertion in incremental triangulation.

### SortPointsByAngle
```csharp
private List<int> SortPointsByAngle()
```
Sorts points by angle from centroid for consistent ordering

#### Returns
List of point indices sorted by angle from centroid

#### Remarks
This method calculates the centroid of all projected points and then sorts the points based on their angle relative to this centroid. This consistent ordering is important for numerical stability and predictable triangulation results. The sorting uses the arctangent function to compute angles in the range [-π, π].

### CreateTriangle
```csharp
private Triangle CreateTriangle(int i1, int i2, int i3)
```
Creates a triangle from three vertex indices

#### Parameters
- `i1` - Index of the first vertex
- `i2` - Index of the second vertex
- `i3` - Index of the third vertex

#### Returns
New Triangle object, or null if creation fails

#### Remarks
This method:
1. Ensures consistent counter-clockwise winding order using orientation test
2. Retrieves the original 3D points from the sphere surface
3. Creates edges between the three points
4. Constructs a Triangle object with proper indexing

The method attempts to find points in the circumcenters database first, falling back to the original points if not found.

### GetOriginalPoint
```csharp
private Point GetOriginalPoint(int index)
```
Gets the original spherical point corresponding to a projected point index

#### Parameters
- `index` - Index of the projected point

#### Returns
Original 3D point from sphere surface, or null if index is invalid

#### Remarks
This method first checks if the index is within valid bounds, then attempts to find the corresponding original point. It优先 checks the circumcenters database in the StructureDatabase, which may contain refined point positions. If not found there, it returns the original point from the input list.

### GetVertexIndex
```csharp
private int GetVertexIndex(Triangle tri, int vertexPosition)
```
Gets vertex index from a triangle

#### Parameters
- `tri` - Triangle containing the vertex
- `vertexPosition` - Position of the vertex in the triangle (0, 1, or 2)

#### Returns
Index of the vertex in the original points list, or -1 if not found

#### Remarks
This method maps a vertex from a triangle back to its index in the original points list by comparing point indices. This is necessary for triangulation algorithms that need to work with point indices rather than point objects.

### FindAdjacentTriangle
```csharp
private Triangle FindAdjacentTriangle(Triangle tri, int v1, int v2)
```
Finds a triangle adjacent to the given triangle sharing the specified edge

#### Parameters
- `tri` - Triangle to find adjacent triangle for
- `v1` - First vertex index of the shared edge
- `v2` - Second vertex index of the shared edge

#### Returns
Adjacent triangle sharing the edge, or null if none found

#### Remarks
This method searches through all triangles in the current triangulation to find one that shares exactly two vertices with the specified edge. Adjacent triangles are important for edge flipping operations in Delaunay triangulation, as they form quadrilaterals that may need to be re-triangulated.

### ShouldFlipEdge
```csharp
private bool ShouldFlipEdge(Triangle tri1, Triangle tri2, int sharedV1, int sharedV2)
```
Checks if an edge should be flipped to maintain Delaunay property

#### Parameters
- `tri1` - First triangle sharing the edge
- `tri2` - Second triangle sharing the edge
- `sharedV1` - First vertex index of the shared edge
- `sharedV2` - Second vertex index of the shared edge

#### Returns
True if the edge should be flipped, false otherwise

#### Remarks
The Delaunay property requires that no point lies inside the circumcircle of any triangle. This method checks if the opposite vertex of one triangle lies inside the circumcircle of the other triangle. If so, the shared edge should be flipped to improve triangle quality and maintain the Delaunay property.

### FlipEdge
```csharp
private void FlipEdge(Triangle tri1, Triangle tri2, int sharedV1, int sharedV2)
```
Flips an edge between two triangles

#### Parameters
- `tri1` - First triangle sharing the edge
- `tri2` - Second triangle sharing the edge
- `sharedV1` - First vertex index of the shared edge
- `sharedV2` - Second vertex index of the shared edge

#### Remarks
Edge flipping is a key operation in Delaunay triangulation. This method:
1. Identifies the opposite vertices of each triangle (not on the shared edge)
2. Removes the original two triangles from the triangulation
3. Creates two new triangles by connecting the opposite vertices

The flip operation replaces the shared edge with a new edge between the opposite vertices, which often improves triangle quality and maintains the Delaunay property.

### IsValidTriangle
```csharp
private bool IsValidTriangle(Triangle tri)
```
Validates that a triangle is properly formed

#### Parameters
- `tri` - Triangle to validate

#### Returns
True if the triangle is valid, false otherwise

#### Remarks
A triangle is considered valid if:
1. It is not null and has exactly 3 points
2. All three points are distinct (no duplicate vertices)
3. The triangle has non-zero area (points are not collinear)

This method uses a cross product test to check for collinearity by ensuring the squared length of the cross product exceeds a small epsilon value.

### Orient2D
```csharp
private static float Orient2D(Point a, Point b, Point c)
```
2D orientation test for three points

#### Parameters
- `a` - First point
- `b` - Second point
- `c` - Third point

#### Returns
Positive if points are counter-clockwise, negative if clockwise, zero if collinear

#### Remarks
This method computes the signed area of the parallelogram formed by vectors (b-a) and (c-a). The sign indicates the orientation of the three points:
- Positive: counter-clockwise orientation
- Negative: clockwise orientation  
- Zero: collinear points

This is a fundamental geometric predicate used in many triangulation algorithms.

### InCircle
```csharp
private static bool InCircle(Point a, Point b, Point c, Point d)
```
In-circle test for Delaunay triangulation

#### Parameters
- `a` - First vertex of the triangle
- `b` - Second vertex of the triangle
- `c` - Third vertex of the triangle
- `d` - Point to test

#### Returns
True if point d lies inside the circumcircle of triangle abc, false otherwise

#### Remarks
This method implements the in-circle test using the determinant method. It checks whether point d lies inside the circumcircle of the triangle formed by points a, b, and c. This is a key test for Delaunay triangulation, as the Delaunay property requires that no point lies inside the circumcircle of any triangle.

The test uses a 4x4 determinant computation optimized for efficiency.