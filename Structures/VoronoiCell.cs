public struct VoronoiCell : IVoronoiCell
{
    public IPoint[] Points { get; set; }
    public ITriangle[] Triangles { get; set; }
    public int Index { get; set; }
    public VoronoiCell(int triangleIndex, IPoint[] points, ITriangle[] triangles)
    {
        Triangles = triangles;
        Points = points;
        Index = triangleIndex;
    }
}
