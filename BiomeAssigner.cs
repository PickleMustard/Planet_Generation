using Godot;
using Structures;
using static Structures.Point;

public static class BiomeAssigner
{
    public static BiomeType AssignBiome(float height, float moisture, float latitude = 0f)
    {
        // Normalize height to 0-1 range
        float normalizedHeight = height / GenerateDocArrayMesh.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        // Whittaker Diagram mapping
        if (normalizedHeight > 0.6f) return BiomeType.Icecap;
        if (normalizedHeight > 0.5f) return BiomeType.Mountain;
        if (normalizedHeight > 0.4f && (latitude > 0.8f || latitude < -0.8f)) return BiomeType.Tundra;
        if (normalizedHeight > 0.4f && (latitude > 0.7f || latitude < -0.7f)) return BiomeType.Taiga;

        if (normalizedHeight < 0.1f) return BiomeType.Ocean;
        if (normalizedHeight < 0.2f) return BiomeType.Coastal;

        // Land biomes based on moisture
        if (normalizedHeight < 0.3f && moisture < 0.2f) return BiomeType.Desert;
        if (normalizedHeight < 0.3f && moisture < 0.5f) return BiomeType.Grassland;
        if (normalizedHeight > 0.3f && moisture < 0.7f) return BiomeType.Forest;
        return BiomeType.Rainforest;
    }

    public static float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        float latitudeFactor = Mathf.Clamp(continent.averagedCenter.Y / 90f, 0f, 1f);
        float sizeFactor = continent.cells.Count / 100f;

        float randomVariation = rng.RandfRange(-0.2f, 0.2f);
        return Mathf.Clamp(baseMoisture + latitudeFactor + sizeFactor + randomVariation, 0f, 1f);
    }
}
