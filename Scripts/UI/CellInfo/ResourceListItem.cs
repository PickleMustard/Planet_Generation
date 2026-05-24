using Godot;
using Structures.Resources;
using System.Linq;

namespace UI.CellInfo;

/// <summary>
/// A reusable UI element that displays a single resource entry with an icon,
/// resource name, and abundance percentage.
/// </summary>
public partial class ResourceListItem : HBoxContainer
{
    private TextureRect? _iconRect;
    private Label? _nameLabel;
    private Label? _abundanceLabel;

    public override void _Ready()
    {
        // Cache node references
        _iconRect = GetNodeOrNull<TextureRect>("IconRect");
        _nameLabel = GetNodeOrNull<Label>("ResourceNameLabel");
        _abundanceLabel = GetNodeOrNull<Label>("AbundanceLabel");
    }

    /// <summary>
    /// Sets up the resource item with the given resource data.
    /// </summary>
    /// <param name="resourceId">The resource identifier</param>
    /// <param name="abundance">Abundance value 0-1</param>
    /// <param name="definition">Optional resource definition for display info</param>
    public void SetResource(string resourceId, float abundance, ResourceDefinition? definition)
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
                // Fallback: create a solid color texture
                _iconRect.Texture = null;
                _iconRect.Modulate = Colors.Gray;
            }
        }

        // Format abundance as percentage
        if (_abundanceLabel != null)
        {
            _abundanceLabel.Text = $"{abundance:P0}";
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

        if (_abundanceLabel != null)
        {
            _abundanceLabel.Text = "-";
        }
    }

    /// <summary>
    /// Loads the resource icon from the definition.
    /// </summary>
    private Texture2D? LoadResourceIcon(ResourceDefinition? definition)
    {
        return definition?.Icon?.Texture;
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
