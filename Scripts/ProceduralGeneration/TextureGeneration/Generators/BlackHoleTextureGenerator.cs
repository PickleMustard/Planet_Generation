using Godot;
using Structures.Enums;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Texture generator for black holes.
/// Creates an accretion disk with gravitational lensing effects.
/// </summary>
public class BlackHoleTextureGenerator : ITextureGenerator
{
    private readonly FastNoiseLite _diskNoise;

    /// <summary>
    /// Creates a new black hole texture generator.
    /// </summary>
    /// <param name="subtype">The black hole subtype.</param>
    public BlackHoleTextureGenerator(BlackHoleSubtype subtype)
    {
        _diskNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.5f,
            FractalOctaves = 3,
        };
    }

    /// <inheritdoc />
    public (ImageTexture Distant, ImageTexture Far, ImageTexture Close) Generate(IOrbitalBody body)
    {
        var seed = body.Mesh?.Seed ?? (ulong)GD.Randi();
        _diskNoise.Seed = (int)(seed % int.MaxValue);

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
        float center = resolution / 2.0f;
        float eventHorizon = resolution * 0.25f; // Schwarzschild radius
        float diskInner = eventHorizon * 1.5f;
        float diskOuter = eventHorizon * 3.0f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                Color pixel;

                // Event horizon (black)
                if (dist < eventHorizon)
                {
                    pixel = Colors.Black;
                }
                // Accretion disk
                else if (dist >= diskInner && dist <= diskOuter)
                {
                    float diskT = (dist - diskInner) / (diskOuter - diskInner);
                    float noise = _diskNoise.GetNoise2D(x * 0.1f, y * 0.1f);

                    // Hotter (brighter/whiter) near event horizon, cooler (redder) outside
                    Color inner = new Color(1.0f, 0.9f, 0.7f); // White-yellow
                    Color outer = new Color(0.8f, 0.3f, 0.1f); // Red-orange
                    pixel = inner.Lerp(outer, diskT);
                    pixel = pixel.Lightened(noise * 0.2f);
                }
                // Gravitational lensing area (dimmed)
                else if (dist < diskInner)
                {
                    float lensing = 1.0f - ((dist - eventHorizon) / (diskInner - eventHorizon));
                    pixel = new Color(0, 0, 0, lensing * 0.5f);
                }
                // Outside
                else
                {
                    pixel = new Color(0, 0, 0, 0);
                }

                image.SetPixel(x, y, pixel);
            }
        }

        return image;
    }
}
