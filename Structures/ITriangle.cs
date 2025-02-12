using System.Collections.Generic;
public interface ITriangle
{
    IEnumerable<IPoint> Points { get; }
    int Index { get; }
}
