using Godot;
using Structures.Enums;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Texture generator for dwarf planets.
/// Creates a uniform color with slight surface variation.
/// </summary>
public class DwarfPlanetTextureGenerator : ITextureGenerator
{
    private readonly Color _baseColor;

    /// <summary>
    /// Creates a new dwarf planet texture generator.
    /// </summary>
    /// <param name="subtype">The dwarf planet subtype.</param>
    public DwarfPlanetTextureGenerator(DwarfPlanetSubtype subtype)
    {
        _baseColor = GetDwarfPlanetColor(subtype);
    }

    /// <inheritdoc />
    public (ImageTexture Distant, ImageTexture Far, ImageTexture Close) Generate(IOrbitalBody body)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.5f,
            FractalOctaves = 2,
        };
        noise.Seed = (int)((body.Mesh?.Seed ?? 0) % int.MaxValue);

        var distantImage = GenerateImage(64, noise);
        var farImage = GenerateImage(256, noise);
        var closeImage = GenerateImage(1024, noise);

        return (
            ImageTexture.CreateFromImage(distantImage),
            ImageTexture.CreateFromImage(farImage),
            ImageTexture.CreateFromImage(closeImage)
        );
    }

    private Image GenerateImage(int resolution, FastNoiseLite noise)
    {
        var image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float n = noise.GetNoise2D(x * 0.05f, y * 0.05f) * 0.1f;
                Color c = _baseColor.Lightened(n);
                image.SetPixel(x, y, c);
            }
        }

        return image;
    }

    private Color GetDwarfPlanetColor(DwarfPlanetSubtype subtype)
    {
        return subtype switch
        {
            DwarfPlanetSubtype.IcyKuiper => new Color(0.85f, 0.9f, 0.95f),
            DwarfPlanetSubtype.RockyBelt => new Color(0.6f, 0.55f, 0.5f),
            DwarfPlanetSubtype.MixedComposition => new Color(0.7f, 0.7f, 0.75f),
            DwarfPlanetSubtype.Plutoid => new Color(0.9f, 0.85f, 0.8f),
            DwarfPlanetSubtype.CeresType => new Color(0.65f, 0.65f, 0.6f),
            DwarfPlanetSubtype.ScatteredDisk => new Color(0.8f, 0.85f, 0.9f),
            DwarfPlanetSubtype.DetachedObject => new Color(0.75f, 0.8f, 0.85f),
            _ => new Color(0.8f, 0.8f, 0.85f),
        };
    }
}
