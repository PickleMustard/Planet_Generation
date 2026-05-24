using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Displays a resource with icon, name, and rate (consumption or production).
/// Used in building detail panels for showing input/output rates.
/// Supports tag-based recipe slots: when <see cref="IsTag"/> is true, a small
/// "tag" badge is shown; unresolved tags display as "Any {TagName}".
/// </summary>
public partial class ResourceRateItem : HBoxContainer
{
    private TextureRect? _iconRect;
    private Label? _nameLabel;
    private Label? _rateLabel;
    private Label? _tagBadge;

    private static readonly Color TagBadgeColor = new(0.62f, 0.52f, 0.42f);

    public override void _Ready()
    {
        _iconRect = GetNodeOrNull<TextureRect>("ResourceIcon");
        _nameLabel = GetNodeOrNull<Label>("ResourceNameLabel");
        _rateLabel = GetNodeOrNull<Label>("RateLabel");
        _tagBadge = GetNodeOrNull<Label>("TagBadge");
    }

    /// <summary>
    /// Sets the resource rate to display.
    /// </summary>
    /// <param name="resourceId">The resource ID</param>
    /// <param name="rate">Rate in units per second (negative for consumption)</param>
    public void SetResourceRate(string resourceId, float rate)
    {
        SetResourceRate(resourceId, rate, isTag: false, tagDisplayName: null);
    }

    /// <summary>
    /// Sets the resource rate to display with optional tag context.
    /// When <paramref name="isTag"/> is true, a "tag" badge is shown next to the rate.
    /// Unresolved tag slots (null <paramref name="resolvedId"/>) display as "Any {TagDisplayName}".
    /// Resolved tag slots display the concrete resource name.
    /// </summary>
    /// <param name="resourceId">The resource ID to look up (resolved concrete ID, or tag key if unresolved)</param>
    /// <param name="rate">Rate in units per second (negative for consumption)</param>
    /// <param name="isTag">True when this slot comes from a tag: recipe key</param>
    /// <param name="tagDisplayName">Human-readable tag name (e.g., "Ore" from "tag:ore"), null if not a tag</param>
    /// <param name="resolvedId">The concrete resource ID resolved at runtime, null if unresolved</param>
    public void SetResourceRate(string resourceId, float rate, bool isTag, string? tagDisplayName, string? resolvedId = null)
    {
        // Get resource definition for display info
        ResourceDefinition? definition = null;
        var resourceDb = ResourceDatabase.Instance;
        if (resourceDb?.IsLoaded == true)
        {
            // Try the resolved ID first, then fall back to the raw resourceId
            string lookupId = resolvedId ?? resourceId;
            resourceDb.TryGetResource(lookupId, out definition);
        }

        // Set resource name
        if (_nameLabel != null)
        {
            if (isTag && definition == null)
            {
                // Unresolved tag — show "Any {Tag}" format
                string tagLabel = !string.IsNullOrEmpty(tagDisplayName)
                    ? FormatResourceName(tagDisplayName)
                    : FormatResourceName(resourceId);
                _nameLabel.Text = $"Any {tagLabel}";
            }
            else
            {
                _nameLabel.Text = FormatResourceName(definition?.IdName ?? resourceId);
            }
        }

        // Load and set icon
        if (_iconRect != null)
        {
            Texture2D? icon = LoadResourceIcon(definition);
            _iconRect.Texture = icon;
        }

        // Set rate with sign indicator
        if (_rateLabel != null)
        {
            string sign = rate >= 0 ? "+" : "";
            _rateLabel.Text = $"{sign}{rate:F1}/s";
            _rateLabel.Modulate = rate >= 0 ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.9f, 0.3f, 0.2f);
        }

        // Tag badge visibility
        if (_tagBadge != null)
        {
            _tagBadge.Visible = isTag;
            if (isTag)
                _tagBadge.Modulate = TagBadgeColor;
        }
    }

    /// <summary>
    /// Clears the display to default state.
    /// </summary>
    public void Clear()
    {
        if (_nameLabel != null)
        {
            _nameLabel.Text = "-";
        }

        if (_rateLabel != null)
        {
            _rateLabel.Text = "-";
            _rateLabel.Modulate = Colors.White;
        }

        if (_iconRect != null)
        {
            _iconRect.Texture = null;
        }

        if (_tagBadge != null)
        {
            _tagBadge.Visible = false;
        }
    }

    private Texture2D? LoadResourceIcon(ResourceDefinition? definition)
    {
        return definition?.Icon?.Texture;
    }

    private string FormatResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return "Unknown";
        }

        // Convert "iron_ore" to "Iron Ore"
        var words = resourceId.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (!string.IsNullOrEmpty(words[i]) && words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }
        }
        return string.Join(" ", words);
    }
}
