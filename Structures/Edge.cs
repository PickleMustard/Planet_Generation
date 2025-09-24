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
    public static int DefineIndex(Point p, Point q)
    {
        int pix = BitConverter.SingleToInt32Bits(p.X);
        int piy = BitConverter.SingleToInt32Bits(p.Y);
        int piz = BitConverter.SingleToInt32Bits(p.Z);
        int qix = BitConverter.SingleToInt32Bits(q.X);
        int qiy = BitConverter.SingleToInt32Bits(q.Y);
        int qiz = BitConverter.SingleToInt32Bits(q.Z);
        int sum = pix + piy + piz + qix + qiy + qiz;
        //int time = BitConverter.SingleToInt32Bits(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return HashCode.Combine(sum, p.Index, q.Index);
    }
    public static Edge MakeEdge(Point p, Point q, int index)
    {
        Edge e = new Edge(0);
        e.Origin = p;
        p.Edge = e;

        e.Dual = MakeDualEdge(1);
        e.Dual.Dual = MakeSymEdge(2, q);
        e.Dual.Dual.Dual = MakeDualEdge(3);

        e.Next = e;
        e.Dual.Next = e.Tor();
        e.Sym().Next = e.Sym();
        e.Tor().Next = e.Rot();

        e.Index = index;
        e.Sym().Index = e.Index;
        return e;
    }
    public static Edge MakeEdge(Point p, Point q)
    {
        Edge e = new Edge(0);
        e.Origin = p;
        p.Edge = e;

        e.Dual = MakeDualEdge(1);
        e.Dual.Dual = MakeSymEdge(2, q);
        e.Dual.Dual.Dual = MakeDualEdge(3);
        e.Dual.Dual.Dual.Dual = e;

        e.Next = e;
        e.Dual.Next = e.Tor();
        e.Sym().Next = e.Sym();
        e.Tor().Next = e.Rot();

        e.Index = DefineIndex(e.P, e.Q);
        e.Sym().Index = e.Index;

        return e;
    }

    public static Edge Connect(Edge a, Edge b)
    {
        Edge e = MakeEdge(a.GetDestination(), b.Origin);
        Splice(e, a.Lnext());
        Splice(e.Sym(), b);
        return e;
    }

    public static Edge MakeDualEdge(int index)
    {
        return new Edge(index);
    }
    public static Edge MakeSymEdge(int index, Point dest)
    {
        return new Edge(index, dest);
    }
    public Point P { get { return Origin; } }
    public Point Q { get { return GetDestination(); } }
    public Point Origin { get; set; }
    public Point Destination { get { return Sym().Origin; } }
    public int QuadIndex { get; set; }
    public int Index { get; set; }
    public int ContinentIndex { get; set; }
    public EdgeType Type { get; set; }
    public EdgeStress Stress { get; set; }

    public float StressMagnitude { get; set; } = 0.0f;

    public Vector3 Midpoint { get { return new Vector3((P.Position.X + Q.Position.X) / 2.0f, (P.Position.Y + Q.Position.Y) / 2.0f, (P.Position.Z + Q.Position.Z) / 2.0f); } }

    public Edge Next { get; set; }
    public Edge Dual { get; set; }

    public Edge()
    {

    }
    public Edge(int i)
    {
        QuadIndex = i;
    }

    public Edge(int e, Point p)
    {
        QuadIndex = e;
        Origin = p;
        p.Edge = this;
    }
    public Edge Rot() { return Dual; }
    public Edge Tor() { return Dual.Dual.Dual; }
    public Edge Sym() { return Dual.Dual; }
    public Edge Onext() { return Next; }

    public Edge Oprev() { return Rot().Onext().Rot(); }
    public Edge Dnext() { return Sym().Onext().Sym(); }
    public Edge Dprev() { return Tor().Onext().Tor(); }
    public Edge Lnext() { return Tor().Onext().Rot(); }
    public Edge Lprev() { return Onext().Sym(); }
    public Edge Rnext() { return Rot().Onext().Tor(); }
    public Edge Rprev() { return Sym().Onext(); }

    public static void Splice(Edge a, Edge b)
    {
        Edge alpha = a.Onext().Rot();
        Edge beta = b.Onext().Rot();

        Edge t1 = b.Onext();
        Edge t2 = a.Onext();
        Edge t3 = beta.Onext();
        Edge t4 = alpha.Onext();

        a.Next = t1;
        b.Next = t2;
        alpha.Next = t3;
        beta.Next = t4;
    }

    override public int GetHashCode()
    {
        return Index;
    }

    public bool Equals(Edge other)
    {
        return other.Index == Index;
    }

    public override bool Equals(object obj)
    {
        if (obj is Edge edge)
        {
            return Equals(edge);
        }
        return false;
    }

    public static bool operator ==(Edge e1, Edge e2)
    {
        if (e1 is null || e2 is null) return false;
        return e1.Equals(e2);
    }

    public static bool operator !=(Edge e1, Edge e2)
    {
        return !(e1 == e2);
    }

    public override string ToString() => $"Edge ({Index}, {P}, {Q})";

    public Edge Rot(Edge[] Quad)
    {
        return (QuadIndex < 3) ? (Quad[QuadIndex + 1]) : (Quad[QuadIndex - 3]);
    }

    public Point GetDestination()
    {
        return Sym().Origin;
    }

}
