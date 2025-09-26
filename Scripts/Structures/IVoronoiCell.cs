using System.Collections.Generic;
namespace Structures;

public interface IVoronoiCell
{
    Point[] Points { get; }
    Triangle[] Triangles { get; }
    int Index { get; }
}
