using Godot;
using ProceduralGeneration.MeshGeneration;
using Structures.Enums;
using Structures.GameState;

namespace ProceduralGeneration.BiomeSystem;

/// <summary>
/// Fallback assigner for non-RockyPlanet bodies (GasGiant, IceGiant, DwarfPlanet, etc).
/// Forwards to the legacy static <see cref="BiomeAssigner"/>.
///
/// RockyPlanet subtypes now flow through <see cref="ConfigurableBiomeAssigner"/> driven by
/// <c>Configuration/Biomes/biome_assigner_config.yaml</c>. The previously hardcoded subtype
/// classes (TemperatePlanetBiomeAssigner, ScouredPlanetBiomeAssigner, ...) were deleted.
/// </summary>
public class DefaultBiomeAssigner : IBiomeAssigner
{
    public Structures.Enums.Biome.BiomeType AssignBiome(
        UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        return BiomeAssigner.AssignBiome(generator, height, moisture, latitude);
    }

    public float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        return BiomeAssigner.CalculateMoisture(continent, rng, baseMoisture);
    }
}
