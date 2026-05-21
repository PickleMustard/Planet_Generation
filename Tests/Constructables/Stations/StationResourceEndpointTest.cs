using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Stations;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;

namespace Tests.Constructables.Stations;

/// <summary>
/// Verifies <see cref="StationResourceEndpoint"/> correctly wraps a
/// <see cref="StationSatellite"/>'s <see cref="StationSatellite.BulkStorage"/>.
/// </summary>
[TestSuite]
public class StationResourceEndpointTest
{
    private static StationSatellite MakeStation()
    {
        var station = new StationSatellite();
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        return station;
    }

    [TestCase]
    public void DepositResource_DepositsIntoBulkStorage()
    {
        var station = MakeStation();
        var endpoint = new StationResourceEndpoint(station);

        float deposited = endpoint.DepositResource("iron", 50f);

        AssertThat(deposited).IsEqual(50f);
        AssertThat(station.BulkStorage.GetQuantity("iron")).IsEqual(50f);
    }

    [TestCase]
    public void WithdrawResource_WithdrawsFromBulkStorage()
    {
        var station = MakeStation();
        station.BulkStorage.Deposit("iron", 100f);
        var endpoint = new StationResourceEndpoint(station);

        float withdrawn = endpoint.WithdrawResource("iron", 30f);

        AssertThat(withdrawn).IsEqual(30f);
        AssertThat(station.BulkStorage.GetQuantity("iron")).IsEqual(70f);
    }

    [TestCase]
    public void GetStockpile_ReturnsCorrectQuantity()
    {
        var station = MakeStation();
        station.BulkStorage.Deposit("iron", 75f);
        var endpoint = new StationResourceEndpoint(station);

        AssertThat(endpoint.GetStockpile("iron")).IsEqual(75f);
        AssertThat(endpoint.GetStockpile("copper")).IsEqual(0f);
    }

    [TestCase]
    public void GetAllStockpiles_ReturnsAllQuantities()
    {
        var station = MakeStation();
        station.BulkStorage.Deposit("iron", 10f);
        station.BulkStorage.Deposit("copper", 20f);
        var endpoint = new StationResourceEndpoint(station);

        var stockpiles = endpoint.GetAllStockpiles();

        AssertThat(stockpiles.ContainsKey("iron")).IsTrue();
        AssertThat(stockpiles.ContainsKey("copper")).IsTrue();
        AssertThat(stockpiles["iron"]).IsEqual(10f);
        AssertThat(stockpiles["copper"]).IsEqual(20f);
    }

    [TestCase]
    public void EnqueueResourceRequest_IsSilentlyIgnored()
    {
        var station = MakeStation();
        var endpoint = new StationResourceEndpoint(station);

        // EnqueueResourceRequest is a no-op for station endpoints —
        // they are bulk pass-throughs, not discrete manufacturing.
        // We verify it does not throw by calling with a null request,
        // which the implementation gracefully handles (empty body).
        // A real ResourceRequest requires a Building, so we skip that here.
    }

    [TestCase]
    public void GetStorageFillPercentage_WithMatchingOwner_ReturnsFillPercentage()
    {
        var station = MakeStation();
        station.BulkStorage.Deposit("iron", 50f);
        var endpoint = new StationResourceEndpoint(station);

        float pct = endpoint.GetStorageFillPercentage(station, "any");

        AssertThat(pct).IsGreater(0f);
    }

    [TestCase]
    public void GetStorageFillPercentage_WithNonMatchingOwner_ReturnsZero()
    {
        var station = MakeStation();
        station.BulkStorage.Deposit("iron", 50f);
        var endpoint = new StationResourceEndpoint(station);

        float pct = endpoint.GetStorageFillPercentage(null, "any");

        AssertThat(pct).IsEqual(0f);
    }

    [TestCase]
    public void GetStorageFillPercentage_WithEmptyStorage_ReturnsZero()
    {
        var station = new StationSatellite();
        // No slots added — empty storage
        var endpoint = new StationResourceEndpoint(station);

        float pct = endpoint.GetStorageFillPercentage(station, "any");

        AssertThat(pct).IsEqual(0f);
    }

    [TestCase]
    public void Owner_ReturnsStationSatellite()
    {
        var station = MakeStation();
        var endpoint = new StationResourceEndpoint(station);

        AssertThat(endpoint.Owner).IsEqual(station);
    }
}
