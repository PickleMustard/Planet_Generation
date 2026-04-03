using System.Collections.Generic;
using Godot;

namespace Structures.Resources;

/// <summary>
/// Configuration for tags associated with a specific celestial body subtype.
/// Defines descriptive tags and generation weight for a subtype.
/// </summary>
public class SubtypeTagConfig
{
    /// <summary>
    /// The string representation of the enum subtype (e.g., "Temperate", "Desert", etc.)
    /// </summary>
    public string? Subtype { get; set; }

    /// <summary>
    /// Set of descriptive tags for this subtype.
    /// Tags are used for resource affinity matching and procedural content selection.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Base weight multiplier for resource generation on this subtype.
    /// Values between 0.0 and 2.0 are recommended.
    /// Default: 1.0 (normal generation rate)
    /// </summary>
    public float BaseResourceWeight { get; set; } = 1.0f;

    /// <summary>
    /// Validates the configuration for consistency and reasonable values.
    /// </summary>
    /// <returns>True if configuration is valid, false otherwise</returns>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Subtype))
        {
            GD.PrintErr($"SubtypeTagConfig: Subtype is null or empty");
            return false;
        }

        if (BaseResourceWeight < 0.0f || BaseResourceWeight > 2.0f)
        {
            GD.PrintErr($"SubtypeTagConfig '{Subtype}': BaseResourceWeight {BaseResourceWeight} is outside valid range [0.0, 2.0]");
            return false;
        }

        if (Tags == null)
        {
            GD.PrintErr($"SubtypeTagConfig '{Subtype}': Tags collection is null");
            return false;
        }

        foreach (var tag in Tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                GD.PrintErr($"SubtypeTagConfig '{Subtype}': Contains empty or whitespace tag");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if this subtype has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check for</param>
    /// <returns>True if the tag is present, false otherwise</returns>
    public bool HasTag(string tag)
    {
        return Tags?.Contains(tag) ?? false;
    }

    /// <summary>
    /// Gets the resource weight multiplier for this subtype.
    /// </summary>
    /// <returns>The base resource weight, clamped to valid range</returns>
    public float GetResourceWeight()
    {
        return Mathf.Clamp(BaseResourceWeight, 0.0f, 2.0f);
    }
}