using System.Collections.Generic;
using Godot;
using Structures.GameState;

namespace Constructables.Buildings;

/// <summary>
/// Adapter that exposes a <see cref="Building"/>'s <see cref="Building.BulkStorage"/>
/// through the <see cref="IResourceEndpoint"/> contract used by
/// <see cref="TransferStationBehavior"/>. Transfer-station buildings create one of these
/// inside their <see cref="TransferStationBehavior"/>.
/// </summary>
public sealed class BuildingResourceEndpoint : IResourceEndpoint
{
    private readonly Building _owner;

    public BuildingResourceEndpoint(Building owner)
    {
        _owner = owner;
    }

    public Building Owner => _owner;

    public int DepositResource(string resourceId, float amount)
    {
        return _owner.BulkStorage.Deposit(resourceId, amount);
    }

    public int WithdrawResource(string resourceId, int amount)
    {
        return _owner.BulkStorage.Withdraw(resourceId, amount);
    }

    public int GetStockpile(string resourceId)
    {
        return _owner.BulkStorage.GetQuantity(resourceId);
    }

    public IReadOnlyDictionary<string, int> GetAllStockpiles()
    {
        return _owner.BulkStorage.GetAllQuantities();
    }

    public void EnqueueResourceRequest(ResourceRequest request)
    {
        // Transfer-station endpoints do not run discrete manufacturing — they are
        // bulk pass-throughs. Requests are silently ignored.
    }

    public float GetStorageFillPercentage(GodotObject? owner, string category)
    {
        if (owner != _owner)
            return 0f;

        var bulk = _owner.BulkStorage;
        if (bulk == null || bulk.Slots.Count == 0)
            return 0f;

        int used = 0;
        int capacity = 0;
        foreach (var slot in bulk.Slots)
        {
            used += slot.Quantity;
            capacity += slot.Capacity;
        }
        return capacity > 0 ? (float)used / capacity : 0f;
    }
}
