using System.Collections.Generic;
using Godot;
namespace Structures;
public class VoronoiCell : IVoronoiCell
{
    public Point[] Points { get; set; }
    public Triangle[] Triangles { get; set; }
    public Edge[] Edges { get; set; }
    public Edge[] OutsideEdges { get; set; } //Edges that lie on the border of a continent
    public int Index { get; set; }
    public int ContinentIndex { get; set; }
    public bool IsBorderTile { get; set; }
    public int[] BoundingContinentIndex { get; set; }
    public Dictionary<Edge, int> EdgeBoundaryMap { get; set; }
    public Vector2 MovementDirection { get; set; }
    public float Height { get; set; }
    public Vector3 Center { get; set; }
    public float Stress { get; set; }
    public VoronoiCell(int triangleIndex, Point[] points, Triangle[] triangles, Edge[] edges)
    {
        Triangles = triangles;
        Points = points;
        Edges = edges;
        Index = triangleIndex;
        ContinentIndex = -1;
        BoundingContinentIndex = new int[] { };
        IsBorderTile = false;
        EdgeBoundaryMap = new Dictionary<Edge, int>();

        Vector3 center = new Vector3(0, 0, 0);
        foreach (Point p in Points)
        {
            center += p.Position;
        }
        center /= Points.Length;
        Center = center;
    }

    public override string ToString()
    {
        string output = "";
        output += $"VoronoiCell: ({Index}, ";
        //foreach(Point p in Points) {
        //  output += $"{p}, ";
        //}
        //foreach(Triangle t in Triangles) {
        //  output += $"{t}, ";
        //}
        output += ")";
        output += $", {Points.Length}# Points, {Edges.Length}# Edges, {Triangles.Length}# Triangles.";
        output += $"Part of: {ContinentIndex}";

        return output;
    }
}
