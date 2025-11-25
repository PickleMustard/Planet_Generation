using System.Collections.Generic;
using Godot;
namespace Structures.MeshGeneration;

public partial class Triangle : Resource
{
    public int Index { get; set; }
    public IList<Point> Points { get; set; }
    public IList<Edge> Edges { get; set; }

    public Triangle(int t, List<Point> points, List<Edge> edges)
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
