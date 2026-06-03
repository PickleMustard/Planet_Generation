using System.Collections.Generic;
using Constructables;
using Structures.GameState;
using UtilityLibrary;
using UtilityLibrary.SaveLoad.Dto;

namespace Constructables.Stations.Behaviors;

/// <summary>
/// Save side of <see cref="MarketStationBehavior"/> (save_version 5). Captures only mutable runtime
/// state — purchase orders, monthly purchase counters, and held/queued units. Config (level limit,
/// capacity, hold time, speed modifiers) is re-applied from the station YAML definition on load via
/// <c>Configure</c>, so it is intentionally not serialized. The restore path re-binds held/queued units
/// by id against the ship registry and re-applies the hold (units are restored before stations — loader
/// pass 5 vs 6).
/// </summary>
public partial class MarketStationBehavior
{
    /// <summary>Snapshots mutable market state into a <see cref="MarketStationStateDto"/>.</summary>
    public MarketStationStateDto SerializeState()
    {
        var dto = new MarketStationStateDto();
        lock (_lock)
        {
            dto.MonthAnchorTick = _monthAnchorTick;

            foreach (var order in _purchaseOrders)
                dto.PurchaseOrders.Add(new MarketPurchaseOrderDto
                {
                    ResourceId = order.ResourceId,
                    MonthlyLimit = order.MonthlyLimit,
                });

            foreach (var kv in _purchasedThisMonth)
                dto.PurchasedThisMonth[kv.Key] = kv.Value;

            foreach (var h in _held)
            {
                var hd = new MarketHeldUnitDto
                {
                    UnitId = h.UnitId,
                    SellSegmentTicks = h.SellSegmentTicks,
                    TotalHoldTicks = h.TotalHoldTicks,
                    ElapsedTicks = h.ElapsedTicks,
                    HasSold = h.HasSold,
                };
                foreach (var kv in h.PlannedPurchase)
                    hd.PlannedPurchase[kv.Key] = kv.Value;
                dto.Held.Add(hd);
            }

            foreach (var q in _queue)
                dto.Queue.Add(q.Id);
        }
        return dto;
    }

    /// <summary>
    /// Restores mutable market state and re-binds held/queued units. Units are resolved from the ship
    /// registry on the owning system (populated in loader pass 5). Held units are re-hidden; their
    /// schedule executors come back as <c>Held</c> in pass 7 (saved that way). Runs on the main thread.
    /// </summary>
    public void RestoreState(MarketStationStateDto dto)
    {
        if (dto == null || _owner == null)
            return;

        var system = SystemData.FindForNode(_owner);

        lock (_lock)
        {
            _monthAnchorTick = dto.MonthAnchorTick;

            _purchaseOrders.Clear();
            foreach (var od in dto.PurchaseOrders)
                _purchaseOrders.Add(new Structures.Logistics.MarketPurchaseOrder(od.ResourceId, od.MonthlyLimit));

            _purchasedThisMonth.Clear();
            foreach (var kv in dto.PurchasedThisMonth)
                _purchasedThisMonth[kv.Key] = kv.Value;
        }

        foreach (var hd in dto.Held)
        {
            if (system == null || !system.TryGetShip(hd.UnitId, out var unit) || unit == null)
            {
                GameLogger.Warning(
                    $"[MarketStation {_owner.Name}] Held unit '{hd.UnitId}' not found on restore; dropping hold.");
                continue;
            }

            var record = new HeldUnit
            {
                UnitId = hd.UnitId,
                Unit = unit,
                SellSegmentTicks = hd.SellSegmentTicks,
                TotalHoldTicks = hd.TotalHoldTicks,
                ElapsedTicks = hd.ElapsedTicks,
                HasSold = hd.HasSold,
                PlannedPurchase = new Dictionary<string, int>(hd.PlannedPurchase),
            };

            unit.EnterMarketHold();
            lock (_lock)
                _held.Add(record);
        }

        foreach (var unitId in dto.Queue)
        {
            if (system == null || !system.TryGetShip(unitId, out var unit) || unit == null)
            {
                GameLogger.Warning(
                    $"[MarketStation {_owner.Name}] Queued unit '{unitId}' not found on restore; dropping.");
                continue;
            }
            // Queued units stay visible (waiting at the station); their schedule is paused (saved Held).
            lock (_lock)
                _queue.Add(unit);
        }
    }
}
