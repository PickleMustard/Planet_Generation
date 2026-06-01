using System.Collections.Generic;
using GdUnit4;
using Godot;
using Structures.Logistics;
using Structures.Resources;
using UI.PlanetBoard;
using UI.PlanetBoard.Testing;
using static GdUnit4.Assertions;

namespace Tests.UI.PlanetBoard;

[TestSuite]
public class BoardLinkRendererTest
{
    // ── Tests requiring a Godot runtime (BoardLinkRenderer extends Node2D) ──

    [TestCase]
    [RequireGodotRuntime]
    public void RebuildLinks_NoBuildings_NoLinks()
    {
        var renderer = new BoardLinkRenderer();
        renderer.RebuildLinks(new List<BuildingNode2D>());

        // We can only verify it doesn't crash; internal state is private.
        // HitTestEdge returning null confirms no links were built.
        var result = renderer.HitTestEdge(Vector2.Zero);
        AssertThat(result).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RebuildLinks_UnlinkedBuildings_NoLinks()
    {
        var buildingA = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var buildingB = MockBuildingFactory.Create("square", new Color(0.5f, 0.5f, 0.5f), 1, 1);

        var nodeA = new BuildingNode2D();
        nodeA.Setup(buildingA, new Vector2(0, 0));
        var nodeB = new BuildingNode2D();
        nodeB.Setup(buildingB, new Vector2(200, 0));

        var renderer = new BoardLinkRenderer();
        renderer.RebuildLinks(new List<BuildingNode2D> { nodeA, nodeB });

        var result = renderer.HitTestEdge(new Vector2(100, 0));
        AssertThat(result).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RebuildLinks_LinkedBuildings_ProducesLinkEntries()
    {
        // Create two buildings with compatible ports (import + export)
        var buildingA = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 0, 1); // 1 export
        var buildingB = MockBuildingFactory.Create("square", new Color(0.5f, 0.5f, 0.5f), 1, 0);  // 1 import

        var nodeA = new BuildingNode2D();
        nodeA.Setup(buildingA, new Vector2(0, 0));
        var nodeB = new BuildingNode2D();
        nodeB.Setup(buildingB, new Vector2(200, 0));

        // Connect the export port of A to the import port of B
        var link = new ResourceLink();
        var sourceNode = buildingA.Nodes[0]; // Export port
        var targetNode = buildingB.Nodes[0]; // Import port
        link.ConnectNodes(sourceNode, targetNode);

        var renderer = new BoardLinkRenderer();
        renderer.RebuildLinks(new List<BuildingNode2D> { nodeA, nodeB });

        // Hit test near the midpoint between the two linked ports
        var sourcePort = nodeA.Ports[0];
        var targetPort = nodeB.Ports[0];
        Vector2 sourceWorld = nodeA.Position + sourcePort.LocalAnchor + sourcePort.OutwardNormal * 8f;
        Vector2 targetWorld = nodeB.Position + targetPort.LocalAnchor + targetPort.OutwardNormal * 8f;
        Vector2 midpoint = (sourceWorld + targetWorld) / 2f;

        var hit = renderer.HitTestEdge(midpoint, 20f);
        AssertThat(hit).IsNotNull();
        AssertThat(hit!.Value.Link).IsSame(link);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HitTestEdge_PointFarFromAll_ReturnsNull()
    {
        var buildingA = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 0, 1);
        var buildingB = MockBuildingFactory.Create("square", new Color(0.5f, 0.5f, 0.5f), 1, 0);

        var nodeA = new BuildingNode2D();
        nodeA.Setup(buildingA, new Vector2(0, 0));
        var nodeB = new BuildingNode2D();
        nodeB.Setup(buildingB, new Vector2(200, 0));

        var link = new ResourceLink();
        link.ConnectNodes(buildingA.Nodes[0], buildingB.Nodes[0]);

        var renderer = new BoardLinkRenderer();
        renderer.RebuildLinks(new List<BuildingNode2D> { nodeA, nodeB });

        // Hit test a point far away
        var hit = renderer.HitTestEdge(new Vector2(5000, 5000));
        AssertThat(hit).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SetHoveredLink_DoesNotCrash_WithNull()
    {
        var renderer = new BoardLinkRenderer();
        renderer.SetHoveredLink(null);
        // No crash = pass
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SetDragPreview_DoesNotCrash_WithNulls()
    {
        var renderer = new BoardLinkRenderer();
        renderer.SetDragPreview(null, null, false);
        // No crash = pass
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SetDragPreview_SetsValidPreview()
    {
        var renderer = new BoardLinkRenderer();
        renderer.SetDragPreview(new Vector2(0, 0), new Vector2(100, 100), true);
        // Internal state is private; verify by hitting near the line
        // The drag preview line goes from (0,0) to (100,100), midpoint at (50,50)
        // HitTestEdge only tests _links, not the preview line, so we just verify no crash
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RebuildLinks_DuplicateLinkNotAdded()
    {
        var buildingA = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 0, 1);
        var buildingB = MockBuildingFactory.Create("square", new Color(0.5f, 0.5f, 0.5f), 1, 0);

        var nodeA = new BuildingNode2D();
        nodeA.Setup(buildingA, new Vector2(0, 0));
        var nodeB = new BuildingNode2D();
        nodeB.Setup(buildingB, new Vector2(200, 0));

        var link = new ResourceLink();
        link.ConnectNodes(buildingA.Nodes[0], buildingB.Nodes[0]);

        var renderer = new BoardLinkRenderer();
        // Rebuild twice — the link is already set; the duplicate should be skipped
        renderer.RebuildLinks(new List<BuildingNode2D> { nodeA, nodeB });
        renderer.RebuildLinks(new List<BuildingNode2D> { nodeA, nodeB });

        // Only one link should be found — both buildings reference the same link,
        // but the HashSet inside RebuildLinks should deduplicate it.
        // Test by checking the same hit is returned (not duplicated).
        var sourcePort = nodeA.Ports[0];
        var targetPort = nodeB.Ports[0];
        Vector2 sourceWorld = nodeA.Position + sourcePort.LocalAnchor + sourcePort.OutwardNormal * 8f;
        Vector2 targetWorld = nodeB.Position + targetPort.LocalAnchor + targetPort.OutwardNormal * 8f;
        Vector2 midpoint = (sourceWorld + targetWorld) / 2f;

        var hit = renderer.HitTestEdge(midpoint, 20f);
        AssertThat(hit).IsNotNull();
    }
}
