using Godot;
using Structures.Resources;
using UI.Components;
using UtilityLibrary;

namespace UI.BuildingInfo;

/// <summary>
/// Detail view for storage buildings.
/// Displays the storage category, capacity with donut chart, and a scrollable list of stored resources.
/// </summary>
public partial class StorageDetails : BaseBuildingDetails
{
    private Label? _categoryLabel;
    private DonutChart? _capacityChart;
    private Label? _capacityLabel;
    private VBoxContainer? _resourcesList;

    private PackedScene? _resourceStorageItemScene;

    public override void _Ready()
    {
        _categoryLabel = GetNodeOrNull<Label>("VBoxContainer/CategoryLabel");
        _capacityChart = GetNodeOrNull<DonutChart>("VBoxContainer/CapacityRow/CapacityChart");
        _capacityLabel = GetNodeOrNull<Label>("VBoxContainer/CapacityRow/CapacityLabel");
        _resourcesList = GetNodeOrNull<VBoxContainer>("VBoxContainer/ScrollContainer/ResourcesList");

        // Load the ResourceStorageItem scene
        _resourceStorageItemScene = ResourceLoader.Load<PackedScene>("res://UI/BuildingInfo/ResourceStorageItem.tscn");
    }

    protected override void UpdateDisplay()
    {
        if (_building == null || _economy == null)
        {
            Clear();
            return;
        }

        var definition = _building.Definition;

        // Get storage category from building definition
        string storageCategory = GetStorageCategory(definition);
        float storageCapacity = GetStorageCapacity(definition);

        // Update category label
        if (_categoryLabel != null)
        {
            _categoryLabel.Text = $"Storage: {FormatCategoryName(storageCategory)}";
        }

        // Get building-specific storage fill percentage
        float fillRatio = _economy.GetStorageFillPercentage(_building, storageCategory) / 100f;
        float myCapacity = GetStorageCapacity(definition);
        float myUsed = myCapacity * fillRatio;

        // Update capacity chart
        if (_capacityChart != null && myCapacity > 0)
        {
            _capacityChart.Value = fillRatio;
        }

        // Update capacity label
        if (_capacityLabel != null)
        {
            _capacityLabel.Text = $"{myUsed:F0}/{myCapacity:F0}";
        }

        // Update resources list
        UpdateResourcesList(storageCategory, myCapacity, fillRatio);
    }

    public override void Clear()
    {
        base.Clear();

        if (_categoryLabel != null)
        {
            _categoryLabel.Text = "Storage: -";
        }

        if (_capacityChart != null)
        {
            _capacityChart.Value = 0f;
        }

        if (_capacityLabel != null)
        {
            _capacityLabel.Text = "0/0";
        }

        ClearContainer(_resourcesList);
    }

    private void UpdateResourcesList(string category, float buildingCapacity, float buildingFillRatio)
    {
        ClearContainer(_resourcesList);

        if (_resourcesList == null || _resourceStorageItemScene == null || _economy == null)
        {
            return;
        }

        if (buildingFillRatio <= 0f) return;

        // Get all stockpiles in this category
        var stockpiles = _economy.GetAllStockpiles();
        
        float globalCategoryUsed = _economy.GetCategoryUsed(category);
        if (globalCategoryUsed <= 0f) return;

        foreach (var kvp in stockpiles)
        {
            string resourceCategory = GetResourceCategory(kvp.Key);
            if (resourceCategory != category)
            {
                continue;
            }

            if (kvp.Value <= 0)
            {
                continue; // Skip empty resources
            }

            var item = _resourceStorageItemScene.Instantiate<ResourceStorageItem>();
            if (item != null)
            {
                // Proportionally allocate global resources to this building
                float resourceRatio = kvp.Value / globalCategoryUsed;
                float localAmount = (buildingCapacity * buildingFillRatio) * resourceRatio;
                
                item.SetResource(kvp.Key, localAmount, buildingCapacity);
                _resourcesList.AddChild(item);
            }
        }
    }

    private string GetStorageCategory(BuildingDefinition? definition)
    {
        if (definition?.StartingStorageCapacity != null && definition.StartingStorageCapacity.Count > 0)
        {
            // Return the first storage category
            foreach (var kvp in definition.StartingStorageCapacity)
            {
                return kvp.Key;
            }
        }

        // Default categories based on building type or fallback
        return "raw_material";
    }

    private float GetStorageCapacity(BuildingDefinition? definition)
    {
        if (definition?.StartingStorageCapacity != null && definition.StartingStorageCapacity.Count > 0)
        {
            // Sum all storage capacity bonuses
            float total = 0f;
            foreach (var kvp in definition.StartingStorageCapacity)
            {
                total += kvp.Value;
            }
            return total;
        }

        return 0f;
    }

    private string GetResourceCategory(string resourceId)
    {
        var resourceDef = GetResourceDefinition(resourceId);
        if (resourceDef?.ResourceType != null)
        {
            return resourceDef.ResourceType;
        }

        if (resourceId.EndsWith("_ore"))
        {
            return "ore";
        }

        if (resourceId == "power")
        {
            return "power";
        }

        return "raw_material";
    }

    private string FormatCategoryName(string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return "Unknown";
        }

        // Convert "raw_material" to "Raw Materials"
        var words = category.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (!string.IsNullOrEmpty(words[i]) && words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }
        }
        return string.Join(" ", words);
    }

    private void ClearContainer(Container? container)
    {
        if (container == null)
        {
            return;
        }

        foreach (var child in container.GetChildren())
        {
            child.QueueFree();
        }
    }
}
