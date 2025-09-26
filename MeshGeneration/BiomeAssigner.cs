using Godot;
using Structures;
using static Structures.Biome;
using UtilityLibrary;

namespace MeshGeneration;
public static class BiomeAssigner
{
    const float MAX_MOISTURE = 2.7f;
    public static BiomeType AssignBiome(GenerateDocArrayMesh generator, float height, float moisture, float latitude = 0f)
    {
        Logger.EnterFunction("AssignBiome", $"height={height:F3}, moisture={moisture:F3}, lat={latitude:F3}");
        // Normalize height to 0-1 range
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        BiomeType result;
        // Whittaker Diagram mapping
        if (normalizedHeight > 0.9f) result = BiomeType.Icecap;
        else if (normalizedHeight > 0.78f) result = BiomeType.Mountain;
        else if (normalizedHeight > 0.4f && (latitude > 0.8f || latitude < -0.8f)) result = BiomeType.Tundra;
        else if (normalizedHeight > 0.4f && (latitude > 0.7f || latitude < -0.7f)) result = BiomeType.Taiga;
        else if (normalizedHeight < 0.1f) result = BiomeType.Ocean;
        else if (normalizedHeight < 0.2f) result = BiomeType.Coastal;
        else if (normalizedHeight < 0.3f && moisture < 0.2f) result = BiomeType.Desert;
        else if (normalizedHeight < 0.3f && moisture < 0.5f) result = BiomeType.Grassland;
        else if (normalizedHeight > 0.3f && moisture < 0.7f) result = BiomeType.Forest;
        else result = BiomeType.Rainforest;

        Logger.ExitFunction("AssignBiome", $"returned {result}");
        return result;
    }

    public static float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        Logger.EnterFunction("CalculateMoisture", $"continentStartIdx={continent.StartingIndex}, base={baseMoisture:F2}");
        float latitudeFactor = Mathf.Clamp(continent.averagedCenter.Y / 9f, 0f, 1f);
        float sizeFactor = continent.cells.Count / 100f;

        float randomVariation = rng.RandfRange(-0.2f, 0.2f);
        float value = MAX_MOISTURE - (baseMoisture + latitudeFactor + sizeFactor + randomVariation) / MAX_MOISTURE;
        Logger.ExitFunction("CalculateMoisture", $"returned {value:F3}");
        return value;
    }
}
