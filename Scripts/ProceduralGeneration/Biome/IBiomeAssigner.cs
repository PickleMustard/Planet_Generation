using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using Structures.GameState;

namespace ProceduralGeneration.BiomeSystem;

public interface IBiomeAssigner
{
    Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator,
        float height,
        float moisture,
        float latitude = 0f);

    float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f);
}
