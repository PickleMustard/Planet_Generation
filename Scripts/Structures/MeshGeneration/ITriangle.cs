using System.Collections.Generic;
namespace Structures.MeshGeneration;

public interface ITriangle
{
    IList<Point> Points { get; }
    IList<Edge> Edges { get; }
    int Index { get; }
}
