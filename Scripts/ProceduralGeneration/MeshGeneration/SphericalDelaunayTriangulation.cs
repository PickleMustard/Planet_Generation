using System.Collections.Generic;
using System.Linq;
using Godot;
using UtilityLibrary;
using Structures.MeshGeneration;

namespace ProceduralGeneration.MeshGeneration;

/// <summary>
/// Performs Delaunay triangulation on a convex hull projected from a sphere.
/// This implementation is specifically designed for triangulating spherical convex hulls
/// that have been projected onto a 2D plane.
/// </summary>
/// <remarks>
/// The class uses two different triangulation strategies based on the number of points:
/// - For small convex hulls (≤ 6 points): Fan triangulation from centroid
/// - For larger convex hulls: Incremental Delaunay triangulation with edge flipping
///
/// The algorithm ensures that the resulting triangulation satisfies the Delaunay property,
/// which means no point lies inside the circumcircle of any triangle.
/// </remarks>
public class SphericalDelaunayTriangulation
{
    /// <summary>
    /// Original 3D points from the sphere surface
    /// </summary>
    private List<Point> originalPoints;

    /// <summary>
    /// Points projected onto a 2D plane for triangulation
    /// </summary>
    private List<Point> projectedPoints;

    /// <summary>
    /// Generated triangles forming the triangulation
    /// </summary>
    private List<Triangle> triangles;

    /// <summary>
    /// Mapping from index to original point for quick lookup
    /// </summary>
    private Dictionary<int, Point> pointMap;

    /// <summary>
    /// Structure database containing circumcenters and other geometric data
    /// </summary>
    private StructureDatabase StrDb;

    /// <summary>
    /// Initializes a new instance of the SphericalDelaunayTriangulation class
    /// </summary>
    /// <param name="db">Structure database containing geometric data and circumcenters</param>
    public SphericalDelaunayTriangulation(StructureDatabase db)
    {
        this.StrDb = db;
        triangles = new List<Triangle>();
        pointMap = new Dictionary<int, Point>();
    }

    /// <summary>
    /// Triangulates a set of points that represent a convex hull on a sphere.
    /// The points should already be projected onto a 2D plane.
    /// </summary>
    /// <param name="projectedPoints">Points projected onto a 2D plane for triangulation</param>
    /// <param name="originalPoints">Original 3D points from the sphere surface corresponding to the projected points</param>
    /// <returns>Array of triangles forming the Delaunay triangulation, or empty array if triangulation fails</returns>
    /// <remarks>
    /// This method performs validation to ensure:
    /// - Projected and original point counts match
    /// - At least 3 points are provided (minimum for triangulation)
    ///
    /// For 3 points, creates a single triangle. For 4-6 points, uses fan triangulation.
    /// For more than 6 points, uses incremental Delaunay triangulation with edge flipping.
    /// </remarks>
    public Triangle[] Triangulate(List<Point> projectedPoints, List<Point> originalPoints)
    {
        Logger.EnterFunction("SphericalDelaunayTriangulation.Triangulate",
            $"projectedCount={projectedPoints.Count}, originalCount={originalPoints.Count}");

        if (projectedPoints.Count != originalPoints.Count)
        {
            Logger.Error("Projected and original point counts must match");
            Logger.ExitFunction("SphericalDelaunayTriangulation.Triangulate", "returned empty array");
            return new Triangle[0];
        }

        if (projectedPoints.Count < 3)
        {
            Logger.Warning("Insufficient points for triangulation (minimum 3 required)");
            Logger.ExitFunction("SphericalDelaunayTriangulation.Triangulate", "returned empty array");
            return new Triangle[0];
        }

        this.projectedPoints = new List<Point>(projectedPoints);
        this.originalPoints = new List<Point>(originalPoints);
        this.triangles = new List<Triangle>();

        // Build point map for index lookup
        for (int i = 0; i < originalPoints.Count; i++)
        {
            pointMap[i] = originalPoints[i];
        }

        // Special cases for small point counts
        if (projectedPoints.Count == 3)
        {
            var tri = CreateTriangle(0, 1, 2);
            if (tri != null)
            {
                triangles.Add(tri);
            }
            Logger.ExitFunction("SphericalDelaunayTriangulation.Triangulate",
                $"returned {triangles.Count} triangles (trivial case)");
            return triangles.ToArray();
        }

        // For convex hulls, we can use a simpler approach than full Delaunay
        // Since the points form a convex polygon, we can use fan triangulation
        // or incremental triangulation
        if (projectedPoints.Count <= 6)
        {
            // For small convex hulls, use fan triangulation
            PerformFanTriangulation();
        }
        else
        {
            // For larger convex hulls, use incremental Delaunay
            PerformIncrementalDelaunay();
        }

        Logger.Info($"Generated {triangles.Count} triangles");
        Logger.ExitFunction("SphericalDelaunayTriangulation.Triangulate",
            $"returned {triangles.Count} triangles");
        return triangles.ToArray();
    }

    /// <summary>
    /// Performs fan triangulation from the centroid for small convex hulls
    /// </summary>
    /// <remarks>
    /// This method is used for convex hulls with 6 or fewer points. It works by:
    /// 1. Sorting points by angle from the centroid
    /// 2. Creating triangles by connecting the first point to all other consecutive point pairs
    ///
    /// This approach is efficient for small convex polygons and ensures proper triangulation
    /// without the need for complex Delaunay checks.
    /// </remarks>
    private void PerformFanTriangulation()
    {
        Logger.EnterFunction("PerformFanTriangulation", $"pointCount={projectedPoints.Count}");

        // Sort points by angle from centroid
        var sortedIndices = SortPointsByAngle();

        // Create triangles using fan from first point
        for (int i = 1; i < sortedIndices.Count - 1; i++)
        {
            var tri = CreateTriangle(sortedIndices[0], sortedIndices[i], sortedIndices[i + 1]);
            if (tri != null && IsValidTriangle(tri))
            {
                triangles.Add(tri);
                Logger.Debug($"Added fan triangle: {sortedIndices[0]}, {sortedIndices[i]}, {sortedIndices[i + 1]}");
            }
        }

        Logger.ExitFunction("PerformFanTriangulation", $"created {triangles.Count} triangles");
    }

    /// <summary>
    /// Performs incremental Delaunay triangulation for larger convex hulls
    /// </summary>
    /// <remarks>
    /// This method is used for convex hulls with more than 6 points. The algorithm:
    /// 1. Sorts points by angle for numerical stability
    /// 2. Creates an initial triangle with the first three points
    /// 3. Incrementally inserts remaining points into the triangulation
    /// 4. Performs edge flipping to ensure the Delaunay property is maintained
    ///
    /// The edge flipping process iteratively checks adjacent triangles and flips edges
    /// when a point lies inside the circumcircle of a triangle, ensuring optimal triangle quality.
    /// </remarks>
    private void PerformIncrementalDelaunay()
    {
        Logger.EnterFunction("PerformIncrementalDelaunay", $"pointCount={projectedPoints.Count}");

        // Sort points for better numerical stability
        var sortedIndices = SortPointsByAngle();

        // Start with the first triangle
        if (sortedIndices.Count >= 3)
        {
            var initialTri = CreateTriangle(sortedIndices[0], sortedIndices[1], sortedIndices[2]);
            if (initialTri != null)
            {
                triangles.Add(initialTri);
                Logger.Debug($"Initial triangle: {sortedIndices[0]}, {sortedIndices[1]}, {sortedIndices[2]}");
            }
        }

        // Add remaining points one by one
        for (int i = 3; i < sortedIndices.Count; i++)
        {
            InsertPointIntoTriangulation(sortedIndices[i]);
        }

        // Perform edge flipping to ensure Delaunay property
        int maxIterations = triangles.Count * 3;
        int iterations = 0;
        bool changed;

        do
        {
            changed = false;
            var trianglesCopy = new List<Triangle>(triangles);

            for (int i = 0; i < trianglesCopy.Count && !changed; i++)
            {
                var tri = trianglesCopy[i];
                if (!triangles.Contains(tri)) continue;

                // Check each edge for potential flip
                for (int j = 0; j < 3; j++)
                {
                    int v1 = GetVertexIndex(tri, j);
                    int v2 = GetVertexIndex(tri, (j + 1) % 3);

                    // Find adjacent triangle sharing this edge
                    var adjacent = FindAdjacentTriangle(tri, v1, v2);
                    if (adjacent != null)
                    {
                        if (ShouldFlipEdge(tri, adjacent, v1, v2))
                        {
                            FlipEdge(tri, adjacent, v1, v2);
                            changed = true;
                            break;
                        }
                    }
                }
            }

            iterations++;
        } while (changed && iterations <= maxIterations);

        Logger.Info($"Edge flipping completed after {iterations} iterations");
        Logger.ExitFunction("PerformIncrementalDelaunay", $"created {triangles.Count} triangles");
    }

    /// <summary>
    /// Inserts a new point into the existing triangulation
    /// </summary>
    /// <param name="pointIndex">Index of the point to insert into the triangulation</param>
    /// <remarks>
    /// This method implements the point insertion step of incremental Delaunay triangulation:
    /// 1. Finds all triangles visible from the new point (forming the horizon)
    /// 2. Identifies horizon edges (boundary edges of visible triangles)
    /// 3. Removes visible triangles from the triangulation
    /// 4. Creates new triangles connecting the new point to each horizon edge
    ///
    /// For convex hulls, the new point should always be visible from some triangles,
    /// as it lies on the convex hull boundary.
    /// </remarks>
    private void InsertPointIntoTriangulation(int pointIndex)
    {
        Logger.Debug($"Inserting point {pointIndex} into triangulation");

        // Find all triangles visible from this point (for convex hull, these form the horizon)
        var visibleTriangles = new List<Triangle>();
        var horizonEdges = new List<(int, int)>();

        foreach (var tri in triangles)
        {
            if (IsPointVisibleFromTriangle(pointIndex, tri))
            {
                visibleTriangles.Add(tri);
            }
        }

        if (visibleTriangles.Count == 0)
        {
            // Point is inside the convex hull - should not happen for convex hull points
            Logger.Warning($"Point {pointIndex} is not visible from any triangle");
            return;
        }

        // Find the horizon edges (boundary of visible triangles)
        var edgeCount = new Dictionary<(int, int), int>();
        foreach (var tri in visibleTriangles)
        {
            for (int i = 0; i < 3; i++)
            {
                int v1 = GetVertexIndex(tri, i);
                int v2 = GetVertexIndex(tri, (i + 1) % 3);
                var edge = v1 < v2 ? (v1, v2) : (v2, v1);

                if (edgeCount.ContainsKey(edge))
                    edgeCount[edge]++;
                else
                    edgeCount[edge] = 1;
            }
        }

        // Horizon edges are those that appear only once
        foreach (var kvp in edgeCount)
        {
            if (kvp.Value == 1)
            {
                horizonEdges.Add(kvp.Key);
            }
        }

        // Remove visible triangles
        foreach (var tri in visibleTriangles)
        {
            triangles.Remove(tri);
        }

        // Create new triangles connecting the point to horizon edges
        foreach (var edge in horizonEdges)
        {
            var newTri = CreateTriangle(pointIndex, edge.Item1, edge.Item2);
            if (newTri != null && IsValidTriangle(newTri))
            {
                triangles.Add(newTri);
            }
        }
    }

    /// <summary>
    /// Checks if a point is visible from a triangle (on the correct side)
    /// </summary>
    /// <param name="pointIndex">Index of the point to check visibility for</param>
    /// <param name="tri">Triangle to check visibility against</param>
    /// <returns>True if the point is visible from the triangle, false otherwise</returns>
    /// <remarks>
    /// Visibility is determined using the 2D orientation test. A point is considered
    /// visible from a triangle if it lies on the same side of the triangle's plane
    /// as the triangle's normal. This is crucial for determining which triangles
    /// should be removed during point insertion in incremental triangulation.
    /// </remarks>
    private bool IsPointVisibleFromTriangle(int pointIndex, Triangle tri)
    {
        var p = projectedPoints[pointIndex];
        var a = projectedPoints[GetVertexIndex(tri, 0)];
        var b = projectedPoints[GetVertexIndex(tri, 1)];
        var c = projectedPoints[GetVertexIndex(tri, 2)];

        // Check if point is on the correct side of the triangle
        return Orient2D(a, b, c) * Orient2D(a, b, p) > 0;
    }

    /// <summary>
    /// Sorts points by angle from centroid for consistent ordering
    /// </summary>
    /// <returns>List of point indices sorted by angle from centroid</returns>
    /// <remarks>
    /// This method calculates the centroid of all projected points and then sorts
    /// the points based on their angle relative to this centroid. This consistent
    /// ordering is important for numerical stability and predictable triangulation results.
    /// The sorting uses the arctangent function to compute angles in the range [-π, π].
    /// </remarks>
    private List<int> SortPointsByAngle()
    {
        // Calculate centroid
        Vector2 centroid = Vector2.Zero;
        foreach (var p in projectedPoints)
        {
            centroid += new Vector2(p.Position.X, p.Position.Y);
        }
        centroid /= projectedPoints.Count;

        // Create list of indices with angles
        var indexedAngles = new List<(int index, float angle)>();
        for (int i = 0; i < projectedPoints.Count; i++)
        {
            var p = new Vector2(projectedPoints[i].Position.X, projectedPoints[i].Position.Y);
            float angle = Mathf.Atan2(p.Y - centroid.Y, p.X - centroid.X);
            indexedAngles.Add((i, angle));
        }

        // Sort by angle
        indexedAngles.Sort((a, b) => a.angle.CompareTo(b.angle));

        return indexedAngles.Select(ia => ia.index).ToList();
    }

    /// <summary>
    /// Creates a triangle from three vertex indices
    /// </summary>
    /// <param name="i1">Index of the first vertex</param>
    /// <param name="i2">Index of the second vertex</param>
    /// <param name="i3">Index of the third vertex</param>
    /// <returns>New Triangle object, or null if creation fails</returns>
    /// <remarks>
    /// This method:
    /// 1. Ensures consistent counter-clockwise winding order using orientation test
    /// 2. Retrieves the original 3D points from the sphere surface
    /// 3. Creates edges between the three points
    /// 4. Constructs a Triangle object with proper indexing
    ///
    /// The method attempts to find points in the circumcenters database first,
    /// falling back to the original points if not found.
    /// </remarks>
    private Triangle CreateTriangle(int i1, int i2, int i3)
    {
        // Ensure consistent winding order
        if (Orient2D(projectedPoints[i1], projectedPoints[i2], projectedPoints[i3]) < 0)
        {
            (i2, i3) = (i3, i2);
            //var temp = i2;
            //i2 = i3;
            //i3 = temp;
        }

        // Get the original points from the sphere
        var p1 = GetOriginalPoint(i1);
        var p2 = GetOriginalPoint(i2);
        var p3 = GetOriginalPoint(i3);

        if (p1 == null || p2 == null || p3 == null)
        {
            Logger.Warning($"Could not find original points for triangle {i1}, {i2}, {i3}");
            return null;
        }

        // Create edges
        var e1 = Edge.MakeEdge(p1, p2);
        var e2 = Edge.MakeEdge(p2, p3);
        var e3 = Edge.MakeEdge(p3, p1);

        // Create triangle
        var tri = new Triangle(
            triangles.Count,
            new List<Point> { p1, p2, p3 },
            new List<Edge> { e1, e2, e3 }
        );

        return tri;
    }

    /// <summary>
    /// Gets the original spherical point corresponding to a projected point index
    /// </summary>
    /// <param name="index">Index of the projected point</param>
    /// <returns>Original 3D point from sphere surface, or null if index is invalid</returns>
    /// <remarks>
    /// This method first checks if the index is within valid bounds, then attempts
    /// to find the corresponding original point. It优先 checks the circumcenters
    /// database in the StructureDatabase, which may contain refined point positions.
    /// If not found there, it returns the original point from the input list.
    /// </remarks>
    private Point GetOriginalPoint(int index)
    {
        if (index < 0 || index >= originalPoints.Count)
            return null;

        var original = originalPoints[index];

        // Try to find the point in the circumcenters database
        if (StrDb.VoronoiVertices.ContainsKey(original.Index))
        {
            return StrDb.VoronoiVertices[original.Index];
        }

        // If not found, return the original point
        return original;
    }

    /// <summary>
    /// Gets vertex index from a triangle
    /// </summary>
    /// <param name="tri">Triangle containing the vertex</param>
    /// <param name="vertexPosition">Position of the vertex in the triangle (0, 1, or 2)</param>
    /// <returns>Index of the vertex in the original points list, or -1 if not found</returns>
    /// <remarks>
    /// This method maps a vertex from a triangle back to its index in the original
    /// points list by comparing point indices. This is necessary for triangulation
    /// algorithms that need to work with point indices rather than point objects.
    /// </remarks>
    private int GetVertexIndex(Triangle tri, int vertexPosition)
    {
        var point = tri.Points[vertexPosition];

        // Find the index of this point in our original points list
        for (int i = 0; i < originalPoints.Count; i++)
        {
            if (originalPoints[i].Index == point.Index)
                return i;
        }

        Logger.Warning($"Could not find index for point {point.Index}");
        return -1;
    }

    /// <summary>
    /// Finds a triangle adjacent to the given triangle sharing the specified edge
    /// </summary>
    /// <param name="tri">Triangle to find adjacent triangle for</param>
    /// <param name="v1">First vertex index of the shared edge</param>
    /// <param name="v2">Second vertex index of the shared edge</param>
    /// <returns>Adjacent triangle sharing the edge, or null if none found</returns>
    /// <remarks>
    /// This method searches through all triangles in the current triangulation
    /// to find one that shares exactly two vertices with the specified edge.
    /// Adjacent triangles are important for edge flipping operations in Delaunay
    /// triangulation, as they form quadrilaterals that may need to be re-triangulated.
    /// </remarks>
    private Triangle FindAdjacentTriangle(Triangle tri, int v1, int v2)
    {
        foreach (var other in triangles)
        {
            if (other == tri) continue;

            int sharedCount = 0;
            for (int i = 0; i < 3; i++)
            {
                int ov = GetVertexIndex(other, i);
                if (ov == v1 || ov == v2)
                    sharedCount++;
            }

            if (sharedCount == 2)
                return other;
        }
        return null;
    }

    /// <summary>
    /// Checks if an edge should be flipped to maintain Delaunay property
    /// </summary>
    /// <param name="tri1">First triangle sharing the edge</param>
    /// <param name="tri2">Second triangle sharing the edge</param>
    /// <param name="sharedV1">First vertex index of the shared edge</param>
    /// <param name="sharedV2">Second vertex index of the shared edge</param>
    /// <returns>True if the edge should be flipped, false otherwise</returns>
    /// <remarks>
    /// The Delaunay property requires that no point lies inside the circumcircle
    /// of any triangle. This method checks if the opposite vertex of one triangle
    /// lies inside the circumcircle of the other triangle. If so, the shared edge
    /// should be flipped to improve triangle quality and maintain the Delaunay property.
    /// </remarks>
    private bool ShouldFlipEdge(Triangle tri1, Triangle tri2, int sharedV1, int sharedV2)
    {
        // Find the opposite vertices
        int oppositeV1 = -1, oppositeV2 = -1;

        for (int i = 0; i < 3; i++)
        {
            int v = GetVertexIndex(tri1, i);
            if (v != sharedV1 && v != sharedV2)
            {
                oppositeV1 = v;
                break;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            int v = GetVertexIndex(tri2, i);
            if (v != sharedV1 && v != sharedV2)
            {
                oppositeV2 = v;
                break;
            }
        }

        if (oppositeV1 < 0 || oppositeV2 < 0)
            return false;

        // Check if oppositeV2 is inside the circumcircle of tri1
        return InCircle(
            projectedPoints[sharedV1],
            projectedPoints[sharedV2],
            projectedPoints[oppositeV1],
            projectedPoints[oppositeV2]
        );
    }

    /// <summary>
    /// Flips an edge between two triangles
    /// </summary>
    /// <param name="tri1">First triangle sharing the edge</param>
    /// <param name="tri2">Second triangle sharing the edge</param>
    /// <param name="sharedV1">First vertex index of the shared edge</param>
    /// <param name="sharedV2">Second vertex index of the shared edge</param>
    /// <remarks>
    /// Edge flipping is a key operation in Delaunay triangulation. This method:
    /// 1. Identifies the opposite vertices of each triangle (not on the shared edge)
    /// 2. Removes the original two triangles from the triangulation
    /// 3. Creates two new triangles by connecting the opposite vertices
    ///
    /// The flip operation replaces the shared edge with a new edge between the
    /// opposite vertices, which often improves triangle quality and maintains
    /// the Delaunay property.
    /// </remarks>
    private void FlipEdge(Triangle tri1, Triangle tri2, int sharedV1, int sharedV2)
    {
        Logger.Debug($"Flipping edge between vertices {sharedV1} and {sharedV2}");

        // Find the opposite vertices
        int oppositeV1 = -1, oppositeV2 = -1;

        for (int i = 0; i < 3; i++)
        {
            int v = GetVertexIndex(tri1, i);
            if (v != sharedV1 && v != sharedV2)
            {
                oppositeV1 = v;
                break;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            int v = GetVertexIndex(tri2, i);
            if (v != sharedV1 && v != sharedV2)
            {
                oppositeV2 = v;
                break;
            }
        }

        // Remove old triangles
        triangles.Remove(tri1);
        triangles.Remove(tri2);

        // Create new triangles with flipped edge
        var newTri1 = CreateTriangle(oppositeV1, oppositeV2, sharedV1);
        var newTri2 = CreateTriangle(oppositeV1, oppositeV2, sharedV2);

        if (newTri1 != null && IsValidTriangle(newTri1))
            triangles.Add(newTri1);
        if (newTri2 != null && IsValidTriangle(newTri2))
            triangles.Add(newTri2);
    }

    /// <summary>
    /// Validates that a triangle is properly formed
    /// </summary>
    /// <param name="tri">Triangle to validate</param>
    /// <returns>True if the triangle is valid, false otherwise</returns>
    /// <remarks>
    /// A triangle is considered valid if:
    /// 1. It is not null and has exactly 3 points
    /// 2. All three points are distinct (no duplicate vertices)
    /// 3. The triangle has non-zero area (points are not collinear)
    ///
    /// This method uses a cross product test to check for collinearity by ensuring
    /// the squared length of the cross product exceeds a small epsilon value.
    /// </remarks>
    private bool IsValidTriangle(Triangle tri)
    {
        if (tri == null || tri.Points == null || tri.Points.Count != 3)
            return false;

        // Check for degenerate triangles
        var p1 = tri.Points[0];
        var p2 = tri.Points[1];
        var p3 = tri.Points[2];

        // Check if points are distinct
        if (p1.Index == p2.Index || p2.Index == p3.Index || p1.Index == p3.Index)
            return false;

        // Check if triangle has non-zero area (not collinear)
        var v1 = p2.ToVector3() - p1.ToVector3();
        var v2 = p3.ToVector3() - p1.ToVector3();
        var cross = v1.Cross(v2);

        return cross.LengthSquared() > 1e-10f;
    }

    /// <summary>
    /// 2D orientation test for three points
    /// </summary>
    /// <param name="a">First point</param>
    /// <param name="b">Second point</param>
    /// <param name="c">Third point</param>
    /// <returns>
    /// Positive if points are counter-clockwise,
    /// negative if clockwise,
    /// zero if collinear
    /// </returns>
    /// <remarks>
    /// This method computes the signed area of the parallelogram formed by vectors
    /// (b-a) and (c-a). The sign indicates the orientation of the three points:
    /// - Positive: counter-clockwise orientation
    /// - Negative: clockwise orientation
    /// - Zero: collinear points
    ///
    /// This is a fundamental geometric predicate used in many triangulation algorithms.
    /// </remarks>
    private static float Orient2D(Point a, Point b, Point c)
    {
        return (b.Position.X - a.Position.X) * (c.Position.Y - a.Position.Y) -
               (b.Position.Y - a.Position.Y) * (c.Position.X - a.Position.X);
    }

    /// <summary>
    /// In-circle test for Delaunay triangulation
    /// </summary>
    /// <param name="a">First vertex of the triangle</param>
    /// <param name="b">Second vertex of the triangle</param>
    /// <param name="c">Third vertex of the triangle</param>
    /// <param name="d">Point to test</param>
    /// <returns>True if point d lies inside the circumcircle of triangle abc, false otherwise</returns>
    /// <remarks>
    /// This method implements the in-circle test using the determinant method.
    /// It checks whether point d lies inside the circumcircle of the triangle
    /// formed by points a, b, and c. This is a key test for Delaunay triangulation,
    /// as the Delaunay property requires that no point lies inside the circumcircle
    /// of any triangle.
    ///
    /// The test uses a 4x4 determinant computation optimized for efficiency.
    /// </remarks>
    private static bool InCircle(Point a, Point b, Point c, Point d)
    {
        float ax = a.Position.X - d.Position.X;
        float ay = a.Position.Y - d.Position.Y;
        float bx = b.Position.X - d.Position.X;
        float by = b.Position.Y - d.Position.Y;
        float cx = c.Position.X - d.Position.X;
        float cy = c.Position.Y - d.Position.Y;

        float ap = ax * ax + ay * ay;
        float bp = bx * bx + by * by;
        float cp = cx * cx + cy * cy;

        float det = ax * (by * cp - bp * cy) -
                   ay * (bx * cp - bp * cx) +
                   ap * (bx * cy - by * cx);

        return det > 0;
    }
}
