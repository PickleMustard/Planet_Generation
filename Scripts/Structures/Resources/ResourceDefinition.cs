using System.Collections.Generic;
using Godot;
using Structures.Enums;

namespace Structures.Resources;

/// <summary>
/// Defines a resource type with display properties, biome affinities, and elevation constraints.
/// </summary>
public class ResourceDefinition
{
    /// <summary>
    /// Unique identifier name for the resource.
    /// </summary>
    public string IdName { get; set; }

    /// <summary>
    /// The tier level of the resource, indicating rarity or value.
    /// </summary>
    public int ResourceTier { get; set; }

    /// <summary>
    /// The category or type classification of the resource.
    /// </summary>
    public string ResourceType { get; set; }

    /// <summary>
    /// The color used to display this resource in the UI and map views.
    /// </summary>
    public Color DisplayColor { get; set; } = Colors.White;

    /// <summary>
    /// Dictionary mapping biome types to spawn rate multipliers.
    /// Values greater than 1.0 increase spawn likelihood, values less than 1.0 decrease it.
    /// </summary>
    public Dictionary<Biome.BiomeType, float> BiomeAffinity { get; set; } = new();

    /// <summary>
    /// The minimum elevation at which this resource can spawn.
    /// </summary>
    public float MinElevation { get; set; } = 0.0f;

    /// <summary>
    /// The maximum elevation at which this resource can spawn.
    /// </summary>
    public float MaxElevation { get; set; } = 1.0f;
}
