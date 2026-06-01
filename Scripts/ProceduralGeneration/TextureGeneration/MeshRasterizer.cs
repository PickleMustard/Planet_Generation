using System.Collections.Generic;
using Godot;
using ProceduralGeneration.ColorSystem;
using Structures.GameState;
using Structures.MeshGeneration;

namespace ProceduralGeneration.TextureGeneration;

/// <summary>
/// CPU software rasterizer that projects mesh triangles onto a 2D image via orthographic projection.
/// Produces a circular planet-disc billboard from VoronoiCell mesh data.
/// Thread-safe: no scene tree or GPU dependencies.
/// </summary>
public static class MeshRasterizer
{
    /// <summary>
    /// Rasterizes a celestial body mesh into a 2D billboard image.
    /// </summary>
    /// <param name="cells">VoronoiCell list with finalized vertex positions.</param>
    /// <param name="colorMapper">Biome-to-color mapper matching the body type.</param>
    /// <param name="useCellBiome">If true, use cell-level biome; otherwise per-point biome.</param>
    /// <param name="bodySize">Body radius used as fallback for projection bounds.</param>
    /// <param name="resolution">Output image resolution (square).</param>
    /// <param name="viewDirection">Normalized view direction (camera looks along this).</param>
    /// <param name="upDirection">Normalized up direction for the camera.</param>
    /// <returns>RGBA8 Image of the given resolution with the rasterized mesh.</returns>
    public static Image RasterizeMesh(
        List<VoronoiCell> cells,
        IColorMapper colorMapper,
        bool useCellBiome,
        float bodySize,
        int resolution,
        Vector3 viewDirection,
        Vector3 upDirection
    )
    {
        // Build orthographic camera basis
        Vector3 right = upDirection.Cross(viewDirection).Normalized();
        Vector3 up = viewDirection.Cross(right).Normalized();

        // Find max vertex radius for projection bounds
        float maxRadius = FindMaxRadius(cells, bodySize);
        float scale = (resolution * 0.5f) / maxRadius;
        float centerOffset = resolution * 0.5f;

        // Allocate z-buffer and image
        float[] zBuffer = new float[resolution * resolution];
        for (int i = 0; i < zBuffer.Length; i++)
            zBuffer[i] = float.NegativeInfinity;

        var image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);

        // Rasterize all triangles
        foreach (VoronoiCell cell in cells)
        {
            Color cellColor = useCellBiome
                ? colorMapper.GetBiomeColor(cell.Biome, cell.Height)
                : default;

            for (int i = 0; i < cell.Points.Length / 3; i++)
            {
                Point pt0 = cell.Points[3 * i];
                Point pt1 = cell.Points[3 * i + 1];
                Point pt2 = cell.Points[3 * i + 2];

                Vector3 p0 = pt0.Position;
                Vector3 p1 = pt1.Position;
                Vector3 p2 = pt2.Position;

                // Back-face culling
                Vector3 faceNormal = (p1 - p0).Cross(p2 - p0);
                if (faceNormal.Dot(viewDirection) >= 0)
                    continue;

                // Get vertex colors
                Color c0, c1, c2;
                if (useCellBiome)
                {
                    c0 = c1 = c2 = cellColor;
                }
                else
                {
                    c0 = colorMapper.GetBiomeColor(pt0.Biome, pt0.Height);
                    c1 = colorMapper.GetBiomeColor(pt1.Biome, pt1.Height);
                    c2 = colorMapper.GetBiomeColor(pt2.Biome, pt2.Height);
                }

                // Project to screen space
                float sx0 = p0.Dot(right) * scale + centerOffset;
                float sy0 = -p0.Dot(up) * scale + centerOffset; // flip Y for image coords
                float sz0 = p0.Dot(viewDirection);

                float sx1 = p1.Dot(right) * scale + centerOffset;
                float sy1 = -p1.Dot(up) * scale + centerOffset;
                float sz1 = p1.Dot(viewDirection);

                float sx2 = p2.Dot(right) * scale + centerOffset;
                float sy2 = -p2.Dot(up) * scale + centerOffset;
                float sz2 = p2.Dot(viewDirection);

                RasterizeTriangle(
                    sx0, sy0, sz0, c0,
                    sx1, sy1, sz1, c1,
                    sx2, sy2, sz2, c2,
                    resolution, zBuffer, image
                );
            }
        }

        // Apply circular disc mask
        ApplyCircularMask(image, resolution);

        return image;
    }

    /// <summary>
    /// Convenience overload using default view direction (forward/-Z) and up (+Y).
    /// </summary>
    public static Image RasterizeMesh(
        List<VoronoiCell> cells,
        IColorMapper colorMapper,
        bool useCellBiome,
        float bodySize,
        int resolution
    )
    {
        return RasterizeMesh(
            cells, colorMapper, useCellBiome, bodySize, resolution,
            Vector3.Forward, Vector3.Up
        );
    }

    private static float FindMaxRadius(List<VoronoiCell> cells, float bodySize)
    {
        float maxRadiusSq = bodySize * bodySize;
        foreach (VoronoiCell cell in cells)
        {
            for (int i = 0; i < cell.Points.Length; i++)
            {
                float rSq = cell.Points[i].Position.LengthSquared();
                if (rSq > maxRadiusSq)
                    maxRadiusSq = rSq;
            }
        }
        // Small margin so the mesh doesn't touch the image edge
        return Mathf.Sqrt(maxRadiusSq) * 1.05f;
    }

    private static void RasterizeTriangle(
        float sx0, float sy0, float sz0, Color c0,
        float sx1, float sy1, float sz1, Color c1,
        float sx2, float sy2, float sz2, Color c2,
        int resolution, float[] zBuffer, Image image
    )
    {
        // Bounding box, clamped to image
        int minX = Mathf.Max(0, (int)Mathf.Floor(Mathf.Min(sx0, Mathf.Min(sx1, sx2))));
        int maxX = Mathf.Min(resolution - 1, (int)Mathf.Ceil(Mathf.Max(sx0, Mathf.Max(sx1, sx2))));
        int minY = Mathf.Max(0, (int)Mathf.Floor(Mathf.Min(sy0, Mathf.Min(sy1, sy2))));
        int maxY = Mathf.Min(resolution - 1, (int)Mathf.Ceil(Mathf.Max(sy0, Mathf.Max(sy1, sy2))));

        // Precompute barycentric denominator
        float denom = (sy1 - sy2) * (sx0 - sx2) + (sx2 - sx1) * (sy0 - sy2);
        if (Mathf.Abs(denom) < 1e-8f)
            return; // Degenerate triangle

        float invDenom = 1.0f / denom;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float pxf = px + 0.5f;
                float pyf = py + 0.5f;

                // Barycentric coordinates
                float w0 = ((sy1 - sy2) * (pxf - sx2) + (sx2 - sx1) * (pyf - sy2)) * invDenom;
                float w1 = ((sy2 - sy0) * (pxf - sx2) + (sx0 - sx2) * (pyf - sy2)) * invDenom;
                float w2 = 1.0f - w0 - w1;

                if (w0 < 0 || w1 < 0 || w2 < 0)
                    continue;

                // Depth test
                float z = w0 * sz0 + w1 * sz1 + w2 * sz2;
                int idx = py * resolution + px;
                if (z <= zBuffer[idx])
                    continue;
                zBuffer[idx] = z;

                // Interpolate color
                float r = w0 * c0.R + w1 * c1.R + w2 * c2.R;
                float g = w0 * c0.G + w1 * c1.G + w2 * c2.G;
                float b = w0 * c0.B + w1 * c1.B + w2 * c2.B;

                image.SetPixel(px, py, new Color(r, g, b, 1.0f));
            }
        }
    }

    private static void ApplyCircularMask(Image image, int resolution)
    {
        float center = resolution * 0.5f;
        float maxRadius = resolution * 0.5f;
        // 1-pixel feather for anti-aliasing at the disc edge
        float featherStart = maxRadius - 1.0f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > maxRadius)
                {
                    image.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
                else if (dist > featherStart)
                {
                    // Smooth edge feather
                    float alpha = 1.0f - (dist - featherStart) / (maxRadius - featherStart);
                    Color c = image.GetPixel(x, y);
                    image.SetPixel(x, y, new Color(c.R, c.G, c.B, c.A * alpha));
                }
            }
        }
    }
}
