using Godot;
using Structures.GameState;

namespace ProceduralGeneration.MeshGeneration;

/// <summary>
/// Static utility for assigning biome IDs to mesh points using a Whittaker-style mapping
/// (height + moisture + latitude). Used as the fallback assigner for non-RockyPlanet bodies.
/// </summary>
public static class BiomeAssigner
{
    private const float MAX_MOISTURE = 2.7f;

    /// <summary>
    /// Returns the assigned biome's stable ID (e.g. <c>biome_forest</c>).
    /// </summary>
    public static string AssignBiome(UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        if (normalizedHeight > 0.9f) return "biome_icecap";
        if (normalizedHeight > 0.68f) return "biome_mountain";
        if (normalizedHeight > 0.4f && (latitude > 0.8f || latitude < -0.8f)) return "biome_tundra";
        if (normalizedHeight > 0.4f && (latitude > 0.7f || latitude < -0.7f)) return "biome_taiga";
        if (normalizedHeight < 0.05f) return "biome_ocean";
        if (normalizedHeight < 0.07f) return "biome_coastal";
        if (normalizedHeight < 0.3f && moisture < 0.2f) return "biome_desert";
        if (normalizedHeight < 0.3f && moisture < 0.5f) return "biome_grassland";
        if (normalizedHeight > 0.3f && moisture < 0.7f) return "biome_forest";
        return "biome_rainforest";
    }

    public static float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        // [0,1] baseline driven by NORMALIZED latitude: drier toward the poles.
        float absLat = Mathf.Abs(continent.averagedCenter.Normalized().Y); // 0 eq .. 1 pole
        float randomVariation = rng.RandfRange(-0.2f, 0.2f);
        float value = baseMoisture + randomVariation - absLat * 0.4f;
        return Mathf.Clamp(value, 0f, 1f);
    }
}
