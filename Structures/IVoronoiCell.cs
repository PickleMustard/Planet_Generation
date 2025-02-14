using System.Collections.Generic;

public interface IVoronoiCell
{
    Point[] Points { get; }
    Triangle[] Triangles { get; }
    int Index { get; }
}
