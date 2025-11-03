using Godot;
using Structures;
using static Structures.Biome;
using UtilityLibrary;

namespace MeshGeneration;

/// <summary>
/// A static utility class for assigning biomes to celestial body mesh faces based on environmental factors.
/// Implements biome assignment logic using a Whittaker Diagram approach that considers height, moisture, and latitude.
/// </summary>
public static class BiomeAssigner
{
    /// <summary>
    /// The maximum moisture value used in moisture calculations.
    /// </summary>
    const float MAX_MOISTURE = 2.7f;

    /// <summary>
    /// Assigns a biome type based on height, moisture, and latitude using a Whittaker Diagram mapping.
    /// </summary>
    /// <param name="generator">The celestial body mesh generator containing height information.</param>
    /// <param name="height">The absolute height value at the location.</param>
    /// <param name="moisture">The moisture level at the location (0-1 range).</param>
    /// <param name="latitude">The latitude of the location (-1 to 1 range, where 0 is equator). Defaults to 0f.</param>
    /// <returns>The assigned BiomeType based on the environmental conditions.</returns>
    /// <remarks>
    /// This method normalizes the height to a 0-1 range and then applies a series of conditional checks
    /// based on the Whittaker Diagram to determine the appropriate biome. The logic considers:
    /// - High elevation areas become icecaps or mountains
    /// - Polar regions become tundra or taiga
    /// - Low elevation areas become ocean or coastal
    /// - Mid elevation areas vary based on moisture levels (desert, grassland, forest, rainforest)
    /// </remarks>
    public static BiomeType AssignBiome(UnifiedCelestialMesh generator, float height, float moisture, float latitude = 0f)
    {
        Logger.EnterFunction("AssignBiome", $"height={height:F3}, moisture={moisture:F3}, lat={latitude:F3}");
        // Normalize height to 0-1 range
        float normalizedHeight = height / generator.maxHeight;
        normalizedHeight = Mathf.Clamp(normalizedHeight, 0f, 1f);

        BiomeType result;
        // Whittaker Diagram mapping
        if (normalizedHeight > 0.9f) result = BiomeType.Icecap;
        else if (normalizedHeight > 0.68f) result = BiomeType.Mountain;
        else if (normalizedHeight > 0.4f && (latitude > 0.8f || latitude < -0.8f)) result = BiomeType.Tundra;
        else if (normalizedHeight > 0.4f && (latitude > 0.7f || latitude < -0.7f)) result = BiomeType.Taiga;
        else if (normalizedHeight < 0.05f) result = BiomeType.Ocean;
        else if (normalizedHeight < 0.07f) result = BiomeType.Coastal;
        else if (normalizedHeight < 0.3f && moisture < 0.2f) result = BiomeType.Desert;
        else if (normalizedHeight < 0.3f && moisture < 0.5f) result = BiomeType.Grassland;
        else if (normalizedHeight > 0.3f && moisture < 0.7f) result = BiomeType.Forest;
        else result = BiomeType.Rainforest;

        Logger.ExitFunction("AssignBiome", $"returned {result}");
        return result;
    }

    /// <summary>
    /// Calculates moisture levels for a continent based on geographic factors and random variation.
    /// </summary>
    /// <param name="continent">The continent for which to calculate moisture.</param>
    /// <param name="rng">Random number generator for adding variation.</param>
    /// <param name="baseMoisture">The base moisture level to start from. Defaults to 0.5f.</param>
    /// <returns>A calculated moisture value between 0 and MAX_MOISTURE.</returns>
    /// <remarks>
    /// The moisture calculation considers several factors:
    /// - Latitude factor: Based on the continent's Y-coordinate position
    /// - Size factor: Based on the number of cells in the continent
    /// - Random variation: Adds natural variation between -0.2 and +0.2
    /// The final value is normalized using MAX_MOISTURE to ensure it stays within expected ranges.
    /// </remarks>
    public static float CalculateMoisture(Continent continent, RandomNumberGenerator rng, float baseMoisture = 0.5f)
    {
        Logger.EnterFunction("CalculateMoisture", $"continentStartIdx={continent.StartingIndex}, base={baseMoisture:F2}");
        float latitudeFactor = Mathf.Clamp(continent.averagedCenter.Y / 9f, 0f, 1f);
        float sizeFactor = continent.cells.Count / 100f;

        float randomVariation = rng.RandfRange(-0.4f, 0.2f);
        float value = MAX_MOISTURE - (baseMoisture + latitudeFactor + sizeFactor + randomVariation) / MAX_MOISTURE;
        Logger.ExitFunction("CalculateMoisture", $"returned {value:F3}");
        return value;
    }
}
