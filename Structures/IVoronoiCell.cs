using System.Collections.Generic;

public interface IVoronoiCell
{
    IPoint[] Points { get; }
    ITriangle[] Triangles { get; }
    int Index { get; }
}
