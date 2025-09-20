using System.Collections.Generic;
namespace Structures;
public class Triangle : ITriangle
{
    public int Index { get; set; }
    public IList<IPoint> Points { get; set; }
    public IList<IEdge> Edges { get; set; }

    public Triangle(int t, List<IPoint> points, List<IEdge> edges)
    {
        Edges = edges;
        Points = points;
        Index = t;
    }


    public override string ToString()
    {
        string output = "";
        output += $"Triangle: ({Index}, ";
        foreach (Point p in Points)
        {
            output += $"{p}, ";
        }
        foreach (Edge e in Edges)
        {
            output += $"{e}, ";
        }
        output += ")";
        return output;
    }
}
