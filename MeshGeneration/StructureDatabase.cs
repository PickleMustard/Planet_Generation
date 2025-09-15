using System;
using System.Collections.Generic;
using Godot;
using Structures;

namespace MeshGeneration;
public static class StructureDatabase
{
    private static object lockObject = new object();

    public static Dictionary<(Point, Point, Point), Triangle> BaseTris = new Dictionary<(Point, Point, Point), Triangle>();
    public static Dictionary<Point, HashSet<Edge>> HalfEdgesFrom = new Dictionary<Point, HashSet<Edge>>();
    public static Dictionary<Point, HashSet<Edge>> HalfEdgesTo = new Dictionary<Point, HashSet<Edge>>();

    public static Dictionary<int, Point> VertexPoints = new Dictionary<int, Point>();
    public static Dictionary<int, Edge> Edges = new Dictionary<int, Edge>();
    public static Dictionary<Edge, List<Triangle>> EdgeTriangles = new Dictionary<Edge, List<Triangle>>();
    public static Dictionary<int, Point> circumcenters = new Dictionary<int, Point>();

    public static List<VoronoiCell> VoronoiCells = new List<VoronoiCell>();
    public static Dictionary<Point, HashSet<VoronoiCell>> CellMap = new Dictionary<Point, HashSet<VoronoiCell>>();
    public static Dictionary<Edge, HashSet<VoronoiCell>> EdgeMap = new Dictionary<Edge, HashSet<VoronoiCell>>();
    public static Dictionary<Point, HashSet<Triangle>> VoronoiTriMap = new Dictionary<Point, HashSet<Triangle>>();
    public static Dictionary<Edge, HashSet<Triangle>> VoronoiEdgeTriMap = new Dictionary<Edge, HashSet<Triangle>>();
    public static Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapFrom = new Dictionary<Point, Dictionary<Point, Edge>>();
    public static Dictionary<Point, Dictionary<Point, Edge>> worldHalfEdgeMapTo = new Dictionary<Point, Dictionary<Point, Edge>>();

    public static void AddTriangle(Triangle triangle)
    {
        lock (lockObject)
        {
            BaseTris.Add((triangle.Points[0], triangle.Points[1], triangle.Points[2]), triangle);
            EdgeTriangles[triangle.Edges[0]].Add(triangle);
            EdgeTriangles[triangle.Edges[1]].Add(triangle);
            EdgeTriangles[triangle.Edges[2]].Add(triangle);
        }
    }

    public static void AddEdge(Edge edge)
    {
        lock (lockObject)
        {
            Edges.Add(edge.Index, edge);
            if (HalfEdgesFrom.ContainsKey(edge.P))
            {
                HalfEdgesFrom[edge.P].Add(edge);
            }
            else
            {
                HalfEdgesFrom.Add(edge.P, new HashSet<Edge>());
                HalfEdgesFrom[edge.P].Add(edge);
            }
            if (HalfEdgesTo.ContainsKey(edge.Q))
            {
                HalfEdgesTo[edge.Q].Add(edge);
            }
            else
            {
                HalfEdgesTo.Add(edge.Q, new HashSet<Edge>());
                HalfEdgesTo[edge.Q].Add(edge);
            }
        }
    }
    public static void AddPoint(Point point)
    {
        lock (lockObject)
        {
            VertexPoints.Add(point.Index, point);
        }
    }

    public static void UpdatePoint(Point point, Point newPoint)
    {
        lock (lockObject)
        {
            //GD.PrintRaw($"Updating Point {point} to {newPoint}\n");
            VertexPoints[point.Index] = newPoint;
        }
    }

    public static Edge UpdateWorldEdgeMap(Point p1, Point p2)
    {
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
        return toUpdate;
    }

    public static void UpdateEdge(Edge edge, Edge newEdge)
    {
        lock (lockObject)
        {
            HalfEdgesFrom[edge.P].Remove(edge);
            HalfEdgesTo[edge.Q].Remove(edge);
            Edges[edge.Index] = newEdge;
            if (HalfEdgesFrom.ContainsKey(newEdge.P))
            {
                HalfEdgesFrom[newEdge.P].Add(newEdge);
            }
            else
            {
                HalfEdgesFrom.Add(edge.P, new HashSet<Edge>());
                HalfEdgesFrom[edge.P].Add(edge);
            }
            if (HalfEdgesTo.ContainsKey(newEdge.Q))
            {
                HalfEdgesTo[newEdge.Q].Add(newEdge);
            }
            else
            {
                HalfEdgesTo.Add(edge.Q, new HashSet<Edge>());
                HalfEdgesTo[edge.Q].Add(edge);
            }
        }
    }

    public static void UpdateTriangle(Triangle triangle, Triangle newTriangle)
    {
        lock (lockObject)
        {
            EdgeTriangles[triangle.Edges[0]].Remove(triangle);
            EdgeTriangles[triangle.Edges[1]].Remove(triangle);
            EdgeTriangles[triangle.Edges[2]].Remove(triangle);
            BaseTris[(triangle.Points[0], triangle.Points[1], triangle.Points[2])] = newTriangle;
            EdgeTriangles[triangle.Edges[0]].Add(newTriangle);
            EdgeTriangles[triangle.Edges[1]].Add(newTriangle);
            EdgeTriangles[triangle.Edges[2]].Add(newTriangle);
        }
    }

    public static Point SelectRandomPoint(RandomNumberGenerator rand)
    {
        lock (lockObject)
        {
            List<Point> points = new List<Point>(VertexPoints.Values);
            return points[rand.RandiRange(0, points.Count - 1)];
        }
    }

    public static void RemoveEdge(Edge edge){
        lock (lockObject) {
            Edges.Remove(edge.Index);
            EdgeTriangles.Remove(edge);
            HalfEdgesFrom.Remove(edge.P);
            HalfEdgesTo.Remove(edge.Q);
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

