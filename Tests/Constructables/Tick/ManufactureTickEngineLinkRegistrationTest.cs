using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Tick;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;

namespace Tests.Constructables.Tick;

[TestSuite]
public class ManufactureTickEngineLinkRegistrationTest
{
    // ========================================================================
    // CONNECT REGISTRATION
    // ========================================================================

    [TestCase]
    public void ResourceLink_ConnectNodes_RegistersWithEngine()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var source = new ResourceNode { Kind = ResourceNodeKind.Export };
        var target = new ResourceNode { Kind = ResourceNodeKind.Import };
        var link = new ResourceLink();

        link.ConnectNodes(source, target);
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(1);
        AssertThat(link.Source).IsEqual(source);
        AssertThat(link.Target).IsEqual(target);
        AssertThat(source.Link).IsEqual(link);
        AssertThat(target.Link).IsEqual(link);

        engine.Stop();
    }

    [TestCase]
    public void ResourceLink_ConnectNodes_WithNullNodes_ThrowsOrHandles()
    {
        var link = new ResourceLink();

        AssertThrown(() => link.ConnectNodes(null!, null!))
            .IsInstanceOf<System.InvalidOperationException>();
    }

    [TestCase]
    public void ResourceLink_ConnectNodes_InvalidKinds_Throws()
    {
        var link = new ResourceLink();
        var source = new ResourceNode { Kind = ResourceNodeKind.Import };
        var target = new ResourceNode { Kind = ResourceNodeKind.Import };

        AssertThrown(() => link.ConnectNodes(source, target))
            .IsInstanceOf<System.InvalidOperationException>();
    }

    // ========================================================================
    // DISCONNECT UNREGISTRATION
    // ========================================================================

    [TestCase]
    public void ResourceLink_Disconnect_UnregistersFromEngine()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var source = new ResourceNode { Kind = ResourceNodeKind.Export };
        var target = new ResourceNode { Kind = ResourceNodeKind.Import };
        var link = new ResourceLink();

        link.ConnectNodes(source, target);
        engine.SingleTickForTesting();
        AssertThat(engine.TickableCount).IsEqual(1);

        link.Disconnect();
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(0);
        AssertThat(link.Source).IsNull();
        AssertThat(link.Target).IsNull();

        engine.Stop();
    }

    [TestCase]
    public void ResourceLink_Disconnect_WithoutEngine_IsNoOp()
    {
        var source = new ResourceNode { Kind = ResourceNodeKind.Export };
        var target = new ResourceNode { Kind = ResourceNodeKind.Import };
        var link = new ResourceLink { Source = source, Target = target };
        source.Link = link;
        target.Link = link;

        // Engine is null here — should not throw
        link.Disconnect();

        AssertThat(link.Source).IsNull();
        AssertThat(link.Target).IsNull();
        AssertThat(source.Link).IsNull();
        AssertThat(target.Link).IsNull();
    }

    [TestCase]
    public void ResourceLink_ConnectNodesThenDisconnect_Lifecycle()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var source = new ResourceNode { Kind = ResourceNodeKind.Export };
        var target = new ResourceNode { Kind = ResourceNodeKind.Import };
        var link = new ResourceLink();

        link.ConnectNodes(source, target);
        engine.SingleTickForTesting();
        AssertThat(engine.TickableCount).IsEqual(1);

        link.Disconnect();
        engine.SingleTickForTesting();
        AssertThat(engine.TickableCount).IsEqual(0);

        // Reconnect
        link.ConnectNodes(source, target);
        engine.SingleTickForTesting();
        AssertThat(engine.TickableCount).IsEqual(1);

        engine.Stop();
    }

    // ========================================================================
    // BUILDING DESTRUCTION CLEANUP
    // ========================================================================

    [TestCase]
    public void Building_DestroyConstructable_DisconnectsAllNodeLinks()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var building = new Building();

        var exportNode = new ResourceNode { Owner = building, Kind = ResourceNodeKind.Export };
        var importNode = new ResourceNode { Owner = building, Kind = ResourceNodeKind.Import };
        building.Nodes.Add(exportNode);
        building.Nodes.Add(importNode);

        var otherBuilding = new Building();
        var otherExport = new ResourceNode { Owner = otherBuilding, Kind = ResourceNodeKind.Export };
        otherBuilding.Nodes.Add(otherExport);

        // Connect building's import to other building's export
        var linkA = new ResourceLink();
        linkA.ConnectNodes(otherExport, importNode);
        engine.SingleTickForTesting();
        AssertThat(engine.TickableCount).IsEqual(1);

        // Connect building's export to other building's import
        var otherImport = new ResourceNode { Owner = otherBuilding, Kind = ResourceNodeKind.Import };
        otherBuilding.Nodes.Add(otherImport);
        var linkB = new ResourceLink();
        linkB.ConnectNodes(exportNode, otherImport);
        engine.SingleTickForTesting();
        AssertThat(engine.TickableCount).IsEqual(2);

        // Destroy the building
        building.DestroyConstructable();
        engine.SingleTickForTesting();

        // Links involving the destroyed building should be unregistered
        AssertThat(engine.TickableCount).IsEqual(0);
        AssertThat(exportNode.Link).IsNull();
        AssertThat(importNode.Link).IsNull();
        AssertThat(linkA.Source).IsNull();
        AssertThat(linkA.Target).IsNull();
        AssertThat(linkB.Source).IsNull();
        AssertThat(linkB.Target).IsNull();

        engine.Stop();
    }

    [TestCase]
    public void Building_DestroyConstructable_UnregistersBuildingFromEngine()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var definition = new BuildingDefinition
        {
            WorkRequired = 10f,
            RequiredResources = new System.Collections.Generic.Dictionary<string, int>()
        };
        var building = new Building();
        building.ApplyDefinition(definition);
        building.StartConstruction(new Godot.Collections.Dictionary());
        building.UpdateProgress(10f);
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(1);

        building.DestroyConstructable();
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(0);

        engine.Stop();
    }

    [TestCase]
    public void Building_DestroyConstructable_NoLinks_IsNoOp()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var definition = new BuildingDefinition
        {
            WorkRequired = 10f,
            RequiredResources = new System.Collections.Generic.Dictionary<string, int>()
        };
        var building = new Building();
        building.ApplyDefinition(definition);
        building.StartConstruction(new Godot.Collections.Dictionary());
        building.UpdateProgress(10f);
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(1);

        // Building has no nodes/links — should still unregister cleanly
        building.DestroyConstructable();
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(0);

        engine.Stop();
    }

    [TestCase]
    public void Building_DestroyConstructable_LeavesOtherBuildingLinksIntact()
    {
        var engine = ManufactureTickEngine.CreateForTesting();

        var buildingA = new Building();
        var exportNodeA = new ResourceNode { Owner = buildingA, Kind = ResourceNodeKind.Export };
        buildingA.Nodes.Add(exportNodeA);

        var buildingB = new Building();
        var importNodeB = new ResourceNode { Owner = buildingB, Kind = ResourceNodeKind.Import };
        buildingB.Nodes.Add(importNodeB);

        var buildingC = new Building();
        var exportNodeC = new ResourceNode { Owner = buildingC, Kind = ResourceNodeKind.Export };
        buildingC.Nodes.Add(exportNodeC);
        var importNodeC = new ResourceNode { Owner = buildingC, Kind = ResourceNodeKind.Import };
        buildingC.Nodes.Add(importNodeC);

        // Link A -> B
        var linkAB = new ResourceLink();
        linkAB.ConnectNodes(exportNodeA, importNodeB);
        engine.SingleTickForTesting();

        // Link C -> B (both nodes on different buildings)
        var linkCB = new ResourceLink();
        linkCB.ConnectNodes(exportNodeC, importNodeB);
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(2);

        // Destroy building A: link AB should be disconnected, link CB should remain
        buildingA.DestroyConstructable();
        engine.SingleTickForTesting();

        AssertThat(engine.TickableCount).IsEqual(1);
        AssertThat(linkAB.Source).IsNull();
        AssertThat(linkAB.Target).IsNull();
        AssertThat(exportNodeA.Link).IsNull();
        AssertThat(linkCB.Source).IsEqual(exportNodeC);
        AssertThat(linkCB.Target).IsEqual(importNodeB);
        AssertThat(exportNodeC.Link).IsEqual(linkCB);
        AssertThat(importNodeB.Link).IsEqual(linkCB);

        engine.Stop();
    }
}
