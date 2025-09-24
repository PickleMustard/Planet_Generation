using System;
using System.Collections.Generic;
using System.Threading;
using Godot;
using Structures;
using UtilityLibrary;

namespace MeshGeneration;
public static class StructureDatabase
{
    private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

    public static Dictionary<(Point, Point, Point), Triangle> BaseTris = new Dictionary<(Point, Point, Point), Triangle>();
    public static Dictionary<int, HashSet<Edge>> HalfEdgesFrom = new Dictionary<int, HashSet<Edge>>();

    public static Dictionary<int, Point> VertexPoints = new Dictionary<int, Point>();
    public static Dictionary<int, Edge> Edges = new Dictionary<int, Edge>();
    public static Dictionary<Edge, HashSet<Triangle>> EdgeTriangles = new Dictionary<Edge, HashSet<Triangle>>();
    public static Dictionary<int, Point> circumcenters = new Dictionary<int, Point>();

    public static List<VoronoiCell> VoronoiCells = new List<VoronoiCell>();
    public static HashSet<Point> VoronoiCellVertices = new HashSet<Point>();
    public static Dictionary<Point, HashSet<VoronoiCell>> CellMap = new Dictionary<Point, HashSet<VoronoiCell>>();
    public static Dictionary<Edge, HashSet<VoronoiCell>> EdgeMap = new Dictionary<Edge, HashSet<VoronoiCell>>();
    public static Dictionary<Point, HashSet<Triangle>> VoronoiTriMap = new Dictionary<Point, HashSet<Triangle>>();
    public static Dictionary<Edge, HashSet<Triangle>> VoronoiEdgeTriMap = new Dictionary<Edge, HashSet<Triangle>>();
    public static Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapFrom = new Dictionary<Point, Dictionary<Point, Edge>>();

    public static Edge[] GetHalfEdges(Point p)
    {
        semaphore.Wait();
        try
        {
            HashSet<Edge> EdgesFrom = null;
            if (HalfEdgesFrom.ContainsKey(p.Index))
            {
                EdgesFrom = HalfEdgesFrom[p.Index];
            }
            Edge[] edges = null;
            if (EdgesFrom != null)
            {
                edges = new Edge[EdgesFrom.Count];
                EdgesFrom.CopyTo(edges);
            }
            return edges;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static Edge[] GetEdgesFromPoint(Point p)
    {
        semaphore.Wait();
        try
        {
            Dictionary<Point, Edge> edgesFromPoint = worldHalfEdgeMapFrom[p];
            HashSet<Edge> EdgesFrom = new HashSet<Edge>();
            foreach (Edge e in edgesFromPoint.Values)
            {
                EdgesFrom.Add(e);
            }
            List<Edge> edges = new List<Edge>(EdgesFrom);
            return edges.ToArray();
        }
        finally
        {
            semaphore.Release();
        }
    }
    public static Triangle[] GetTrianglesFromEdge(Edge e)
    {
        semaphore.Wait();
        try
        {
            HashSet<Triangle> trianglesFromEdge = null;
            if (EdgeTriangles.ContainsKey(e))
            {
                trianglesFromEdge = EdgeTriangles[e];
            }
            Triangle[] triangles = null;
            if (trianglesFromEdge != null)
            {
                triangles = new Triangle[trianglesFromEdge.Count];
                trianglesFromEdge.CopyTo(triangles);
            }
            return triangles;

        }
        finally
        {
            semaphore.Release();
        }
    }
    public static void AddTriangle(Triangle triangle)
    {
        Logger.EnterFunction("StructureDatabase.AddTriangle", $"Triangle: {triangle.Points[0].Index} | {triangle.Points[1].Index} | {triangle.Points[2].Index}");
        semaphore.Wait();
        try
        {
            Logger.Debug($"Acquired semaphore for AddTriangle operation", "StructureDatabase");

            if (!BaseTris.ContainsKey((triangle.Points[0], triangle.Points[1], triangle.Points[2])))
            {
                BaseTris.Add((triangle.Points[0], triangle.Points[1], triangle.Points[2]), triangle);
                Logger.Info($"Added new triangle to BaseTris", "StructureDatabase");
            }
            else
            {
                Logger.Debug($"Triangle already exists in BaseTris, skipping addition", "StructureDatabase");
            }

            // Add edges to EdgeTriangles mapping
            for (int i = 0; i < 3; i++)
            {
                Edge edge = triangle.Edges[i];
                if (!EdgeTriangles.ContainsKey(edge))
                {
                    EdgeTriangles.Add(edge, new HashSet<Triangle>());
                    //Logger.Debug($"Created new EdgeTriangles entry for edge {edge.Index}", "StructureDatabase");
                }
                EdgeTriangles[edge].Add(triangle);
                Logger.Debug($"Added triangle to EdgeTriangles for edge {edge.Index}", "StructureDatabase");
            }

            Logger.Triangle($"Successfully added triangle with points {triangle.Points[0].Index} | {triangle.Points[1].Index} | {triangle.Points[2].Index}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in AddTriangle: {ex.Message}", "StructureDatabase");
            throw;
        }
        finally
        {
            semaphore.Release();
            Logger.Debug($"Released semaphore for AddTriangle operation", "StructureDatabase");
            Logger.ExitFunction("StructureDatabase.AddTriangle");
        }
    }

    public static Edge AddEdge(Point p, Point q)
    {
        Edge e = Edge.MakeEdge(p, q);
        AddEdge(e);
        return e;
    }

    public static void AddEdge(Edge edge)
    {
        semaphore.Wait();
        try
        {
            if (!Edges.ContainsKey(edge.Index))
            {
                Edges.Add(edge.Index, edge);
            }
            if (HalfEdgesFrom.ContainsKey(edge.P.Index))
            {
                HalfEdgesFrom[edge.P.Index].Add(edge);
            }
            else
            {
                HalfEdgesFrom.Add(edge.P.Index, new HashSet<Edge>());
                HalfEdgesFrom[edge.P.Index].Add(edge);
            }
            if (HalfEdgesFrom.ContainsKey(edge.Q.Index))
            {
                HalfEdgesFrom[edge.Q.Index].Add(edge);
            }
            else
            {
                HalfEdgesFrom.Add(edge.Q.Index, new HashSet<Edge>());
                HalfEdgesFrom[edge.Q.Index].Add(edge);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
    public static void AddPoint(Point point)
    {
        semaphore.Wait();
        try
        {
            VertexPoints.Add(point.Index, point);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static void UpdatePoint(Point point, Point newPoint)
    {
        semaphore.Wait();
        try
        {
            VertexPoints[point.Index] = newPoint;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static Edge UpdateWorldEdgeMap(Point origin, Point dest)
    {
        Edge generatedEdge = Edge.MakeEdge(origin, dest);
        Edge toUpdate = null;
        //Create Dictionaries for Half edge map if they don't exist
        if (!MapContains(worldHalfEdgeMapFrom, origin))
        {
            worldHalfEdgeMapFrom.Add(origin, new Dictionary<Point, Edge>());
        }
        if (!MapContains(worldHalfEdgeMapFrom, dest))
        {
            worldHalfEdgeMapFrom.Add(dest, new Dictionary<Point, Edge>());
        }
        //If the edge already exists, return the existing edge
        if (MapContains(worldHalfEdgeMapFrom, origin, dest))
        {
            toUpdate = worldHalfEdgeMapFrom[origin][dest];
        }
        else
        {
            worldHalfEdgeMapFrom[origin].Add(dest, generatedEdge);
            toUpdate = generatedEdge;
        }
        //Additionally, add the reverse edge
        if (!MapContains(worldHalfEdgeMapFrom, dest, origin))
        {
            worldHalfEdgeMapFrom[dest].Add(origin, generatedEdge);
        }
        return toUpdate;
    }

    public static void UpdateEdge(Edge edge, Edge newEdge)
    {
        semaphore.Wait();
        try
        {
            Edges[edge.Index] = newEdge;
            if (HalfEdgesFrom.ContainsKey(newEdge.P.Index))
            {
                HalfEdgesFrom[newEdge.P.Index].Add(newEdge);
            }
            else
            {
                HalfEdgesFrom.Add(edge.P.Index, new HashSet<Edge>());
                HalfEdgesFrom[edge.P.Index].Add(newEdge);
            }
            if (HalfEdgesFrom.ContainsKey(newEdge.Q.Index))
            {
                HalfEdgesFrom[newEdge.Q.Index].Add(newEdge);
            }
            else
            {
                HalfEdgesFrom.Add(edge.Q.Index, new HashSet<Edge>());
                HalfEdgesFrom[edge.Q.Index].Add(newEdge);
            }
            HalfEdgesFrom[edge.P.Index].Remove(edge);
            HalfEdgesFrom[edge.Q.Index].Remove(edge);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static void UpdateTriangle(Triangle triangle, Triangle newTriangle)
    {
        semaphore.Wait();
        try
        {
            BaseTris[(triangle.Points[0], triangle.Points[1], triangle.Points[2])] = newTriangle;
            if (!EdgeTriangles.ContainsKey(newTriangle.Edges[0]))
            {
                EdgeTriangles.Add(newTriangle.Edges[0], new HashSet<Triangle>());
            }
            if (!EdgeTriangles.ContainsKey(newTriangle.Edges[1]))
            {
                EdgeTriangles.Add(newTriangle.Edges[1], new HashSet<Triangle>());
            }
            if (!EdgeTriangles.ContainsKey(newTriangle.Edges[2]))
            {
                EdgeTriangles.Add(newTriangle.Edges[2], new HashSet<Triangle>());
            }
            EdgeTriangles[newTriangle.Edges[0]].Add(newTriangle);
            EdgeTriangles[newTriangle.Edges[1]].Add(newTriangle);
            EdgeTriangles[newTriangle.Edges[2]].Add(newTriangle);
            if (EdgeTriangles.ContainsKey(triangle.Edges[0]))
            {
                EdgeTriangles[triangle.Edges[0]].Remove(triangle);
            }
            if (EdgeTriangles.ContainsKey(triangle.Edges[1]))
            {
                EdgeTriangles[triangle.Edges[1]].Remove(triangle);
            }
            if (EdgeTriangles.ContainsKey(triangle.Edges[2]))
            {
                EdgeTriangles[triangle.Edges[2]].Remove(triangle);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static Point SelectRandomPoint(RandomNumberGenerator rand)
    {
        semaphore.Wait();
        try
        {
            Point[] points = new Point[VertexPoints.Count];
            VertexPoints.Values.CopyTo(points, 0);
            return points[rand.RandiRange(0, points.Length - 1)];
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static void RemoveEdge(Edge edge)
    {
        semaphore.Wait();
        try
        {
            Edges.Remove(edge.Index);
            EdgeTriangles.Remove(edge);
            HalfEdgesFrom[edge.P.Index].Remove(edge);
            HalfEdgesFrom[edge.Q.Index].Remove(edge);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static void ClearEdges()
    {
        semaphore.Wait();
        try
        {
            HalfEdgesFrom.Clear();
        }
        finally
        {
            semaphore.Release();
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
}

