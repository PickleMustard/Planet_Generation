using Godot;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Helper methods for noise-based texture generation.
/// </summary>
public static class NoiseHelpers
{
    /// <summary>
    /// Creates a configured FastNoiseLite instance for surface detail generation.
    /// </summary>
    /// <param name="seed">The random seed for the noise.</param>
    /// <param name="frequency">Base frequency of the noise.</param>
    /// <param name="octaves">Number of octaves for fractal noise.</param>
    /// <returns>A configured FastNoiseLite instance.</returns>
    public static FastNoiseLite CreateSurfaceNoise(int seed, float frequency = 0.5f, int octaves = 4)
    {
        return new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = frequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = octaves,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f
        };
    }

    /// <summary>
    /// Creates a configured FastNoiseLite instance for turbulence/warping effects.
    /// </summary>
    /// <param name="seed">The random seed for the noise.</param>
    /// <param name="frequency">Base frequency of the noise.</param>
    /// <param name="octaves">Number of octaves for fractal noise.</param>
    /// <returns>A configured FastNoiseLite instance.</returns>
    public static FastNoiseLite CreateTurbulenceNoise(int seed, float frequency = 2.0f, int octaves = 3)
    {
        return new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = frequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = octaves,
            FractalLacunarity = 2.0f,
            FractalGain = 0.4f
        };
    }

    /// <summary>
    /// Samples 3D noise at a normalized direction on a sphere.
    /// </summary>
    /// <param name="noise">The noise generator.</param>
    /// <param name="direction">Normalized direction vector on the unit sphere.</param>
    /// <param name="scale">Scale factor for the sampling.</param>
    /// <returns>Noise value at the specified direction.</returns>
    public static float SampleSphericalNoise(FastNoiseLite noise, Vector3 direction, float scale = 1.0f)
    {
        return noise.GetNoise3D(direction.X * scale, direction.Y * scale, direction.Z * scale);
    }

    /// <summary>
    /// Remaps a noise value from [-1, 1] to [0, 1] range.
    /// </summary>
    /// <param name="noiseValue">Raw noise value.</param>
    /// <returns>Remapped value in [0, 1] range.</returns>
    public static float Remap01(float noiseValue)
    {
        return (noiseValue + 1.0f) * 0.5f;
    }

    /// <summary>
    /// Applies turbulence warping to coordinates.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <param name="turbulenceNoise">Turbulence noise generator.</param>
    /// <param name="strength">Warping strength.</param>
    /// <returns>Warped coordinates.</returns>
    public static (float x, float y) ApplyTurbulence(float x, float y, FastNoiseLite turbulenceNoise, float strength = 0.1f)
    {
        float warpX = turbulenceNoise.GetNoise2D(x, y) * strength;
        float warpY = turbulenceNoise.GetNoise2D(x + 100, y + 100) * strength;
        return (x + warpX, y + warpY);
    }

    /// <summary>
    /// Calculates latitude bands for gas giant textures.
    /// </summary>
    /// <param name="latitude">Latitude in range [-1, 1] where 0 is equator.</param>
    /// <param name="numBands">Number of bands.</param>
    /// <param name="turbulence">Optional turbulence noise value.</param>
    /// <returns>Band index.</returns>
    public static int CalculateLatitudeBand(float latitude, int numBands, float turbulence = 0.0f)
    {
        float warpedLat = latitude + turbulence * 0.1f;
        int band = Mathf.FloorToInt((warpedLat + 1.0f) * 0.5f * numBands);
        return Mathf.Clamp(band, 0, numBands - 1);
    }

    /// <summary>
    /// Generates a circular gradient for star/circle textures.
    /// </summary>
    /// <param name="x">X pixel coordinate.</param>
    /// <param name="y">Y pixel coordinate.</param>
    /// <param name="center">Center coordinate of the image.</param>
    /// <param name="radius">Radius of the circle.</param>
    /// <returns>Normalized distance from center (0 at center, 1 at edge).</returns>
    public static float CircleGradient(float x, float y, float center, float radius)
    {
        float dx = x - center;
        float dy = y - center;
        return Mathf.Sqrt(dx * dx + dy * dy) / radius;
    }

    /// <summary>
    /// Applies limb darkening effect (darker at edges).
    /// </summary>
    /// <param name="baseColor">Base color.</param>
    /// <param name="normalizedDistance">Normalized distance from center (0-1).</param>
    /// <param name="intensity">Intensity of the darkening effect.</param>
    /// <returns>Darkened color.</returns>
    public static Color ApplyLimbDarkening(Color baseColor, float normalizedDistance, float intensity = 0.3f)
    {
        float limb = Mathf.Sqrt(1.0f - normalizedDistance * normalizedDistance);
        return baseColor.Darkened((1.0f - limb) * intensity);
    }
}
