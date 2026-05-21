using System.Collections.Generic;
using Godot;

namespace Structures.Resources;

/// <summary>
/// Data-driven biome definition loaded from <c>Configuration/Biomes/biomes.yaml</c>.
/// Replaces the old <c>Biome.BiomeType</c> enum + scattered switch statements for color,
/// hazard, geothermal vent probability, and resource weight modifiers.
/// </summary>
public sealed class BiomeDefinition
{
    /// <summary>Stable string ID — used as a foreign key across configs. e.g. <c>biome_forest</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name for editor + UI. e.g. "Forest".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Color used when rendering a cell of this biome on a subtype that has no override
    /// in <see cref="ColorOverrides"/>.
    /// </summary>
    public Color DefaultColor { get; set; } = Colors.Gray;

    /// <summary>
    /// Per-subtype color overrides. Key is a <see cref="SubtypeDefinition.Id"/>.
    /// </summary>
    public Dictionary<string, Color> ColorOverrides { get; set; } = new();

    /// <summary>
    /// Per-biome hazard contribution used by the editor's HazardCalculator and at runtime
    /// for cell hazard scores. Range [0, 1].
    /// </summary>
    public float HazardWeight { get; set; }

    /// <summary>
    /// Probability that a cell of this biome receives a geothermal vent during deposit
    /// placement. Range [0, 1]. Replaces hardcoded switch in <c>GeothermalVentGenerator</c>.
    /// </summary>
    public float GeothermalVentProbability { get; set; }

    /// <summary>
    /// Resource weight modifiers keyed by resource ID. Replaces <c>BiomeResourceConfig</c>
    /// + <c>BiomeResourceEntry</c> as the canonical home for these modifiers.
    /// </summary>
    public Dictionary<string, float> ResourceWeightModifiers { get; set; } = new();

    /// <summary>
    /// Free-form tags used by tag-based queries (replaces <c>BiomeTagConfig</c>).
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Resolves the rendering color for this biome on a given subtype.
    /// </summary>
    public Color GetColorForSubtype(string subtypeId)
    {
        return ColorOverrides.TryGetValue(subtypeId, out var c) ? c : DefaultColor;
    }

    /// <summary>
    /// Resource weight modifier lookup with default of 1.0 (no modification).
    /// </summary>
    public float GetWeightModifier(string resourceId)
    {
        return ResourceWeightModifiers.TryGetValue(resourceId, out var w) ? w : 1.0f;
    }
}
