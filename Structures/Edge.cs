using System;
using Godot;
using static MeshGeneration.StructureDatabase;
namespace Structures;
public enum EdgeType
{
    joined, transform, divergent, convergent
}
public class Edge : IEdge, IEquatable<Edge>
{
    public static int DefineIndex(Point p, Point q)
    {
        int pix = BitConverter.SingleToInt32Bits(p.X);
        int piy = BitConverter.SingleToInt32Bits(p.Y);
        int piz = BitConverter.SingleToInt32Bits(p.Z);
        int qix = BitConverter.SingleToInt32Bits(q.X);
        int qiy = BitConverter.SingleToInt32Bits(q.Y);
        int qiz = BitConverter.SingleToInt32Bits(q.Z);
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
    public Point P { get; set; }
    public Point Q { get; set; }
    public int Index { get; set; }

    public int ContinentIndex { get; set; }
    public EdgeType Type { get; set; }
    public float CalculatedStress { get; set; }
    public float PropogatedStress { get; set; }
    public float TotalStress { get { return CalculatedStress + PropogatedStress; } }

    public Vector3 Midpoint { get { return new Vector3((P.X + Q.X) / 2.0f, (P.Y + Q.Y) / 2.0f, (P.Z + Q.Z) / 2.0f); } }

    public Edge(int e, Point p, Point q)
    {
        Index = e;
        P = p;
        Q = q;
        Type = EdgeType.joined;
    }

    public Edge(Point p, Point q)
    {
        P = p;
        Q = q;
        Index = DefineIndex(p, q);
        Type = EdgeType.joined;
    }

    public bool Equals(Edge other)
    {
        return P == other.P && Q == other.Q;
    }

    public Edge ReverseEdge()
    {
        return new Edge(Q, P);
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
