using Structures.Enums;
using Structures.MeshGeneration;

namespace Structures.GameState;

public interface IVoronoiCell
{
    Point[] Points { get; }
    Triangle[] Triangles { get; }
    int Index { get; }
    Biome.BiomeType Biome { get; }
}
