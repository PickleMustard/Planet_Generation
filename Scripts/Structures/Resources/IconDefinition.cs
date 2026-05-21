using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Defines icon textures for all three standard sizes.
/// All sizes are eagerly loaded at startup.
/// </summary>
public class IconDefinition
{
    /// <summary>Base path pattern for icon files (without size suffix).</summary>
    /// <example>res://Assets/Icons/Resources/ore/iron_ore</example>
    public string? BasePath { get; set; }

    /// <summary>64x64 icon texture.</summary>
    public Texture2D? SmallTexture { get; set; }

    /// <summary>128x128 icon texture.</summary>
    public Texture2D? MediumTexture { get; set; }

    /// <summary>512x512 icon texture.</summary>
    public Texture2D? LargeTexture { get; set; }

    /// <summary>Scale multiplier for UI display.</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Tint color for the icon. White = no tint.</summary>
    public Color Tint { get; set; } = Colors.White;

    /// <summary>
    /// Returns true if all three icon sizes are loaded and valid.
    /// </summary>
    public bool HasAllSizes =>
        SmallTexture != null &&
        MediumTexture != null &&
        LargeTexture != null;

    /// <summary>
    /// Returns true if at least the large (default) icon is loaded.
    /// </summary>
    public bool IsValid => LargeTexture != null;

    /// <summary>
    /// Gets the icon texture for a specific size.
    /// Returns null if not loaded, or if Off (deactivated).
    /// </summary>
    public Texture2D? GetTexture(IconSize size)
    {
        return size switch
        {
            IconSize.Small => SmallTexture,
            IconSize.Medium => MediumTexture,
            IconSize.Large => LargeTexture,
            _ => LargeTexture
        };
    }

    /// <summary>
    /// Gets the effective pixel dimensions for a given size, accounting for scale.
    /// </summary>
    public Vector2 GetDimensions(IconSize size)
    {
        float baseSize = size.GetPixels();
        float scaled = baseSize * Scale;
        return new Vector2(scaled, scaled);
    }
}
