using System.Collections.Generic;

namespace Structures;
public class Triangle2D
{
    public List<Point2D> Points { get; set; }
    public List<Edge2D> Edges { get; set; }
    public int Index { get; set; }
    public Triangle2D(int t, List<Point2D> points, List<Edge2D> edges)
    {
        Index = t;
        Edges = edges;
        Points = points;
    }
}
