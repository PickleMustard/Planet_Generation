using System.Collections.Generic;
using Godot;

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
    /// The color used to display this resource in the UI and map views.
    /// Defaults to white if not specified in configuration.
    /// </summary>
    public Color DisplayColor { get; set; } = Colors.White;

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
    /// Gets whether this resource can generate naturally on celestial bodies.
    /// A resource is generatable if it has tags defined for tag-based generation matching.
    /// </summary>
    public bool IsGeneratable => Tags != null && Tags.Count > 0;

    /// <summary>
    /// Visual representation including 2D icon for UI display.
    /// </summary>
    public IconDefinition Icon { get; set; } = new();

    /// <summary>
    /// Gets the effective icon tint, falling back to DisplayColor if not set.
    /// </summary>
    public Color GetEffectiveIconTint()
    {
        if (Icon != null && Icon.Tint != Colors.White)
        {
            return Icon.Tint;
        }
        return DisplayColor;
    }
}
