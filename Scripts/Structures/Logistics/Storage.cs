using System;
using System.Collections.Generic;
using Godot;
using Structures.Resources;
using UtilityLibrary;

namespace Structures.Logistics;

/// <summary>
/// A container of typed slots. Each slot's <see cref="StorageSlot.Filter"/> decides
/// which resources may ever occupy it; the slot then locks to the first resource
/// deposited until it empties. Slot capacity is the occupant's MaxStackSize.
/// Resources are stored as whole units. Sub-unit deposits accumulate in a per-resource
/// fractional buffer that is invisible to readers; whole units flow into slots
/// as the buffer crosses 1.0.
/// </summary>
public partial class Storage : Resource
{
    private readonly List<StorageSlot> _slots = new();

    /// <summary>
    /// Per-resource fractional accumulator. Fractional deposits stay here until the
    /// summed amount yields at least one whole unit. Never observable from outside
    /// the storage; <see cref="GetQuantity"/> and friends only report whole units in slots.
    /// </summary>
    private readonly Dictionary<string, float> _fractionalBuffer = new();

    /// <summary>
    /// Raised after any whole-unit change to slot contents (Deposit, Withdraw,
    /// AddSlot/RemoveSlot of a non-empty slot). Arguments: resource id, signed integer
    /// delta. Subscribers must be tolerant of being invoked from the manufacture tick
    /// thread. Exceptions thrown by subscribers are logged and swallowed so they cannot
    /// corrupt accounting. Fractional buffer changes never fire this event.
    /// </summary>
    public event Action<string, int>? StorageUpdated;

    private void RaiseStorageUpdated(string resourceId, int delta)
    {
        var handler = StorageUpdated;
        if (handler == null)
            return;

        try
        {
            handler(resourceId, delta);
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Storage.StorageUpdated subscriber threw: {ex}");
        }
    }

    public IReadOnlyList<StorageSlot> Slots => _slots;

    public void AddSlot(StorageSlot slot)
    {
        if (slot == null)
            return;

        _slots.Add(slot);

        if (!string.IsNullOrEmpty(slot.OccupiedResourceId) && slot.Quantity > 0)
            RaiseStorageUpdated(slot.OccupiedResourceId!, slot.Quantity);
    }

    public bool RemoveSlot(StorageSlot slot)
    {
        if (slot == null)
            return false;

        bool removed = _slots.Remove(slot);
        if (removed && !string.IsNullOrEmpty(slot.OccupiedResourceId) && slot.Quantity > 0)
            RaiseStorageUpdated(slot.OccupiedResourceId!, -slot.Quantity);

        return removed;
    }

    /// <summary>
    /// Attempts to deposit <paramref name="amount"/> of <paramref name="resourceId"/>.
    /// Accepts a fractional amount: the fractional remainder accumulates in an internal
    /// buffer that is invisible to readers. Whole units (current floor of buffer) are
    /// placed into slots — top off slots already locked to this resource first, then
    /// claim empty slots whose filter accepts. Each slot caps at the resource's
    /// MaxStackSize. Whole units that don't fit (slots full) remain buffered and will
    /// flow in on subsequent deposits that free or open capacity.
    /// </summary>
    /// <returns>Whole units actually placed in slots this call.</returns>
    public int Deposit(string resourceId, float amount)
    {
        if (amount <= 0f || string.IsNullOrEmpty(resourceId))
            return 0;

        _fractionalBuffer.TryGetValue(resourceId, out float buffered);
        buffered += amount;

        int wholeAvailable = (int)Mathf.Floor(buffered);
        if (wholeAvailable <= 0)
        {
            _fractionalBuffer[resourceId] = buffered;
            return 0;
        }

        var def = ResolveResource(resourceId);
        int stackSize = def?.MaxStackSize ?? StorageSlot.FallbackCapacity;

        int remaining = wholeAvailable;
        int deposited = 0;

        // First pass: top off slots already locked to this resource.
        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (!string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                continue;

            int space = stackSize - slot.Quantity;
            if (space <= 0) continue;

            int toDeposit = System.Math.Min(space, remaining);
            slot.Quantity += toDeposit;
            remaining -= toDeposit;
            deposited += toDeposit;
        }

        // Second pass: claim empty slots whose filter accepts. When the resource
        // definition is missing (e.g., test env without a database) we can still
        // honor Resource-kind filters by matching ids directly; other filter kinds
        // require the full definition and are permissively treated as accepting.
        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (!slot.IsEmpty) continue;
            if (def != null)
            {
                if (!slot.Filter.Accepts(def)) continue;
            }
            else if (slot.Filter.Kind == SlotFilterKind.Resource
                     && !string.Equals(slot.Filter.ResourceId, resourceId, StringComparison.Ordinal))
            {
                continue;
            }

            int toDeposit = System.Math.Min(stackSize, remaining);
            slot.OccupiedResourceId = resourceId;
            slot.Quantity = toDeposit;
            remaining -= toDeposit;
            deposited += toDeposit;
        }

        // Subtract the units we actually placed from the buffer. Whole units that did
        // not fit (slots full) stay buffered alongside the original fractional residue.
        float newBuffer = buffered - deposited;
        if (newBuffer > 0f)
            _fractionalBuffer[resourceId] = newBuffer;
        else
            _fractionalBuffer.Remove(resourceId);

        if (deposited > 0)
            RaiseStorageUpdated(resourceId, deposited);

        return deposited;
    }

    /// <summary>
    /// Withdraws up to <paramref name="amount"/> whole units of <paramref name="resourceId"/>
    /// from matching slots. Slots that drain to zero are unlocked (free for any future
    /// resource the filter accepts). The fractional buffer is never touched.
    /// </summary>
    public int Withdraw(string resourceId, int amount)
    {
        if (amount <= 0 || string.IsNullOrEmpty(resourceId))
            return 0;

        int remaining = amount;
        int withdrawn = 0;

        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (!string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                continue;
            if (slot.Quantity <= 0) continue;

            int toWithdraw = System.Math.Min(slot.Quantity, remaining);
            slot.Quantity -= toWithdraw;
            remaining -= toWithdraw;
            withdrawn += toWithdraw;

            if (slot.Quantity <= 0)
                slot.OccupiedResourceId = null;
        }

        if (withdrawn > 0)
            RaiseStorageUpdated(resourceId, -withdrawn);

        return withdrawn;
    }

    public int GetQuantity(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return 0;

        int total = 0;
        foreach (var slot in _slots)
        {
            if (string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                total += slot.Quantity;
        }
        return total;
    }

    public bool HasSpace(string resourceId, int amount)
    {
        return GetFreeSpace(resourceId) >= amount;
    }

    /// <summary>
    /// Total capacity addressable by this resource: sum of MaxStackSize for every
    /// occupied-by-this-resource slot, plus MaxStackSize for every empty slot whose
    /// filter accepts the resource.
    /// </summary>
    public int GetCapacity(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return 0;

        var def = ResolveResource(resourceId);
        int stackSize = def?.MaxStackSize ?? StorageSlot.FallbackCapacity;
        int total = 0;

        foreach (var slot in _slots)
        {
            if (string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                total += stackSize;
            else if (slot.IsEmpty && (def == null || slot.Filter.Accepts(def)))
                total += stackSize;
        }
        return total;
    }

    public int GetFreeSpace(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return 0;

        var def = ResolveResource(resourceId);
        int stackSize = def?.MaxStackSize ?? StorageSlot.FallbackCapacity;
        int total = 0;

        foreach (var slot in _slots)
        {
            if (string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                total += stackSize - slot.Quantity;
            else if (slot.IsEmpty && (def == null || slot.Filter.Accepts(def)))
                total += stackSize;
        }
        return total;
    }

    /// <summary>
    /// Repopulates slot contents from a saved snapshot (resource id → whole units). Units are
    /// placed into existing accepting slots first; any remainder gets fresh resource-locked slots
    /// sized to the resource's MaxStackSize. Used by the save/load system — the fractional buffer
    /// is intentionally not restored. Slots created here mirror the resource-locked slots that a
    /// running recipe / storage hub would have produced, so capacity is not artificially inflated.
    /// </summary>
    public void RestoreContents(IReadOnlyDictionary<string, int> quantities)
    {
        if (quantities == null)
            return;

        foreach (var kv in quantities)
        {
            string id = kv.Key;
            int qty = kv.Value;
            if (string.IsNullOrEmpty(id) || qty <= 0)
                continue;

            int free = GetFreeSpace(id);
            int intoExisting = System.Math.Min(free, qty);
            if (intoExisting > 0)
            {
                Deposit(id, intoExisting);
                qty -= intoExisting;
            }

            if (qty <= 0)
                continue;

            var def = ResolveResource(id);
            int stackSize = def?.MaxStackSize ?? StorageSlot.FallbackCapacity;
            if (stackSize <= 0)
                stackSize = StorageSlot.FallbackCapacity;

            while (qty > 0)
            {
                int amt = System.Math.Min(stackSize, qty);
                AddSlot(new StorageSlot(SlotFilter.ForResource(id))
                {
                    OccupiedResourceId = id,
                    Quantity = amt,
                });
                qty -= amt;
            }
        }
    }

    public IReadOnlyDictionary<string, int> GetAllQuantities()
    {
        var result = new Dictionary<string, int>();
        foreach (var slot in _slots)
        {
            if (string.IsNullOrEmpty(slot.OccupiedResourceId) || slot.Quantity <= 0)
                continue;

            string key = slot.OccupiedResourceId!;
            if (result.TryGetValue(key, out var existing))
                result[key] = existing + slot.Quantity;
            else
                result[key] = slot.Quantity;
        }
        return result;
    }

    private static ResourceDefinition? ResolveResource(string resourceId)
    {
        var db = ResourceDatabase.Instance;
        if (db == null) return null;
        try
        {
            return db.TryGetResource(resourceId, out var def) ? def : null;
        }
        catch
        {
            // Database not yet loaded (typical in pure-unit tests). Caller falls back.
            return null;
        }
    }
}
