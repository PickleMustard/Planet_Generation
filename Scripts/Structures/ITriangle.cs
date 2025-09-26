using System.Collections.Generic;
namespace Structures;
public interface ITriangle
{
    IList<Point> Points { get; }
    IList<Edge> Edges { get; }
    int Index { get; }
}
