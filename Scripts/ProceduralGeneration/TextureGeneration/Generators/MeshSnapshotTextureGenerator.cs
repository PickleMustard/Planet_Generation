using Godot;
using ProceduralGeneration.ColorSystem;
using UtilityLibrary;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// Texture generator that rasterizes the actual generated mesh into billboard textures.
/// Falls back to a noise-based generator if mesh data is unavailable.
/// </summary>
public class MeshSnapshotTextureGenerator : ITextureGenerator
{
    private readonly ITextureGenerator? _fallback;

    public MeshSnapshotTextureGenerator(ITextureGenerator? fallback = null)
    {
        _fallback = fallback;
    }

    public (ImageTexture Distant, ImageTexture Far, ImageTexture Close) Generate(IOrbitalBody body)
    {
        var cells = body.Mesh?.StrDb?.VoronoiCells;
        if (cells == null || cells.Count == 0)
        {
            if (_fallback != null)
            {
                GameLogger.Info(
                    $"MeshSnapshotTextureGenerator: No mesh data for {body.BodyName}, using fallback"
                );
                return _fallback.Generate(body);
            }

            GameLogger.Warning(
                $"MeshSnapshotTextureGenerator: No mesh data and no fallback for {body.BodyName}"
            );
            return (
                CreateFallbackTexture(64),
                CreateFallbackTexture(256),
                CreateFallbackTexture(1024)
            );
        }

        var colorMapper =
            body.Mesh!.Classification != null
                ? ColorMapperFactory.GetMapper(body.Mesh.Classification)
                : new DataDrivenColorMapper();

        bool useCellBiome = true;
        float bodySize = body.Mesh.size;

        var distantImage = MeshRasterizer.RasterizeMesh(
            cells,
            colorMapper,
            useCellBiome,
            bodySize,
            64
        );
        var farImage = MeshRasterizer.RasterizeMesh(
            cells,
            colorMapper,
            useCellBiome,
            bodySize,
            256
        );
        var closeImage = MeshRasterizer.RasterizeMesh(
            cells,
            colorMapper,
            useCellBiome,
            bodySize,
            1024
        );

        GameLogger.Info($"Generated texture for {body} using MeshSnapshotTextureGenerator");

        return (
            ImageTexture.CreateFromImage(distantImage),
            ImageTexture.CreateFromImage(farImage),
            ImageTexture.CreateFromImage(closeImage)
        );
    }

    private static ImageTexture CreateFallbackTexture(int resolution)
    {
        var image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);
        image.Fill(Colors.Gray);
        return ImageTexture.CreateFromImage(image);
    }
}
