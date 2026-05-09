using System.Collections.Generic;
using Godot;
using Constructables.Buildings.Behaviors;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Pulls raw resources from the deposits beneath the owner's occupied cells. Builds a synthetic
/// <see cref="RecipeDefinition"/> from the cell mix at registration time and drives the
/// owner's <see cref="ManufacturingBehavior"/> with it. Empty inputs, outputs scaled by
/// <see cref="RatePerTick"/>, work duration <see cref="WorkPerCycle"/>.
///
/// Spec: early-tier extractors set <see cref="ExtractTypes"/>=1 (single resource, low rate);
/// late-tier set higher values for parallel multi-resource output. The behavior assumes cell
/// deposits are infinite for v1 — depletion is a follow-up.
/// </summary>
public partial class ExtractionBehavior : RefCounted, IBuildingBehavior
{
    private Building? _owner;

    public Building? Owner => _owner;

    /// <summary>Maximum number of distinct resources to pull from the cell mix per cycle.</summary>
    public int ExtractTypes { get; set; } = 1;

    /// <summary>Output quantity per resource per cycle, before SpeedModifier.</summary>
    public float RatePerTick { get; set; } = 1f;

    /// <summary>Seconds of work per extraction cycle.</summary>
    public float WorkPerCycle { get; set; } = 1f;

    /// <summary>
    /// Synthetic recipe rebuilt from the owner's cells on placement. Read by
    /// <see cref="Building.TryStartManufacturingCycleFromRegistration"/> in preference to
    /// the YAML-declared default recipe.
    /// </summary>
    public RecipeDefinition? SyntheticRecipe { get; private set; }

    public void OnAttach(Building owner) => _owner = owner;

    public void OnRegister()
    {
        RebuildSyntheticRecipe();
    }

    public void OnUnregister() { }
    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, Building owner) { }

    /// <summary>
    /// Picks up to <see cref="ExtractTypes"/> resources from the union of the owner's
    /// occupied cells, weighted by abundance, and stages a synthetic <see cref="RecipeDefinition"/>.
    /// Public so tests and placement-change logic can re-trigger after a cell-set change.
    /// </summary>
    public void RebuildSyntheticRecipe()
    {
        if (_owner == null)
        {
            SyntheticRecipe = null;
            return;
        }

        var pooled = new Dictionary<string, float>();
        foreach (var cell in _owner.OccupiedCells)
        {
            if (cell?.Resources == null) continue;
            foreach (var kvp in cell.Resources)
            {
                if (string.IsNullOrEmpty(kvp.Key) || kvp.Value <= 0f) continue;
                pooled[kvp.Key] = pooled.GetValueOrDefault(kvp.Key) + kvp.Value;
            }
        }

        if (pooled.Count == 0)
        {
            SyntheticRecipe = null;
            return;
        }

        // Pick top-N by abundance. Stable iteration order so identical inputs → identical recipes.
        var sorted = new List<KeyValuePair<string, float>>(pooled);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        int take = Mathf.Min(ExtractTypes, sorted.Count);
        var outputs = new Dictionary<string, float>();
        for (int i = 0; i < take; i++)
            outputs[sorted[i].Key] = RatePerTick;

        // Output capacity: ensure OutputStorage has a resource-locked slot for each
        // extracted resource. Slot capacity is the resource's MaxStackSize.
        foreach (var resourceId in outputs.Keys)
        {
            if (_owner.OutputStorage.GetCapacity(resourceId) <= 0f)
                _owner.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(resourceId)));
        }

        SyntheticRecipe = new RecipeDefinition
        {
            RecipeId = $"extraction_synthetic_{_owner.Id}",
            DisplayName = "Extraction",
            Category = "extraction",
            WorkRequired = WorkPerCycle,
            InputResources = new Dictionary<string, float>(),
            OutputResources = outputs,
        };
    }
}
