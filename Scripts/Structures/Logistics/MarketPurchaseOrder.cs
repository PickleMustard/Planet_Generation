namespace Structures.Logistics;

/// <summary>
/// A player-configured standing order at a Market Station: buy back up to <see cref="MonthlyLimit"/>
/// units of <see cref="ResourceId"/> per month, loaded into a held unit's cargo hold after its sale.
/// The order is constrained at fulfillment time by company funds, the unit's remaining cargo capacity,
/// and the running monthly purchase counter (whose reset is currently stubbed — see
/// <c>MarketStationBehavior.ResetMonthlyLimitsIfNeeded</c>).
/// </summary>
public sealed class MarketPurchaseOrder
{
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Maximum units to buy per month across all held units. 0 disables the order.</summary>
    public int MonthlyLimit { get; set; }

    public MarketPurchaseOrder() { }

    public MarketPurchaseOrder(string resourceId, int monthlyLimit)
    {
        ResourceId = resourceId;
        MonthlyLimit = monthlyLimit;
    }
}
