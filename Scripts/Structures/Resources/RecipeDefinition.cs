using System.Collections.Generic;

namespace Structures.Resources;

/// <summary>
/// Defines a production recipe with input/output resources and work time per cycle.
/// Buildings reference recipes by ID to determine what they produce and consume.
/// </summary>
public class RecipeDefinition
{
    /// <summary>
    /// Unique identifier for this recipe.
    /// </summary>
    public string? RecipeId { get; set; }

    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description shown in recipe tooltips.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Recipe category (agriculture, extraction, power, etc.)
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Seconds of work required to complete one production cycle.
    /// </summary>
    public float WorkRequired { get; set; } = 10.0f;

    /// <summary>
    /// Resources consumed per production cycle. Key is resource ID, value is amount.
    /// </summary>
    public Dictionary<string, float> InputResources { get; set; } = new();

    /// <summary>
    /// Resources produced per production cycle. Key is resource ID, value is amount.
    /// </summary>
    public Dictionary<string, float> OutputResources { get; set; } = new();
}
