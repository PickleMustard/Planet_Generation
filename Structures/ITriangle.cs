using System.Collections.Generic;
public interface ITriangle
{
    IList<Point> Points { get; }
    IList<Edge> Edges { get; }
    int Index { get; }
}
