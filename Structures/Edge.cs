using System;
using Godot;
using static MeshGeneration.StructureDatabase;
namespace Structures;
public enum EdgeType
{
    inactive, transform, divergent, convergent
}
public class Edge : IEdge, IEquatable<Edge>
{
    private Point _p;
    private Point _q;

    public static int DefineIndex(Point p, Point q)
    {
        int pix = BitConverter.SingleToInt32Bits(p.Position.X);
        int piy = BitConverter.SingleToInt32Bits(p.Position.Y);
        int piz = BitConverter.SingleToInt32Bits(p.Position.Z);
        int qix = BitConverter.SingleToInt32Bits(q.Position.X);
        int qiy = BitConverter.SingleToInt32Bits(q.Position.Y);
        int qiz = BitConverter.SingleToInt32Bits(q.Position.Z);
        return HashCode.Combine(pix, piy, piz, qix, qiy, qiz);
    }
    public static Edge AddEdge(Point p1, Point p2)
    {
        Edge returnEdge = new Edge(p1, p2);
        if (!Edges.ContainsKey(returnEdge.Index))
        {
            Edges.Add(returnEdge.Index, returnEdge);
        }
        else
        {
            returnEdge = Edges[returnEdge.Index];
        }
        return returnEdge;
    }

    public IPoint P { get { return _p; } set { _p = (Point)value; } }
    public IPoint Q { get { return _q; } set { _q = (Point)value; } }
    public int Index { get; set; }

    public int ContinentIndex { get; set; }
    public EdgeType Type { get; set; }
    public EdgeStress Stress { get; set; }

    public float StressMagnitude { get; set; } = 0.0f;

    public Vector3 Midpoint { get { return new Vector3((_p.Position.X + _q.Position.X) / 2.0f, (_p.Position.Y + _q.Position.Y) / 2.0f, (_p.Position.Z + _q.Position.Z) / 2.0f); } }

    public Edge(int e, Point p, Point q)
    {
        Index = e;
        P = p;
        Q = q;
        Type = EdgeType.inactive;
    }

    public Edge(Point p, Point q)
    {
        P = p;
        Q = q;
        Index = DefineIndex(p, q);
        Type = EdgeType.inactive;
    }

    public bool Equals(Edge other)
    {
        return P == other.P && Q == other.Q;
    }

    public Edge ReverseEdge()
    {
        return new Edge(_q, _p);
    }

    public static bool operator ==(Edge e1, Edge e2)
    {
        return e1.Equals(e2);
    }

    public static bool operator !=(Edge e1, Edge e2)
    {
        return !e1.Equals(e2);
    }
    public override bool Equals(Object obj)
    {
        return false;
    }

    public override int GetHashCode()
    {
        return this.Index.GetHashCode();
    }

    public override string ToString() => $"Edge ({Index}, {P}, {Q})";

}
