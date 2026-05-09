using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using Constructables.Tick;

namespace Tests.Constructables.Buildings;

[TestSuite]
public class BuildingLinkFlowTest
{
    // ========================================================================
    // END-TO-END: OUTPUT -> LINK -> INPUT
    // ========================================================================

    [TestCase]
    public void EndToEnd_ResourceFlowsFromA_Output_ToB_Input()
    {
        var buildingA = CreateBuildingWithExport("iron");
        var buildingB = CreateBuildingWithImport("iron");
        var link = CreateLink(buildingA.ExportNode!, buildingB.ImportNode!, transportSpeed: 1.0f);

        var manufacturingA = new ManufacturingBehavior();
        manufacturingA.OnAttach(buildingA.Building);
        buildingA.Building.Behaviors.Add(manufacturingA);

        buildingA.OutputStorage.Deposit("iron", 25f);

        buildingA.Building.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(buildingB.InputStorage.GetQuantity("iron")).IsEqual(25f);
        AssertThat(buildingA.OutputStorage.GetQuantity("iron")).IsEqual(0f);
    }

    [TestCase]
    public void EndToEnd_UsingTickEngine_ResourceFlowsFromA_ToB()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var buildingA = CreateBuildingWithExport("coal");
        var buildingB = CreateBuildingWithImport("coal");
        var link = CreateLink(buildingA.ExportNode!, buildingB.ImportNode!, transportSpeed: 60.0f);

        var manufacturingA = new ManufacturingBehavior();
        manufacturingA.OnAttach(buildingA.Building);
        buildingA.Building.Behaviors.Add(manufacturingA);

        engine.Register(buildingA.Building);
        engine.Register(link);

        buildingA.OutputStorage.Deposit("coal", 10f);

        engine.SingleTickForTesting();

        AssertThat(buildingB.InputStorage.GetQuantity("coal")).IsEqual(10f);
        AssertThat(buildingA.OutputStorage.GetQuantity("coal")).IsEqual(0f);

        engine.Stop();
    }

    [TestCase]
    public void EndToEnd_ManufacturingCycle_ProducesAndDelivers()
    {
        var recipe = new RecipeDefinition
        {
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float> { ["iron_ore"] = 10f },
            OutputResources = new Dictionary<string, float> { ["iron"] = 5f }
        };

        var buildingA = CreateBuildingWithExport("iron");
        buildingA.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));

        var manufacturingA = new ManufacturingBehavior();
        manufacturingA.OnAttach(buildingA.Building);
        buildingA.Building.Behaviors.Add(manufacturingA);

        buildingA.InputStorage.Deposit("iron_ore", 10f);

        var buildingB = CreateBuildingWithImport("iron");
        var link = CreateLink(buildingA.ExportNode!, buildingB.ImportNode!, transportSpeed: 1.0f);

        manufacturingA.StartCycle(recipe, 10.0f);

        AssertThat(manufacturingA.State).IsEqual(ManufacturingState.Manufacturing);
        AssertThat(buildingA.InputStorage.GetQuantity("iron_ore")).IsEqual(0f);

        // Tick enough to complete manufacturing (WorkRequired=0.1, speed=10 => 0.01s)
        buildingA.Building.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(buildingB.InputStorage.GetQuantity("iron")).IsEqual(5f);
        AssertThat(manufacturingA.State).IsEqual(ManufacturingState.Idle);
    }

    [TestCase]
    public void EndToEnd_ManufacturingCycle_WaitsForInputsThenResumes()
    {
        // Building checks InputStorage for required inputs before manufacturing. Partial inputs
        // park the cycle in WaitingForInputs; a later deposit (simulating a link arrival) plus
        // a tick must resume Manufacturing.
        var recipe = new RecipeDefinition
        {
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float> { ["copper_ore"] = 5f },
            OutputResources = new Dictionary<string, float> { ["copper"] = 3f }
        };

        var buildingA = CreateBuildingWithExport("copper");
        buildingA.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("copper_ore")));

        var manufacturingA = new ManufacturingBehavior();
        manufacturingA.OnAttach(buildingA.Building);
        buildingA.Building.Behaviors.Add(manufacturingA);

        buildingA.InputStorage.Deposit("copper_ore", 2f);

        manufacturingA.StartCycle(recipe, productionSpeed: 10.0f);

        AssertThat(manufacturingA.State).IsEqual(ManufacturingState.WaitingForInputs);

        buildingA.InputStorage.Deposit("copper_ore", 3f);
        manufacturingA.OnManufactureTick(0.001f, buildingA.Building);

        AssertThat(manufacturingA.State).IsEqual(ManufacturingState.Manufacturing);
        AssertThat(buildingA.InputStorage.GetQuantity("copper_ore")).IsEqual(0f);
    }

    [TestCase]
    public void EndToEnd_ManufacturingCycle_MissingInputsArriveViaLink()
    {
        var recipe = new RecipeDefinition
        {
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float> { ["silicon"] = 4f },
            OutputResources = new Dictionary<string, float> { ["chip"] = 1f }
        };

        var buildingA = CreateBuildingWithExport("chip");
        buildingA.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("silicon")));

        // Add an import node so silicon can be delivered via link
        var importNode = new ResourceNode { Owner = buildingA.Building, Kind = ResourceNodeKind.Import };
        buildingA.Building.Nodes.Add(importNode);

        var manufacturingA = new ManufacturingBehavior();
        manufacturingA.OnAttach(buildingA.Building);
        buildingA.Building.Behaviors.Add(manufacturingA);

        var buildingSource = CreateBuildingWithExport("silicon");
        var sourceManufacturing = new ManufacturingBehavior();
        sourceManufacturing.OnAttach(buildingSource.Building);
        buildingSource.Building.Behaviors.Add(sourceManufacturing);
        buildingSource.OutputStorage.Deposit("silicon", 4f);

        var link = CreateLink(buildingSource.ExportNode!, importNode, transportSpeed: 1.0f);

        manufacturingA.StartCycle(recipe, productionSpeed: 10.0f);

        AssertThat(manufacturingA.State).IsEqual(ManufacturingState.WaitingForInputs);

        // Tick source building to push silicon into link, then link delivers to A
        buildingSource.Building.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        // Now A has silicon in InputStorage; tick A to pull it in
        // Use small delta so manufacturing doesn't immediately complete
        buildingA.Building.OnManufactureTick(0.001f);

        AssertThat(manufacturingA.State).IsEqual(ManufacturingState.Manufacturing);
        AssertThat(buildingA.InputStorage.GetQuantity("silicon")).IsEqual(0f);
    }

    // ========================================================================
    // STORAGE HUB BEHAVIOR
    // ========================================================================

    [TestCase]
    public void StorageHubBehavior_AllocatesAndReleasesBulkSlots()
    {
        var building = new Building();

        var hub = new StorageHubBehavior
        {
            StorageCapacity = 5,
            SlotFilters = new System.Collections.Generic.List<SlotFilterSpec>
            {
                new(SlotFilter.ForResource("iron"), 1),
                new(SlotFilter.ForResource("copper"), 1),
                // The remaining 3 default to Any.
            }
        };
        hub.OnAttach(building);

        AssertThat(building.BulkStorage.Slots.Count).IsEqual(0);

        hub.OnRegister();

        AssertThat(building.BulkStorage.Slots.Count).IsEqual(5);

        hub.OnUnregister();

        AssertThat(building.BulkStorage.Slots.Count).IsEqual(0);
    }

    // ========================================================================
    // TRANSPORT HUB BEHAVIOR
    // ========================================================================

    [TestCase]
    public void TransportHubBehavior_DrainsBulkBufferThroughLinks()
    {
        var buildingA = CreateBuildingWithExport("coal");
        var buildingB = CreateBuildingWithImport("coal");
        var link = CreateLink(buildingA.ExportNode!, buildingB.ImportNode!, transportSpeed: 1.0f);

        var transport = new TransportHubBehavior();
        transport.OnAttach(buildingA.Building);
        buildingA.Building.Behaviors.Add(transport);
        transport.BulkBuffer["coal"] = 40f;

        buildingA.Building.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(buildingB.InputStorage.GetQuantity("coal")).IsEqual(40f);
        AssertThat(transport.BulkBuffer.ContainsKey("coal")).IsFalse();
    }

    [TestCase]
    public void TransportHubBehavior_PartialDrain_WhenLinkCapacityLimited()
    {
        var buildingA = CreateBuildingWithExport("water");
        var buildingB = CreateBuildingWithImport("water");
        var link = CreateLink(buildingA.ExportNode!, buildingB.ImportNode!, transportSpeed: 1.0f, slotCapacity: 1, packageSize: 10);

        var transport = new TransportHubBehavior();
        transport.OnAttach(buildingA.Building);
        buildingA.Building.Behaviors.Add(transport);
        transport.BulkBuffer["water"] = 50f;

        buildingA.Building.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        // Only 10 fits in one package
        AssertThat(buildingB.InputStorage.GetQuantity("water")).IsEqual(10f);
        AssertThat(transport.BulkBuffer["water"]).IsEqual(40f);
    }

    // ========================================================================
    // BULK STORAGE ROUTING
    // ========================================================================

    [TestCase]
    public void BulkRouting_PureBulk_LinkDeliversToBulk()
    {
        // Pure-bulk receiver: no manufacturing, single Any slot in bulk, import node.
        var receiver = new Building();
        receiver.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(receiver);
        receiver.Behaviors.Add(routing);

        var importNode = new ResourceNode { Owner = receiver, Kind = ResourceNodeKind.Import };
        receiver.Nodes.Add(importNode);

        var sender = CreateBuildingWithExport("ore");
        sender.OutputStorage.Deposit("ore", 30f);

        var senderMfg = new ManufacturingBehavior();
        senderMfg.OnAttach(sender.Building);
        sender.Building.Behaviors.Add(senderMfg);

        var link = CreateLink(sender.ExportNode!, importNode, transportSpeed: 1.0f);

        // Drive sender's PushOutputs directly to skip Building.TryStartManufacturingCycleFromRegistration
        // which dereferences a null Definition in tests.
        senderMfg.OnManufactureTick(1f, sender.Building);
        link.OnManufactureTick(1f);

        AssertThat(receiver.BulkStorage.GetQuantity("ore")).IsEqual(30f);
        AssertThat(receiver.InputStorage.GetQuantity("ore")).IsEqual(0f);
    }

    [TestCase]
    public void BulkRouting_PureBulk_LinkDrainsBulk()
    {
        // Pure-bulk source pre-loaded; routing behavior should drain bulk into export link.
        var source = new Building();
        source.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        source.BulkStorage.Deposit("coal", 25f);
        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(source);
        source.Behaviors.Add(routing);

        var exportNode = new ResourceNode { Owner = source, Kind = ResourceNodeKind.Export };
        source.Nodes.Add(exportNode);

        var receiver = CreateBuildingWithImport("coal");
        var link = CreateLink(exportNode, receiver.ImportNode!, transportSpeed: 1.0f);

        // No ManufacturingBehavior on source → safe to use Building.OnManufactureTick;
        // TryStartManufacturingCycleFromRegistration short-circuits when no mfg behavior.
        source.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(receiver.InputStorage.GetQuantity("coal")).IsEqual(25f);
        AssertThat(source.BulkStorage.GetQuantity("coal")).IsEqual(0f);
    }

    [TestCase]
    public void BulkRouting_Hybrid_DeliveryFillsBulkNotInput()
    {
        // Hybrid receiver has both a recipe-locked iron InputStorage slot AND a bulk Any slot.
        // Incoming iron must land in bulk, not input.
        var receiver = new Building();
        receiver.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        receiver.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(receiver);
        receiver.Behaviors.Add(routing);

        var importNode = new ResourceNode { Owner = receiver, Kind = ResourceNodeKind.Import };
        receiver.Nodes.Add(importNode);

        var sender = CreateBuildingWithExport("iron");
        sender.OutputStorage.Deposit("iron", 15f);
        var senderMfg = new ManufacturingBehavior();
        senderMfg.OnAttach(sender.Building);
        sender.Building.Behaviors.Add(senderMfg);

        var link = CreateLink(sender.ExportNode!, importNode, transportSpeed: 1.0f);

        senderMfg.OnManufactureTick(1f, sender.Building);
        link.OnManufactureTick(1f);

        AssertThat(receiver.BulkStorage.GetQuantity("iron")).IsEqual(15f);
        AssertThat(receiver.InputStorage.GetQuantity("iron")).IsEqual(0f);
    }

    [TestCase]
    public void BulkRouting_Hybrid_PullsFromBulkViaInput()
    {
        // Bulk pre-loaded with iron; manufacturing waits for iron. Routing pulls bulk→input
        // before manufacturing's TryFulfillInputsFromStorage runs in the same tick.
        var building = new Building();
        building.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        building.BulkStorage.Deposit("iron", 10f);
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));

        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(building);
        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        // Insertion order matters here because Register() (which sorts by Priority) is not
        // called in this test. Routing first (priority -1 conceptually) so it runs before mfg.
        building.Behaviors.Add(routing);
        building.Behaviors.Add(mfg);

        var recipe = new RecipeDefinition
        {
            WorkRequired = 100f,
            InputResources = new Dictionary<string, float> { ["iron"] = 5f },
            OutputResources = new Dictionary<string, float> { ["plate"] = 1f },
        };
        mfg.StartCycle(recipe, productionSpeed: 1f);
        AssertThat(mfg.State).IsEqual(ManufacturingState.WaitingForInputs);

        building.OnManufactureTick(0.001f);

        // Routing tops InputStorage from BulkStorage, then ManufacturingBehavior withdraws
        // the recipe's needs. Either ordering is observable via the cycle leaving WaitingForInputs.
        AssertThat(mfg.State).IsEqual(ManufacturingState.Manufacturing);
        // Total iron remaining across all storages plus held inputs must equal pre-tick total.
        float totalAfter =
            building.BulkStorage.GetQuantity("iron")
            + building.InputStorage.GetQuantity("iron")
            + (mfg.InputsHeld.TryGetValue("iron", out var held) ? held : 0f);
        AssertThat(totalAfter).IsEqual(10f);
    }

    [TestCase]
    public void BulkRouting_Hybrid_OutputsFlowOutputBulkLink()
    {
        // FinishCycle deposits to OutputStorage; tick N+1 routing moves OutputStorage→Bulk
        // and tick N+1 also enqueues Bulk into the export link.
        var producer = new Building();
        producer.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        producer.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("plate")));
        producer.OutputStorage.Deposit("plate", 8f);

        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(producer);
        producer.Behaviors.Add(routing);

        var exportNode = new ResourceNode { Owner = producer, Kind = ResourceNodeKind.Export };
        producer.Nodes.Add(exportNode);

        var receiver = CreateBuildingWithImport("plate");
        var link = CreateLink(exportNode, receiver.ImportNode!, transportSpeed: 1.0f);

        producer.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(producer.OutputStorage.GetQuantity("plate")).IsEqual(0f);
        AssertThat(producer.BulkStorage.GetQuantity("plate")).IsEqual(0f);
        AssertThat(receiver.InputStorage.GetQuantity("plate")).IsEqual(8f);
    }

    [TestCase]
    public void BulkRouting_Fallback_BulkRejectsButRecipeSlotAccepts()
    {
        // Bulk's only slot is locked to iron and full → no room for coal. InputStorage has a
        // recipe-locked coal slot → fallback path deposits to InputStorage.
        // (Pre-filling the iron slot is required because Storage.GetFreeSpace is permissive
        // for empty Resource-filtered slots when ResourceDatabase is unavailable in tests;
        // an occupied slot is checked strictly.)
        var building = new Building();
        building.BulkStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        building.BulkStorage.Deposit("iron", 100f);
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("coal")));

        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(building);
        building.Behaviors.Add(routing);

        var package = new ResourcePackage { ResourceId = "coal", Quantity = 12f };
        bool accepted = building.OnDelivery(package);

        AssertThat(accepted).IsTrue();
        AssertThat(building.InputStorage.GetQuantity("coal")).IsEqual(12f);
        AssertThat(building.BulkStorage.GetQuantity("coal")).IsEqual(0f);
    }

    [TestCase]
    public void BulkRouting_Reject_BulkFullNoFallback_LinkBuffers()
    {
        // Bulk rejects (filter mismatch), no recipe-locked input slot for the resource → false.
        // Same caveat as above: occupy the slot to force a strict filter check.
        var building = new Building();
        building.BulkStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        building.BulkStorage.Deposit("iron", 100f);

        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(building);
        building.Behaviors.Add(routing);

        var package = new ResourcePackage { ResourceId = "coal", Quantity = 5f };
        bool accepted = building.OnDelivery(package);

        AssertThat(accepted).IsFalse();
        AssertThat(building.BulkStorage.GetQuantity("coal")).IsEqual(0f);
        AssertThat(building.InputStorage.GetQuantity("coal")).IsEqual(0f);
    }

    [TestCase]
    public void BulkRouting_PartialBulkSpace_AllOrNothing()
    {
        // Bulk has only one Any-slot already 80% full of iron (capacity 100). Package of 30
        // exceeds remaining 20 free space — all-or-nothing rejects without partial deposit.
        var building = new Building();
        building.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        building.BulkStorage.Deposit("iron", 80f);

        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(building);
        building.Behaviors.Add(routing);

        var package = new ResourcePackage { ResourceId = "iron", Quantity = 30f };
        bool accepted = building.OnDelivery(package);

        AssertThat(accepted).IsFalse();
        AssertThat(building.BulkStorage.GetQuantity("iron")).IsEqual(80f);
    }

    [TestCase]
    public void BulkRouting_LegacyPath_NoBulkBehavior()
    {
        // Regression: building with no BulkStorageRoutingBehavior delivers straight to
        // InputStorage as before.
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));

        var package = new ResourcePackage { ResourceId = "iron", Quantity = 7f };
        bool accepted = building.OnDelivery(package);

        AssertThat(accepted).IsTrue();
        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(7f);
        AssertThat(building.BulkStorage.GetQuantity("iron")).IsEqual(0f);
    }

    [TestCase]
    public void BulkRouting_ManufacturingPushOutputs_GatedWhenBulkActive()
    {
        // ManufacturingBehavior.PushOutputs must be a no-op when bulk routing is active,
        // so the routing behavior is the sole owner of OutputStorage drainage.
        // Drive the per-behavior ticks directly to avoid Building.TryStartManufacturingCycleFromRegistration's
        // null-Definition NRE in tests.
        var producer = new Building();
        producer.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        producer.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("plate")));
        producer.OutputStorage.Deposit("plate", 5f);

        var routing = new BulkStorageRoutingBehavior();
        routing.OnAttach(producer);
        producer.Behaviors.Add(routing);

        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(producer);
        producer.Behaviors.Add(mfg);

        var exportNode = new ResourceNode { Owner = producer, Kind = ResourceNodeKind.Export };
        producer.Nodes.Add(exportNode);

        var receiver = CreateBuildingWithImport("plate");
        var link = CreateLink(exportNode, receiver.ImportNode!, transportSpeed: 1.0f);

        // First tick: routing moves OutputStorage→Bulk and tries to drain to link.
        // Manufacturing's PushOutputs is gated → no-op for OutputStorage drain.
        routing.OnManufactureTick(1f, producer);
        mfg.OnManufactureTick(1f, producer);
        link.OnManufactureTick(1f);

        AssertThat(receiver.InputStorage.GetQuantity("plate")).IsEqual(5f);
        AssertThat(producer.OutputStorage.GetQuantity("plate")).IsEqual(0f);
        AssertThat(producer.BulkStorage.GetQuantity("plate")).IsEqual(0f);
    }

    // ========================================================================
    // FINISHED OUTPUTS LAND IN OUTPUT STORAGE
    // ========================================================================

    [TestCase]
    public void Manufacture_FinishedCycle_DepositsOutputsToOutputStorage()
    {
        // After a cycle completes, ManufacturingBehavior must deposit produced resources
        // into the building's OutputStorage (the source half of the link-flow contract).
        var recipe = new RecipeDefinition
        {
            WorkRequired = 0.1f,
            InputResources = new Dictionary<string, float> { ["iron_ore"] = 2f },
            OutputResources = new Dictionary<string, float> { ["iron"] = 1f },
        };

        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        building.InputStorage.Deposit("iron_ore", 2f);

        var mfg = new ManufacturingBehavior();
        mfg.OnAttach(building);
        building.Behaviors.Add(mfg);

        mfg.StartCycle(recipe, productionSpeed: 10f);
        AssertThat(mfg.State).IsEqual(ManufacturingState.Manufacturing);

        // 1f easily exceeds WorkRequired/speed = 0.01s.
        mfg.OnManufactureTick(1f, building);

        AssertThat(building.OutputStorage.GetQuantity("iron")).IsEqual(1f);
        AssertThat(mfg.State).IsEqual(ManufacturingState.Idle);
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private static BuildingWithNodes CreateBuildingWithExport(string resourceId)
    {
        var building = new Building();
        building.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(resourceId)));

        var exportNode = new ResourceNode { Owner = building, Kind = ResourceNodeKind.Export };
        building.Nodes.Add(exportNode);

        return new BuildingWithNodes(building, exportNode, null);
    }

    private static BuildingWithNodes CreateBuildingWithImport(string resourceId)
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(resourceId)));

        var importNode = new ResourceNode { Owner = building, Kind = ResourceNodeKind.Import };
        building.Nodes.Add(importNode);

        return new BuildingWithNodes(building, null, importNode);
    }

    private static ResourceLink CreateLink(ResourceNode source, ResourceNode target, float transportSpeed, int slotCapacity = 5, int packageSize = 50)
    {
        var link = new ResourceLink
        {
            Source = source,
            Target = target,
            Profile = new LinkProfile
            {
                SlotCapacity = slotCapacity,
                BundleTime = 0,
                TransportSpeed = transportSpeed,
                PackageSize = packageSize
            }
        };
        source.Link = link;
        target.Link = link;
        return link;
    }

    private class BuildingWithNodes
    {
        public Building Building { get; }
        public ResourceNode? ExportNode { get; }
        public ResourceNode? ImportNode { get; }

        public BuildingWithNodes(Building building, ResourceNode? exportNode, ResourceNode? importNode)
        {
            Building = building;
            ExportNode = exportNode;
            ImportNode = importNode;
        }

        public Storage InputStorage => Building.InputStorage;
        public Storage OutputStorage => Building.OutputStorage;
        public Godot.Collections.Array<ResourceNode> Nodes => Building.Nodes;
    }

}
