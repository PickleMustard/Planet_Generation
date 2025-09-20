using System.Collections.Generic;
namespace Structures;
public interface ITriangle
{
    IList<IPoint> Points { get; }
    IList<IEdge> Edges { get; }
    int Index { get; }
}
