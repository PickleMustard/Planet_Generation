using Structures.Resources;

namespace Structures.Logistics;

/// <summary>
/// A single storage slot. Each slot has a fixed <see cref="Filter"/> deciding what
/// resources it can ever accept; once a resource occupies the slot, the slot is
/// further locked to that resource until it empties. Slot capacity is dynamic and
/// equals the occupant's <see cref="ResourceDefinition.MaxStackSize"/>.
/// </summary>
public class StorageSlot
{
    public SlotFilter Filter { get; set; }

    /// <summary>
    /// The resource currently locking this slot. Null when the slot is empty and
    /// available to be claimed by any resource the filter accepts.
    /// </summary>
    public string? OccupiedResourceId { get; set; }

    public float Quantity { get; set; }

    public StorageSlot()
    {
        Filter = SlotFilter.Any();
    }

    public StorageSlot(SlotFilter filter)
    {
        Filter = filter ?? SlotFilter.Any();
    }

    /// <summary>
    /// Fallback capacity used when <see cref="ResourceDatabase"/> is unavailable
    /// (e.g., pure-unit tests that don't bootstrap the runtime). Production code
    /// always resolves through ResourceDatabase.
    /// </summary>
    public const float FallbackCapacity = 100f;

    /// <summary>
    /// Capacity of this slot — equal to the occupant's MaxStackSize, or zero if empty.
    /// Falls back to <see cref="FallbackCapacity"/> when the resource database isn't
    /// available so tests can exercise storage mechanics without a runtime.
    /// </summary>
    public float Capacity
    {
        get
        {
            if (string.IsNullOrEmpty(OccupiedResourceId))
                return 0f;

            var db = ResourceDatabase.Instance;
            if (db != null)
            {
                try
                {
                    if (db.TryGetResource(OccupiedResourceId!, out var def) && def != null)
                        return def.MaxStackSize;
                }
                catch
                {
                    // Database not yet loaded — fall through to fallback.
                }
            }
            return FallbackCapacity;
        }
    }

    public float FreeSpace => Capacity - Quantity;

    public bool IsEmpty => string.IsNullOrEmpty(OccupiedResourceId) && Quantity <= 0;
}
