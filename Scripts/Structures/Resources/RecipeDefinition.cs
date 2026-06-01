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

    /// <summary>
    /// Additional outputs gated by per-cycle condition expressions. Each entry
    /// fires independently in addition to <see cref="OutputResources"/> when its
    /// compiled condition evaluates true against the building's recipe context.
    /// </summary>
    public List<ConditionalOutput> ConditionalOutputs { get; set; } = new();

    /// <summary>
    /// Visual representation for UI display.
    /// Recipes must explicitly define an icon_base_path.
    /// </summary>
    public IconDefinition Icon { get; set; } = new();

    /// <summary>
    /// Free-form tags used for filtering and discovery in the recipe editor.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Returns true if this recipe has an explicit icon configured.
    /// </summary>
    public bool HasIcon => Icon?.IsValid == true;

    public const string TagInputPrefix = "tag:";

    /// <summary>True when an input key represents a resource-tag requirement instead of a specific resource ID.</summary>
    public static bool IsTagInput(string key) => key != null && key.StartsWith(TagInputPrefix);

    /// <summary>Strip the tag: prefix from a tag-input key.</summary>
    public static string GetTagName(string key) => IsTagInput(key) ? key[TagInputPrefix.Length..] : key;
}
