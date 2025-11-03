using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using UtilityLibrary;

namespace MeshGeneration;

/// <summary>
/// Handles the generation of Voronoi cells from a spherical Delaunay triangulation.
/// This class is responsible for creating Voronoi diagrams on a sphere by computing
/// circumcenters of triangles and organizing them into cells around each site point.
/// </summary>
public class VoronoiCellGeneration
{
    /// <summary>
    /// Reference to the structure database containing all mesh data and relationships.
    /// </summary>
    private StructureDatabase StrDb;

    /// <summary>
    /// Spherical triangulator used for triangulating projected points.
    /// </summary>
    private SphericalDelaunayTriangulation sphericalTriangulator;

    /// <summary>
    /// Initializes a new instance of the VoronoiCellGeneration class.
    /// </summary>
    /// <param name="db">The structure database containing vertex, edge, and triangle data.</param>
    public VoronoiCellGeneration(StructureDatabase db)
    {
        StrDb = db;
    }

    private UnifiedCelestialMesh mesh;

    /// <summary>
    /// Generates Voronoi cells for all sites in the structure database.
    /// This method processes each site point, finds incident triangles, computes circumcenters,
    /// and creates Voronoi cells by triangulating the projected circumcenters.
    /// </summary>
    /// <param name="percent">Progress tracking object for monitoring generation progress.</param>
    public void GenerateVoronoiCells(GenericPercent percent, UnifiedCelestialMesh mesh, Octree<Point> oct)
    {
        this.mesh = mesh;
        Logger.EnterFunction("GenerateVoronoiCells", $"startPercent={percent.PercentCurrent}/{percent.PercentTotal}");
        GD.Print($"Generating Voronoi Cells: {StrDb.BaseVertices.Count} Sites");
        Logger.Info($"Structure Database: {StrDb.Index}");
        try
        {
            Logger.Info($"Generating Voronoi Cells: {StrDb.BaseVertices.Count} Sites");
            Point[] initialPoints = StrDb.BaseVertices.Values.ToArray();
            foreach (Point p in initialPoints)
            {
                // Find all base triangles incident to this site using existing half-edge maps
                Edge[] edgesWithPointFrom = StrDb.GetIncidentHalfEdges(p);
                HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();

                foreach (Edge e in edgesWithPointFrom)
                {
                    foreach (Triangle t in StrDb.GetTrianglesByEdge(e))
                    {
                        trianglesWithPoint.Add(t);
                    }
                }
                // Build Voronoi vertices (spherical circumcenters of incident triangles)
                List<Point> triCircumcenters = new List<Point>();
                Logger.Info($"Building Voronoi Cell: incidentTris={trianglesWithPoint.Count}, siteIndex={p.Index}");
                foreach (var tri in trianglesWithPoint)
                {
                    Logger.Debug($"Calculating circumcenter for triangle: {tri.Index}");
                    var v3 = Point.ToVectors3(tri.Points);
                    var ac = v3[2] - v3[0];
                    var ab = v3[1] - v3[0];
                    var abXac = ab.Cross(ac);
                    var vToCircumsphereCenter = (abXac.Cross(ab) * ac.LengthSquared() + ac.Cross(abXac) * ab.LengthSquared()) / (2.0f * abXac.LengthSquared());
                    Point cc = new Point(v3[0] + vToCircumsphereCenter);
                    if (triCircumcenters.Contains(cc))
                    {
                        Logger.Debug($"Duplicate circumcenter encountered: {cc.Index}");
                        continue;
                    }
                    if (StrDb.VoronoiVertices.ContainsKey(cc.Index))
                    {
                        Point existing = StrDb.VoronoiVertices[cc.Index];
                        triCircumcenters.Add(existing);
                        StrDb.VoronoiCellVertices.Add(existing);
                        Logger.Debug($"Reused existing circumcenter: {existing.Index}");
                    }
                    else
                    {
                        var stored = StrDb.GetOrCreateCircumcenter(cc.Index, cc.Position);
                        triCircumcenters.Add(stored);
                        StrDb.VoronoiCellVertices.Add(stored);
                        Logger.Debug($"Added new circumcenter: {stored.Index}");
                    }
                }

                if (triCircumcenters.Count == 0)
                {
                    percent.PercentCurrent++;
                    continue;
                }

                // Compute a plane normal unique to this cell for projection
                Vector3 unitNorm;
                if (triCircumcenters.Count >= 3)
                {
                    var v1 = triCircumcenters[1].ToVector3() - triCircumcenters[0].ToVector3();
                    var v2 = triCircumcenters[2].ToVector3() - triCircumcenters[0].ToVector3();
                    unitNorm = v1.Cross(v2).Normalized();
                    if (unitNorm.Dot(triCircumcenters[0].ToVector3()) < 0f)
                    {
                        unitNorm = -unitNorm;
                    }
                }
                else
                {
                    unitNorm = p.ToVector3().Normalized();
                }

                Logger.Debug($"Unit Norm: {unitNorm}");
                VoronoiCell calculated = TriangulatePoints(unitNorm, triCircumcenters, StrDb.VoronoiCells.Count);
                Logger.Info($"Generated Voronoi Cell: {calculated.Index} with {calculated.Points.Length} points, {calculated.Edges.Length} edges");
                calculated.IsBorderTile = false;
                if (calculated != null)
                {
                    StrDb.VoronoiCells.Add(calculated);
                }
                foreach (Edge e in calculated.Edges)
                {
                    StrDb.AddCellForEdge(e.key, calculated);
                }
                triCircumcenters.Clear();
                percent.PercentCurrent++;
            }
            GD.Print($"VoronoiCells Count: {StrDb.VoronoiCells.Count}");
            GD.Print($"EdgeMap Count: {StrDb.EdgeMap.Count}");
            GD.Print($"EdgeKeyCellMap: {StrDb.EdgeKeyCellMap.Count}");
        }
        catch (Exception e)
        {
            GD.PrintRaw($"\u001b[2J\u001b[H");
            GD.PrintErr($"Error in GenerateVoronoiCells: {e.Message}\n{e.StackTrace}");
            Logger.Error($"Error in GenerateVoronoiCells: {e.Message}\n{e.StackTrace}");
        }
        Logger.ExitFunction("GenerateVoronoiCells", $"endPercent={percent.PercentCurrent}/{percent.PercentTotal}, cells={StrDb.VoronoiCells.Count}");
    }

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
    {
        Logger.EnterFunction("TriangulatePoints", $"unitNorm=({unitNorm.X:F3},{unitNorm.Y:F3},{unitNorm.Z:F3}), count={TriCircumcenters.Count}, index={index}");
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

        List<Point> projectedPoints = new List<Point>();
        var ccs = Point.ToVectors3(TriCircumcenters);
        for (int i = 0; i < TriCircumcenters.Count; i++)
        {
            var projection = new Vector2((ccs[i] - ccs[0]).Dot(u), (ccs[i] - ccs[0]).Dot(v));
            projectedPoints.Add(new Point(new Vector3(projection.X, projection.Y, 0.0f), TriCircumcenters[i].Index));
        }

        // Order and clean points to form a simple polygon before CDT
        //projectedPoints = RemoveCollinearPoints(ReorderPoints(projectedPoints));

        sphericalTriangulator = new SphericalDelaunayTriangulation(StrDb);
        Triangle[] tris = sphericalTriangulator.Triangulate(projectedPoints, TriCircumcenters);
        List<Triangle> Triangles = new List<Triangle>(tris);
        List<Point> TriangulatedIndices = new List<Point>();
        HashSet<Edge> CellEdges = new HashSet<Edge>();
        foreach (Triangle t in Triangles)
        {
            TriangulatedIndices.AddRange(t.Points);
            StrDb.AddTriangle(t);
            //PolygonRendererSDL.RenderTriangleAndConnections(mesh, 10, t);
        }
        foreach (Triangle t in Triangles)
        {
            foreach (Edge e in t.Edges)
            {
                CellEdges.Add(e);
            }
        }
        Logger.Info($"# Triangles: {Triangles.Count}, # Edges: {CellEdges.Count}");

        VoronoiCell GeneratedCell = new VoronoiCell(index, TriangulatedIndices.ToArray(), Triangles.ToArray(), CellEdges.ToArray());
        foreach (Point p in TriangulatedIndices)
        {
            // Canonical registration
            StrDb.AddCellForVertex(p, GeneratedCell);
            //// Legacy mirror preserved below
            //if (!StrDb.CellMap.ContainsKey(p))
            //{
            //    StrDb.CellMap.Add(p, new HashSet<VoronoiCell>());
            //    StrDb.CellMap[p].Add(GeneratedCell);
            //    Logger.Debug($"CellMap: added new key pointIndex={p.Index} with cell={GeneratedCell.Index}");
            //}
            //else
            //{
            //    StrDb.CellMap[p].Add(GeneratedCell);
            //    Logger.Debug($"CellMap: appended cell={GeneratedCell.Index} to pointIndex={p.Index}");
            //}
        }
        Logger.ExitFunction("TriangulatePoints", $"returned cellIndex={GeneratedCell.Index}");
        return GeneratedCell;
    }

    /// <summary>
    /// Reorders points in a list based on their angular position relative to the centroid.
    /// This method calculates the average position of all points and sorts them by angle
    /// to create a consistent ordering for polygon formation.
    /// </summary>
    /// <param name="points">List of points to reorder.</param>
    /// <returns>List of points reordered by angular position.</returns>
    public List<Point> ReorderPoints(List<Point> points)
    {
        Logger.EnterFunction("ReorderPoints", $"count={points.Count}");
        var average = new Vector3(0, 0, 0);
        foreach (Point p in points)
        {
            average += p.ToVector3();
        }
        average /= points.Count;
        var center = new Vector2(average.X, average.Y);
        List<Point> orderedPoints = new List<Point>();
        for (int i = 0; i < points.Count; i++)
        {
            orderedPoints.Add(new Point(new Vector3(points[i].Position.X, points[i].Position.Y, less(center, new Vector2(points[i].Position.X, points[i].Position.Y))), points[i].Index));
        }
        orderedPoints = orderedPoints.OrderBy(p => p.Position.Z).ToList();
        for (int i = 0; i < orderedPoints.Count; i++)
        {
            points[i] = new Point(new Vector3(orderedPoints[i].Position.X, orderedPoints[i].Position.Y, 0.0f), orderedPoints[i].Index);
        }
        Logger.ExitFunction("ReorderPoints", $"returned count={points.Count}");
        return points;
    }


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
    {
        Logger.EnterFunction("IsPointInTriangle", $"reversed={reversed}");
        var ab = b - a;
        var bc = c - b;
        var ca = a - c;

        var ap = p - a;
        var bp = p - b;
        var cp = p - c;

        if (reversed)
        {

            if (ab.Cross(ap) < 0f || bc.Cross(bp) < 0f || ca.Cross(cp) < 0f)
            {
                Logger.ExitFunction("IsPointInTriangle", "returned false");
                return false;
            }
        }
        else
        {
            if (ab.Cross(ap) > 0f || bc.Cross(bp) > 0f || ca.Cross(cp) > 0f)
            {
                Logger.ExitFunction("IsPointInTriangle", "returned false");
                return false;
            }
        }
        Logger.ExitFunction("IsPointInTriangle", "returned true");
        return true;
    }

    /// <summary>
    /// Gets a point from a list using modular arithmetic to handle out-of-bounds indices.
    /// This method wraps around the list boundaries, allowing negative indices and
    /// indices larger than the list size.
    /// </summary>
    /// <param name="points">List of points to access.</param>
    /// <param name="index">Index of the point to retrieve (can be negative or out of bounds).</param>
    /// <returns>The point at the specified index (with wrap-around behavior).</returns>
    public Point GetOrderedPoint(List<Point> points, int index)
    {
        Logger.EnterFunction("GetOrderedPoint", $"index={index}, count={points.Count}");
        Point result;
        if (index >= points.Count)
        {
            result = points[index % points.Count];
        }
        else if (index < 0)
        {
            result = points[index % points.Count + points.Count];
        }
        else
        {
            result = points[index];
        }
        Logger.ExitFunction("GetOrderedPoint", $"returned pointIndex={result.Index}");
        return result;
    }

    /// <summary>
    /// Calculates the angle in degrees between a center point and another point.
    /// This method computes the arctangent of the relative position and converts
    /// it to degrees, normalized to the range [0, 360).
    /// </summary>
    /// <param name="center">The center reference point.</param>
    /// <param name="a">The point to calculate the angle for.</param>
    /// <returns>The angle in degrees from the center to point a.</returns>
    public float less(Vector2 center, Vector2 a)
    {
        float a1 = (Mathf.RadToDeg(Mathf.Atan2(a.X - center.X, a.Y - center.Y)) + 360) % 360;
        return a1;
    }

    /// <summary>
    /// Performs fan triangulation on a set of ordered points to create triangles.
    /// This method uses a simple fan triangulation approach suitable for convex polygons,
    /// creating triangles from the first point to consecutive pairs of points.
    /// </summary>
    /// <param name="orderedPoints">List of points ordered in convex polygon formation.</param>
    /// <returns>List of triangle point arrays representing the triangulation.</returns>
    private List<Point[]> MonotoneChainTriangulation(List<Point> orderedPoints)
    {
        Logger.EnterFunction("MonotoneChainTriangulation", $"count={orderedPoints.Count}");
        if (orderedPoints.Count < 3)
            return new List<Point[]>();

        if (orderedPoints.Count == 3)
            return new List<Point[]> { new Point[] { orderedPoints[0], orderedPoints[1], orderedPoints[2] } };

        List<Point[]> triangles = new List<Point[]>();

        // Use fan triangulation for convex polygons
        // This is more reliable than ear-clipping for Voronoi cells
        for (int i = 1; i < orderedPoints.Count - 1; i++)
        {
            triangles.Add(new Point[] { orderedPoints[0], orderedPoints[i], orderedPoints[i + 1] });
        }

        Logger.ExitFunction("MonotoneChainTriangulation", $"returned tris={triangles.Count}");
        return triangles;
    }

    /// <summary>
    /// Removes collinear points from a list while preserving the polygon shape.
    /// This method iterates through consecutive triplets of points and removes
    /// the middle point if it lies on the line segment between the other two.
    /// </summary>
    /// <param name="points">List of points to process.</param>
    /// <returns>List of points with collinear points removed.</returns>
    private List<Point> RemoveCollinearPoints(List<Point> points)
    {
        Logger.EnterFunction("RemoveCollinearPoints", $"count={points.Count}");
        if (points.Count <= 3)
            return points;

        List<Point> cleanedPoints = new List<Point>();
        int n = points.Count;

        for (int i = 0; i < n; i++)
        {
            int prev = (i - 1 + n) % n;
            int next = (i + 1) % n;

            Vector2 a = new Vector2(points[prev].Position.X, points[prev].Position.Y);
            Vector2 b = new Vector2(points[i].Position.X, points[i].Position.Y);
            Vector2 c = new Vector2(points[next].Position.X, points[next].Position.Y);

            Vector2 ab = b - a;
            Vector2 bc = c - b;

            // Check if points are collinear (cross product close to zero)
            if (Mathf.Abs(ab.Cross(bc)) > 0.00001f)
            {
                cleanedPoints.Add(points[i]);
            }
        }

        // Ensure we have at least 3 points
        if (cleanedPoints.Count < 3 && points.Count >= 3)
        {
            Logger.ExitFunction("RemoveCollinearPoints", "returned original first three points");
            return new List<Point> { points[0], points[1], points[2] };
        }

        Logger.ExitFunction("RemoveCollinearPoints", $"returned count={cleanedPoints.Count}");
        return cleanedPoints.Count >= 3 ? cleanedPoints : points;
    }
}
