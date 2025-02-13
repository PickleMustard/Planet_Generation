using System.Collections.Generic;
public struct Triangle : ITriangle
{
    public int Index { get; set; }
    public List<IPoint> Points { get; set; }
    public List<IEdge> Edges { get; set; }

    public Triangle(int t, List<IPoint> points, List<IEdge> edges)
    {
        Edges = edges;
        Points = points;
        Index = t;
    }
}
