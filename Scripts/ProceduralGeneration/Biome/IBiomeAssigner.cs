using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.GameState;

namespace ProceduralGeneration.BiomeSystem;

public interface IBiomeAssigner
{
    /// <summary>
    /// Returns the assigned biome's stable ID (e.g. <c>biome_forest</c>).
    /// </summary>
    string AssignBiome(
        UnifiedCelestialMesh generator,
        float height,
        float moisture,
        float latitude = 0f);

    float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f);
}
