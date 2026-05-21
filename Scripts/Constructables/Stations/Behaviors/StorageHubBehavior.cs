using System.Collections.Generic;
using Godot;
using Constructables.Stations;
using Structures.Logistics;
using UtilityLibrary;

namespace Constructables.Stations.Behaviors;

/// <summary>
/// Allocates bulk-storage slots into the owning station's <see cref="StationSatellite.BulkStorage"/>
/// at registration time. Slot count is driven by <see cref="StorageCapacity"/>;
/// specific filters are applied first, any remaining slots default to
/// <see cref="SlotFilter.Any"/>. Storage population is one-time setup —
/// <see cref="WantsTick"/> returns <c>false</c>.
/// </summary>
public partial class StorageHubBehavior : RefCounted, IStationBehavior
{
    private StationSatellite? _owner;
    private readonly List<StorageSlot> _addedSlots = new();

    /// <summary>Total number of bulk-storage slots to allocate.</summary>
    public int StorageCapacity { get; set; }

    /// <summary>
    /// Filter mix for the bulk-storage slots. The sum of <see cref="SlotFilterSpec.Count"/>
    /// values must be ≤ <see cref="StorageCapacity"/>; any remaining slots default to
    /// <see cref="SlotFilter.Any"/>.
    /// </summary>
    public List<SlotFilterSpec> SlotFilters { get; set; } = new();

    public StationSatellite? Owner => _owner;

    public void OnAttach(StationSatellite owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null) return;

        int allocated = 0;

        // Specific filters first
        foreach (var spec in SlotFilters)
        {
            for (int i = 0; i < spec.Count; i++)
            {
                if (allocated >= StorageCapacity) break;
                var slot = new StorageSlot(spec.Filter);
                _owner.BulkStorage.AddSlot(slot);
                _addedSlots.Add(slot);
                allocated++;
            }
            if (allocated >= StorageCapacity) break;
        }

        // Fill remainder with Any filter
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
            _owner?.BulkStorage.RemoveSlot(slot);
        _addedSlots.Clear();
    }

    public void OnDetach() => _owner = null;

    public void OnManufactureTick(float delta, StationSatellite owner) { }

    public bool WantsTick => false;
    public int Priority => 0;
}
