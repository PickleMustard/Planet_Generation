#if DEBUG
using System.Collections.Generic;
using Godot;

namespace DeveloperTools.Common;

/// <summary>
/// Neutral, data-model-free view-model rendered by <see cref="EntityPickerPopup"/>.
/// Adapters in <see cref="EntityPickers"/> map heterogeneous definition types
/// (resources, recipes, buildings, …) onto this shape so the picker stays decoupled.
/// </summary>
public sealed class PickerItem
{
    /// <summary>Value emitted on pick (e.g. resource IdName, recipe RecipeId).</summary>
    public string Id { get; set; } = "";

    /// <summary>Shown on the cell label.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Grouping/filter category. Null/empty renders as "(uncategorized)".</summary>
    public string? Category { get; set; }

    /// <summary>Tier for grouping/filtering. Null = item has no tier.</summary>
    public int? Tier { get; set; }

    /// <summary>Free-form tags for the tags filter. Null/empty = no tags.</summary>
    public IReadOnlyCollection<string>? Tags { get; set; }

    /// <summary>Cell icon. Null renders a label-only cell.</summary>
    public Texture2D? IconTexture { get; set; }

    /// <summary>Precomputed icon tint (default white).</summary>
    public Color IconTint { get; set; } = Colors.White;

    /// <summary>Optional extra lines appended to the cell tooltip.</summary>
    public IReadOnlyList<string>? ExtraTooltipLines { get; set; }
}
#endif
