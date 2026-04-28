using Godot;
using Constructables;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;
using System.Linq;

namespace UI.BuildingInfo;

/// <summary>
/// Abstract base class for all building detail views.
/// Provides common functionality for getting BuildingRegistration from economy.
/// </summary>
public abstract partial class BaseBuildingDetails : Control
{
    protected BuildingConstruction? _building;
    protected ContinentEconomy? _economy;
    protected ContinentEconomy.BuildingRegistration? _registration;

    /// <summary>
    /// Sets the building and economy to display details for.
    /// Must be called before the view is shown.
    /// </summary>
    public virtual void SetBuilding(BuildingConstruction building, ContinentEconomy economy)
    {
        _building = building;
        _economy = economy;
        _registration = GetBuildingRegistration();

        UpdateDisplay();
    }

    /// <summary>
    /// Clears the view to default state.
    /// </summary>
    public virtual void Clear()
    {
        _building = null;
        _economy = null;
        _registration = null;
    }

    /// <summary>
    /// Updates the display with current data.
    /// Called automatically when SetBuilding is called.
    /// Subclasses should override this to update their UI.
    /// </summary>
    protected abstract void UpdateDisplay();

    /// <summary>
    /// Gets the BuildingRegistration for the current building from the economy.
    /// </summary>
    protected ContinentEconomy.BuildingRegistration? GetBuildingRegistration()
    {
        if (_building == null || _economy == null)
        {
            return null;
        }

        // Find the registration by matching the building node
        var buildings = _economy.GetType()
            .GetField("_activeBuildings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_economy) as System.Collections.Generic.List<ContinentEconomy.BuildingRegistration>;

        if (buildings != null)
        {
            return buildings.FirstOrDefault(b => b.BuildingNode == _building);
        }

        return null;
    }

    /// <summary>
    /// Gets the active recipe for the current building.
    /// </summary>
    protected RecipeDefinition? GetActiveRecipe()
    {
        if (_registration == null)
        {
            return null;
        }

        var recipeDb = RecipeDatabase.Instance;
        if (recipeDb?.IsLoaded != true)
        {
            return null;
        }

        if (recipeDb.TryGetRecipe(_registration.RecipeId, out var recipe))
        {
            return recipe;
        }

        return null;
    }

    /// <summary>
    /// Gets the stockpile amount for a specific resource.
    /// </summary>
    protected float GetResourceStockpile(string resourceId)
    {
        if (_economy == null)
        {
            return 0f;
        }

        return _economy.GetStockpile(resourceId);
    }

    /// <summary>
    /// Gets the storage capacity for a resource category.
    /// </summary>
    protected float GetCategoryCapacity(string category)
    {
        if (_economy == null)
        {
            return 1000f; // Default capacity
        }

        return _economy.GetCategoryCapacity(category);
    }

    /// <summary>
    /// Gets the total amount stored in a category.
    /// </summary>
    protected float GetCategoryUsed(string category)
    {
        if (_economy == null)
        {
            return 0f;
        }

        return _economy.GetCategoryUsed(category);
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
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }
        }
        return string.Join(" ", words);
    }
}
