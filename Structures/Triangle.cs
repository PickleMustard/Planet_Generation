using System;
using System.Collections.Generic;
namespace Structures;
public class Triangle : ITriangle
{
    public int Index { get; set; }
    public IList<Point> Points { get; set; }
    public IList<Edge> Edges { get; set; }

    private int DetermineIndex()
    {
        int points = 0;
        foreach (Point p in Points)
        {
            points += p.Index;
        }
        int edges = 0;
        foreach (Edge e in Edges)
        {
            edges += e.Index;
        }
        //int time = BitConverter.SingleToInt32Bits(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return HashCode.Combine(points, edges);

    }

    override public int GetHashCode()
    {
        return Index;
    }

    public Triangle(List<Point> points, List<Edge> edges)
    {
        Edges = edges;
        Points = points;
        Index = DetermineIndex();
    }

    public Triangle(int index, List<Point> points, List<Edge> edges)
    {
        Index = Index;
        Edges = edges;
        Points = points;
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
