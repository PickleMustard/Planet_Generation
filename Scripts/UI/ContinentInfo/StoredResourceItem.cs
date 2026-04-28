using Godot;
using Structures.Resources;
using System.Linq;

namespace UI.ContinentInfo;

/// <summary>
/// A list item for displaying a stored resource in a category section.
/// Shows icon, name, amount, and percentage of category capacity.
/// Used in Section 3 of the Continent Resource Panel.
/// </summary>
public partial class StoredResourceItem : HBoxContainer
{
    private TextureRect? _iconRect;
    private Label? _nameLabel;
    private Label? _amountLabel;
    private Label? _percentageLabel;

    public override void _Ready()
    {
        // Cache node references
        _iconRect = GetNodeOrNull<TextureRect>("IconRect");
        _nameLabel = GetNodeOrNull<Label>("ResourceNameLabel");
        _amountLabel = GetNodeOrNull<Label>("AmountLabel");
        _percentageLabel = GetNodeOrNull<Label>("PercentageLabel");
    }

    /// <summary>
    /// Sets up the resource item with the given resource data.
    /// </summary>
    /// <param name="resourceId">The resource identifier</param>
    /// <param name="amount">Amount stored</param>
    /// <param name="percentageOfCategory">Percentage of category capacity (0-1)</param>
    /// <param name="definition">Optional resource definition for display info</param>
    public void SetResource(string resourceId, float amount, float percentageOfCategory, ResourceDefinition? definition)
    {
        // Get display name from definition or format resource ID
        string displayName = definition?.IdName != null
            ? FormatResourceName(definition.IdName)
            : FormatResourceName(resourceId);

        if (_nameLabel != null)
        {
            _nameLabel.Text = displayName;
        }

        // Load and set icon, falling back to color-based fallback if no icon
        if (_iconRect != null)
        {
            Texture2D? icon = LoadResourceIcon(definition);
            if (icon != null)
            {
                _iconRect.Texture = icon;
                _iconRect.Modulate = Colors.White;
            }
            else
            {
                // Fallback: use modulate color
                _iconRect.Texture = null;
                _iconRect.Modulate = definition?.DisplayColor ?? Colors.Gray;
            }
        }

        // Format amount
        if (_amountLabel != null)
        {
            _amountLabel.Text = FormatAmount(amount);
        }

        // Format percentage
        if (_percentageLabel != null)
        {
            _percentageLabel.Text = $"{percentageOfCategory:P0}";
        }
    }

    /// <summary>
    /// Clears the item back to default state.
    /// </summary>
    public void Clear()
    {
        if (_nameLabel != null)
        {
            _nameLabel.Text = "-";
        }

        if (_iconRect != null)
        {
            _iconRect.Texture = null;
            _iconRect.Modulate = Colors.Gray;
        }

        if (_amountLabel != null)
        {
            _amountLabel.Text = "-";
        }

        if (_percentageLabel != null)
        {
            _percentageLabel.Text = "-";
        }
    }

    /// <summary>
    /// Formats an amount value into a readable string.
    /// Large numbers use K/M suffixes.
    /// </summary>
    private string FormatAmount(float amount)
    {
        if (amount >= 1_000_000)
        {
            return $"{amount / 1_000_000:F1}M";
        }
        if (amount >= 1_000)
        {
            return $"{amount / 1_000:F1}K";
        }
        return $"{amount:F1}";
    }

    /// <summary>
    /// Loads the resource icon from the definition.
    /// </summary>
    private Texture2D? LoadResourceIcon(ResourceDefinition? definition)
    {
        if (definition?.Icon?.IsValid == true)
        {
            return definition.Icon.SmallTexture ?? definition.Icon.MediumTexture;
        }
        return null;
    }

    /// <summary>
    /// Formats a resource ID into a human-readable name.
    /// Converts "iron_ore" to "Iron Ore".
    /// </summary>
    private string FormatResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return "Unknown";
        }

        var words = resourceId.Split('_');
        var capitalizedWords = words.Select(w =>
        {
            if (string.IsNullOrEmpty(w))
            {
                return w;
            }
            return char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant();
        });

        return string.Join(" ", capitalizedWords);
    }
}
