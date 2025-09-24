using System.Collections.Generic;
using Godot;
using UtilityLibrary;

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
        Logger.EnterFunction("VoronoiCell.Constructor", $"triangleIndex: {triangleIndex}, points: {points?.Length ?? 0}, triangles: {triangles?.Length ?? 0}, edges: {edges?.Length ?? 0}");
        
        Triangles = triangles;
        Points = points;
        Edges = edges;
        Index = triangleIndex;
        ContinentIndex = -1;
        BoundingContinentIndex = new int[] { };
        IsBorderTile = false;
        EdgeBoundaryMap = new Dictionary<Edge, int>();
        
        Logger.Debug($"Initialized VoronoiCell with {points?.Length ?? 0} points, {triangles?.Length ?? 0} triangles, {edges?.Length ?? 0} edges", "VoronoiCell");

        Vector3 center = new Vector3(0, 0, 0);
        Logger.Debug($"Calculating center position for cell {Index}", "VoronoiCell");
        foreach (Point p in Points)
        {
            center += p.Position;
            Logger.Point($"Added point {p.Index} at position {p.Position} to center calculation");
        }
        center /= Points.Length;
        Center = center;
        Logger.Debug($"Calculated cell center at {Center}", "VoronoiCell");
        
        Logger.ExitFunction("VoronoiCell.Constructor");
    }

    public override string ToString()
    {
        string output = "";
        output += $"VoronoiCell: ({Index}, #Points: {Points.Length}, #Triangles: {Triangles.Length}, #Edges: {Edges.Length}, ";
        //foreach(Point p in Points) {
        //  output += $"{p}, ";
        //}
        //foreach(Triangle t in Triangles) {
        //  output += $"{t}, ";
        //}
        output += ")";
        return output;
    }
}
