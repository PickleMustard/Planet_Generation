using System;
public class Edge : IEdge, IEquatable<Edge>
{
    public Point P { get; set; }
    public Point Q { get; set; }
    public int Index { get; set; }

    public Edge(int e, Point p, Point q)
    {
        Index = e;
        P = p;
        Q = q;
    }

    public bool Equals(Edge other)
    {
        return other.Index == Index && P == other.P && Q == other.Q;
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
