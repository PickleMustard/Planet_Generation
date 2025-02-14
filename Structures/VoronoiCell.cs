public class VoronoiCell : IVoronoiCell
{
    public Point[] Points { get; set; }
    public Triangle[] Triangles { get; set; }
    public int Index { get; set; }
    public VoronoiCell(int triangleIndex, Point[] points, Triangle[] triangles)
    {
        Triangles = triangles;
        Points = points;
        Index = triangleIndex;
    }
}
