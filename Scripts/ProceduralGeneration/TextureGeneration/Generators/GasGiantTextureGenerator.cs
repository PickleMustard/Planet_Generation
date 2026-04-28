using Godot;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Texture generator for gas giants and ice giants.
/// Creates banded atmospheric patterns with storm features.
/// </summary>
public class GasGiantTextureGenerator : ITextureGenerator
{
    private readonly FastNoiseLite _bandNoise;
    private readonly FastNoiseLite _stormNoise;
    private readonly Color[] _palette;

    /// <summary>
    /// Creates a new gas giant texture generator.
    /// </summary>
    /// <param name="subtype">The gas giant subtype.</param>
    public GasGiantTextureGenerator(object subtype)
    {
        _palette = GetPalette(subtype);

        _bandNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.3f,
            FractalOctaves = 3,
        };

        _stormNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 1.0f,
            FractalOctaves = 2,
        };
    }

    /// <inheritdoc />
    public (ImageTexture Distant, ImageTexture Far, ImageTexture Close) Generate(IOrbitalBody body)
    {
        var seed = body.Mesh?.Seed ?? (ulong)GD.Randi();
        _bandNoise.Seed = (int)(seed % int.MaxValue);
        _stormNoise.Seed = (int)((seed + 1) % int.MaxValue);

        var distantImage = GenerateImage(64);
        var farImage = GenerateImage(256);
        var closeImage = GenerateImage(1024);

        return (
            ImageTexture.CreateFromImage(distantImage),
            ImageTexture.CreateFromImage(farImage),
            ImageTexture.CreateFromImage(closeImage)
        );
    }

    private Image GenerateImage(int resolution)
    {
        var image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);

        for (int y = 0; y < resolution; y++)
        {
            float latitude = (y / (float)resolution) * 2.0f - 1.0f;

            // Calculate latitude bands with turbulence
            float turbulence = _bandNoise.GetNoise1D(latitude * 10.0f);
            float warpedLat = latitude + turbulence * 0.1f;

            int bandIndex = Mathf.FloorToInt((warpedLat + 1.0f) * 0.5f * _palette.Length);
            bandIndex = Mathf.Clamp(bandIndex, 0, _palette.Length - 1);
            Color baseColor = _palette[bandIndex];

            for (int x = 0; x < resolution; x++)
            {
                float lon = (x / (float)resolution) * 2.0f * Mathf.Pi;
                float storm = _stormNoise.GetNoise2D(lon * 5.0f, latitude * 10.0f);

                Color pixel = baseColor;
                if (storm > 0.6f)
                    pixel = pixel.Darkened(0.2f);

                image.SetPixel(x, y, pixel);
            }
        }

        return image;
    }

    private Color[] GetPalette(object subtype)
    {
        // Default palette for StandardJupiter
        return new[]
        {
            new Color(0.9f, 0.8f, 0.7f), // Light band
            new Color(0.8f, 0.6f, 0.4f), // Dark band
            new Color(0.85f, 0.75f, 0.65f), // Light band
            new Color(0.7f, 0.5f, 0.3f), // Dark band
            new Color(0.9f, 0.8f, 0.7f), // Light band
            new Color(0.75f, 0.55f, 0.35f), // Dark band
        };
    }
}
