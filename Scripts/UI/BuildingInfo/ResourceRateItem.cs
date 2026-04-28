using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Displays a resource with icon, name, and rate (consumption or production).
/// Used in building detail panels for showing input/output rates.
/// </summary>
public partial class ResourceRateItem : HBoxContainer
{
    private TextureRect? _iconRect;
    private Label? _nameLabel;
    private Label? _rateLabel;

    public override void _Ready()
    {
        _iconRect = GetNodeOrNull<TextureRect>("ResourceIcon");
        _nameLabel = GetNodeOrNull<Label>("ResourceNameLabel");
        _rateLabel = GetNodeOrNull<Label>("RateLabel");
    }

    /// <summary>
    /// Sets the resource rate to display.
    /// </summary>
    /// <param name="resourceId">The resource ID</param>
    /// <param name="rate">Rate in units per second (negative for consumption)</param>
    public void SetResourceRate(string resourceId, float rate)
    {
        // Get resource definition for display info
        ResourceDefinition? definition = null;
        var resourceDb = ResourceDatabase.Instance;
        if (resourceDb?.IsLoaded == true)
        {
            resourceDb.TryGetResource(resourceId, out definition);
        }

        // Set resource name
        if (_nameLabel != null)
        {
            _nameLabel.Text = FormatResourceName(definition?.IdName ?? resourceId);
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
    }

    private Texture2D? LoadResourceIcon(ResourceDefinition? definition)
    {
        if (definition?.Icon?.IsValid == true)
        {
            return definition.Icon.SmallTexture ?? definition.Icon.MediumTexture;
        }
        return null;
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
