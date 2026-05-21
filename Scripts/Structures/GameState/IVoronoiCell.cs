using Structures.MeshGeneration;

namespace Structures.GameState;

public interface IVoronoiCell
{
    Point[] Points { get; }
    Triangle[] Triangles { get; }
    int Index { get; }
    /// <summary>Stable biome ID (e.g. <c>biome_forest</c>).</summary>
    string Biome { get; }
}
