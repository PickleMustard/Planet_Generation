using System.Collections.Generic;
using Constructables;
using GdUnit4;
using Godot;
using Structures.Resources;
using UI.PlanetBoard;
using UI.PlanetBoard.Testing;
using static GdUnit4.Assertions;

namespace Tests.UI.PlanetBoard;

[TestSuite]
public class BuildingNode2DTest
{
    // ── Tests requiring a Godot runtime (BuildingNode2D extends Node2D) ────

    [TestCase]
    [RequireGodotRuntime]
    public void Setup_ReadsVisualData()
    {
        var building = MockBuildingFactory.Create("hexagon", new Color(0.4f, 0.6f, 0.8f), 1, 1);
        var node = new BuildingNode2D();
        node.Setup(building, new Vector2(100, 200));

        AssertThat(node.ShapeId).IsEqual("hexagon");
        AssertThat(node.Radius).IsEqual(64f);
        AssertThat(node.FillColor.R).IsEqual(0.4f);
        AssertThat(node.FillColor.G).IsEqual(0.6f);
        AssertThat(node.FillColor.B).IsEqual(0.8f);
        AssertThat(node.DisplayName).IsEqual("Mock hexagon");
        AssertThat(node.Building).IsSame(building);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Setup_SetsPositionToBoardPosition()
    {
        var building = MockBuildingFactory.Create("square", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var boardPos = new Vector2(150, -75);

        var node = new BuildingNode2D();
        node.Setup(building, boardPos);

        AssertThat(Mathf.IsEqualApprox(node.Position.X, boardPos.X, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(node.Position.Y, boardPos.Y, 0.01f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HitTestPort_ReturnsPortWithinRadius()
    {
        var building = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var node = new BuildingNode2D();
        node.Setup(building, Vector2.Zero);

        // Building has 1 import port on side Top (edge 0 of hexagon)
        // The port anchor is at the midpoint of the top edge, plus an outward normal * 8
        AssertThat(node.Ports.Count).IsGreaterEqual(1);

        // Test with a position right at the first port's drawn position
        var port = node.Ports[0];
        Vector2 portDrawn = port.LocalAnchor + port.OutwardNormal * 8f;

        var hit = node.HitTestPort(portDrawn, 12f);
        AssertThat(hit).IsNotNull();
        AssertThat(hit!.Node).IsSame(port.Node);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HitTestPort_ReturnsNullWhenNoneNearby()
    {
        var building = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var node = new BuildingNode2D();
        node.Setup(building, Vector2.Zero);

        // Test with a position far away from any port
        var hit = node.HitTestPort(new Vector2(500, 500), 12f);
        AssertThat(hit).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HitTestShape_ReturnsTrueInsidePolygon()
    {
        // A hexagon of radius 64 centered at origin — the center is definitely inside
        var building = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var node = new BuildingNode2D();
        node.Setup(building, Vector2.Zero);

        AssertThat(node.HitTestShape(Vector2.Zero)).IsTrue();
        AssertThat(node.HitTestShape(new Vector2(5, 5))).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HitTestShape_ReturnsFalseOutsidePolygon()
    {
        var building = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var node = new BuildingNode2D();
        node.Setup(building, Vector2.Zero);

        // Point far outside the shape
        AssertThat(node.HitTestShape(new Vector2(500, 500))).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DragTo_UpdatesPosition()
    {
        var building = MockBuildingFactory.Create("pentagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var node = new BuildingNode2D();
        node.Setup(building, new Vector2(0, 0));

        node.DragTo(new Vector2(250, 100));

        AssertThat(Mathf.IsEqualApprox(node.Position.X, 250f, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(node.Position.Y, 100f, 0.01f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StartDrag_EndDrag_UpdateFlags()
    {
        var building = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var node = new BuildingNode2D();
        node.Setup(building, Vector2.Zero);

        AssertThat(node.IsDragged).IsFalse();

        node.StartDrag();
        AssertThat(node.IsDragged).IsTrue();

        node.EndDrag();
        AssertThat(node.IsDragged).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Ports_PopulatedFromBuildingNodes()
    {
        // Create a building with 2 import + 2 export = 4 total ports
        var building = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 2, 2);
        var node = new BuildingNode2D();
        node.Setup(building, Vector2.Zero);

        AssertThat(node.Ports.Count).IsEqual(4);

        // Verify port data fields are populated
        foreach (var port in node.Ports)
        {
            AssertThat(port.Node).IsNotNull();
            AssertThat(port.Owner).IsSame(node);
        }
    }
}
