using System;
using Godot;
using Registries;
using Structures.Resources;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Static library for loading icon textures from <c>IconConfig</c> <c>.tres</c> wrapper
/// resources. Configuration points at the <c>.tres</c> (export-safe) instead of a raw
/// <c>.png</c>/<c>.svg</c>; the texture is pulled from the wrapper. Godot's mipmap pipeline
/// handles runtime resizing at the TextureRect level.
/// </summary>
public static class IconDataLoader
{
    private const int FallbackPixelSize = 128;

    private static Texture2D? _fallbackTexture;

    public static int IconsLoaded { get; private set; }
    public static int IconsFailed { get; private set; }

    /// <summary>
    /// Resets loading statistics.
    /// </summary>
    public static void ResetStats()
    {
        IconsLoaded = 0;
        IconsFailed = 0;
    }

    /// <summary>
    /// Loads an icon definition from an <c>IconConfig</c> <c>.tres</c> wrapper path.
    /// </summary>
    /// <param name="resourcePath">Path to the wrapper resource (e.g., "res://Materials/Icons/ore.tres")</param>
    /// <param name="context">Context for logging (e.g., entity name)</param>
    /// <returns>IconDefinition populated from the wrapper, or empty if resourcePath is null/empty</returns>
    public static IconDefinition LoadIcon(string? resourcePath, string context)
    {
        var icon = new IconDefinition { ResourcePath = resourcePath };

        if (string.IsNullOrEmpty(resourcePath))
        {
            return icon; // Return empty - data loader will apply fallback
        }

        var config = LoadIconConfig(resourcePath, context);
        if (config != null)
        {
            icon.Texture = config.Texture;
            icon.Scale = config.Scale;
            icon.Tint = config.Tint;
        }
        return icon;
    }

    /// <summary>
    /// Loads the texture out of an <c>IconConfig</c> <c>.tres</c> wrapper.
    /// </summary>
    /// <param name="resourcePath">Path to the wrapper resource (a <c>.tres</c>)</param>
    /// <param name="context">Context for logging</param>
    /// <returns>Loaded Texture2D or null if the wrapper is missing/invalid</returns>
    public static Texture2D? LoadIconTexture(string resourcePath, string context)
        => LoadIconConfig(resourcePath, context)?.Texture;

    /// <summary>
    /// Loads an <c>IconConfig</c> wrapper resource, guarding against a missing file so the
    /// exporter-stripped/absent case logs a warning rather than throwing.
    /// </summary>
    private static IconConfig? LoadIconConfig(string resourcePath, string context)
    {
        try
        {
            // Use a rooted cache so the shared managed wrapper is never GC-collected.
            // GD.Load returns the single shared instance for a cached resource; letting it be
            // orphaned makes its finalizer run on the GC thread (SIGILL), and disposing it
            // would empty the config for every other consumer that shares it.
            var config = WrapperResourceCache.Load<IconConfig>(resourcePath);
            if (config?.Texture != null)
            {
                GameLogger.Debug($"Loaded icon for {context}: {resourcePath}");
                IconsLoaded++;
                return config;
            }

            GameLogger.Error($"Failed to load icon wrapper for {context}: {resourcePath}");
            IconsFailed++;
            return null;
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Exception loading icon for {context}: {resourcePath} - {ex.Message}");
            IconsFailed++;
            return null;
        }
    }

    /// <summary>
    /// Gets the shared fallback icon texture, generating it on first access.
    /// </summary>
    public static Texture2D GetFallbackIcon()
    {
        return _fallbackTexture ??= GenerateFallbackTexture();
    }

    /// <summary>
    /// Creates an IconDefinition populated with the fallback texture.
    /// </summary>
    public static IconDefinition CreateFallbackIconDefinition()
    {
        return new IconDefinition
        {
            ResourcePath = null,
            Texture = GetFallbackIcon()
        };
    }

    private static Texture2D GenerateFallbackTexture()
    {
        const int pixels = FallbackPixelSize;
        string svg = $@"<svg width=""{pixels}"" height=""{pixels}"" viewBox=""0 0 {pixels} {pixels}"" xmlns=""http://www.w3.org/2000/svg"">
            <rect width=""{pixels}"" height=""{pixels}"" fill=""#333333"" rx=""8"" ry=""8""/>
            <text x=""{pixels / 2}"" y=""{pixels / 2 + pixels / 8}"" font-family=""Arial, sans-serif""
                  font-size=""{pixels / 2}"" fill=""#666666"" text-anchor=""middle"">?</text>
        </svg>";

        var image = new Image();
        image.LoadSvgFromBuffer(svg.ToUtf8Buffer(), pixels);
        var tex = ImageTexture.CreateFromImage(image);
        image.Dispose();
        return tex;
    }
}
