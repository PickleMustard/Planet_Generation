using System.Collections.Generic;
using Godot;
using Structures.Logistics;

namespace Constructables.Buildings.Behaviors;

/// <summary>
/// Populates the parent building's <see cref="Building.BulkStorage"/> with a
/// configured number of slots and filter mix. Slot count and filter mix come from
/// <see cref="Structures.Resources.BuildingDefinition.StorageCapacity"/> and
/// <see cref="Structures.Resources.BuildingDefinition.SlotFilters"/>; any leftover
/// slots beyond the explicit filter assignments default to <see cref="SlotFilter.Any"/>.
/// </summary>
public partial class StorageHubBehavior : RefCounted, IBuildingBehavior
{
    private Building? _owner;

    public Building? Owner => _owner;

    public int StorageCapacity { get; set; }
    public List<SlotFilterSpec> SlotFilters { get; set; } = new();

    private readonly List<StorageSlot> _addedSlots = new();

    public void OnAttach(Building owner) => _owner = owner;

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
