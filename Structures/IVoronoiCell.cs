using System.Collections.Generic;

public interface IVoronoiCell
{
    IPoint[] Points { get; }
    int Index { get; }
}
