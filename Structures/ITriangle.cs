using System.Collections.Generic;
public interface ITriangle
{
    IEnumerable<IPoint> Points { get; }
    IEnumerable<IEdge> Edges { get; }
    int Index { get; }
}
