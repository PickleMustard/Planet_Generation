using Godot;
using ProceduralGeneration.PlanetGeneration;
using UtilityLibrary;
using UtilityLibrary.TaskSystem;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Node that manages billboard texture generation for celestial bodies.
/// Stores three resolution levels (64x64, 256x256, 1024x1024) as exportable properties for serialization.
/// </summary>
[GlobalClass]
public partial class BodyBillboardTextures : Resource
{
    [ExportCategory("Billboard Textures")]
    /// <summary>
    /// Low-resolution texture (64x64) for distant viewing.
    /// </summary>
    [Export]
    public ImageTexture? DistantTextureImage { get; set; }

    /// <summary>
    /// Medium-resolution texture (256x256) for far viewing.
    /// </summary>
    [Export]
    public ImageTexture? FarTextureImage { get; set; }

    /// <summary>
    /// High-resolution texture (1024x1024) for close viewing.
    /// </summary>
    [Export]
    public ImageTexture? CloseTextureImage { get; set; }

    private CelestialBody? _targetBody;

    private ImageTexture? _distantTexture;
    private ImageTexture? _farTexture;
    private ImageTexture? _closeTexture;

    public int GenerateAllTextures(ITextureGenerator gen, IOrbitalBody body)
    {
        try
        {
            var (distant, far, close) = gen.Generate(body);
            DistantTextureImage = distant;
            FarTextureImage = far;
            CloseTextureImage = close;
            return 0;
        }
        catch (System.Exception ex)
        {
            GameLogger.Warning($"Texture generation failed: {ex.Message}. Using fallback.");
            DistantTextureImage = CreateFallbackTexture(64);
            FarTextureImage = CreateFallbackTexture(256);
            CloseTextureImage = CreateFallbackTexture(1024);
            return 0;
        }
    }

    private ImageTexture CreateFallbackTexture(int resolution)
    {
        var image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);
        image.Fill(Colors.Gray);
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Gets the appropriate texture for a given view distance.
    /// </summary>
    /// <param name="distance">Distance to the body in world units.</param>
    /// <param name="distantThreshold">Distance threshold for distant texture (default: 10000).</param>
    /// <param name="farThreshold">Distance threshold for far texture (default: 2000).</param>
    /// <returns>The appropriate texture for the distance.</returns>
    public ImageTexture? GetTextureForDistance(
        float distance,
        float distantThreshold = 10000f,
        float farThreshold = 2000f
    )
    {
        if (distance > distantThreshold)
            return DistantTextureImage;
        if (distance > farThreshold)
            return FarTextureImage;
        return CloseTextureImage;
    }

    /// <summary>
    /// Checks if all textures have been generated.
    /// </summary>
    public bool AreTexturesReady =>
        DistantTextureImage != null && FarTextureImage != null && CloseTextureImage != null;
}
