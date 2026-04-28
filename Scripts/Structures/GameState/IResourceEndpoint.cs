using System.Collections.Generic;

namespace Structures.GameState;

/// <summary>
/// Common interface for entities that can send and receive resources via transfers.
/// Implemented by ContinentEconomy and StationEconomy.
/// </summary>
public interface IResourceEndpoint
{
    /// <summary>
    /// Deposits resources into this endpoint's stockpile.
    /// Returns the amount actually deposited (may be less than requested due to capacity).
    /// </summary>
    float DepositResource(string resourceId, float amount);

    /// <summary>
    /// Withdraws resources from this endpoint's stockpile.
    /// Returns the amount actually withdrawn (may be less than requested due to availability).
    /// </summary>
    float WithdrawResource(string resourceId, float amount);

    /// <summary>
    /// Gets the current stockpile quantity of a specific resource.
    /// </summary>
    float GetStockpile(string resourceId);

    /// <summary>
    /// Gets all current stockpile quantities.
    /// </summary>
    IReadOnlyDictionary<string, float> GetAllStockpiles();

    /// <summary>
    /// Queues a resource request for discrete manufacturing.
    /// </summary>
    void EnqueueResourceRequest(ResourceRequest request);

    /// <summary>
    /// Gets the visual fill percentage for a specific storage building and category.
    /// </summary>
    float GetStorageFillPercentage(Constructables.BuildingConstruction building, string category);
}
