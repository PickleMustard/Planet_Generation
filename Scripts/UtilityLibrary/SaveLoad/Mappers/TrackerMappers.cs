using System.Collections.Generic;
using Structures.GameState;
using UtilityLibrary.SaveLoad.Dto;

namespace UtilityLibrary.SaveLoad.Mappers;

public static class CompanyMapper
{
    public static CompanyDto ToDto(CompanyDataTracker tracker) => new CompanyDto
    {
        Budget = tracker.Budget,
        Debt = tracker.Debt,
        Antagonism = tracker.Antagonism,
        Research = tracker.Research,
    };

    public static void Restore(CompanyDataTracker tracker, CompanyDto dto) =>
        tracker.LoadState(dto.Budget, dto.Debt, dto.Antagonism, dto.Research);
}

public static class EconomyMapper
{
    public static EconomyDto ToDto(EconomyTracker tracker)
    {
        var dto = new EconomyDto { PriceUpdateInterval = tracker.PriceUpdateIntervalSeconds };
        foreach (var kv in tracker.GetMarket())
        {
            dto.Market.Add(new MarketEntryDto
            {
                Id = kv.Key,
                BasePrice = kv.Value.BasePrice,
                CurrentPrice = kv.Value.CurrentPrice,
            });
        }
        return dto;
    }

    public static void Restore(EconomyTracker tracker, EconomyDto dto)
    {
        var entries = new List<(string, double, double)>(dto.Market.Count);
        foreach (var e in dto.Market)
            entries.Add((e.Id, e.BasePrice, e.CurrentPrice));
        tracker.LoadState(dto.PriceUpdateInterval, entries);
    }
}
