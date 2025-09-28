using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures;
using UtilityLibrary;

namespace MeshGeneration;
public class StructureDatabase
{
    public int Index { get; }
    public enum MeshState
    {
        Ungenerated = 0, BaseMesh = 1, DualMesh = 2
    }
    private object lockObject = new object();

    //Starts at Ungenerated and increments after a Mesh has been generated
    public MeshState state = MeshState.Ungenerated;
    public Dictionary<(Point, Point, Point), Triangle> BaseTris = new Dictionary<(Point, Point, Point), Triangle>();
    public Dictionary<Point, HashSet<Edge>> HalfEdgesFrom = new Dictionary<Point, HashSet<Edge>>();

    public Dictionary<int, Point> VertexPoints = new Dictionary<int, Point>();
    public Dictionary<int, Edge> Edges = new Dictionary<int, Edge>();
    public Dictionary<Edge, List<Triangle>> EdgeTriangles = new Dictionary<Edge, List<Triangle>>();
    public Dictionary<int, Point> circumcenters = new Dictionary<int, Point>();

    public List<VoronoiCell> VoronoiCells = new List<VoronoiCell>();
    public HashSet<Point> VoronoiCellVertices = new HashSet<Point>();
    public Dictionary<Point, HashSet<VoronoiCell>> CellMap = new Dictionary<Point, HashSet<VoronoiCell>>();
    public Dictionary<Edge, HashSet<VoronoiCell>> EdgeMap = new Dictionary<Edge, HashSet<VoronoiCell>>();
    public Dictionary<Point, HashSet<Triangle>> VoronoiTriMap = new Dictionary<Point, HashSet<Triangle>>();
    public Dictionary<Edge, HashSet<Triangle>> VoronoiEdgeTriMap = new Dictionary<Edge, HashSet<Triangle>>();
    public Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapFrom = new Dictionary<Point, Dictionary<Point, Edge>>();
    public Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapTo = new Dictionary<Point, Dictionary<Point, Edge>>();

    // Canonical registries (Phase 0)
    internal Dictionary<int, Point> PointsById = new Dictionary<int, Point>();
    internal Dictionary<int, HalfEdge> HalfEdgeById = new Dictionary<int, HalfEdge>();
    internal Dictionary<EdgeKey, int> UndirectedEdgeIndex = new Dictionary<EdgeKey, int>();
    internal Dictionary<int, Triangle> TrianglesById = new Dictionary<int, Triangle>();
    internal Dictionary<EdgeKey, List<Triangle>> TrianglesByEdgeKey = new Dictionary<EdgeKey, List<Triangle>>();
    internal Dictionary<Point, HashSet<HalfEdge>> OutHalfEdgesByPoint = new Dictionary<Point, HashSet<HalfEdge>>();
    internal Dictionary<EdgeKey, HashSet<VoronoiCell>> EdgeKeyCellMap = new Dictionary<EdgeKey, HashSet<VoronoiCell>>();

    // Phase 3–4: Internal containers and validation toggle
    public bool EnableValidation { get; set; } = true;

    internal BaseContainer Base { get; }
    internal DualContainer Dual { get; }

    public StructureDatabase(int index)
    {
        Index = index;
        Base = new BaseContainer(this);
        Dual = new DualContainer(this);
    }

    public void IncrementMeshState()
    {
        state = (MeshState)((int)state + 1);
    }

    public void Validate(string stage)
    {
        if (!EnableValidation) return;
        Logger.EnterFunction("Validate", $"stage={stage}");
        try
        {
            int pointCount = VertexPoints?.Count ?? 0;
            int edgeCount = Edges?.Count ?? 0;
            int triCount = BaseTris?.Count ?? 0;
            Logger.Info($"Validate[{stage}]: points={pointCount}, edges={edgeCount}, baseTris={triCount}");

            int indexMismatches = 0;
            foreach (var kv in VertexPoints)
            {
                if (kv.Key != kv.Value.Index) indexMismatches++;
            }
            if (indexMismatches > 0)
            {
                Logger.Info($"Validate[{stage}]: point index mismatches={indexMismatches}");
            }

            int overfullEdges = 0;
            foreach (var kv in EdgeTriangles)
            {
                if (kv.Value.Count > 2) overfullEdges++;
            }
            if (overfullEdges > 0)
            {
                Logger.Info($"Validate[{stage}]: edges with >2 triangles={overfullEdges}");
            }

            int asymmetry = 0;
            foreach (var from in worldHalfEdgeMapFrom)
            {
                foreach (var kv in from.Value)
                {
                    if (!worldHalfEdgeMapTo.TryGetValue(kv.Key, out var dict) || !dict.ContainsKey(from.Key))
                        asymmetry++;
                }
            }
            if (asymmetry > 0)
            {
                Logger.Info($"Validate[{stage}]: dual adjacency asymmetry cases={asymmetry}");
            }
        }
        catch (Exception e)
        {
            Logger.Error($"Validate[{stage}] error: {e.Message}\n{e.StackTrace}", "ERROR");
        }
        Logger.ExitFunction("Validate");
    }

    // ===== Phase 1: Facade APIs =====
    public Point GetOrCreatePoint(Vector3 pos)
    {
        int idx = Point.DetermineIndex(pos.X, pos.Y, pos.Z);
        return GetOrCreatePoint(idx, pos);
    }

    public Point GetOrCreatePoint(int index, Vector3 pos)
    {
        lock (lockObject)
        {
            if (PointsById.TryGetValue(index, out var existing))
            {
                return existing;
            }
            Point p = new Point(pos, index);
            PointsById[index] = p;
            // Mirror legacy
            if (!VertexPoints.ContainsKey(index)) VertexPoints[index] = p;
            return p;
        }
    }

    public bool TryGetEdge(Point a, Point b, out Edge edge)
    {
        lock (lockObject)
        {
            // Prefer canonical half-edges
            if (OutHalfEdgesByPoint.TryGetValue(a, out var set))
            {
                foreach (var h in set)
                {
                    if (h.To == b)
                    {
                        int idx = Edge.DefineIndex(a, b);
                        if (Edges.TryGetValue(idx, out edge))
                        {
                            return true;
                        }
                        break;
                    }
                }
            }
            // Fallback to legacy HalfEdgesFrom
            if (HalfEdgesFrom.TryGetValue(a, out var legacySet))
            {
                foreach (var e in legacySet)
                {
                    if (e.P == a && e.Q == b)
                    {
                        edge = e;
                        return true;
                    }
                }
            }
            // Fallback to legacy Edges by index
            int index = Edge.DefineIndex(a, b);
            if (Edges.TryGetValue(index, out edge))
            {
                return true;
            }
            edge = null;
            return false;
        }
    }

    public Edge GetOrCreateEdge(Point a, Point b)
    {
        lock (lockObject)
        {
            if (TryGetEdge(a, b, out var found)) return found;
            // Create new legacy edge and mirror canonicals
            var e = new Edge(a, b);
            RegisterEdgeCanonicalAndLegacy(e);
            return e;
        }
    }

    public Edge GetOrCreateEdge(Point a, Point b, int index)
    {
        lock (lockObject)
        {
            if (TryGetEdge(a, b, out var found)) return found;
            var e = new Edge(index, a, b);
            RegisterEdgeCanonicalAndLegacy(e);
            return e;
        }
    }

    public Edge[] GetIncidentHalfEdges(Point p)
    {
        // Temporary facade that returns legacy directed Edge objects
        Logger.EnterFunction("GetIncidentHalfEdges", $"pIndex={p.Index}");
        try
        {
            lock (lockObject)
            {
                HashSet<Edge> result = new HashSet<Edge>();
                // Prefer HalfEdgesFrom when available
                if (HalfEdgesFrom.TryGetValue(p, out var edgesFrom) && edgesFrom.Count > 0)
                {
                    foreach (var e in edgesFrom) result.Add(e);
                }
                else
                {
                    // Preserve current logic using world maps in DualMesh
                    switch (state)
                    {
                        case MeshState.BaseMesh:
                            if (HalfEdgesFrom.TryGetValue(p, out var baseEdges))
                            {
                                foreach (var e in baseEdges) result.Add(e);
                            }
                            break;
                        case MeshState.DualMesh:
                            if (worldHalfEdgeMapFrom.TryGetValue(p, out var fromDict))
                            {
                                foreach (var e in fromDict.Values) result.Add(e);
                            }
                            if (worldHalfEdgeMapTo.TryGetValue(p, out var toDict))
                            {
                                foreach (var e in toDict.Values) result.Add(e);
                            }
                            break;
                    }
                }
                var arr = result.ToArray();
                Logger.ExitFunction("GetIncidentHalfEdges", $"returned {arr.Length} edges");
                return arr;
            }
        }
        catch
        {
            Logger.ExitFunction("GetIncidentHalfEdges", "returned 0 edges (exception)");
            return Array.Empty<Edge>();
        }
    }

    public Triangle AddTriangle(List<Point> points)
    {
        // Wires edges, updates registries + legacy (base maps)
        if (points == null || points.Count != 3) throw new ArgumentException("Triangle requires exactly 3 points");
        lock (lockObject)
        {
            // Ensure points exist
            foreach (var p in points)
            {
                if (!PointsById.ContainsKey(p.Index)) PointsById[p.Index] = p;
                if (!VertexPoints.ContainsKey(p.Index)) VertexPoints[p.Index] = p;
            }
            // Create/wire edges in order
            var e0 = GetOrCreateEdge(points[0], points[1]);
            var e1 = GetOrCreateEdge(points[1], points[2]);
            var e2 = GetOrCreateEdge(points[2], points[0]);

            // Allocate next non-negative triangle id
            int newId = 0;
            while (newId < int.MaxValue && TrianglesById.ContainsKey(newId)) newId++;
            var tri = new Triangle(newId, new List<Point>(points), new List<Edge> { e0, e1, e2 });

            RegisterTriangleInRegistriesOnly(tri);

            // Mirror legacy maps for base mesh
            BaseTris[(points[0], points[1], points[2])] = tri;
            if (!EdgeTriangles.ContainsKey(e0)) EdgeTriangles[e0] = new List<Triangle>();
            if (!EdgeTriangles.ContainsKey(e1)) EdgeTriangles[e1] = new List<Triangle>();
            if (!EdgeTriangles.ContainsKey(e2)) EdgeTriangles[e2] = new List<Triangle>();
            EdgeTriangles[e0].Add(tri);
            EdgeTriangles[e1].Add(tri);
            EdgeTriangles[e2].Add(tri);

            return tri;
        }
    }

    public IEnumerable<Triangle> GetIncidentTriangles(Point p)
    {
        lock (lockObject)
        {
            HashSet<Triangle> result = new HashSet<Triangle>();
            // Prefer canonical via incident edges
            var edges = GetIncidentHalfEdges(p);
            foreach (var e in edges)
            {
                if (EdgeTriangles.TryGetValue(e, out var list))
                {
                    foreach (var t in list) result.Add(t);
                }
            }
            return result;
        }
    }

    public List<Triangle> GetTrianglesByEdgeIndex(int index)
    {
        lock (lockObject)
        {
            List<Triangle> result = new List<Triangle>();
            if (Edges.TryGetValue(index, out var edge))
            {
                var key = EdgeKey.From(edge.P, edge.Q);
                if (TrianglesByEdgeKey.TryGetValue(key, out var list))
                {
                    result.AddRange(list);
                    return result;
                }
            }
            // Fallback legacy path: union of both directions by index
            if (Edges.TryGetValue(index, out var e1))
            {
                if (EdgeTriangles.TryGetValue(e1, out var l1)) result.AddRange(l1);
                var rev = e1.ReverseEdge();
                if (EdgeTriangles.TryGetValue(rev, out var l2)) result.AddRange(l2);
            }
            return result;
        }
    }

    public bool FlipEdge(Edge e, out Triangle t1, out Triangle t2)
    {
        lock (lockObject)
        {
            var key = EdgeKey.From(e.P, e.Q);
            t1 = null; t2 = null;
            if (TrianglesByEdgeKey.TryGetValue(key, out var list))
            {
                if (list.Count == 2)
                {
                    t1 = list[0];
                    t2 = list[1];
                    return true;
                }
                if (list.Count == 1)
                {
                    t1 = list[0];
                }
                return false;
            }
            // Legacy fallback
            List<Triangle> tmp = new List<Triangle>();
            if (EdgeTriangles.TryGetValue(e, out var l1)) tmp.AddRange(l1);
            var rev = e.ReverseEdge();
            if (EdgeTriangles.TryGetValue(rev, out var l2)) tmp.AddRange(l2);
            if (tmp.Count == 2)
            {
                t1 = tmp[0]; t2 = tmp[1];
                return true;
            }
            if (tmp.Count == 1) t1 = tmp[0];
            return false;
        }
    }

    // ===== Legacy shims routed through registries =====
    public Edge[] GetEdgesFromPoint(Point p)
    {
        Logger.EnterFunction("GetEdgesFromPoint", $"pIndex={p.Index}");

        var edges = GetIncidentHalfEdges(p);
        Logger.ExitFunction("GetEdgesFromPoint", $"returned {edges.Length} edges");
        return edges;
    }

    public void AddPoint(Point point)
    {
        Logger.EnterFunction("AddPoint", $"MeshState: {state}, pointIndex={point.Index}");
        lock (lockObject)
        {
            // Registry first
            if (!PointsById.ContainsKey(point.Index)) PointsById[point.Index] = point;
            if (!VertexPoints.ContainsKey(point.Index)) VertexPoints.Add(point.Index, point);

            // Preserve legacy structure/actions by state (currently only Ungenerated branch mutates)
            switch (state)
            {
                case MeshState.Ungenerated:
                    // Legacy path already mirrored above
                    break;
                case MeshState.BaseMesh:
                    break;
                case MeshState.DualMesh:
                    break;
            }
        }
    }

    public Edge AddEdge(Point p1, Point p2)
    {
        Logger.EnterFunction("AddEdge", $"MeshState: {state}, From {p1} to {p2}");
        Edge returnEdge = GetOrCreateEdge(p1, p2);
        Logger.ExitFunction("AddEdge", $"edgeIndex={returnEdge.Index}");
        return returnEdge;
    }
    public Edge AddEdge(Point p1, Point p2, int index)
    {
        Logger.EnterFunction("AddEdge", $"MeshState: {state}, From {p1} to {p2} with index {index}");
        Edge returnEdge = GetOrCreateEdge(p1, p2, index);
        Logger.ExitFunction("AddEdge", $"edgeIndex={returnEdge.Index}");
        return returnEdge;
    }
    public void AddEdge(Edge edge)
    {
        Logger.EnterFunction("AddEdge", $"MeshState: {state}, edgeIndex={edge.Index}, reverse={edge.ReverseEdge().Index}");
        lock (lockObject)
        {
            RegisterEdgeCanonicalAndLegacy(edge);
        }
        Logger.ExitFunction("AddEdge");
    }
    public void AddTriangle(Triangle triangle)
    {
        Logger.EnterFunction("AddTriangle", $"MeshState: {state}, triIndex={triangle.Index}");
        lock (lockObject)
        {
            // Registry first
            RegisterTriangleInRegistriesOnly(triangle);

            // Legacy mirroring by state to preserve behavior
            switch (state)
            {
                case MeshState.Ungenerated:
                    BaseTris.Add(((Point)triangle.Points[0], (Point)triangle.Points[1], (Point)triangle.Points[2]), triangle);
                    if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[0])) EdgeTriangles[(Edge)triangle.Edges[0]] = new List<Triangle>();
                    if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[1])) EdgeTriangles[(Edge)triangle.Edges[1]] = new List<Triangle>();
                    if (!EdgeTriangles.ContainsKey((Edge)triangle.Edges[2])) EdgeTriangles[(Edge)triangle.Edges[2]] = new List<Triangle>();
                    EdgeTriangles[(Edge)triangle.Edges[0]].Add(triangle);
                    EdgeTriangles[(Edge)triangle.Edges[1]].Add(triangle);
                    EdgeTriangles[(Edge)triangle.Edges[2]].Add(triangle);
                    Logger.Info($"Added triangle {triangle.Index} to BaseTris and EdgeTriangles");
                    break;
                case MeshState.BaseMesh:
                    foreach (Point p in triangle.Points)
                    {
                        var found = VoronoiTriMap.TryGetValue(p, out HashSet<Triangle> value);
                        if (!found) { VoronoiTriMap.Add(p, new HashSet<Triangle>()); }
                        VoronoiTriMap[p].Add(triangle);
                    }
                    foreach (Edge e in triangle.Edges)
                    {
                        var found = VoronoiEdgeTriMap.TryGetValue(e, out HashSet<Triangle> value);
                        if (!found) { VoronoiEdgeTriMap.Add(e, new HashSet<Triangle>()); }
                        VoronoiEdgeTriMap[e].Add(triangle);
                        UpdateWorldEdgeMap((Point)e.P, (Point)e.Q);
                    }
                    break;
                case MeshState.DualMesh:
                    break;
            }
        }
        Logger.ExitFunction("AddTriangle");
    }

    public void UpdatePointBaseMesh(Point point, Point newPoint)
    {
        Logger.EnterFunction("UpdatePointBaseMesh", $"pointIndex={point.Index} -> newIndex={newPoint.Index}");
        lock (lockObject)
        {
            VertexPoints[point.Index] = newPoint;
            PointsById[newPoint.Index] = newPoint;
        }
        Logger.ExitFunction("UpdatePointBaseMesh");
    }

    public Edge UpdateWorldEdgeMap(Point p1, Point p2)
    {
        Logger.EnterFunction("UpdateWorldEdgeMap", $"p1={p1.Index}, p2={p2.Index}");
        Edge generatedEdge = new Edge(p1, p2);
        // Ensure canonical/legacy half-edges exist
        AddEdge(generatedEdge);

        Edge toUpdate = null;
        if (!MapContains(worldHalfEdgeMapTo, p1))
        {
            worldHalfEdgeMapTo.Add(p1, new Dictionary<Point, Edge>());
        }
        if (!MapContains(worldHalfEdgeMapFrom, p2))
        {
            worldHalfEdgeMapFrom.Add(p2, new Dictionary<Point, Edge>());
        }
        if (MapContains(worldHalfEdgeMapTo, p1, p2))
        {
            toUpdate = worldHalfEdgeMapTo[p1][p2];
        }
        else
        {
            worldHalfEdgeMapTo[p1].Add(p2, generatedEdge);
            toUpdate = generatedEdge;
        }
        if (MapContains(worldHalfEdgeMapFrom, p2, p1))
        {
            toUpdate = worldHalfEdgeMapFrom[p2][p1];
        }
        else
        {
            worldHalfEdgeMapFrom[p2].Add(p1, generatedEdge);
            toUpdate = generatedEdge;
        }
        Logger.ExitFunction("UpdateWorldEdgeMap", $"returned edgeIndex={toUpdate.Index}");
        return toUpdate;
    }

    public void UpdateEdge(Edge edge, Edge newEdge)
    {
        Logger.EnterFunction("UpdateEdge", $"edgeIndex={edge.Index} | reverse={edge.ReverseEdge().Index} -> newIndex={newEdge.Index} | reverse={newEdge.ReverseEdge().Index}");
        lock (lockObject)
        {
            // Remove canonical half-edges for old edge
            RemoveCanonicalHalfEdges(edge.P, edge.Q);

            // Legacy update
            if (HalfEdgesFrom.ContainsKey((Point)edge.P))
            {
                HalfEdgesFrom[(Point)edge.P].Remove(edge);
            }
            if (HalfEdgesFrom.ContainsKey((Point)edge.Q))
            {
                HalfEdgesFrom[(Point)edge.Q].Remove(edge);
            }
            Edges[edge.Index] = newEdge;
            Edges[edge.ReverseEdge().Index] = newEdge.ReverseEdge();
            if (HalfEdgesFrom.ContainsKey((Point)newEdge.P))
            {
                HalfEdgesFrom[(Point)newEdge.P].Add(newEdge);
            }
            else
            {
                HalfEdgesFrom.Add((Point)newEdge.P, new HashSet<Edge>());
                HalfEdgesFrom[(Point)newEdge.P].Add(newEdge);
            }
            if (HalfEdgesFrom.ContainsKey((Point)newEdge.Q))
            {
                HalfEdgesFrom[(Point)newEdge.Q].Add(newEdge.ReverseEdge());
            }
            else
            {
                HalfEdgesFrom.Add((Point)newEdge.Q, new HashSet<Edge>());
                HalfEdgesFrom[(Point)newEdge.Q].Add(newEdge.ReverseEdge());
            }

            // Recreate canonical half-edges for new edge
            RegisterEdgeCanonicalAndLegacy(newEdge);
        }
        Logger.ExitFunction("UpdateEdge");
    }

    public void UpdateTriangle(Triangle triangle, Triangle newTriangle)
    {
        Logger.EnterFunction("UpdateTriangle", $"triIndex={triangle.Index} -> newIndex={newTriangle.Index}");
        lock (lockObject)
        {
            // Canonical: replace in registry and edge-key map
            if (TrianglesById.ContainsKey(triangle.Index))
            {
                TrianglesById[triangle.Index] = newTriangle;
            }
            foreach (var e in triangle.Edges)
            {
                var key = EdgeKey.From(e.P, e.Q);
                if (TrianglesByEdgeKey.TryGetValue(key, out var list))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].Index == triangle.Index) { list[i] = newTriangle; }
                    }
                }
            }

            // Legacy mirrors
            EdgeTriangles[(Edge)triangle.Edges[0]].Remove(triangle);
            EdgeTriangles[(Edge)triangle.Edges[1]].Remove(triangle);
            EdgeTriangles[(Edge)triangle.Edges[2]].Remove(triangle);
            BaseTris[((Point)triangle.Points[0], (Point)triangle.Points[1], (Point)triangle.Points[2])] = newTriangle;
            EdgeTriangles[(Edge)triangle.Edges[0]].Add(newTriangle);
            EdgeTriangles[(Edge)triangle.Edges[1]].Add(newTriangle);
            EdgeTriangles[(Edge)triangle.Edges[2]].Add(newTriangle);
        }
        Logger.ExitFunction("UpdateTriangle");
    }

    public Point SelectRandomPoint(RandomNumberGenerator rand)
    {
        Logger.EnterFunction("SelectRandomPoint");
        lock (lockObject)
        {
            List<Point> points = new List<Point>(VertexPoints.Values);
            Point selected = points[rand.RandiRange(0, points.Count - 1)];
            Logger.ExitFunction("SelectRandomPoint", $"returned pointIndex={selected.Index}");
            return selected;
        }
    }

    public void RemoveEdge(Edge edge)
    {
        Logger.EnterFunction("RemoveEdge", $"edgeIndex={edge.Index}");
        lock (lockObject)
        {
            // Canonical removal
            RemoveCanonicalHalfEdges(edge.P, edge.Q);

            // Legacy behavior (preserved)
            Edges.Remove(edge.Index);
            Edges.Remove(edge.ReverseEdge().Index);
            EdgeTriangles.Remove(edge);
            EdgeTriangles.Remove(edge.ReverseEdge());
            HalfEdgesFrom.Remove((Point)edge.P);
            HalfEdgesFrom.Remove((Point)edge.Q);
        }
        Logger.ExitFunction("RemoveEdge");
    }

    // ===== Phase 1: Minimal helpers for later phases =====
    public Point GetOrCreateCircumcenter(int index, Vector3 pos)
    {
        lock (lockObject)
        {
            if (circumcenters.TryGetValue(index, out var p)) return p;
            var point = new Point(pos, index);
            circumcenters[index] = point;
            if (!PointsById.ContainsKey(index)) PointsById[index] = point;
            if (!VertexPoints.ContainsKey(index)) VertexPoints[index] = point;
            return point;
        }
    }

    public void AddCellForVertex(Point p, VoronoiCell cell)
    {
        lock (lockObject)
        {
            if (!CellMap.TryGetValue(p, out var set))
            {
                set = new HashSet<VoronoiCell>();
                CellMap[p] = set;
            }
            set.Add(cell);
            VoronoiCellVertices.Add(p);
        }
    }

    public void AddCellForEdge(EdgeKey key, VoronoiCell cell)
    {
        lock (lockObject)
        {
            if (!EdgeKeyCellMap.TryGetValue(key, out var set))
            {
                set = new HashSet<VoronoiCell>();
                EdgeKeyCellMap[key] = set;
            }
            set.Add(cell);

            // Mirror to legacy EdgeMap only if a matching legacy key (same endpoints, any instance) exists
            if (VertexPoints.TryGetValue(key.A, out var pa) && VertexPoints.TryGetValue(key.B, out var pb))
            {
                Edge matchAB = null;
                Edge matchBA = null;
                foreach (var kv in EdgeMap)
                {
                    var e = kv.Key;
                    if (e.P == pa && e.Q == pb) matchAB = e;
                    else if (e.P == pb && e.Q == pa) matchBA = e;
                    if (matchAB != null && matchBA != null) break;
                }
                if (matchAB != null)
                {
                    if (!EdgeMap[matchAB].Contains(cell)) EdgeMap[matchAB].Add(cell);
                }
                if (matchBA != null)
                {
                    if (!EdgeMap[matchBA].Contains(cell)) EdgeMap[matchBA].Add(cell);
                }
            }
        }
    }

    // ===== Phase 3–4: Phase reset hook =====
    public void ResetPhase(MeshState target)
    {
        Logger.EnterFunction("ResetPhase", $"target={target}");
        lock (lockObject)
        {
            switch (target)
            {
                case MeshState.Ungenerated:
                    // No-op
                    break;
                case MeshState.BaseMesh:
                    // Reset dual-related containers
                    worldHalfEdgeMapFrom.Clear();
                    worldHalfEdgeMapTo.Clear();
                    VoronoiCells.Clear();
                    VoronoiCellVertices.Clear();
                    CellMap.Clear();
                    EdgeMap.Clear();
                    VoronoiTriMap.Clear();
                    VoronoiEdgeTriMap.Clear();
                    break;
                case MeshState.DualMesh:
                    // No destructive reset at entry to dual
                    break;
            }
        }
        Logger.ExitFunction("ResetPhase");
    }

    // ===== Helpers =====
    private void RegisterEdgeCanonicalAndLegacy(Edge e)
    {
        // Points registry
        if (!PointsById.ContainsKey(e.P.Index)) PointsById[e.P.Index] = e.P;
        if (!PointsById.ContainsKey(e.Q.Index)) PointsById[e.Q.Index] = e.Q;
        if (!VertexPoints.ContainsKey(e.P.Index)) VertexPoints[e.P.Index] = e.P;
        if (!VertexPoints.ContainsKey(e.Q.Index)) VertexPoints[e.Q.Index] = e.Q;

        // Canonical half-edges (both directions, twin-linked)
        EnsureHalfEdgePair(e.P, e.Q);

        // Canonical undirected index (do not overwrite once present)
        var key = EdgeKey.From(e.P, e.Q);
        if (!UndirectedEdgeIndex.ContainsKey(key)) UndirectedEdgeIndex[key] = e.Index;

        // Legacy Edges map should contain both directions
        if (!Edges.ContainsKey(e.Index)) Edges[e.Index] = e;
        var rev = e.ReverseEdge();
        if (!Edges.ContainsKey(rev.Index)) Edges[rev.Index] = rev;

        // Legacy HalfEdgesFrom symmetry
        if (!HalfEdgesFrom.TryGetValue(e.P, out var setFrom)) { setFrom = new HashSet<Edge>(); HalfEdgesFrom[e.P] = setFrom; }
        setFrom.Add(e);
        if (!HalfEdgesFrom.TryGetValue(e.Q, out var setFromQ)) { setFromQ = new HashSet<Edge>(); HalfEdgesFrom[e.Q] = setFromQ; }
        setFromQ.Add(rev);
    }

    private void RegisterTriangleInRegistriesOnly(Triangle tri)
    {
        if (!TrianglesById.ContainsKey(tri.Index)) TrianglesById[tri.Index] = tri;
        // Ensure all edges have canonical entries and register edge->triangle
        for (int i = 0; i < 3; i++)
        {
            var e = tri.Edges[i];
            RegisterEdgeCanonicalAndLegacy(e);
            var key = EdgeKey.From(e.P, e.Q);
            if (!TrianglesByEdgeKey.TryGetValue(key, out var list))
            {
                list = new List<Triangle>();
                TrianglesByEdgeKey[key] = list;
            }
            list.Add(tri);

            // Ensure half-edges exist and optionally set Left for forward orientation
            var hf = FindHalfEdge(e.P, e.Q);
            if (hf != null && hf.Left == null) hf.Left = tri;
        }
    }

    private void EnsureHalfEdgePair(Point a, Point b)
    {
        var fwd = FindHalfEdge(a, b);
        var rev = FindHalfEdge(b, a);
        if (fwd != null && rev != null)
        {
            if (fwd.Twin == null) fwd.Twin = rev;
            if (rev.Twin == null) rev.Twin = fwd;
            return;
        }

        // Create missing ones
        if (fwd == null) fwd = CreateHalfEdge(a, b);
        if (rev == null) rev = CreateHalfEdge(b, a);
        fwd.Twin = rev;
        rev.Twin = fwd;
    }

    private HalfEdge CreateHalfEdge(Point from, Point to)
    {
        int id = HalfEdgeById.Count;
        var h = new HalfEdge(id, from, to);
        HalfEdgeById[id] = h;
        if (!OutHalfEdgesByPoint.TryGetValue(from, out var set))
        {
            set = new HashSet<HalfEdge>();
            OutHalfEdgesByPoint[from] = set;
        }
        set.Add(h);
        return h;
    }

    private HalfEdge FindHalfEdge(Point from, Point to)
    {
        if (OutHalfEdgesByPoint.TryGetValue(from, out var set))
        {
            foreach (var h in set)
            {
                if (h.To == to) return h;
            }
        }
        return null;
    }

    private void RemoveCanonicalHalfEdges(Point a, Point b)
    {
        var fwd = FindHalfEdge(a, b);
        var rev = FindHalfEdge(b, a);
        if (fwd != null)
        {
            if (OutHalfEdgesByPoint.TryGetValue(a, out var set)) set.Remove(fwd);
            HalfEdgeById.Remove(fwd.Id);
        }
        if (rev != null)
        {
            if (OutHalfEdgesByPoint.TryGetValue(b, out var set2)) set2.Remove(rev);
            HalfEdgeById.Remove(rev.Id);
        }
    }

    private static bool MapContains(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1)
    {
        return map.ContainsKey(p1);
    }
    private static bool MapContains(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1, Point p2)
    {
        if (map.ContainsKey(p1))
        {
            return map[p1].ContainsKey(p2);
        }
        return false;
    }

    private static bool MapIndexCreated(Dictionary<Point, Dictionary<Point, Edge>> map, Point p1)
    {
        return map[p1] != null;
    }

    // ===== Phase 3–4: Legacy read-only views =====
    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<int, Point> LegacyVertexPoints => new System.Collections.ObjectModel.ReadOnlyDictionary<int, Point>(VertexPoints);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<int, Edge> LegacyEdges => new System.Collections.ObjectModel.ReadOnlyDictionary<int, Edge>(Edges);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<Edge, List<Triangle>> LegacyEdgeTriangles => new System.Collections.ObjectModel.ReadOnlyDictionary<Edge, List<Triangle>>(EdgeTriangles);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<Point, HashSet<Edge>> LegacyHalfEdgesFrom => new System.Collections.ObjectModel.ReadOnlyDictionary<Point, HashSet<Edge>>(HalfEdgesFrom);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<(Point, Point, Point), Triangle> LegacyBaseTris => new System.Collections.ObjectModel.ReadOnlyDictionary<(Point, Point, Point), Triangle>(BaseTris);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>> LegacyWorldHalfEdgeMapFrom => new System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>>(worldHalfEdgeMapFrom);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>> LegacyWorldHalfEdgeMapTo => new System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>>(worldHalfEdgeMapTo);

    [Obsolete("Use canonical registries and facades; this view is read-only.")]
    public System.Collections.ObjectModel.ReadOnlyDictionary<int, Point> LegacyCircumcenters => new System.Collections.ObjectModel.ReadOnlyDictionary<int, Point>(circumcenters);

    // ===== Phase 3–4: Internal container types =====
    public sealed class BaseContainer
    {
        private readonly StructureDatabase db;
        internal BaseContainer(StructureDatabase db) { this.db = db; }

        public System.Collections.ObjectModel.ReadOnlyDictionary<(Point, Point, Point), Triangle> Triangles =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<(Point, Point, Point), Triangle>(db.BaseTris);

        public System.Collections.ObjectModel.ReadOnlyDictionary<Point, HashSet<Edge>> OutHalfEdges =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Point, HashSet<Edge>>(db.HalfEdgesFrom);
    }

    public sealed class DualContainer
    {
        private readonly StructureDatabase db;
        internal DualContainer(StructureDatabase db) { this.db = db; }

        public System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>> WorldFrom =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>>(db.worldHalfEdgeMapFrom);

        public System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>> WorldTo =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Point, Dictionary<Point, Edge>>(db.worldHalfEdgeMapTo);

        public System.Collections.ObjectModel.ReadOnlyDictionary<Edge, HashSet<VoronoiCell>> EdgeCells =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Edge, HashSet<VoronoiCell>>(db.EdgeMap);

        public System.Collections.ObjectModel.ReadOnlyDictionary<Point, HashSet<VoronoiCell>> CellMap =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Point, HashSet<VoronoiCell>>(db.CellMap);
    }
}

