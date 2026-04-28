using Godot;
using ProceduralGeneration.ColorSystem;
using Structures.Enums;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Texture generator for rocky planets.
/// Generates planetary textures based on biome assignment, elevation, and latitude-based climate zones.
/// </summary>
public class RockyPlanetTextureGenerator : ITextureGenerator
{
    private readonly IColorMapper _colorMapper;
    private readonly FastNoiseLite _surfaceNoise;
    private readonly FastNoiseLite _turbulenceNoise;

    /// <summary>
    /// Creates a new rocky planet texture generator.
    /// </summary>
    /// <param name="subtype">The rocky planet subtype (e.g., Temperate, Desert, Ice).</param>
    public RockyPlanetTextureGenerator(RockyPlanetSubtype subtype)
    {
        _colorMapper = ColorMapperFactory.GetMapper(
            new Structures.BodyClassification.RockyPlanet(subtype)
        );

        _surfaceNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.5f,
            FractalOctaves = 4,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f,
        };

        _turbulenceNoise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 2.0f,
            FractalOctaves = 3,
            FractalLacunarity = 2.0f,
            FractalGain = 0.4f,
        };
    }

    /// <inheritdoc />
    public (ImageTexture Distant, ImageTexture Far, ImageTexture Close) Generate(IOrbitalBody body)
    {
        var seed = body.Mesh?.Seed ?? (ulong)GD.Randi();
        _surfaceNoise.Seed = (int)(seed % int.MaxValue);
        _turbulenceNoise.Seed = (int)((seed + 1) % int.MaxValue);

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
            for (int x = 0; x < resolution; x++)
            {
                float u = x / (float)resolution;
                float v = y / (float)resolution;

                // Convert to spherical coordinates
                float lon = u * 2.0f * Mathf.Pi - Mathf.Pi;
                float lat = v * Mathf.Pi - Mathf.Pi / 2.0f;

                // Direction vector on unit sphere
                Vector3 dir = new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon),
                    Mathf.Sin(lat),
                    Mathf.Cos(lat) * Mathf.Sin(lon)
                );

                // Sample height and biome color
                float heightNoise = _surfaceNoise.GetNoise3Dv(dir * 5.0f);
                Color baseColor = SampleBiomeColor(lat, heightNoise);

                // Apply surface variation/turbulence
                float turbulence = _turbulenceNoise.GetNoise3Dv(dir * 30.0f);
                baseColor = baseColor.Darkened(turbulence * 0.1f);

                image.SetPixel(x, y, baseColor);
            }
        }

        return image;
    }

    private Color SampleBiomeColor(float latitude, float height)
    {
        // Ice caps at poles
        if (Mathf.Abs(latitude) > 0.8f)
            return _colorMapper.GetBiomeColor(Biome.BiomeType.Icecap, height);

        // Mountains at high elevation
        if (height > 0.4f)
            return _colorMapper.GetBiomeColor(Biome.BiomeType.Mountain, height);

        // Ocean at low elevation
        if (height < -0.2f)
            return _colorMapper.GetBiomeColor(Biome.BiomeType.Ocean, height);

        // Default to grassland/forest/taiga based on latitude
        var biome = Mathf.Abs(latitude) switch
        {
            < 0.3f => Biome.BiomeType.Grassland,
            < 0.6f => Biome.BiomeType.Forest,
            _ => Biome.BiomeType.Taiga,
        };

        return _colorMapper.GetBiomeColor(biome, height);
    }
}
