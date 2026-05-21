using Structures;

namespace ProceduralGeneration.ColorSystem;

/// <summary>
/// Builds an <see cref="IColorMapper"/> for a given body classification.
/// Now always returns a <see cref="DataDrivenColorMapper"/>; color tables live in
/// <c>Configuration/Biomes/biomes.yaml</c> keyed by subtype ID.
/// </summary>
public static class ColorMapperFactory
{
    public static IColorMapper GetMapper(BodyClassification classification)
    {
        return classification switch
        {
            BodyClassification.RockyPlanet rp =>
                new DataDrivenColorMapper(BiomeIdMapper.RockyPlanetSubtypeToId(rp.Subtype)),
            _ => new DataDrivenColorMapper()
        };
    }
}
