using Godot;
using ProceduralGeneration.PlanetGeneration;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Interface for texture generators that create billboard textures for celestial bodies.
/// Implementations generate three resolution levels (64x64, 256x256, 1024x1024) for LOD rendering.
/// </summary>
public interface ITextureGenerator
{
    /// <summary>
    /// Generates three texture resolutions for a celestial body.
    /// </summary>
    /// <param name="body">The celestial body to generate textures for.</param>
    /// <returns>A tuple containing Distant (64x64), Far (256x256), and Close (1024x1024) textures.</returns>
    (ImageTexture Distant, ImageTexture Far, ImageTexture Close) Generate(IOrbitalBody body);
}
