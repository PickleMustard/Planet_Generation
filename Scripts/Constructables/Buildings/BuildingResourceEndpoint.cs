using System.Collections.Generic;
using Structures.GameState;

namespace Constructables.Buildings;

/// <summary>
/// Adapter that exposes a <see cref="Building"/>'s <see cref="Building.BulkStorage"/>
/// through the <see cref="IResourceEndpoint"/> contract used by
/// <see cref="BodyTransferManager"/>. Transfer-station buildings register one of these
/// via <see cref="Behaviors.TransferStationBehavior"/>.
/// </summary>
public sealed class BuildingResourceEndpoint : IResourceEndpoint
{
    private readonly Building _owner;

    public BuildingResourceEndpoint(Building owner)
    {
        _owner = owner;
    }

    public Building Owner => _owner;

    public float DepositResource(string resourceId, float amount)
    {
        return _owner.BulkStorage.Deposit(resourceId, amount);
    }

    public float WithdrawResource(string resourceId, float amount)
    {
        return _owner.BulkStorage.Withdraw(resourceId, amount);
    }

    public float GetStockpile(string resourceId)
    {
        return _owner.BulkStorage.GetQuantity(resourceId);
    }

    public IReadOnlyDictionary<string, float> GetAllStockpiles()
    {
        return _owner.BulkStorage.GetAllQuantities();
    }

    public void EnqueueResourceRequest(ResourceRequest request)
    {
        // Transfer-station endpoints do not run discrete manufacturing — they are
        // bulk pass-throughs. Requests are silently ignored.
    }

    public float GetStorageFillPercentage(Building building, string category)
    {
        if (building != _owner)
            return 0f;

        var bulk = _owner.BulkStorage;
        if (bulk == null || bulk.Slots.Count == 0)
            return 0f;

        float used = 0f;
        float capacity = 0f;
        foreach (var slot in bulk.Slots)
        {
            used += slot.Quantity;
            capacity += slot.Capacity;
        }
        return capacity > 0f ? used / capacity : 0f;
    }
}
