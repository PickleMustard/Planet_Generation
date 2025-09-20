using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using static MeshGeneration.StructureDatabase;

namespace MeshGeneration;
public class VoronoiCellGeneration
{
    public void GenerateVoronoiCells(GenericPercent percent)
    {
        foreach (Point p in VertexPoints.Values)
        {
            //GD.Print($"Triangulating Point: {p}\r");
            //Find all triangles that contain the current point
            HashSet<Edge> edgesWithPoint = HalfEdgesFrom[p];
            HashSet<Edge> edgesWithPointTo = HalfEdgesTo[p];
            HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();

            foreach (Edge e in edgesWithPoint)
            {
                foreach (Triangle t in EdgeTriangles[e])
                {
                    trianglesWithPoint.Add(t);
                }
            }
            foreach (Edge e in edgesWithPointTo)
            {
                foreach (Triangle t in EdgeTriangles[e])
                {
                    trianglesWithPoint.Add(t);
                }
            }
            List<Point> triCircumcenters = new List<Point>();
            foreach (var tri in trianglesWithPoint)
            {
                var v3 = Point.ToVectors3((IEnumerable<Point>)tri.Points);
                var ac = v3[2] - v3[0];
                var ab = v3[1] - v3[0];
                var abXac = ab.Cross(ac);
                var vToCircumsphereCenter = (abXac.Cross(ab) * ac.LengthSquared() + ac.Cross(abXac) * ab.LengthSquared()) / (2.0f * abXac.LengthSquared());
                float circumsphereRadius = vToCircumsphereCenter.Length();
                Point cc = new Point(v3[0] + vToCircumsphereCenter);
                if (triCircumcenters.Contains(cc))
                {
                    continue;
                }
                if (circumcenters.ContainsKey(cc.Index))
                {
                    Point usedCC = circumcenters[cc.Index];
                    triCircumcenters.Add(usedCC);
                }
                else
                {
                    circumcenters.Add(cc.Index, cc);
                    triCircumcenters.Add(cc);
                }
            }
            Vector3 center = new Vector3(0, 0, 0);
            foreach (Point triCenter in triCircumcenters)
            {
                center += triCenter.ToVector3();
            }
            center /= triCircumcenters.Count;
            center = center.Normalized();

            var centroid = new Vector3(0, 0, 0);
            var v1 = triCircumcenters[1].ToVector3() - triCircumcenters[0].ToVector3();
            var v2 = triCircumcenters[2].ToVector3() - triCircumcenters[0].ToVector3();
            var UnitNorm = v1.Cross(v2);
            UnitNorm = UnitNorm.Normalized();
            if (UnitNorm.Dot(triCircumcenters[0].ToVector3()) < 0f)
            {
                UnitNorm = -UnitNorm;
            }
            VoronoiCell calculated = TriangulatePoints(UnitNorm, triCircumcenters, VoronoiCells.Count);
            calculated.IsBorderTile = false;
            foreach (Point vertex in calculated.Points)
            {
                VoronoiCellVertices.Add(vertex);
            }
            if (calculated != null)
            {
                VoronoiCells.Add(calculated);
            }
            percent.PercentCurrent++;
        }
    }

    public VoronoiCell TriangulatePoints(Vector3 unitNorm, List<Point> TriCircumcenters, int index)
    {
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
        //Order List of 2D points in clockwise order
        var withoutCollinearPoints = RemoveCollinearPoints(projectedPoints);
        var orderedPoints = ReorderPoints(withoutCollinearPoints);
        var orderedPointsReversed = new List<Point>(orderedPoints);
        orderedPointsReversed.Reverse();

        List<Point> TriangulatedIndices = new List<Point>();
        List<Triangle> Triangles = new List<Triangle>();
        HashSet<IEdge> CellEdges = new HashSet<IEdge>();

        // Remove collinear points to avoid degenerate triangles

        if (orderedPoints.Count < 3)
        {
            // Handle degenerate case - create a single triangle with duplicate points
            if (orderedPoints.Count >= 1)
            {
                Point p1 = circumcenters[orderedPoints[0].Index];
                Point p2 = p1;
                Point p3 = p1;

                if (orderedPoints.Count >= 2)
                    p2 = circumcenters[orderedPoints[1].Index];
                if (orderedPoints.Count >= 3)
                    p3 = circumcenters[orderedPoints[2].Index];

                IEdge[] triEdges = new Edge[3];
                triEdges[0] = UpdateWorldEdgeMap(p1, p2);
                triEdges[1] = UpdateWorldEdgeMap(p2, p3);
                triEdges[2] = UpdateWorldEdgeMap(p3, p1);

                Triangle newTri = new Triangle(Triangles.Count, new List<IPoint>() { p1, p2, p3 }, triEdges.ToList());
                AddTriangleToMaps(newTri);
                Triangles.Add(newTri);
                TriangulatedIndices.Add(p1);
                TriangulatedIndices.Add(p2);
                TriangulatedIndices.Add(p3);
                CellEdges.Add(triEdges[0]);
                CellEdges.Add(triEdges[1]);
                CellEdges.Add(triEdges[2]);
            }
        }
        else
        {
            // Use monotone chain triangulation for convex polygon
            var triangles = MonotoneChainTriangulation(orderedPoints);

            // Convert 2D triangles back to 3D and create proper triangles
            foreach (var triangle in triangles)
            {
                Point v1 = circumcenters[triangle[0].Index];
                Point v2 = circumcenters[triangle[1].Index];
                Point v3 = circumcenters[triangle[2].Index];

                Vector3 vec1 = v2.ToVector3() - v1.ToVector3();
                Vector3 vec2 = v3.ToVector3() - v1.ToVector3();
                Vector3 triangleCrossProduct = vec1.Cross(vec2).Normalized();
                float angleTriangleFace = Mathf.Acos(triangleCrossProduct.Dot(unitNorm));

                IEdge[] triEdges;
                if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
                { //Inverse Winding
                    triEdges = new Edge[3];
                    triEdges[0] = UpdateWorldEdgeMap(v1, v2);
                    triEdges[1] = UpdateWorldEdgeMap(v3, v1);
                    triEdges[2] = UpdateWorldEdgeMap(v2, v3);

                    Triangle newTri = new Triangle(Triangles.Count,
                            new List<IPoint>() { v3, v1, v2 },
                            triEdges.ToList());
                    AddTriangleToMaps(newTri);
                    Triangles.Add(newTri);
                    TriangulatedIndices.AddRange(new Point[] { v3, v1, v2 });
                }
                else
                {
                    triEdges = new Edge[3];
                    triEdges[0] = UpdateWorldEdgeMap(v1, v2);
                    triEdges[1] = UpdateWorldEdgeMap(v2, v3);
                    triEdges[2] = UpdateWorldEdgeMap(v3, v1);

                    Triangle newTri = new Triangle(Triangles.Count,
                            new List<IPoint>() { v1, v2, v3 },
                            triEdges.ToList());
                    AddTriangleToMaps(newTri);
                    Triangles.Add(newTri);
                    TriangulatedIndices.AddRange(new Point[] { v1, v2, v3 });
                }

                CellEdges.Add(triEdges[0]);
                CellEdges.Add(triEdges[1]);
                CellEdges.Add(triEdges[2]);
            }
        }


        VoronoiCell GeneratedCell = new VoronoiCell(index, TriangulatedIndices.ToArray(), Triangles.ToArray(), (Edge[])CellEdges.ToArray());
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
            if (!EdgeMap.ContainsKey(e.ReverseEdge()))
            {
                EdgeMap.Add(e.ReverseEdge(), new HashSet<VoronoiCell>());
                EdgeMap[e.ReverseEdge()].Add(GeneratedCell);
            }
            EdgeMap[e].Add(GeneratedCell);
            EdgeMap[e.ReverseEdge()].Add(GeneratedCell);
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
            orderedPoints.Add(new Point(new Vector3(points[i].Position.X, points[i].Position.Y, less(center, new Vector2(points[i].Position.X, points[i].Position.Y))), points[i].Index));
        }
        orderedPoints = orderedPoints.OrderBy(p => p.Position.Z).ToList();
        for (int i = 0; i < orderedPoints.Count; i++)
        {
            points[i] = new Point(new Vector3(orderedPoints[i].Position.X, orderedPoints[i].Position.Y, 0.0f), orderedPoints[i].Index);
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
        float a1 = (Mathf.RadToDeg(Mathf.Atan2(a.X - center.X, a.Y - center.Y)) + 360) % 360;
        return a1;
    }

    private List<Point[]> MonotoneChainTriangulation(List<Point> orderedPoints)
    {
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

        return triangles;
    }

    private List<Point> RemoveCollinearPoints(List<Point> points)
    {
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
            return new List<Point> { points[0], points[1], points[2] };
        }

        return cleanedPoints.Count >= 3 ? cleanedPoints : points;
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
