using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Logistics;
using Structures.Resources;

namespace UI.BuildingInfo;

/// <summary>
/// Abstract base class for all building detail views.
/// Provides common functionality for getting BuildingRegistration from economy.
/// </summary>
public abstract partial class BaseBuildingDetails : Control
{
    protected Building? _building;

    /// <summary>
    /// Sets the building and economy to display details for.
    /// Must be called before the view is shown.
    /// </summary>
    public virtual void SetBuilding(Building building)
    {
        _building = building;

        UpdateDisplay();
    }

    /// <summary>
    /// Clears the view to default state.
    /// </summary>
    public virtual void Clear()
    {
        _building = null;
    }

    /// <summary>
    /// Updates the display with current data.
    /// Called automatically when SetBuilding is called.
    /// Subclasses should override this to update their UI.
    /// </summary>
    protected abstract void UpdateDisplay();

    /// <summary>
    /// Gets the active recipe for the current building.
    /// </summary>
    protected RecipeDefinition? GetActiveRecipe()
    {
        var recipeDb = RecipeDatabase.Instance;
        if (recipeDb?.IsLoaded != true || _building == null)
            return null;
        string? recipeId = _building.ActiveRecipeId ?? _building.Definition?.Production?.DefaultRecipe;
        if (string.IsNullOrEmpty(recipeId))
            return null;
        recipeDb.TryGetRecipe(recipeId, out var recipe);
        return recipe;
    }

    protected float GetBuildingResourceQuantity(string resourceId)
    {
        if (_building == null || string.IsNullOrEmpty(resourceId)) return 0f;
        return _building.InputStorage.GetQuantity(resourceId)
             + _building.OutputStorage.GetQuantity(resourceId);
    }

    protected float GetBuildingResourceCapacity(string resourceId)
    {
        if (_building == null || string.IsNullOrEmpty(resourceId)) return 0f;
        return _building.InputStorage.GetCapacity(resourceId)
             + _building.OutputStorage.GetCapacity(resourceId);
    }

    protected float GetStorageTotalUsed(Storage? storage)
    {
        if (storage == null) return 0f;
        float total = 0f;
        foreach (var kvp in storage.GetAllQuantities()) total += kvp.Value;
        return total;
    }

    protected float GetStorageTotalCapacity(Storage? storage)
    {
        if (storage == null) return 0f;
        float total = 0f;
        foreach (var slot in storage.Slots) total += slot.Capacity;
        return total;
    }

    protected void PopulateSlotGrid(GridContainer? grid, Storage? storage, PackedScene? slotScene)
    {
        if (grid == null) return;

        foreach (var child in grid.GetChildren())
        {
            child.QueueFree();
        }

        if (storage == null || slotScene == null) return;

        foreach (var slot in storage.Slots)
        {
            var item = slotScene.Instantiate<ResourceSlotItem>();
            if (item == null) continue;
            grid.AddChild(item);
            item.SetSlot(slot);
        }
    }

    /// <summary>
    /// Gets the ResourceDefinition for a resource ID.
    /// </summary>
    protected ResourceDefinition? GetResourceDefinition(string resourceId)
    {
        var resourceDb = ResourceDatabase.Instance;
        if (resourceDb?.IsLoaded != true)
        {
            return null;
        }

        if (resourceDb.TryGetResource(resourceId, out var definition))
        {
            return definition;
        }

        return null;
    }

    /// <summary>
    /// Per-second input/output rates for the active recipe at the building's production speed.
    /// "power" entries are excluded so callers can render resource flows directly.
    /// </summary>
    public static (Dictionary<string, float> inputs, Dictionary<string, float> outputs)
        ComputeRecipeRates(RecipeDefinition? recipe, BuildingDefinition? definition)
    {
        var inputs = new Dictionary<string, float>();
        var outputs = new Dictionary<string, float>();
        if (recipe == null || definition?.Production == null)
            return (inputs, outputs);
        float speed = definition.Production.ProductionSpeed;
        float cycleTime = recipe.WorkRequired / Mathf.Max(speed, 0.001f);
        if (cycleTime <= 0f)
            return (inputs, outputs);
        foreach (var kvp in recipe.InputResources)
            if (kvp.Key != "power") inputs[kvp.Key] = kvp.Value / cycleTime;
        foreach (var kvp in recipe.OutputResources)
            if (kvp.Key != "power") outputs[kvp.Key] = kvp.Value / cycleTime;
        return (inputs, outputs);
    }

    /// <summary>
    /// Formats a resource ID into a human-readable name.
    /// </summary>
    protected string FormatResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return "Unknown";
        }

        var definition = GetResourceDefinition(resourceId);
        if (definition?.IdName != null)
        {
            resourceId = definition.IdName;
        }

        var words = resourceId.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (!string.IsNullOrEmpty(words[i]) && words[i].Length > 0)
            {
                words[i] =
                    char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }
        }
        return string.Join(" ", words);
    }
}
