using Structures;
using Structures.Enums;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Factory class for creating texture generators based on body classification.
/// </summary>
public static class TextureGeneratorFactory
{
    /// <summary>
    /// Gets the appropriate texture generator for a given body classification.
    /// </summary>
    public static ITextureGenerator GetGenerator(BodyClassification classification)
    {
        return classification switch
        {
            BodyClassification.RockyPlanet rp => new MeshSnapshotTextureGenerator(
                fallback: new RockyPlanetTextureGenerator(rp.Subtype)),
            BodyClassification.GasGiant gg => new GasGiantTextureGenerator(gg.Subtype),
            BodyClassification.IceGiant ig => new GasGiantTextureGenerator(ig.Subtype),
            BodyClassification.Star s => new StarTextureGenerator(s.Subtype),
            BodyClassification.DwarfPlanet dp => new MeshSnapshotTextureGenerator(
                fallback: new DwarfPlanetTextureGenerator(dp.Subtype)),
            BodyClassification.BlackHole bh => new BlackHoleTextureGenerator(bh.Subtype),
            BodyClassification.NeutronStar ns => new StarTextureGenerator(ns.Subtype),
            _ => new MeshSnapshotTextureGenerator(
                fallback: new RockyPlanetTextureGenerator(RockyPlanetSubtype.Temperate))
        };
    }
}
