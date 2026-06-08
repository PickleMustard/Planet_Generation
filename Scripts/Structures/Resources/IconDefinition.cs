using Godot;

namespace Structures.Resources;

/// <summary>
/// Defines the icon texture for an entity. A single imported Texture2D is
/// used; Godot's mipmap pipeline handles runtime scaling.
/// </summary>
public class IconDefinition
{
    /// <summary>Path to the icon wrapper resource (<c>IconConfig</c> <c>.tres</c>).</summary>
    /// <example>res://Materials/Icons/ore.tres</example>
    public string? ResourcePath { get; set; }

    /// <summary>The loaded icon texture.</summary>
    public Texture2D? Texture { get; set; }

    /// <summary>Scale multiplier for UI display.</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Tint color for the icon. White = no tint.</summary>
    public Color Tint { get; set; } = Colors.White;

    /// <summary>
    /// Returns true if the icon texture is loaded.
    /// </summary>
    public bool IsValid => Texture != null;
}
