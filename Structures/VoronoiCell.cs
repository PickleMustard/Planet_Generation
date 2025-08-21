using Godot;
public class VoronoiCell : IVoronoiCell
{
    public Point[] Points { get; set; }
    public Triangle[] Triangles { get; set; }
    public Edge[] Edges { get; set; }
    public Edge[] OutsideEdges { get; set; } //Edges that lie on the border of a continent
    public int Index { get; set; }
    public int ContinentIndex {get; set;}
    public bool IsBorderTile {get; set;}
    public int[] BoundingContinentIndex {get; set;}
    public Vector2 MovementDirection { get; set; }
    public float Height {get; set;}
    public Vector3 Center {
        get {
            if (Points == null || Points.Length == 0)
                return new Vector3(0, 0, 0);

            Vector3 center = new Vector3(0, 0, 0);
            foreach (Point p in Points)
            {
                center += p.Position;
            }
            return center / Points.Length;
        }
    }
    public VoronoiCell(int triangleIndex, Point[] points, Triangle[] triangles, Edge[] edges)
    {
        Triangles = triangles;
        Points = points;
        Edges = edges;
        Index = triangleIndex;
        ContinentIndex = -1;
        BoundingContinentIndex = new int[]{};
        IsBorderTile = false;
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
