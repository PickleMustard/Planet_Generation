using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using GdUnit4;
using Structures.Logistics;
using static GdUnit4.Assertions;

namespace Tests.Constructables.Buildings;

[TestSuite]
public class BuildingResourceEndpointTest
{
    [TestCase]
    public void DepositAndWithdraw_RoundTripThroughBulkStorage()
    {
        var building = new Building();
        var hub = new StorageHubBehavior
        {
            StorageCapacity = 4,
            SlotFilters = new System.Collections.Generic.List<SlotFilterSpec>
            {
                new(SlotFilter.Any(), 4),
            },
        };
        hub.OnAttach(building);
        hub.OnRegister();

        var endpoint = new BuildingResourceEndpoint(building);

        float deposited = endpoint.DepositResource("iron", 50f);
        AssertThat(deposited).IsGreater(0f);
        AssertThat(endpoint.GetStockpile("iron")).IsEqual(deposited);

        float withdrawn = endpoint.WithdrawResource("iron", deposited);
        AssertThat(withdrawn).IsEqual(deposited);
        AssertThat(endpoint.GetStockpile("iron")).IsEqual(0f);
    }

    [TestCase]
    public void GetAllStockpiles_ReportsEveryDepositedResource()
    {
        var building = new Building();
        var hub = new StorageHubBehavior
        {
            StorageCapacity = 4,
            SlotFilters = new System.Collections.Generic.List<SlotFilterSpec>
            {
                new(SlotFilter.Any(), 4),
            },
        };
        hub.OnAttach(building);
        hub.OnRegister();

        var endpoint = new BuildingResourceEndpoint(building);
        endpoint.DepositResource("iron", 30f);
        endpoint.DepositResource("copper", 20f);

        var all = endpoint.GetAllStockpiles();
        AssertThat(all.ContainsKey("iron")).IsTrue();
        AssertThat(all.ContainsKey("copper")).IsTrue();
        AssertThat(all["iron"]).IsGreater(0f);
        AssertThat(all["copper"]).IsGreater(0f);
    }
}
