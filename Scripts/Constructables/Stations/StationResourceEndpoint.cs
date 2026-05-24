using System.Collections.Generic;
using Godot;
using Structures.GameState;

namespace Constructables.Stations;

/// <summary>
/// Adapter that exposes a <see cref="StationSatellite"/>'s <see cref="StationSatellite.BulkStorage"/>
/// through the <see cref="IResourceEndpoint"/> contract. Transfer-hub behaviors and
/// <see cref="Buildings.Behaviors.TransferStationBehavior"/> use this to deposit/withdraw resources
/// from a station's bulk storage.
/// </summary>
public sealed class StationResourceEndpoint : IResourceEndpoint
{
    private readonly StationSatellite _owner;

    public StationResourceEndpoint(StationSatellite owner) => _owner = owner;

    public StationSatellite Owner => _owner;

    public int DepositResource(string resourceId, float amount)
        => _owner.BulkStorage.Deposit(resourceId, amount);

    public int WithdrawResource(string resourceId, int amount)
        => _owner.BulkStorage.Withdraw(resourceId, amount);

    public int GetStockpile(string resourceId)
        => _owner.BulkStorage.GetQuantity(resourceId);

    public IReadOnlyDictionary<string, int> GetAllStockpiles()
        => _owner.BulkStorage.GetAllQuantities();

    public void EnqueueResourceRequest(ResourceRequest request) { }

    public float GetStorageFillPercentage(GodotObject? owner, string category)
    {
        if (owner != _owner) return 0f;
        var bulk = _owner.BulkStorage;
        if (bulk.Slots.Count == 0) return 0f;
        int used = 0, capacity = 0;
        foreach (var slot in bulk.Slots) { used += slot.Quantity; capacity += slot.Capacity; }
        return capacity > 0 ? (float)used / capacity : 0f;
    }
}
