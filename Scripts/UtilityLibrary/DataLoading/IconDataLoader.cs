using System;
using Godot;
using Structures.Resources;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Static library for loading icon textures from the filesystem.
/// Loads a single texture per icon and lets Godot's mipmap pipeline handle
/// runtime resizing at the TextureRect level.
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
    /// Loads an icon definition from a base path.
    /// </summary>
    /// <param name="basePath">Base path without extension (e.g., "res://Assets/Icons/ore/iron_ore")</param>
    /// <param name="context">Context for logging (e.g., entity name)</param>
    /// <returns>IconDefinition with loaded texture, or empty if basePath is null/empty</returns>
    public static IconDefinition LoadIcon(string? basePath, string context)
    {
        var icon = new IconDefinition { BasePath = basePath };

        if (string.IsNullOrEmpty(basePath))
        {
            return icon; // Return empty - data loader will apply fallback
        }

        icon.Texture = LoadIconTexture(basePath, context);
        return icon;
    }

    /// <summary>
    /// Loads a single icon texture. Tries SVG first, then PNG.
    /// </summary>
    /// <param name="basePath">Base path without extension</param>
    /// <param name="context">Context for logging</param>
    /// <returns>Loaded Texture2D or null if loading fails</returns>
    public static Texture2D? LoadIconTexture(string basePath, string context)
    {
        string fullPath = $"{basePath}.svg";

        try
        {
            if (!Godot.FileAccess.FileExists(fullPath))
            {
                fullPath = $"{basePath}.png";
                if (!Godot.FileAccess.FileExists(fullPath))
                {
                    GameLogger.Warning($"Icon not found for {context}: {basePath}");
                    IconsFailed++;
                    return null;
                }
            }

            var texture = GD.Load<Texture2D>(fullPath);
            if (texture != null)
            {
                GameLogger.Debug($"Loaded icon for {context}: {fullPath}");
                IconsLoaded++;
                return texture;
            }
            else
            {
                GameLogger.Error($"Failed to load icon for {context}: {fullPath}");
                IconsFailed++;
                return null;
            }
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Exception loading icon for {context}: {fullPath} - {ex.Message}");
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
            BasePath = null,
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
        return ImageTexture.CreateFromImage(image);
    }
}
