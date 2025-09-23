using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using static MeshGeneration.StructureDatabase;

namespace MeshGeneration;
public class VoronoiCellGeneration
{
    int ranThing = 0;
    public void GenerateVoronoiCells(GenericPercent percent)
    {
        try
        {
            foreach (Point p in VertexPoints.Values)
            {
                //GD.Print($"Triangulating Point: {p}\r");
                //Find all triangles that contain the current point
                HashSet<Edge> edgesWithPoint = new HashSet<Edge>();
                if (HalfEdgesFrom.ContainsKey(p.Index))
                {
                    edgesWithPoint = HalfEdgesFrom[p.Index];
                }
                else
                {
                    continue;
                }
                HashSet<Triangle> trianglesWithPoint = new HashSet<Triangle>();

                foreach (Edge e in edgesWithPoint)
                {
                    foreach (Triangle t in EdgeTriangles[e])
                    {
                        trianglesWithPoint.Add(t);
                    }
                }
                List<Point> triCircumcenters = new List<Point>();
                foreach (var tri in trianglesWithPoint)
                {
                    var v3 = Point.ToVectors3(tri.Points);
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
                if (calculated != null)
                {
                    VoronoiCells.Add(calculated);
                }
                foreach (Point vert in calculated.Points)
                {
                    VoronoiCellVertices.Add(vert);
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
        /*
        var orderedPointsReversed = new List<Point>(orderedPoints);
        orderedPointsReversed.Reverse();
        */
        //Order List of 2D points in clockwise order
        var orderedPoints = ReorderPoints(projectedPoints);
        var Triangles = DelaunayTriangulation.TriangulateCell(projectedPoints, unitNorm);

        //if (ranThing < 3)
        //{
        //    var dl = DelaunayTriangulation.TriangulateCell(projectedPoints);
        //    GD.PrintRaw($"Points: {dl.Count}\n");
        //    foreach (var triangle in dl)
        //    {
        //        PolygonRendererSDL.DrawTriangle(GenerateDocArrayMesh.instance, 10, circumcenters[triangle.Points[0].Index].ToVector3(), circumcenters[triangle.Points[2].Index].ToVector3(), circumcenters[triangle.Points[1].Index].ToVector3());
        //    }

        //    ranThing++;
        //}

        //List<Triangle> Triangles = DelaunayTriangulation.Triangulate(orderedPoints);
        //List<Triangle> Triangles = new List<Triangle>();
        List<Point> TriangulatedIndices = new List<Point>();
        HashSet<Edge> CellEdges = new HashSet<Edge>();

        foreach (var triangle in Triangles)
        {
            // Create proper edges for the triangle
            Edge[] triEdges = new Edge[3];

            // Get the actual 3D circumcenter points
            Point p1 = circumcenters[triangle.Points[0].Index];
            Point p2 = circumcenters[triangle.Points[1].Index];
            Point p3 = circumcenters[triangle.Points[2].Index];

            // Create edges using the world edge map
            triEdges[0] = UpdateWorldEdgeMap(p1, p2);
            triEdges[1] = UpdateWorldEdgeMap(p2, p3);
            triEdges[2] = UpdateWorldEdgeMap(p3, p1);

            //// Update the triangle with proper edges
            triangle.Edges = triEdges.ToList();

            //// Add points to triangulated indices
            TriangulatedIndices.Add(p1);
            TriangulatedIndices.Add(p2);
            TriangulatedIndices.Add(p3);

            //// Add edges to cell edges
            CellEdges.Add(triEdges[0]);
            CellEdges.Add(triEdges[1]);
            CellEdges.Add(triEdges[2]);

            //// Add triangle to maps
            AddTriangleToMaps(triangle);
        }
        //Edge[] triEdges;
        //Point v1, v2, v3;
        //Vector3 v1Tov2, v1Tov3, triangleCrossProduct;
        //float angleTriangleFace;
        //int end = 0;
        //while (orderedPoints.Count > 3)
        //{
        //    for (int i = 0; i < orderedPoints.Count; i++)
        //    {
        //        var a = GetOrderedPoint(orderedPoints, i);
        //        var b = GetOrderedPoint(orderedPoints, i - 1);
        //        var c = GetOrderedPoint(orderedPoints, i + 1);

        //        Vector3 tab = b.ToVector3() - a.ToVector3();
        //        Vector3 tac = c.ToVector3() - a.ToVector3();
        //        Vector2 ab = new Vector2(tab.X, tab.Y);
        //        Vector2 ac = new Vector2(tac.X, tac.Y);

        //        //bool variationResult = TryVariation(orderedPoints, i);
        //        if (ab.Cross(ac) < 0.0f)// && !variationResult)
        //        {
        //            continue;
        //        }
        //        //else if (variationResult)
        //        //{
        //        //    a = GetOrderedPoint(orderedPoints, i);
        //        //    b = GetOrderedPoint(orderedPoints, i + 1);
        //        //    c = GetOrderedPoint(orderedPoints, i - 1);
        //        //    tab = b.ToVector3() - a.ToVector3();
        //        //    tac = c.ToVector3() - a.ToVector3();
        //        //    ab = new Vector2(tab.X, tab.Y);
        //        //    ac = new Vector2(tac.X, tac.Y);
        //        //}


        //        bool isEar = true;
        //        for (int j = 0; j < orderedPoints.Count; j++)
        //        {
        //            if (orderedPoints[j].Index == a.Index || orderedPoints[j].Index == b.Index || orderedPoints[j].Index == c.Index)
        //            {
        //                continue;
        //            }
        //            Vector2 p = new Vector2(orderedPoints[j].X, orderedPoints[j].Y);
        //            if (IsPointInTriangle(p, new Vector2(a.X, a.Y), new Vector2(b.X, b.Y), new Vector2(c.X, c.Y), false))
        //            {
        //                isEar = false;
        //                break;
        //            }
        //        }
        //        if (isEar)
        //        {
        //            //Take the 3 points in 3D space and generate the normal
        //            //If angle between triangle Normal and UnitNormal <90
        //            v1 = circumcenters[c.Index];
        //            v2 = circumcenters[a.Index];
        //            v3 = circumcenters[b.Index];

        //            v1Tov2 = v2.ToVector3() - v1.ToVector3();
        //            v1Tov3 = v3.ToVector3() - v1.ToVector3();

        //            triangleCrossProduct = v1Tov2.Cross(v1Tov3);
        //            triangleCrossProduct = triangleCrossProduct.Normalized();
        //            angleTriangleFace = triangleCrossProduct.Dot(unitNorm);
        //            if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
        //            { //Inverse Winding
        //                triEdges = new Edge[3];
        //                triEdges[0] = UpdateWorldEdgeMap(circumcenters[a.Index], circumcenters[b.Index]);
        //                triEdges[1] = UpdateWorldEdgeMap(circumcenters[c.Index], circumcenters[a.Index]);
        //                triEdges[2] = UpdateWorldEdgeMap(circumcenters[b.Index], circumcenters[c.Index]);
        //                //generatedEdges.Add(triEdges[0]);
        //                //generatedEdges.Add(triEdges[1]);
        //                //generatedEdges.Add(triEdges[2]);
        //                Triangle newTri = new Triangle(Triangles.Count,
        //                        new List<Point>() { circumcenters[c.Index], circumcenters[a.Index], circumcenters[b.Index] },
        //                        triEdges.ToList());
        //                foreach (Point p in newTri.Points)
        //                {
        //                    if (!VoronoiTriMap.ContainsKey(p))
        //                    {
        //                        VoronoiTriMap.Add(p, new HashSet<Triangle>());
        //                    }
        //                    VoronoiTriMap[p].Add(newTri);
        //                }
        //                foreach (Edge e in triEdges)
        //                {
        //                    if (!VoronoiEdgeTriMap.ContainsKey(e))
        //                    {
        //                        VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
        //                    }
        //                    VoronoiEdgeTriMap[e].Add(newTri);
        //                }
        //                Triangles.Add(newTri);
        //                AddTriangleToMaps(newTri);
        //                TriangulatedIndices.Add(circumcenters[c.Index]);
        //                TriangulatedIndices.Add(circumcenters[a.Index]);
        //                TriangulatedIndices.Add(circumcenters[b.Index]);
        //                CellEdges.Add(triEdges[0]);
        //                CellEdges.Add(triEdges[1]);
        //                CellEdges.Add(triEdges[2]);
        //            }
        //            else
        //            {
        //                triEdges = new Edge[3];
        //                triEdges[0] = UpdateWorldEdgeMap(circumcenters[b.Index], circumcenters[a.Index]);
        //                triEdges[1] = UpdateWorldEdgeMap(circumcenters[a.Index], circumcenters[c.Index]);
        //                triEdges[2] = UpdateWorldEdgeMap(circumcenters[c.Index], circumcenters[b.Index]);
        //                //generatedEdges.Add(triEdges[0]);
        //                //generatedEdges.Add(triEdges[1]);
        //                //generatedEdges.Add(triEdges[2]);
        //                Triangle newTri = new Triangle(Triangles.Count,
        //                        new List<Point>() { circumcenters[b.Index], circumcenters[a.Index], circumcenters[c.Index] },
        //                        triEdges.ToList());
        //                foreach (Point p in newTri.Points)
        //                {
        //                    if (!VoronoiTriMap.ContainsKey(p))
        //                    {
        //                        VoronoiTriMap.Add(p, new HashSet<Triangle>());
        //                    }
        //                    VoronoiTriMap[p].Add(newTri);
        //                }
        //                foreach (Edge e in triEdges)
        //                {
        //                    if (!VoronoiEdgeTriMap.ContainsKey(e))
        //                    {
        //                        VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
        //                    }
        //                    VoronoiEdgeTriMap[e].Add(newTri);
        //                }
        //                Triangles.Add(newTri);
        //                AddTriangleToMaps(newTri);
        //                TriangulatedIndices.Add(circumcenters[b.Index]);
        //                TriangulatedIndices.Add(circumcenters[a.Index]);
        //                TriangulatedIndices.Add(circumcenters[c.Index]);
        //                CellEdges.Add(triEdges[0]);
        //                CellEdges.Add(triEdges[1]);
        //                CellEdges.Add(triEdges[2]);
        //            }

        //            orderedPoints.RemoveAt(i);
        //            break;
        //        }
        //    }
        //    end++;
        //}
        //v1 = circumcenters[orderedPoints[2].Index];
        //v2 = circumcenters[orderedPoints[1].Index];
        //v3 = circumcenters[orderedPoints[0].Index];

        //v1Tov2 = v2.ToVector3() - v1.ToVector3();
        //v1Tov3 = v3.ToVector3() - v1.ToVector3();

        //triangleCrossProduct = v1Tov2.Cross(v1Tov3);
        //triangleCrossProduct = triangleCrossProduct.Normalized();
        //angleTriangleFace = triangleCrossProduct.Dot(unitNorm);
        //if (Mathf.Abs(Mathf.RadToDeg(angleTriangleFace)) > 90)
        //{
        //    triEdges = new Edge[3];
        //    triEdges[0] = UpdateWorldEdgeMap(circumcenters[orderedPoints[1].Index], circumcenters[orderedPoints[0].Index]);
        //    triEdges[1] = UpdateWorldEdgeMap(circumcenters[orderedPoints[2].Index], circumcenters[orderedPoints[1].Index]);
        //    triEdges[2] = UpdateWorldEdgeMap(circumcenters[orderedPoints[0].Index], circumcenters[orderedPoints[2].Index]);
        //    //generatedEdges.Add(triEdges[0]);
        //    //generatedEdges.Add(triEdges[1]);
        //    //generatedEdges.Add(triEdges[2]);
        //    Triangle newTri = new Triangle(Triangles.Count, new List<Point>() { circumcenters[orderedPoints[2].Index], circumcenters[orderedPoints[1].Index], circumcenters[orderedPoints[0].Index] }, triEdges.ToList());
        //    foreach (Point p in newTri.Points)
        //    {
        //        if (!VoronoiTriMap.ContainsKey(p))
        //        {
        //            VoronoiTriMap.Add(p, new HashSet<Triangle>());
        //        }
        //        VoronoiTriMap[p].Add(newTri);
        //    }
        //    foreach (Edge e in triEdges)
        //    {
        //        if (!VoronoiEdgeTriMap.ContainsKey(e))
        //        {
        //            VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
        //        }
        //        VoronoiEdgeTriMap[e].Add(newTri);
        //    }
        //    Triangles.Add(newTri);
        //    AddTriangleToMaps(newTri);
        //    TriangulatedIndices.Add(circumcenters[orderedPoints[2].Index]);
        //    TriangulatedIndices.Add(circumcenters[orderedPoints[1].Index]);
        //    TriangulatedIndices.Add(circumcenters[orderedPoints[0].Index]);
        //    CellEdges.Add(triEdges[0]);
        //    CellEdges.Add(triEdges[1]);
        //    CellEdges.Add(triEdges[2]);
        //}
        //else
        //{
        //    triEdges = new Edge[3];
        //    triEdges[0] = UpdateWorldEdgeMap(circumcenters[orderedPoints[0].Index], circumcenters[orderedPoints[1].Index]);
        //    triEdges[1] = UpdateWorldEdgeMap(circumcenters[orderedPoints[1].Index], circumcenters[orderedPoints[2].Index]);
        //    triEdges[2] = UpdateWorldEdgeMap(circumcenters[orderedPoints[2].Index], circumcenters[orderedPoints[0].Index]);
        //    //generatedEdges.Add(triEdges[0]);
        //    //generatedEdges.Add(triEdges[1]);
        //    //generatedEdges.Add(triEdges[2]);

        //    Triangle newTri = new Triangle(Triangles.Count, new List<Point>() { circumcenters[orderedPoints[0].Index], circumcenters[orderedPoints[1].Index], circumcenters[orderedPoints[2].Index] }, triEdges.ToList());
        //    foreach (Point p in newTri.Points)
        //    {
        //        if (!VoronoiTriMap.ContainsKey(p))
        //        {
        //            VoronoiTriMap.Add(p, new HashSet<Triangle>());
        //        }
        //        VoronoiTriMap[p].Add(newTri);
        //    }
        //    foreach (Edge e in triEdges)
        //    {
        //        if (!VoronoiEdgeTriMap.ContainsKey(e))
        //        {
        //            VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>());
        //        }
        //        VoronoiEdgeTriMap[e].Add(newTri);
        //    }
        //    Triangles.Add(newTri);
        //    AddTriangleToMaps(newTri);
        //    TriangulatedIndices.Add(circumcenters[orderedPoints[0].Index]);
        //    TriangulatedIndices.Add(circumcenters[orderedPoints[1].Index]);
        //    TriangulatedIndices.Add(circumcenters[orderedPoints[2].Index]);
        //    CellEdges.Add(triEdges[0]);
        //    CellEdges.Add(triEdges[1]);
        //    CellEdges.Add(triEdges[2]);
        //}

        VoronoiCell GeneratedCell = new VoronoiCell(index, TriangulatedIndices.ToArray(), Triangles.ToArray(), CellEdges.ToArray());
        //GD.PrintRaw($"Generated Cell: {GeneratedCell}\n");
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
        float a1 = (Mathf.RadToDeg(Mathf.Atan2(a.X - center.X, a.Y - center.Y)) + 360) % 360;
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
