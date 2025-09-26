using System;
using System.Collections.Generic;
using Godot;
using Structures;
using UtilityLibrary;

namespace MeshGeneration;
public class StructureDatabase
{
    public enum MeshState
    {
        Ungenerated = 0, BaseMesh = 1, DualMesh = 2
    }
    private object lockObject = new object();

    //Starts at Ungenerated and increments after a Mesh has been generated
    public MeshState state = MeshState.Ungenerated;
    public Dictionary<(Point, Point, Point), Triangle> BaseTris = new Dictionary<(Point, Point, Point), Triangle>();
    public Dictionary<Point, HashSet<Edge>> HalfEdgesFrom = new Dictionary<Point, HashSet<Edge>>();
    public Dictionary<Point, HashSet<Edge>> HalfEdgesTo = new Dictionary<Point, HashSet<Edge>>();

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

    public void IncrementMeshState()
    {
        state = (MeshState)((int)state + 1);
    }

    public Edge[] GetEdgesFromPoint(Point p)
    {
        Logger.EnterFunction("GetEdgesFromPoint", $"pIndex={p.Index}");

        HashSet<Edge> EdgesFrom = new HashSet<Edge>();
        switch (state)
        {
            case MeshState.BaseMesh:
                var found = HalfEdgesFrom.TryGetValue(p, out HashSet<Edge> baseEdgesFromPoint);
                if (found)
                {
                    foreach (Edge e in baseEdgesFromPoint)
                    {
                        EdgesFrom.Add(e);
                    }
                }
                found = HalfEdgesTo.TryGetValue(p, out HashSet<Edge> baseEdgesToPoint);
                if (found)
                {
                    foreach (Edge e in baseEdgesToPoint)
                    {
                        EdgesFrom.Add(e);
                    }
                }
                break;
            case MeshState.DualMesh:
                found = worldHalfEdgeMapFrom.TryGetValue(p, out Dictionary<Point, Edge> edgesFromPoint);
                if (found)
                {
                    foreach (Edge e in edgesFromPoint.Values)
                    {
                        EdgesFrom.Add(e);
                    }
                }
                found = worldHalfEdgeMapTo.TryGetValue(p, out Dictionary<Point, Edge> edgesToPoint);
                if (found)
                {
                    foreach (Edge e in edgesToPoint.Values)
                    {
                        EdgesFrom.Add(e);
                    }
                }
                break;
        }
        List<Edge> edges = new List<Edge>(EdgesFrom);
        Logger.ExitFunction("GetEdgesFromPoint", $"returned {edges.Count} edges");
        return edges.ToArray();
    }

    public void AddPoint(Point point)
    {
        Logger.EnterFunction("AddPoint", $"MeshState: {state}, pointIndex={point.Index}");
        lock (lockObject)
        {
            switch (state)
            {
                case MeshState.Ungenerated:
                    Logger.EnterFunction("AddPointBaseMesh", $"pointIndex={point.Index}");
                    lock (lockObject)
                    {
                        VertexPoints.Add(point.Index, point);
                    }
                    Logger.ExitFunction("AddPointBaseMesh");
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
        Edge returnEdge = new Edge(p1, p2);
        AddEdge(returnEdge);
        return returnEdge;
    }
    public Edge AddEdge(Point p1, Point p2, int index)
    {
        Logger.EnterFunction("AddEdge", $"MeshState: {state}, From {p1} to {p2} with index {index}");
        Edge returnEdge = new Edge(index, p1, p2);
        AddEdge(returnEdge);
        return returnEdge;
    }
    public void AddEdge(Edge edge)
    {
        Logger.EnterFunction("AddEdge", $"MeshState: {state}, edgeIndex={edge.Index}");
        lock (lockObject)
        {
            switch (state)
            {
                case MeshState.Ungenerated:
                    Edges.Add(edge.Index, edge);
                    if (HalfEdgesFrom.ContainsKey((Point)edge.P))
                    {
                        HalfEdgesFrom[(Point)edge.P].Add(edge);
                    }
                    else
                    {
                        HalfEdgesFrom.Add((Point)edge.P, new HashSet<Edge>());
                        HalfEdgesFrom[(Point)edge.P].Add(edge);
                    }
                    if (HalfEdgesTo.ContainsKey((Point)edge.Q))
                    {
                        HalfEdgesTo[(Point)edge.Q].Add(edge);
                    }
                    else
                    {
                        HalfEdgesTo.Add((Point)edge.Q, new HashSet<Edge>());
                        HalfEdgesTo[(Point)edge.Q].Add(edge);
                    }
                    break;
                case MeshState.BaseMesh:
                    break;
                case MeshState.DualMesh:
                    break;
            }
        }
    }
    public void AddTriangle(Triangle triangle)
    {
        Logger.EnterFunction("AddTriangle", $"MeshState: {state}, triIndex={triangle.Index}");
        lock (lockObject)
        {
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
    }

    public void UpdatePointBaseMesh(Point point, Point newPoint)
    {
        Logger.EnterFunction("UpdatePointBaseMesh", $"pointIndex={point.Index} -> newIndex={newPoint.Index}");
        lock (lockObject)
        {
            VertexPoints[point.Index] = newPoint;
        }
        Logger.ExitFunction("UpdatePointBaseMesh");
    }

    public Edge UpdateWorldEdgeMap(Point p1, Point p2)
    {
        Logger.EnterFunction("UpdateWorldEdgeMap", $"p1={p1.Index}, p2={p2.Index}");
        Edge generatedEdge = new Edge(p1, p2);
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
        Logger.EnterFunction("UpdateEdge", $"edgeIndex={edge.Index} -> newIndex={newEdge.Index}");
        lock (lockObject)
        {
            HalfEdgesFrom[(Point)edge.P].Remove(edge);
            HalfEdgesTo[(Point)edge.Q].Remove(edge);
            Edges[edge.Index] = newEdge;
            if (HalfEdgesFrom.ContainsKey((Point)newEdge.P))
            {
                HalfEdgesFrom[(Point)newEdge.P].Add(newEdge);
            }
            else
            {
                HalfEdgesFrom.Add((Point)newEdge.P, new HashSet<Edge>());
                HalfEdgesFrom[(Point)newEdge.P].Add(newEdge);
            }
            if (HalfEdgesTo.ContainsKey((Point)newEdge.Q))
            {
                HalfEdgesTo[(Point)newEdge.Q].Add(newEdge);
            }
            else
            {
                HalfEdgesTo.Add((Point)newEdge.Q, new HashSet<Edge>());
                HalfEdgesTo[(Point)newEdge.Q].Add(newEdge);
            }
        }
        Logger.ExitFunction("UpdateEdge");
    }

    public void UpdateTriangle(Triangle triangle, Triangle newTriangle)
    {
        Logger.EnterFunction("UpdateTriangle", $"triIndex={triangle.Index} -> newIndex={newTriangle.Index}");
        lock (lockObject)
        {
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
            Edges.Remove(edge.Index);
            EdgeTriangles.Remove(edge);
            HalfEdgesFrom.Remove((Point)edge.P);
            HalfEdgesTo.Remove((Point)edge.Q);
        }
        Logger.ExitFunction("RemoveEdge");
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
}
