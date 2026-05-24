using System.Collections.Generic;
using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Defines a resource type with display properties and generation tags.
/// </summary>
public class ResourceDefinition
{
    /// <summary>
    /// Unique identifier name for the resource.
    /// </summary>
    public string? IdName { get; set; }

    /// <summary>
    /// The tier level of the resource, indicating rarity or value.
    /// </summary>
    public int ResourceTier { get; set; }

    /// <summary>
    /// The category or type classification of the resource.
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>
    /// Set of tags that define where and how this resource can generate.
    /// Tags match against planetary type tags and biome probability modifiers
    /// to determine generation eligibility and probability per cell.
    /// Empty set if resource does not generate naturally on celestial bodies.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Capacity consumed per unit when transported via transfer stations.
    /// Higher values mean each unit takes more cargo space.
    /// </summary>
    public float TransportWeight { get; set; } = 1.0f;

    /// <summary>
    /// Maximum amount of this resource that can occupy a single storage slot.
    /// Resources are stored as whole units only.
    /// </summary>
    public int MaxStackSize { get; set; } = 100;

    /// <summary>
    /// Physical state. Storage slots enforce matter compatibility — fluids cannot
    /// occupy solid-filtered slots and vice versa.
    /// </summary>
    public StateOfMatter StateOfMatter { get; set; }

    /// <summary>
    /// Gets whether this resource can generate naturally on celestial bodies.
    /// A resource is generatable if it has tags defined for tag-based generation matching.
    /// </summary>
    public bool IsGeneratable => Tags != null && Tags.Count > 0;

    /// <summary>
    /// Visual representation including 2D icon for UI display.
    /// </summary>
    public IconDefinition Icon { get; set; } = new();

    /// <summary>
    /// Gets the effective icon tint. Returns the icon's explicit tint if set,
    /// otherwise white (no tint).
    /// </summary>
    public Color GetEffectiveIconTint()
    {
        if (Icon != null && Icon.Tint != Colors.White)
        {
            return Icon.Tint;
        }
        return Colors.White;
    }
}
