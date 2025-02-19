using Godot;
public class VoronoiCell : IVoronoiCell
{
    public Point[] Points { get; set; }
    public Triangle[] Triangles { get; set; }
    public int Index { get; set; }
    public Vector2 MovementDirection { get; set; }
    public VoronoiCell(int triangleIndex, Point[] points, Triangle[] triangles)
    {
        Triangles = triangles;
        Points = points;
        Index = triangleIndex;
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
        return output;
    }
}
