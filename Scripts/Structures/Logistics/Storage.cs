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
/// </summary>
public partial class Storage : Resource
{
    private readonly List<StorageSlot> _slots = new();

    /// <summary>
    /// Raised after any quantity-changing operation (Deposit, Withdraw, AddSlot/RemoveSlot of a
    /// non-empty slot). Arguments: resource id, signed delta (positive on deposit, negative on
    /// withdraw). Subscribers must be tolerant of being invoked from the manufacture tick thread.
    /// Exceptions thrown by subscribers are logged and swallowed so they cannot corrupt accounting.
    /// </summary>
    public event Action<string, float>? StorageUpdated;

    private void RaiseStorageUpdated(string resourceId, float delta)
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
    /// Fills slots already occupied by that resource first, then claims empty slots
    /// whose filter accepts the resource. Each slot caps at the resource's MaxStackSize.
    /// </summary>
    public float Deposit(string resourceId, float amount)
    {
        if (amount <= 0 || string.IsNullOrEmpty(resourceId))
            return 0f;

        var def = ResolveResource(resourceId);
        float stackSize = def?.MaxStackSize ?? StorageSlot.FallbackCapacity;

        float remaining = amount;
        float deposited = 0f;

        // First pass: top off slots already locked to this resource.
        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (!string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                continue;

            float space = stackSize - slot.Quantity;
            if (space <= 0) continue;

            float toDeposit = Mathf.Min(space, remaining);
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

            float toDeposit = Mathf.Min(stackSize, remaining);
            slot.OccupiedResourceId = resourceId;
            slot.Quantity = toDeposit;
            remaining -= toDeposit;
            deposited += toDeposit;
        }

        if (deposited > 0)
            RaiseStorageUpdated(resourceId, deposited);

        return deposited;
    }

    /// <summary>
    /// Withdraws up to <paramref name="amount"/> of <paramref name="resourceId"/> from
    /// matching slots. Slots that drain to zero are unlocked (free for any future
    /// resource the filter accepts).
    /// </summary>
    public float Withdraw(string resourceId, float amount)
    {
        if (amount <= 0 || string.IsNullOrEmpty(resourceId))
            return 0f;

        float remaining = amount;
        float withdrawn = 0f;

        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (!string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                continue;
            if (slot.Quantity <= 0) continue;

            float toWithdraw = Mathf.Min(slot.Quantity, remaining);
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

    public float GetQuantity(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return 0f;

        float total = 0f;
        foreach (var slot in _slots)
        {
            if (string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                total += slot.Quantity;
        }
        return total;
    }

    public bool HasSpace(string resourceId, float amount)
    {
        return GetFreeSpace(resourceId) >= amount;
    }

    /// <summary>
    /// Total capacity addressable by this resource: sum of MaxStackSize for every
    /// occupied-by-this-resource slot, plus MaxStackSize for every empty slot whose
    /// filter accepts the resource.
    /// </summary>
    public float GetCapacity(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return 0f;

        var def = ResolveResource(resourceId);
        float stackSize = def?.MaxStackSize ?? StorageSlot.FallbackCapacity;
        float total = 0f;

        foreach (var slot in _slots)
        {
            if (string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                total += stackSize;
            else if (slot.IsEmpty && (def == null || slot.Filter.Accepts(def)))
                total += stackSize;
        }
        return total;
    }

    public float GetFreeSpace(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return 0f;

        var def = ResolveResource(resourceId);
        float stackSize = def?.MaxStackSize ?? StorageSlot.FallbackCapacity;
        float total = 0f;

        foreach (var slot in _slots)
        {
            if (string.Equals(slot.OccupiedResourceId, resourceId, StringComparison.Ordinal))
                total += stackSize - slot.Quantity;
            else if (slot.IsEmpty && (def == null || slot.Filter.Accepts(def)))
                total += stackSize;
        }
        return total;
    }

    public IReadOnlyDictionary<string, float> GetAllQuantities()
    {
        var result = new Dictionary<string, float>();
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
