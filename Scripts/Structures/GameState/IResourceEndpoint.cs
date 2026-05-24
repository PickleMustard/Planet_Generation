using System.Collections.Generic;
using Godot;

namespace Structures.GameState;

/// <summary>
/// Common interface for entities that can send and receive resources via transfers.
/// Implemented by ContinentEconomy and StationEconomy.
/// Resources are accounted as whole units; production paths may pass fractional
/// deposits which the underlying storage buffers internally.
/// </summary>
public interface IResourceEndpoint
{
    /// <summary>
    /// Deposits resources into this endpoint's stockpile. Accepts a fractional amount;
    /// returns whole units actually placed (sub-unit residue is buffered internally).
    /// </summary>
    int DepositResource(string resourceId, float amount);

    /// <summary>
    /// Withdraws whole resource units from this endpoint's stockpile.
    /// Returns the amount actually withdrawn (may be less than requested due to availability).
    /// </summary>
    int WithdrawResource(string resourceId, int amount);

    /// <summary>
    /// Gets the current stockpile quantity of a specific resource (whole units).
    /// </summary>
    int GetStockpile(string resourceId);

    /// <summary>
    /// Gets all current stockpile quantities (whole units).
    /// </summary>
    IReadOnlyDictionary<string, int> GetAllStockpiles();

    /// <summary>
    /// Queues a resource request for discrete manufacturing.
    /// </summary>
    void EnqueueResourceRequest(ResourceRequest request);

    /// <summary>
    /// Gets the visual fill percentage for a specific storage owner and category.
    /// Owner may be a Building, StationSatellite, or any other GodotObject-backed entity.
    /// </summary>
    float GetStorageFillPercentage(GodotObject? owner, string category);
}
