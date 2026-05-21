using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Godot;
using Structures.Logistics;
using Structures.Resources;

namespace UI.BuildingInfo;

/// <summary>
/// Tag resolution context for a single recipe slot.
/// When <see cref="IsTag"/> is true, the slot originated from a tag: recipe key.
/// <see cref="TagName"/> holds the bare tag (e.g., "ore" from "tag:ore").
/// <see cref="ResolvedId"/> holds the concrete resource ID if the tag was resolved
/// at runtime, or null when the tag is still unresolved (idle / no cycle started).
/// </summary>
public readonly record struct TagSlotContext(bool IsTag, string? TagName, string? ResolvedId);

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
        var mfg = _building.GetBehavior<ManufacturingBehavior>();
        var producer = _building.GetBehavior<PowerProducerBehavior>();
        var ext = _building.GetBehavior<ExtractionBehavior>();
        string? recipeId = _building.ActiveRecipeId ?? mfg?.DefaultRecipe ?? producer?.DefaultRecipe ?? ext?.DefaultRecipe;
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
        ComputeRecipeRates(RecipeDefinition? recipe, ManufacturingBehavior? mfg)
    {
        var inputs = new Dictionary<string, float>();
        var outputs = new Dictionary<string, float>();
        if (recipe == null || mfg == null)
            return (inputs, outputs);
        float speed = mfg.ProductionSpeed;
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
    /// Per-second input/output rates for the active recipe at the producer's production speed.
    /// Accepts <see cref="PowerProducerBehavior"/> as an alternative to
    /// <see cref="ManufacturingBehavior"/> for speed/cycle-time resolution.
    /// </summary>
    public static (Dictionary<string, float> inputs, Dictionary<string, float> outputs)
        ComputeRecipeRates(RecipeDefinition? recipe, PowerProducerBehavior? producer)
    {
        var inputs = new Dictionary<string, float>();
        var outputs = new Dictionary<string, float>();
        if (recipe == null || producer == null)
            return (inputs, outputs);
        float speed = producer.ProductionSpeed;
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
    /// Tag-aware variant of <see cref="ComputeRecipeRates(RecipeDefinition?, ManufacturingBehavior?)"/>.
    /// Returns per-second input/output rates with tag slots resolved to their concrete resource IDs
    /// when available. The <paramref name="tagContext"/> dictionary maps each output key to its
    /// <see cref="TagSlotContext"/> so callers can render tag badges and "Any {Tag}" fallbacks.
    /// Unresolved tag keys are emitted under a synthetic key "tag:{tagName}" so they don't collide
    /// with concrete resource IDs.
    /// </summary>
    public static (Dictionary<string, float> inputs, Dictionary<string, float> outputs,
        Dictionary<string, TagSlotContext> tagContext)
        ComputeRecipeRatesWithTagContext(RecipeDefinition? recipe, ManufacturingBehavior? mfg)
    {
        var inputs = new Dictionary<string, float>();
        var outputs = new Dictionary<string, float>();
        var tagContext = new Dictionary<string, TagSlotContext>();

        if (recipe == null || mfg == null)
            return (inputs, outputs, tagContext);

        float speed = mfg.ProductionSpeed;
        float cycleTime = recipe.WorkRequired / Mathf.Max(speed, 0.001f);
        if (cycleTime <= 0f)
            return (inputs, outputs, tagContext);

        foreach (var kvp in recipe.InputResources)
        {
            if (kvp.Key == "power") continue;

            if (RecipeDefinition.IsTagInput(kvp.Key))
            {
                string tagName = RecipeDefinition.GetTagName(kvp.Key);
                mfg.ResolvedTagInputs.TryGetValue(kvp.Key, out string? resolvedId);

                // Use resolved concrete ID as the dictionary key when available;
                // fall back to the tag key itself so unresolved tags don't collide
                // with real resource IDs.
                string displayKey = !string.IsNullOrEmpty(resolvedId) ? resolvedId : kvp.Key;
                inputs[displayKey] = kvp.Value / cycleTime;
                tagContext[displayKey] = new TagSlotContext(true, tagName, resolvedId);
            }
            else
            {
                inputs[kvp.Key] = kvp.Value / cycleTime;
            }
        }

        foreach (var kvp in recipe.OutputResources)
        {
            if (kvp.Key == "power") continue;

            if (RecipeDefinition.IsTagInput(kvp.Key))
            {
                string tagName = RecipeDefinition.GetTagName(kvp.Key);
                mfg.ResolvedTagOutputs.TryGetValue(kvp.Key, out string? resolvedId);

                string displayKey = !string.IsNullOrEmpty(resolvedId) ? resolvedId : kvp.Key;
                outputs[displayKey] = kvp.Value / cycleTime;
                tagContext[displayKey] = new TagSlotContext(true, tagName, resolvedId);
            }
            else
            {
                outputs[kvp.Key] = kvp.Value / cycleTime;
            }
        }

        return (inputs, outputs, tagContext);
    }

    /// <summary>
    /// Tag-aware variant for <see cref="ExtractionBehavior"/>.
    /// Reads <see cref="ExtractionBehavior.ResolvedTagOutputs"/> for tag resolution context
    /// and <see cref="ExtractionBehavior.ProductionSpeed"/> for cycle time.
    /// </summary>
    public static (Dictionary<string, float> inputs, Dictionary<string, float> outputs,
        Dictionary<string, TagSlotContext> tagContext)
        ComputeRecipeRatesWithTagContext(RecipeDefinition? recipe, ExtractionBehavior? ext)
    {
        var inputs = new Dictionary<string, float>();
        var outputs = new Dictionary<string, float>();
        var tagContext = new Dictionary<string, TagSlotContext>();

        if (recipe == null || ext == null)
            return (inputs, outputs, tagContext);

        float speed = ext.ProductionSpeed;
        float cycleTime = recipe.WorkRequired / Mathf.Max(speed, 0.001f);
        if (cycleTime <= 0f)
            return (inputs, outputs, tagContext);

        foreach (var kvp in recipe.InputResources)
        {
            if (kvp.Key == "power") continue;

            if (RecipeDefinition.IsTagInput(kvp.Key))
            {
                string tagName = RecipeDefinition.GetTagName(kvp.Key);
                ext.ResolvedTagOutputs.TryGetValue(kvp.Key, out string? resolvedId);
                string displayKey = !string.IsNullOrEmpty(resolvedId) ? resolvedId : kvp.Key;
                inputs[displayKey] = kvp.Value / cycleTime;
                tagContext[displayKey] = new TagSlotContext(true, tagName, resolvedId);
            }
            else
            {
                inputs[kvp.Key] = kvp.Value / cycleTime;
            }
        }

        foreach (var kvp in recipe.OutputResources)
        {
            if (kvp.Key == "power") continue;

            if (RecipeDefinition.IsTagInput(kvp.Key))
            {
                string tagName = RecipeDefinition.GetTagName(kvp.Key);
                ext.ResolvedTagOutputs.TryGetValue(kvp.Key, out string? resolvedId);
                string displayKey = !string.IsNullOrEmpty(resolvedId) ? resolvedId : kvp.Key;
                outputs[displayKey] = kvp.Value / cycleTime;
                tagContext[displayKey] = new TagSlotContext(true, tagName, resolvedId);
            }
            else
            {
                outputs[kvp.Key] = kvp.Value / cycleTime;
            }
        }

        return (inputs, outputs, tagContext);
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
