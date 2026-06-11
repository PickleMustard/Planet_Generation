#if DEBUG
namespace DeveloperTools.Common;

/// <summary>
/// Declares the UI capabilities of an <see cref="EntityPickerPopup"/> instance.
/// Computed by the per-type adapters in <see cref="EntityPickers"/>. Filters/group-modes
/// also auto-hide when the underlying items contain no values for them.
/// </summary>
public sealed class PickerConfig
{
    public string Title { get; set; } = "Pick";
    public string SearchPlaceholder { get; set; } = "Search…";
    public int Columns { get; set; } = 6;

    /// <summary>Offer "Tier" in the group-by dropdown and show the tier filter.</summary>
    public bool AllowGroupByTier { get; set; } = true;

    /// <summary>Offer "Category" in the group-by dropdown.</summary>
    public bool AllowGroupByCategory { get; set; } = true;

    public bool ShowTierFilter { get; set; } = true;
    public bool ShowCategoryFilter { get; set; } = true;
    public bool ShowTagsFilter { get; set; } = true;

    /// <summary>Forward-compat surface only; not yet implemented (see EntityPickerPopup).</summary>
    public bool MultiSelect { get; set; } = false;
}
#endif
