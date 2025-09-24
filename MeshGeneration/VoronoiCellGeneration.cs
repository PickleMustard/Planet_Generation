using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using UtilityLibrary;
using static MeshGeneration.StructureDatabase;

namespace MeshGeneration;
public class VoronoiCellGeneration
{
    int ranThing = 0;
    public void GenerateVoronoiCells(GenericPercent percent)
    {
        Logger.EnterFunction("GenerateVoronoiCells", $"Points: {VertexPoints.Count}");
        try
        {
            int pointCount = 0;
            foreach (Point p in VertexPoints.Values)
            {
                pointCount++;
                Logger.Debug($"Processing point {pointCount}/{VertexPoints.Count}: {p.Index}", "VoronoiCellGeneration");
                //GD.Print($"Triangulating Point: {p}\r");
                //Find all triangles that contain the current point
                HashSet<Edge> edgesWithPoint = new HashSet<Edge>();
                if (HalfEdgesFrom.ContainsKey(p.Index))
                {
                    edgesWithPoint = HalfEdgesFrom[p.Index];
                }
                else
                {
                    Logger.Debug($"No edges found for point {p.Index}, skipping", "VoronoiCellGeneration");
                    continue;
                }
                HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();
                Logger.Debug($"Found {edgesWithPoint.Count} edges for point {p.Index}", "VoronoiCellGeneration");

                foreach (Edge e in edgesWithPoint)
                {
                    foreach (Triangle t in EdgeTriangles[e])
                    {
                        trianglesWithPoint.Add(t);
                    }
                }
                Logger.Debug($"Found {trianglesWithPoint.Count} triangles for point {p.Index}", "VoronoiCellGeneration");
                List<Point> triCircumcenters = new List<Point>();
                foreach (var tri in trianglesWithPoint)
                {
                    Logger.Debug($"Calculating circumcenter for triangle {tri.Index}", "VoronoiCellGeneration");
                    var v3 = Point.ToVectors3(tri.Points);
                    var ac = v3[2] - v3[0];
                    var ab = v3[1] - v3[0];
                    var abXac = ab.Cross(ac);
                    var vToCircumsphereCenter = (abXac.Cross(ab) * ac.LengthSquared() + ac.Cross(abXac) * ab.LengthSquared()) / (2.0f * abXac.LengthSquared());
                    float circumsphereRadius = vToCircumsphereCenter.Length();
                    Point cc = new Point(v3[0] + vToCircumsphereCenter);
                    if (triCircumcenters.Contains(cc))
                    {
                        Logger.Debug($"Circumcenter {cc.Index} already exists, skipping", "VoronoiCellGeneration");
                        continue;
                    }
                    if (circumcenters.ContainsKey(cc.Index))
                    {
                        Point usedCC = circumcenters[cc.Index];
                        triCircumcenters.Add(usedCC);
                        Logger.Debug($"Using existing circumcenter {usedCC.Index}", "VoronoiCellGeneration");
                    }
                    else
                    {
                        circumcenters.Add(cc.Index, cc);
                        triCircumcenters.Add(cc);
                        Logger.Debug($"Added new circumcenter {cc.Index}", "VoronoiCellGeneration");
                    }
                }
                Vector3 center = new Vector3(0, 0, 0);
                foreach (Point triCenter in triCircumcenters)
                {
                    center += triCenter.ToVector3();
                }
                center /= triCircumcenters.Count;
                center = center.Normalized();
                Logger.Debug($"Calculated normalized center for point {p.Index}: {center}", "VoronoiCellGeneration");

                var centroid = new Vector3(0, 0, 0);
                var v1 = triCircumcenters[1].ToVector3() - triCircumcenters[0].ToVector3();
                var v2 = triCircumcenters[2].ToVector3() - triCircumcenters[0].ToVector3();
                var UnitNorm = v1.Cross(v2);
                UnitNorm = UnitNorm.Normalized();
                if (UnitNorm.Dot(triCircumcenters[0].ToVector3()) < 0f)
                {
                    UnitNorm = -UnitNorm;
                }
                Logger.Debug($"Unit normal vector calculated: {UnitNorm}", "VoronoiCellGeneration");
                VoronoiCell calculated = TriangulatePoints(UnitNorm, triCircumcenters, VoronoiCells.Count);
                calculated.IsBorderTile = false;
                if (calculated != null)
                {
                    VoronoiCells.Add(calculated);
                    Logger.Debug($"Added Voronoi cell {calculated.Index} to collection", "VoronoiCellGeneration");
                }
                foreach (Point vert in calculated.Points)
                {
                    VoronoiCellVertices.Add(vert);
                    Logger.Debug($"Added {calculated.Points.Length} vertices to VoronoiCellVertices", "VoronoiCellGeneration");
                }
                percent.PercentCurrent++;
            }
        }
        catch (Exception e)
        {
            GD.PrintRaw($"Cell Generation Error: {e.Message}\n{e.StackTrace}\n");
        }
    }

    public VoronoiCell TriangulatePoints(Vector3 unitNorm, List<Point> TriCircumcenters, int index)
    {
        // Validate input
        if (TriCircumcenters.Count < 3)
        {
            Logger.Debug($"Cannot triangulate cell {index} with only {TriCircumcenters.Count} points", "TriangulatePoints");
            return new VoronoiCell(index, new Point[0], new Triangle[0], new Edge[0]);
        }
        
        // Create an orthonormal basis for the tangent plane
        Vector3 t = Mathf.Abs(unitNorm.Y) < 0.9f ? unitNorm.Cross(Vector3.Up) : unitNorm.Cross(Vector3.Right);
        var u = t.Normalized();
        var v = unitNorm.Cross(u);
        
        // Find the centroid of all circumcenters
        Vector3 centroid = Vector3.Zero;
        var ccs = Point.ToVectors3(TriCircumcenters);
        foreach (var p in ccs)
        {
            centroid += p;
        }
        centroid /= ccs.Length;
        centroid = centroid.Normalized(); // Project to sphere surface

        // Project points onto the tangent plane at the centroid
        List<Point> projectedPoints = new List<Point>();
        for (int i = 0; i < TriCircumcenters.Count; i++)
        {
            // Project to tangent plane centered at centroid
            Vector3 relPos = ccs[i].Normalized() - centroid;
            var projection = new Vector2(relPos.Dot(u), relPos.Dot(v));
            projectedPoints.Add(new Point(new Vector3(projection.X, projection.Y, 0.0f), TriCircumcenters[i].Index));
        }
        
        // Order points for better triangulation
        var orderedPoints = ReorderPoints(projectedPoints);
        var GenTriangles = DelaunayTriangulation.TriangulateCell(orderedPoints, unitNorm);

        HashSet<Triangle> Triangles = new HashSet<Triangle>();
        List<Point> TriangulatedIndices = new List<Point>();
        HashSet<Edge> CellEdges = new HashSet<Edge>();

        foreach (var triangle in GenTriangles)
        {
            // Get the actual 3D circumcenter points
            Point p1 = circumcenters[triangle.Points[0].Index];
            Point p2 = circumcenters[triangle.Points[1].Index];
            Point p3 = circumcenters[triangle.Points[2].Index];
            
            // Validate triangle is not degenerate
            Vector3 v1 = p1.ToVector3().Normalized();
            Vector3 v2 = p2.ToVector3().Normalized();
            Vector3 v3 = p3.ToVector3().Normalized();
            
            // Check for degenerate triangles (points too close or collinear)
            float minAngle = 0.01f; // Minimum angle in radians
            Vector3 e1 = (v2 - v1).Normalized();
            Vector3 e2 = (v3 - v1).Normalized();
            float crossMag = e1.Cross(e2).Length();
            
            if (crossMag < minAngle)
            {
                Logger.Debug($"Skipping degenerate triangle with indices {p1.Index}, {p2.Index}, {p3.Index}", "TriangulatePoints");
                continue;
            }
            
            // Check triangle doesn't span too large an arc on the sphere
            float maxArc = Mathf.Pi / 3.0f; // Maximum 60 degrees
            if (Mathf.Acos(v1.Dot(v2)) > maxArc || 
                Mathf.Acos(v2.Dot(v3)) > maxArc || 
                Mathf.Acos(v3.Dot(v1)) > maxArc)
            {
                Logger.Debug($"Skipping triangle that spans too large an arc", "TriangulatePoints");
                continue;
            }

            // Create proper edges for the triangle
            Edge[] triEdges = new Edge[3];
            triEdges[0] = UpdateWorldEdgeMap(p1, p2);
            triEdges[1] = UpdateWorldEdgeMap(p2, p3);
            triEdges[2] = UpdateWorldEdgeMap(p3, p1);

            // Update the triangle with proper edges
            triangle.Points[0] = p1;
            triangle.Points[1] = p2;
            triangle.Points[2] = p3;
            triangle.Edges = triEdges.ToList();

            // Add points to triangulated indices
            TriangulatedIndices.Add(p1);
            TriangulatedIndices.Add(p2);
            TriangulatedIndices.Add(p3);

            // Add edges to cell edges
            CellEdges.Add(triEdges[0]);
            CellEdges.Add(triEdges[1]);
            CellEdges.Add(triEdges[2]);

            // Add triangle to maps
            AddTriangleToMaps(triangle);
            Triangles.Add(triangle);
        }

        VoronoiCell GeneratedCell = new VoronoiCell(index, TriangulatedIndices.ToArray(), Triangles.ToArray(), CellEdges.ToArray());
        foreach (Point p in TriangulatedIndices)
        {
            if (!CellMap.ContainsKey(p))
            {
                CellMap.Add(p, new HashSet<VoronoiCell>());
                CellMap[p].Add(GeneratedCell);
            }
            else
            {
                CellMap[p].Add(GeneratedCell);
            }
        }
        foreach (Edge e in CellEdges)
        {
            if (!EdgeMap.ContainsKey(e))
            {
                EdgeMap.Add(e, new HashSet<VoronoiCell>());
                EdgeMap[e].Add(GeneratedCell);
            }
            else
            {
                EdgeMap[e].Add(GeneratedCell);
            }
        }
        return GeneratedCell;
    }

    public List<Point> ReorderPoints(List<Point> points)
    {
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
            orderedPoints.Add(new Point(new Vector3(points[i].X, points[i].Y, less(center, new Vector2(points[i].X, points[i].Y))), points[i].Index));
        }
        orderedPoints = orderedPoints.OrderBy(p => p.Z).ToList();
        for (int i = 0; i < orderedPoints.Count; i++)
        {
            points[i] = new Point(new Vector3(orderedPoints[i].X, orderedPoints[i].Y, 0.0f), orderedPoints[i].Index);
        }
        return points;
    }


    public bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, bool reversed)
    {
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
                return false;
            }
        }
        else
        {
            if (ab.Cross(ap) > 0f || bc.Cross(bp) > 0f || ca.Cross(cp) > 0f)
            {
                return false;
            }
        }
        return true;
    }

    public Point GetOrderedPoint(List<Point> points, int index)
    {
        if (index >= points.Count)
        {
            return points[index % points.Count];
        }
        else if (index < 0)
        {
            return points[index % points.Count + points.Count];
        }
        else
        {
            return points[index];
        }
    }

    public float less(Vector2 center, Vector2 a)
    {
        float a1 = (Mathf.RadToDeg(Mathf.Atan2(a.Y - center.Y, a.X - center.X)) + 360) % 360;
        return a1;
    }

    private void AddTriangleToMaps(Triangle triangle)
    {
        foreach (Point p in triangle.Points)
        {
            if (!VoronoiTriMap.ContainsKey(p))
            {
                VoronoiTriMap.Add(p, new HashSet<Triangle>());
            }
            VoronoiTriMap[p].Add(triangle);
        }

        foreach (Edge e in triangle.Edges)
        {
            if (!VoronoiEdgeTriMap.ContainsKey(e))
            {
                VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
            }
            VoronoiEdgeTriMap[e].Add(triangle);
        }
    }

}
