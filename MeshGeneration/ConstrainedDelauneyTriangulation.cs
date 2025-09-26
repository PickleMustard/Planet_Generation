using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using UtilityLibrary;

namespace MeshGeneration;

public class ConstrainedDelauneyTriangulation
{
    private struct EdgeKey
    {
        public int a;
        public int b;
    }

    private struct EdgeRecord
    {
        public EdgeRecord()
        {
            tri = -1;
            edge = -1;
        }

        public int tri { get; set; }
        public int edge { get; set; }
    }

    private class Triangle
    {
        public int[] vertices { get; set; }
        public int[] neighbors { get; set; }
        public bool[] constrained { get; set; }
        public bool alive { get; set; }

        public Triangle()
        {
            vertices = new int[3];
            neighbors = new int[3];
            constrained = new bool[3];
            alive = true;
        }
    }

    private struct WalkResult
    {
        public int triangle { get; set; }
        public int edge { get; set; }
        public WalkResult()
        {
            triangle = -1;
            edge = -1;
        }
    }

    List<Point> vertices { get; set; }
    List<Triangle> triangles { get; set; }
    Dictionary<EdgeKey, EdgeRecord> edgeLookup { get; set; }
    int[] superVerts = new int[3];

    StructureDatabase StrDb;

    private static float Orient2D(Point a, Point b, Point c)
    {
        return (b.Position.X - a.Position.X) * (c.Position.Y - a.Position.Y) - (b.Position.Y - a.Position.Y) * (c.Position.X - a.Position.X);
    }

    private static bool IsCounterClockwise(Point a, Point b, Point c)
    {
        return Orient2D(a, b, c) > 0;
    }

    private float Det3x3(float[][] mat)
    {
        float a11 = mat[0][0];
        float a12 = mat[0][1];
        float a13 = mat[0][2];
        float a21 = mat[1][0];
        float a22 = mat[1][1];
        float a23 = mat[1][2];
        float a31 = mat[2][0];
        float a32 = mat[2][1];
        float a33 = mat[2][2];

        float determinant = a11 * (a22 * a33 - a23 * a32) - a12 * (a21 * a33 - a23 * a31) + a13 * (a21 * a32 - a22 * a31);
        return determinant;
    }

    private bool InCircle(Point a, Point b, Point c, Point d)
    {
        float adx = a.Position.X - d.Position.X;
        float ady = a.Position.Y - d.Position.Y;
        float bdx = b.Position.X - d.Position.X;
        float bdy = b.Position.Y - d.Position.Y;
        float cdx = c.Position.X - d.Position.X;
        float cdy = c.Position.Y - d.Position.Y;

        float ad = adx * adx + ady * ady;
        float bd = bdx * bdx + bdy * bdy;
        float cd = cdx * cdx + cdy * cdy;

        float determinant = Det3x3(new float[][] { new float[] { adx, ady, ad }, new float[] { bdx, bdy, bd }, new float[] { cdx, cdy, cd } });
        float orient = Orient2D(a, b, c);
        return (orient > 0f && determinant > 0f) || (orient < 0f && determinant < 0f);
    }

    private static double PolygonArea(List<Point> points)
    {
        float accumulator = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Point a = points[i];
            Point b = points[(i + 1) % points.Count];
            accumulator += a.Position.X * b.Position.Y - a.Position.Y * b.Position.X;
        }
        return 0.5f * accumulator;
    }

    private static Point PolygonCentroid(List<Point> points)
    {
        float A = 0f;
        float cx = 0f;
        float cy = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            Point a = points[i];
            Point b = points[(i + 1) % points.Count];
            float cross = a.Position.X * b.Position.Y - a.Position.Y * b.Position.X;
            A += cross;
            cx += (a.Position.X + b.Position.X) * cross;
            cy += (a.Position.Y + b.Position.Y) * cross;
        }
        A *= 0.5f;
        float factor = 1.0f / (6.0f * A);
        return new Point { Position = new Vector3(cx * factor, cy * factor, 0f), Index = 0 };
    }

    public static bool PointInTriangle(Point a, Point b, Point c, Point p)
    {
        bool o1 = Orient2D(a, b, p) >= 0f;
        bool o2 = Orient2D(b, c, p) >= 0f;
        bool o3 = Orient2D(c, a, p) >= 0f;
        return (o1 && o2 && o3);
    }

    private static bool SegmentsIntersectProperly(Point a, Point b, Point c, Point d)
    {
        Logger.EnterFunction("SegmentsIntersectProperly", $"a={a}, b={b}, c={c}, d={d}");
        float o1 = Orient2D(a, b, c);
        float o2 = Orient2D(a, b, d);
        float o3 = Orient2D(c, d, a);
        float o4 = Orient2D(c, d, b);

        if ((o1 * o2 < 0f) && (o3 * o4 < 0f))
        {
            Logger.ExitFunction("SegmentsIntersectProperly", $"Segments intersect properly");
            return true;
        }
        Logger.ExitFunction("SegmentsIntersectProperly", $"Segments do not intersect properly");
        return false;
    }

    private static bool PointInPolygon(List<Point> points, Point p)
    {
        bool inside = false;
        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            Point pi = points[i];
            Point pj = points[j];
            bool intersects = ((pi.Position.Y > p.Position.Y) != (pj.Position.Y > p.Position.Y))
                && (p.Position.X < (pj.Position.X - pi.Position.X)
                        * (p.Position.Y - pi.Position.Y) / (pj.Position.Y - pi.Position.Y) + pi.Position.X);
            if (intersects) inside = !inside;
        }
        return inside;
    }

    public ConstrainedDelauneyTriangulation(StructureDatabase db, List<Point> points)
    {
        this.StrDb = db;
        Logger.Info($"ConstrainedDelauneyTriangulation: Initializing with {points.Count} points");

        if (points.Count < 3)
        {
            Logger.Warning("ConstrainedDelauneyTriangulation: Insufficient points for triangulation (minimum 3 required)");
        }

        if (PolygonArea(points) < 0f)
        {
            Logger.Info("ConstrainedDelauneyTriangulation: Polygon has negative area, reversing point order");
            List<Point> tmp = new List<Point>(points);
            tmp.Reverse();
            vertices = new List<Point>(tmp);
        }
        else
        {
            Logger.Info("ConstrainedDelauneyTriangulation: Using original point order");
            vertices = new List<Point>(points);
        }

        triangles = new List<Triangle>();
        edgeLookup = new Dictionary<EdgeKey, EdgeRecord>();

        Logger.Info($"ConstrainedDelauneyTriangulation: Initialized with {vertices.Count} vertices");
    }

    public Structures.Triangle[] Triangulate()
    {
        Logger.EnterFunction("Triangulate");
        Logger.Info("Starting triangulation process");

        int N_ORIGINAL = vertices.Count;

        BuildSuperTriangle();
        Logger.Info("Super triangle built successfully");

        Logger.Info($"Processing {N_ORIGINAL} original vertices");

        for (int i = 0; i < N_ORIGINAL; i++)
        {
            Logger.Info($"Inserting vertex {i} of {N_ORIGINAL - 1}");
            InsertVertex(i);
        }

        Logger.Info("Recovering constrained edges");
        for (int i = 0; i < N_ORIGINAL; i++)
        {
            int a = i;
            int b = (i + 1) % N_ORIGINAL;
            Logger.Info($"Recovering constrained edge between vertices {a} and {b}");
            RecoverConstrainedEdge(a, b);
        }

        PurgeSuperTriangle();
        Logger.Info("Super triangle purged");

        List<bool> insideList = new List<bool>(Enumerable.Repeat(false, triangles.Count));
        if (triangles.Count > 0)
        {
            Point seedPoint = PolygonCentroid(vertices);
            int seedTri = LocateTriangle(seedPoint);
            if (seedTri >= 0)
            {
                Logger.Info($"Starting flood fill from triangle {seedTri}");
                FloodFill(seedTri, insideList);
            }
            else
            {
                Logger.Warning("No valid seed triangle found for flood fill");
            }
        }

        List<Structures.Triangle> result = new List<Structures.Triangle>();
        int validTriangles = 0;
        for (int i = 0; i < triangles.Count; i++)
        {
            if (!triangles[i].alive || !insideList[i]) continue;
            result.Add(ConvertTriangle(triangles[i], result.Count));
            validTriangles++;
        }

        Logger.Info($"Triangulation complete: {validTriangles} valid triangles generated");
        Logger.ExitFunction("Triangulate", $"returned {result.Count} triangles");
        return result.ToArray();
    }

    private Structures.Triangle ConvertTriangle(Triangle t, int index)
    {
        Logger.EnterFunction("ConvertTriangle", $"index={index}, vertices=[{t.vertices[0]},{t.vertices[1]},{t.vertices[2]}]");

        Point a = vertices[t.vertices[0]];
        Point b = vertices[t.vertices[1]];
        Point c = vertices[t.vertices[2]];
        Point[] triVerts = new Point[] {
            StrDb.circumcenters[a.Index],
            StrDb.circumcenters[b.Index],
            StrDb.circumcenters[c.Index] };
        Structures.Edge e1 = new Structures.Edge(triVerts[0], triVerts[1]);
        Structures.Edge e2 = new Structures.Edge(triVerts[1], triVerts[2]);
        Structures.Edge e3 = new Structures.Edge(triVerts[2], triVerts[0]);

        Structures.Triangle newTri = new Structures.Triangle(index,
                new List<Point>() { triVerts[0], triVerts[1], triVerts[2] },
                new List<Structures.Edge>() { e1, e2, e3 });

        Logger.ExitFunction("ConvertTriangle", $"returned triangle with vertices [{triVerts[0].Index},{triVerts[1].Index},{triVerts[2].Index}]");
        return newTri;
    }

    private void InsertVertex(int idx)
    {
        Logger.EnterFunction("InsertVertex", $"idx={idx}");
        Logger.Info($"Inserting vertex {idx} at position ({vertices[idx].Position.X}, {vertices[idx].Position.Y})");

        List<int> badTriangles = new List<int>();
        int badTriangleCount = 0;

        for (int tIdx = 0; tIdx < triangles.Count; tIdx++)
        {
            Triangle t = triangles[tIdx];
            if (!t.alive) continue;

            if (InCircle(vertices[t.vertices[0]], vertices[t.vertices[1]], vertices[t.vertices[2]], vertices[idx]))
            {
                Logger.Debug($"Vertex {idx} is inside circumcircle of triangle {tIdx}, marking as bad");
                t.alive = false;
                badTriangles.Add(tIdx);
                RemoveTriangleFromMap(tIdx);
                badTriangleCount++;
            }
        }

        Logger.Info($"Found {badTriangleCount} bad triangles to remove");

        if (badTriangles.Count == 0)
        {
            Logger.Info("No bad triangles found, vertex insertion complete");
            Logger.ExitFunction("InsertVertex");
            return;
        }

        Dictionary<EdgeKey, (int, int)> boundary = new Dictionary<EdgeKey, (int, int)>();
        foreach (int tIdx in badTriangles)
        {
            Triangle t = triangles[tIdx];
            for (int e = 0; e < 3; e++)
            {
                int a = t.vertices[e];
                int b = t.vertices[(e + 1) % 3];
                EdgeKey sortedEdge = new EdgeKey { a = Math.Min(a, b), b = Math.Max(a, b) };
                var found = boundary.TryGetValue(sortedEdge, out (int, int) value);
                if (found)
                {
                    boundary.Remove(sortedEdge);
                    Logger.Debug($"Removing shared edge ({a},{b}) from boundary");
                }
                else
                {
                    boundary.Add(sortedEdge, (a, b));
                    Logger.Debug($"Adding boundary edge ({a},{b})");
                }
            }
        }

        Logger.Info($"Created boundary with {boundary.Count} edges");

        // Connect new point to each boundary edge to form new triangles
        foreach (var kvp in boundary)
        {
            var pair = kvp.Value;
            AddTriangle(idx, pair.Item1, pair.Item2);
        }

        Logger.ExitFunction("InsertVertex");
    }

    private void AddTriangle(int ia, int ib, int ic)
    {
        Triangle t = new Triangle();
        if (!IsCounterClockwise(vertices[ia], vertices[ib], vertices[ic]))
        {
            ib = ib + ic;
            ic = ib - ic;
            ib = ib - ic;
        }
        t.vertices = new int[] { ia, ib, ic };
        t.neighbors = new int[] { -1, -1, -1 };
        t.constrained = new bool[] { false, false, false };
        t.alive = true;

        int triIndex = triangles.Count;
        triangles.Add(t);
        for (int e = 0; e < 3; e++)
        {
            int a = triangles[triIndex].vertices[e];
            int b = triangles[triIndex].vertices[(e + 1) % 3];
            EdgeKey sortedEdge = new EdgeKey { a = Math.Min(a, b), b = Math.Max(a, b) };
            var found = edgeLookup.TryGetValue(sortedEdge, out EdgeRecord record);
            if (!found)
            {
                edgeLookup.Add(sortedEdge, new EdgeRecord { tri = triIndex, edge = e });
            }
            else
            {
                int otherTri = record.tri;
                int otherEdge = record.edge;
                triangles[triIndex].neighbors[e] = otherTri;
                triangles[otherTri].neighbors[otherEdge] = triIndex;
                edgeLookup.Remove(sortedEdge);
            }
        }

        for (int edge = 0; edge < 3; edge++)
        {
            LegalizeEdge(triIndex, edge);
        }
    }

    private void LegalizeEdge(int startTri, int startEdge)
    {
        var stack = new Stack<(int tri, int edge)>();
        stack.Push((startTri, startEdge));
        int safety = 0;
        while (stack.Count > 0 && safety++ < 200000)
        {
            var (triIndex, edge) = stack.Pop();
            if (triIndex < 0 || triIndex >= triangles.Count) continue;
            if (edge < 0 || edge > 2) continue;
            if (!triangles[triIndex].alive) continue;
            if (triangles[triIndex].constrained[edge]) continue;

            int neighbor = triangles[triIndex].neighbors[edge];
            if (neighbor < 0 || neighbor >= triangles.Count || !triangles[neighbor].alive) continue;

            int a = triangles[triIndex].vertices[edge];
            int b = triangles[triIndex].vertices[(edge + 1) % 3];
            int c = triangles[triIndex].vertices[(edge + 2) % 3];

            int neighborEdge = FindNeighborEdge(neighbor, triIndex);
            if (neighborEdge < 0) continue;
            if (triangles[neighbor].constrained[neighborEdge]) continue;

            int d = triangles[neighbor].vertices[(neighborEdge + 2) % 3];

            if (InCircle(vertices[a], vertices[b], vertices[c], vertices[d]))
            {
                FlipEdge(triIndex, edge);
                // After flipping, find the shared edge indices again
                int triShared = FindNeighborEdge(triIndex, neighbor);
                int neiShared = FindNeighborEdge(neighbor, triIndex);
                if (triShared >= 0)
                {
                    stack.Push((triIndex, (triShared + 1) % 3));
                    stack.Push((triIndex, (triShared + 2) % 3));
                }
                if (neiShared >= 0)
                {
                    stack.Push((neighbor, (neiShared + 1) % 3));
                    stack.Push((neighbor, (neiShared + 2) % 3));
                }
            }
        }
    }

    private void FlipEdge(int triIndex, int edge)
    {
        int neighbor = triangles[triIndex].neighbors[edge];
        if (neighbor < 0) return;

        int triEdge = edge;
        int neiEdge = FindNeighborEdge(neighbor, triIndex);
        if (neiEdge < 0) return;

        Triangle t = triangles[triIndex];
        Triangle n = triangles[neighbor];
        int a = t.vertices[(triEdge + 2) % 3];
        int b = t.vertices[triEdge];
        int c = t.vertices[(triEdge + 1) % 3];
        int d = n.vertices[(neiEdge + 2) % 3];

        // Remove old shared edge (b,c) if present
        EdgeKey oldKey = new EdgeKey { a = Math.Min(b, c), b = Math.Max(b, c) };
        if (edgeLookup.ContainsKey(oldKey)) edgeLookup.Remove(oldKey);

        // Perform flip: new triangles are (a,b,d) and (d,b,c)
        t.vertices = new int[] { a, b, d };
        n.vertices = new int[] { d, b, c };

        UpdateNeighborsAfterFlip(triIndex, triEdge, neighbor, neiEdge);

        // Register new edges into map for potential pairing
        EdgeKey keyT = new EdgeKey { a = Math.Min(b, d), b = Math.Max(b, d) };
        edgeLookup[keyT] = new EdgeRecord { tri = triIndex, edge = triEdge == 0 ? 1 : 0 };
        EdgeKey keyN = new EdgeKey { a = Math.Min(d, c), b = Math.Max(d, c) };
        edgeLookup[keyN] = new EdgeRecord { tri = neighbor, edge = neiEdge == 0 ? 1 : 0 };
    }

    private void UpdateNeighborsAfterFlip(int triIndex, int triEdge, int neighbor, int neiEdge)
    {
        Triangle t = triangles[triIndex];
        Triangle n = triangles[neighbor];

        int oppT = t.neighbors[(triEdge + 2) % 3];
        int oppN = n.neighbors[(neiEdge + 2) % 3];
        int tOther = n.neighbors[(neiEdge + 1) % 3];
        int nOther = t.neighbors[(triEdge + 1) % 3];

        t.neighbors = new int[] { neighbor, oppT, tOther };
        n.neighbors = new int[] { triIndex, oppN, nOther };

        // Fix back-links on adjacent triangles
        if (tOther >= 0)
        {
            for (int e = 0; e < 3; e++)
            {
                if (triangles[tOther].neighbors[e] == neighbor)
                {
                    triangles[tOther].neighbors[e] = triIndex;
                    break;
                }
            }
        }
        if (nOther >= 0)
        {
            for (int e = 0; e < 3; e++)
            {
                if (triangles[nOther].neighbors[e] == triIndex)
                {
                    triangles[nOther].neighbors[e] = neighbor;
                    break;
                }
            }
        }

        t.constrained[0] = false;
        n.constrained[0] = false;
    }

    private void SetNeighbor(int triangle, int newNeighbor, int edge)
    {
        Triangle t = triangles[triangle];
        for (int e = 0; e < 3; e++)
        {
            if (t.neighbors[e] == -1) continue;
            if (t.neighbors[e] == newNeighbor)
            {
                t.neighbors[e] = newNeighbor;
                return;
            }
        }
        t.neighbors[edge] = newNeighbor;
    }

    private int FindNeighborEdge(int tri, int neighbor)
    {
        Triangle t = triangles[tri];
        for (int e = 0; e < 3; e++)
        {
            if (t.neighbors[e] == neighbor) return e;
        }
        return -1;
    }

    private void RemoveTriangleFromMap(int triIndex)
    {
        for (int e = 0; e < 3; e++)
        {
            int a = triangles[triIndex].vertices[e];
            int b = triangles[triIndex].vertices[(e + 1) % 3];

            EdgeKey sortedEdge = new EdgeKey { a = Math.Min(a, b), b = Math.Max(a, b) };
            var found = edgeLookup.TryGetValue(sortedEdge, out EdgeRecord record);
            if (found && record.tri == triIndex)
            {
                edgeLookup.Remove(sortedEdge);
            }
        }
    }

    private void BuildSuperTriangle()
    {
        float minX = vertices[0].Position.X;
        float minY = vertices[0].Position.Y;
        float maxX = vertices[0].Position.X;
        float maxY = vertices[0].Position.Y;

        foreach (Point p in vertices)
        {
            minX = Mathf.Min(minX, p.Position.X);
            minY = Mathf.Min(minY, p.Position.Y);
            maxX = Mathf.Max(maxX, p.Position.X);
            maxY = Mathf.Max(maxY, p.Position.Y);
        }
        float dx = maxX - minX;
        float dy = maxY - minY;
        float delta = Mathf.Max(dx, dy) * 10f;

        Vector2 c = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 p0 = new Vector2(c.X - 2f * delta, c.Y - delta);
        Vector2 p1 = new Vector2(c.X, c.Y + 2f * delta);
        Vector2 p2 = new Vector2(c.X + 2f * delta, c.Y - delta);

        superVerts = new int[] { vertices.Count, vertices.Count + 1, vertices.Count + 2 };
        vertices.Add(new Point { Position = new Vector3(p0.X, p0.Y, 0f), Index = 0 });
        vertices.Add(new Point { Position = new Vector3(p1.X, p1.Y, 0f), Index = 0 });
        vertices.Add(new Point { Position = new Vector3(p2.X, p2.Y, 0f), Index = 0 });

        Triangle super = new Triangle();
        super.vertices = superVerts;
        super.neighbors = new int[] { -1, -1, -1 };
        super.constrained = new bool[] { false, false, false };
        super.alive = true;
        triangles.Add(super);
    }

    private void RecoverConstrainedEdge(int a, int b)
    {
        Logger.EnterFunction("RecoverConstrainedEdge", $"a={a}, b={b}");
        if (a == b) return;
        if (IsEdgePresent(a, b))
        {
            MarkConstraint(a, b);
            return;
        }

        Stack<(int, int)> intersecting = new Stack<(int, int)>();
        FindIntersectingEdges(a, b, intersecting);

        int guard = 0;
        while (!IsEdgePresent(a, b))
        {
            if (guard++ > 1000) break;
            GD.PrintRaw($"\nGuard: {guard}");
            if (intersecting.Count == 0)
            {
                FindIntersectingEdges(a, b, intersecting);
                if (intersecting.Count == 0)
                {
                    Logger.ExitFunction("RecoverConstrainedEdge", $"No intersecting edges found");
                    break;
                }
            }
            var intersection = intersecting.Pop();
            var triangle = intersection.Item1;
            var edge = intersection.Item2;
            Logger.Debug($"Recovering constrained edge between vertices {a} and {b} from triangle {triangle} edge {edge}");
            if (!triangles[triangle].alive) continue;
            if (triangles[triangle].constrained[edge]) continue;

            int neighbor = triangles[triangle].neighbors[edge];
            if (neighbor < 0 || !triangles[neighbor].alive) continue;
            if (!SegmentsIntersectProperly(vertices[triangles[triangle].vertices[edge]],
                        vertices[triangles[triangle].vertices[(edge + 1) % 3]],
                        vertices[a], vertices[b])) continue;

            FlipEdge(triangle, edge);
            LegalizeEdge(triangle, edge);
            LegalizeEdge(neighbor, FindNeighborEdge(neighbor, triangle));

            FindIntersectingEdges(a, b, intersecting);
        }
        MarkConstraint(a, b);
    }

    private void FindIntersectingEdges(int a, int b, Stack<(int, int)> intersecting)
    {
        Logger.EnterFunction("FindIntersectingEdges", $"a={a}, b={b} | {intersecting.Count} intersecting edges");
        intersecting.Clear();
        for (int i = 0; i < triangles.Count; i++)
        {
            Triangle t = triangles[i];
            if (!t.alive) continue;
            for (int e = 0; e < 3; e++)
            {
                if (t.constrained[e]) continue;
                int v0 = t.vertices[e];
                int v1 = t.vertices[(e + 1) % 3];
                if (v0 == a || v0 == b || v1 == a || v1 == b) continue;
                if (SegmentsIntersectProperly(vertices[v0], vertices[v1], vertices[a], vertices[b]))
                {
                    intersecting.Push((i, e));
                }
            }
        }
        Logger.ExitFunction("FindIntersectingEdges", $"{intersecting.Count} intersecting edges found");
    }

    private bool IsEdgePresent(int a, int b)
    {
        Logger.EnterFunction("IsEdgePresent", $"a={a}, b={b}");
        foreach (Triangle t in triangles)
        {
            if (!t.alive) continue;
            for (int e = 0; e < 3; e++)
            {
                int v0 = t.vertices[e];
                int v1 = t.vertices[(e + 1) % 3];
                if ((v0 == a && v1 == b) || (v0 == b && v1 == a))
                {
                    Logger.ExitFunction("IsEdgePresent", $"Edge {a},{b} is in triangle {t}");
                    return true;
                }
            }
        }
        Logger.ExitFunction("IsEdgePresent", $"Edge {a},{b} is not in any triangle");
        return false;
    }

    private void MarkConstraint(int a, int b)
    {
        Logger.EnterFunction("MarkConstraint", $"a={a}, b={b}");
        foreach (Triangle t in triangles)
        {
            if (!t.alive) continue;
            for (int e = 0; e < 3; e++)
            {
                int v0 = t.vertices[e];
                int v1 = t.vertices[(e + 1) % 3];
                if ((v0 == a && v1 == b) || (v0 == b && v1 == a))
                {
                    t.constrained[e] = true;
                }
            }
        }
    }

    private void PurgeSuperTriangle()
    {
        HashSet<int> superSet = new HashSet<int>() { superVerts[0], superVerts[1], superVerts[2] };
        for (int t = 0; t < triangles.Count; t++)
        {
            if (!triangles[t].alive) continue;
            foreach (int v in triangles[t].vertices)
            {
                if (superSet.Contains(v))
                {
                    Triangle t2 = triangles[t];
                    t2.alive = false;
                    break;
                }
            }
        }
    }

    private void FloodFill(int seedTriangle, List<bool> insideList)
    {
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(seedTriangle);
        insideList[seedTriangle] = true;

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            Triangle t = triangles[current];
            for (int e = 0; e < 3; e++)
            {
                if (t.constrained[e]) continue;
                int neighbor = t.neighbors[e];
                if (neighbor < 0 || !triangles[neighbor].alive) continue;
                if (!insideList[neighbor])
                {
                    insideList[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    private int LocateTriangle(Point p)
    {
        foreach (Triangle t in triangles)
        {
            if (!t.alive) continue;
            if (PointInTriangle(vertices[t.vertices[0]], vertices[t.vertices[1]], vertices[t.vertices[2]], p))
            {
                return triangles.IndexOf(t);
            }
        }
        return -1;
    }
}
