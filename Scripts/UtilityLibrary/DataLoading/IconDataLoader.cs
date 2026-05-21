using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Resources;

namespace UtilityLibrary.DataLoading;

/// <summary>
/// Static library for loading icon textures from the filesystem.
/// Supports loading all three standard sizes (64, 128, 512) from a base path pattern.
/// </summary>
public static class IconDataLoader
{
    // Fallback textures cached by size
    private static readonly Dictionary<IconSize, Texture2D> _fallbackTextures = new();
    private static bool _fallbacksInitialized = false;

    // Statistics tracking
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
    /// Loads an icon definition with all three sizes from a base path.
    /// </summary>
    /// <param name="basePath">Base path without size suffix (e.g., "res://Assets/Icons/ore/iron_ore")</param>
    /// <param name="context">Context for logging (e.g., entity name)</param>
    /// <returns>IconDefinition with loaded textures, or empty if basePath is null/empty</returns>
    public static IconDefinition LoadIcon(string? basePath, string context)
    {
        var icon = new IconDefinition { BasePath = basePath };

        if (string.IsNullOrEmpty(basePath))
        {
            return icon; // Return empty - data loader will apply fallback
        }

        // Load all three sizes (Off skips in LoadIconTexture)
        icon.SmallTexture = LoadIconTexture(basePath, IconSize.Small, context);
        icon.MediumTexture = LoadIconTexture(basePath, IconSize.Medium, context);
        icon.LargeTexture = LoadIconTexture(basePath, IconSize.Large, context);

        return icon;
    }

    /// <summary>
    /// Loads a single icon texture for a specific size.
    /// Large uses the base name with no suffix; Medium adds "_med"; Small adds "_small".
    /// Returns null immediately for Off.
    /// </summary>
    /// <param name="basePath">Base path without size suffix</param>
    /// <param name="size">Icon size to load</param>
    /// <param name="context">Context for logging</param>
    /// <returns>Loaded Texture2D or null if loading fails</returns>
    public static Texture2D? LoadIconTexture(string basePath, IconSize size, string context)
    {
        string suffix = size.GetSuffix();
        string fullPath = $"{basePath}{suffix}.svg";

        try
        {
            if (!Godot.FileAccess.FileExists(fullPath))
            {
                // Try PNG fallback
                fullPath = $"{basePath}{suffix}.png";
                if (!Godot.FileAccess.FileExists(fullPath))
                {
                    GameLogger.Warning($"Icon not found for {context}: {basePath}{suffix} ({size})");
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
    /// Gets the fallback texture for a specific size.
    /// Generates and caches fallback on first call.
    /// </summary>
    public static Texture2D GetFallbackIcon(IconSize size)
    {
        InitializeFallbacks();
        return _fallbackTextures.GetValueOrDefault(size, _fallbackTextures[IconSize.Large]);
    }

    /// <summary>
    /// Creates a complete IconDefinition using fallback textures for all sizes.
    /// </summary>
    public static IconDefinition CreateFallbackIconDefinition()
    {
        InitializeFallbacks();

        return new IconDefinition
        {
            BasePath = null,
            SmallTexture = _fallbackTextures[IconSize.Small],
            MediumTexture = _fallbackTextures[IconSize.Medium],
            LargeTexture = _fallbackTextures[IconSize.Large]
        };
    }

    private static void InitializeFallbacks()
    {
        if (_fallbacksInitialized)
            return;

        foreach (IconSize size in Enum.GetValues<IconSize>())
        {
            _fallbackTextures[size] = GenerateFallbackTexture(size);
        }

        _fallbacksInitialized = true;
    }

    private static Texture2D GenerateFallbackTexture(IconSize size)
    {
        int pixels = size.GetPixels();

        // Generate SVG with ? mark
        string svg = $@"<svg width=""{pixels}"" height=""{pixels}"" viewBox=""0 0 {pixels} {pixels}"" xmlns=""http://www.w3.org/2000/svg"">
            <rect width=""{pixels}"" height=""{pixels}"" fill=""#333333"" rx=""8"" ry=""8""/>
            <text x=""{pixels / 2}"" y=""{pixels / 2 + pixels / 8}"" font-family=""Arial, sans-serif""
                  font-size=""{pixels / 2}"" fill=""#666666"" text-anchor=""middle"">?</text>
        </svg>";

        var image = new Image();
        image.LoadSvgFromBuffer(svg.ToUtf8Buffer(), (float)pixels);
        return ImageTexture.CreateFromImage(image);
    }
}
