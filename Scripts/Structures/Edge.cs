using System;
using Godot;
using static MeshGeneration.StructureDatabase;
namespace Structures;
public enum EdgeType
{
    inactive, transform, divergent, convergent
}
public class Edge : IEquatable<Edge>
{
    HalfEdge[] halfEdges = new HalfEdge[2];
    public EdgeKey key;
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

    public Point P { get { return halfEdges[0].From; } set { halfEdges[0].From = (Point)value; } }
    public Point Q { get { return halfEdges[1].From; } set { halfEdges[1].From = (Point)value; } }
    public int Index { get; set; }

    public int ContinentIndex { get; set; }
    public EdgeType Type { get; set; }
    public EdgeStress Stress { get; set; }

    public float StressMagnitude { get; set; } = 0.0f;

    public Vector3 Midpoint { get { return new Vector3((P.Position.X + Q.Position.X) / 2.0f, (P.Position.Y + Q.Position.Y) / 2.0f, (P.Position.Z + Q.Position.Z) / 2.0f); } }

    public Edge(int e, HalfEdge e1, HalfEdge e2)
    {
        Index = e;
        halfEdges[0] = e1;
        halfEdges[1] = e2;
        Type = EdgeType.inactive;
    }

    public Edge(HalfEdge e1, HalfEdge e2)
    {
        halfEdges[0] = e1;
        halfEdges[1] = e2;
        Index = DefineIndex(P, Q);
        Type = EdgeType.inactive;
    }

    public static Edge MakeEdge(int index, Point p, Point q)
    {
        EdgeKey key = EdgeKey.From(p, q);
        HalfEdge e1 = new HalfEdge(p, q, key);
        HalfEdge e2 = new HalfEdge(q, p, key);
        e1.Twin = e2;
        e2.Twin = e1;
        Edge e = new Edge(index, e1, e2);
        e.key = key;
        return e;
    }
    public static Edge MakeEdge(Point p, Point q)
    {
        EdgeKey key = EdgeKey.From(p, q);
        HalfEdge e1 = new HalfEdge(p, q, key);
        HalfEdge e2 = new HalfEdge(q, p, key);
        e1.Twin = e2;
        e2.Twin = e1;
        Edge e = new Edge(e1, e2);
        e.key = key;
        return e;
    }

    public bool Equals(Edge other)
    {
        return other.Index == Index;
    }

    public static bool operator ==(Edge e1, Edge e2)
    {
        if (e1 is null || e2 is null) return false;
        return e1.Equals(e2);
    }

    public static bool operator !=(Edge e1, Edge e2)
    {
        if (e1 is null || e2 is null) return false;
        return !e1.Equals(e2);
    }
    public override bool Equals(Object obj)
    {
        if (obj is null) return false;
        if (obj is Edge e) return Equals(e);
        return false;
    }

    public override int GetHashCode()
    {
        return this.Index.GetHashCode();
    }

    public override string ToString() => $"Edge ({Index}, {P}, {Q})";

}
