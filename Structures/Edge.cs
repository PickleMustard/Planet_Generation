using System;
namespace Structures;
public class Edge : IEdge, IEquatable<Edge>
{
    public static int DefineIndex(Point p, Point q) {
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
        if (!GenerateDocArrayMesh.Edges.ContainsKey(returnEdge.Index))
        {
            GenerateDocArrayMesh.Edges.Add(returnEdge.Index, returnEdge);
        }
        else
        {
            returnEdge = GenerateDocArrayMesh.Edges[returnEdge.Index];
        }
        return returnEdge;
    }
    public Point P { get; set; }
    public Point Q { get; set; }
    public int Index { get; set; }
    public float Stress { get; set; }

    public Edge(int e, Point p, Point q)
    {
        Index = e;
        P = p;
        Q = q;
    }

    public Edge(Point p, Point q) {
        P = p;
        Q = q;
        Index = DefineIndex(p, q);
    }

    public bool Equals(Edge other)
    {
        return P == other.P && Q == other.Q;
    }

    public Edge ReverseEdge() {
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

    public override string ToString() => $"Edge ({Index}, {P}, {Q})";

}
