using Godot;
using Structures.Resources;
using UI.Components;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Displays a single resource in storage with icon, name, amount, and mini donut chart.
/// Used in scrollable resource lists inside the building info panels.
/// </summary>
public partial class ResourceStorageItem : HBoxContainer
{
    private TextureRect? _iconRect;
    private Label? _nameLabel;
    private Label? _amountLabel;
    private DonutChart? _fillChart;

    public override void _Ready()
    {
        _iconRect = GetNodeOrNull<TextureRect>("ResourceIcon");
        _nameLabel = GetNodeOrNull<Label>("ResourceNameLabel");
        _amountLabel = GetNodeOrNull<Label>("AmountLabel");
        _fillChart = GetNodeOrNull<DonutChart>("FillChart");
    }

    /// <summary>
    /// Sets the resource storage data to display.
    /// </summary>
    /// <param name="resourceId">The resource ID</param>
    /// <param name="amount">Current amount stored</param>
    /// <param name="capacity">Maximum capacity for this resource</param>
    public void SetResource(string resourceId, float amount, float capacity)
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

        // Set amount text
        if (_amountLabel != null)
        {
            if (capacity > 0)
            {
                _amountLabel.Text = $"{amount:F0}/{capacity:F0}";
            }
            else
            {
                _amountLabel.Text = $"{amount:F0}";
            }
        }

        // Update fill chart
        if (_fillChart != null && capacity > 0)
        {
            float fillRatio = Mathf.Clamp(amount / capacity, 0f, 1f);
            _fillChart.Value = fillRatio;
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

        if (_amountLabel != null)
        {
            _amountLabel.Text = "-";
        }

        if (_iconRect != null)
        {
            _iconRect.Texture = null;
        }

        if (_fillChart != null)
        {
            _fillChart.Value = 0f;
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
