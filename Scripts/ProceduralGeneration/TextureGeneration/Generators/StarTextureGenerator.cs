using Godot;
using Structures.Enums;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Texture generator for stars.
/// Creates a circular gradient with limb darkening and surface granulation.
/// </summary>
public class StarTextureGenerator : ITextureGenerator
{
    private readonly float _temperature;
    private readonly Color _baseColor;

    /// <summary>
    /// Creates a new star texture generator.
    /// </summary>
    /// <param name="subtype">The star subtype.</param>
    public StarTextureGenerator(object subtype)
    {
        _temperature = GetTemperature(subtype);
        _baseColor = TemperatureToRgb(_temperature);
    }

    /// <inheritdoc />
    public (ImageTexture Distant, ImageTexture Far, ImageTexture Close) Generate(IOrbitalBody body)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.1f,
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
        float center = resolution / 2.0f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / (resolution / 2.0f);

                if (dist > 1.0f)
                {
                    image.SetPixel(x, y, new Color(0, 0, 0, 0));
                    continue;
                }

                // Limb darkening + granulation
                float limb = Mathf.Sqrt(1.0f - dist * dist);
                float gran = noise.GetNoise2D(x * 0.1f, y * 0.1f) * 0.1f;

                Color c = _baseColor;
                c = c.Darkened((1.0f - limb) * 0.3f);
                c = c.Lightened(gran);

                image.SetPixel(x, y, c);
            }
        }

        return image;
    }

    private float GetTemperature(object subtype)
    {
        return subtype switch
        {
            StarSubtype.BlueSupergiant => 30000f,
            StarSubtype.MainSequence => 5778f,
            StarSubtype.YellowDwarf => 5500f,
            StarSubtype.RedDwarf => 3500f,
            StarSubtype.RedGiant => 3500f,
            StarSubtype.WhiteDwarf => 15000f,
            _ => 5778f, // Default to Sun-like
        };
    }

    private Color TemperatureToRgb(float temp)
    {
        // Simplified blackbody color approximation
        return temp switch
        {
            > 20000f => new Color(0.7f, 0.8f, 1.0f), // Blue-white
            > 10000f => new Color(0.9f, 0.95f, 1.0f), // White-blue
            > 7500f => new Color(1.0f, 1.0f, 1.0f), // White
            > 6000f => new Color(1.0f, 0.98f, 0.9f), // Yellow-white
            > 5000f => new Color(1.0f, 0.95f, 0.8f), // Yellow
            > 3500f => new Color(1.0f, 0.8f, 0.5f), // Orange
            _ => new Color(1.0f, 0.5f, 0.3f), // Red
        };
    }
}
