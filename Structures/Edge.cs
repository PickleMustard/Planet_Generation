public class Edge : IEdge
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

    public override string ToString() => $"Edge ({Index}, {P}, {Q})";

}
