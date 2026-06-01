using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Logistics;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Populates the parent building's <see cref="Building.BulkStorage"/> with a
/// configured number of slots and filter mix. Slot count and filter mix come from
/// the behavior's YAML config; any leftover slots beyond the explicit filter
/// assignments default to <see cref="SlotFilter.Any"/>.
/// </summary>
public partial class StorageHubBehavior : RefCounted, IBuildingBehavior, IBehaviorConfigurable
{
    private Building? _owner;

    public Building? Owner => _owner;

    public int StorageCapacity { get; set; }
    public List<SlotFilterSpec> SlotFilters { get; set; } = new();

    private readonly List<StorageSlot> _addedSlots = new();

    public void OnAttach(Building owner) => _owner = owner;

    public void Configure(Dictionary<string, object> config)
    {
        StorageCapacity = BehaviorConfigHelper.ReadInt(config, "storage_capacity", 0);
        SlotFilters = ParseSlotFilters(config);
    }

    /// <summary>
    /// Parses the slot_filters dict from a behavior config into SlotFilterSpec entries.
    /// Keys use prefix syntax: "any", "state:solid", "state:fluid", "category:&lt;name&gt;",
    /// "resource:&lt;id&gt;". A bare key falls back to category lookup (legacy behavior).
    /// </summary>
    private static List<SlotFilterSpec> ParseSlotFilters(Dictionary<string, object> config)
    {
        var specs = new List<SlotFilterSpec>();

        if (!config.TryGetValue("slot_filters", out var filtersVal))
            return specs;

        if (filtersVal is not Dictionary<object, object> filtersDict)
            return specs;

        foreach (var kvp in filtersDict)
        {
            string key = ((string)kvp.Key).Trim();
            int count = BehaviorConfigHelper.NodeToInt(kvp.Value, 0);
            if (count <= 0 || string.IsNullOrEmpty(key))
                continue;

            SlotFilter filter;
            if (string.Equals(key, "any", StringComparison.OrdinalIgnoreCase))
            {
                filter = SlotFilter.Any();
            }
            else if (key.StartsWith("state:", StringComparison.OrdinalIgnoreCase))
            {
                string stateName = key.Substring("state:".Length);
                filter = SlotFilter.ForState(StateOfMatterExtensions.Parse(stateName));
            }
            else if (key.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
            {
                filter = SlotFilter.ForCategory(key.Substring("category:".Length));
            }
            else if (key.StartsWith("resource:", StringComparison.OrdinalIgnoreCase))
            {
                filter = SlotFilter.ForResource(key.Substring("resource:".Length));
            }
            else
            {
                // Legacy bare key: treat as category name.
                filter = SlotFilter.ForCategory(key);
            }

            specs.Add(new SlotFilterSpec(filter, count));
        }

        return specs;
    }

    public void OnRegister()
    {
        if (_owner == null)
            return;

        int allocated = 0;
        foreach (var spec in SlotFilters)
        {
            for (int i = 0; i < spec.Count; i++)
            {
                if (allocated >= StorageCapacity)
                    break;
                var slot = new StorageSlot(spec.Filter);
                _owner.BulkStorage.AddSlot(slot);
                _addedSlots.Add(slot);
                allocated++;
            }
            if (allocated >= StorageCapacity)
                break;
        }

        // Remainder defaults to Any-filtered slots.
        while (allocated < StorageCapacity)
        {
            var slot = new StorageSlot(SlotFilter.Any());
            _owner.BulkStorage.AddSlot(slot);
            _addedSlots.Add(slot);
            allocated++;
        }
    }

    public void OnUnregister()
    {
        foreach (var slot in _addedSlots)
        {
            _owner?.BulkStorage.RemoveSlot(slot);
        }
        _addedSlots.Clear();
    }

    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, Building owner) { }
}
