using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Structures.MeshGeneration;

public partial class Face : Resource
{
    public Point[] v;
    public Edge[] e;

    public Face(Point v0, Point v1, Point v2, Edge e0, Edge e1, Edge e2)
    {
        v = new Point[] { v0, v1, v2 };
        e = new Edge[] { e0, e1, e2 };
    }
    public Face(Edge e0, Edge e1, Edge e2, params Point[] points)
    {
        v = points;
        e = new Edge[] { e0, e1, e2 };
    }
    public Face(Point v0, Point v1, Point v2, params Edge[] edges)
    {
        v = new Point[] { v0, v1, v2 };
        e = edges;
    }
    public Face(IEnumerable<Point> points)
    {
        v = points.ToArray();
    }

    public override string ToString() => $"Face: ({v[0]}, {v[1]}, {v[2]})";
}

