using System.Collections.Generic;
using Godot;
namespace Structures;
public partial class VoronoiCell : Resource
{
    public Point[] Points { get; set; }
    public Triangle[] Triangles { get; set; }
    public Edge[] Edges { get; set; }
    public Edge[] OutsideEdges { get; set; } //Edges that lie on the border of a continent
    public Aabb BoundingBox { get; set; }
    public int Index { get; set; }
    public int ContinentIndex { get; set; }
    public bool IsBorderTile { get; set; }
    public int[] BoundingContinentIndex { get; set; }
    public int Interiorness { get; set; } = int.MaxValue;
    public Dictionary<Edge, int> EdgeBoundaryMap { get; set; }
    public Vector2 MovementDirection { get; set; }
    public float Height { get; set; }
    public Vector3 Center { get; set; }
    public float Stress { get; set; } = 0.0f;
    public int Increment { get; set; } = 1;
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

    public void GenerateBoundingBox()
    {
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
        Vector3 center = new Vector3(0, 0, 0);
        float height = 0f;
        foreach (Point p in Points)
        {
            center += p.Position;
            height += (p.Height / 10f);
            minX = Mathf.Min(minX, p.Position.X);
            minY = Mathf.Min(minY, p.Position.Y);
            minZ = Mathf.Min(minZ, p.Position.Z);
            maxX = Mathf.Max(maxX, p.Position.X);
            maxY = Mathf.Max(maxY, p.Position.Y);
            maxZ = Mathf.Max(maxZ, p.Position.Z);
        }
        center /= Points.Length;
        height /= Points.Length;
        Center = center;
        Height = height;
        Vector3 size = new Vector3(maxX - minX, maxY - minY, maxZ - minZ) * 1.1f;
        Vector3 centerOffset = center - size / 2f;
        BoundingBox = new Aabb(centerOffset, size).Abs();
    }

    public override string ToString()
    {
        string output = "";
        output += $"VoronoiCell: ({Index}";
        //foreach(Point p in Points) {
        //  output += $"{p}, ";
        //}
        //foreach(Triangle t in Triangles) {
        //  output += $"{t}, ";
        //}
        output += ")";
        output += $"{BoundingBox}, ";
        output += $", {Points.Length}# Points, {Edges.Length}# Edges, {Triangles.Length}# Triangles.";
        output += $"Part of: {ContinentIndex}, Height: {Height}";

        return output;
    }
}
