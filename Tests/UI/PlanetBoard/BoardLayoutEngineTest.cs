using System.Collections.Generic;
using Constructables;
using GdUnit4;
using Godot;
using UI.PlanetBoard.Testing;
using static GdUnit4.Assertions;

using LayoutEngine = UI.PlanetBoard.BoardLayoutEngine;

namespace Tests.UI.PlanetBoard;

[TestSuite]
public class BoardLayoutEngineTest
{
    // Shape diameter for MockBuildingFactory = ShapeSize(64) * 2 = 128.
    // Padding inside the engine is 24, so packing c = 128 + 24 = 152.
    private const float MockShapeDiameter = 128f;
    private const float ExpectedPackingC = 152f;

    // ── Pure unit tests (no Godot runtime needed) ──────────────────

    [TestCase]
    public void Compute_EmptyList_ReturnsEmptyPositions()
    {
        var layout = LayoutEngine.Compute(new List<Building>());

        AssertThat(layout.Positions.Count).IsEqual(0);
        AssertThat(layout.BoundingBox.Size.X).IsGreater(0f);
        AssertThat(layout.BoundingBox.Size.Y).IsGreater(0f);
    }

    [TestCase]
    public void Compute_NullList_ReturnsEmptyPositions()
    {
        var layout = LayoutEngine.Compute(null!);

        AssertThat(layout.Positions.Count).IsEqual(0);
    }

    // ── Tests requiring Godot runtime (Building/ResourceNode need it) ──

    [TestCase]
    [RequireGodotRuntime]
    public void Compute_SingleBuilding_PlacedAtCenter()
    {
        // Sunflower index 0 sits at the origin regardless of shape size.
        var building = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var layout = LayoutEngine.Compute(new List<Building> { building });

        AssertThat(layout.Positions.Count).IsEqual(1);
        AssertThat(layout.Positions.ContainsKey(building)).IsTrue();

        var pos = layout.Positions[building];
        AssertThat(pos.Length()).IsLess(1f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Compute_TwoBuildings_FirstAtCenterSecondOnRing()
    {
        var b1 = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var b2 = MockBuildingFactory.Create("square", new Color(0.5f, 0.5f, 0.5f), 1, 1);

        var layout = LayoutEngine.Compute(new List<Building> { b1, b2 });

        AssertThat(layout.Positions.Count).IsEqual(2);

        var pos1 = layout.Positions[b1];
        var pos2 = layout.Positions[b2];

        AssertThat(pos1.Length()).IsLess(1f);
        AssertThat(Mathf.IsEqualApprox(pos2.Length(), ExpectedPackingC, 0.5f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Compute_ManyBuildings_NoOverlap_AndWithinDisk()
    {
        const int count = 8;
        var buildings = new List<Building>();
        for (int i = 0; i < count; i++)
            buildings.Add(MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1));

        var layout = LayoutEngine.Compute(buildings);

        AssertThat(layout.Positions.Count).IsEqual(count);

        var posList = new List<Vector2>();
        foreach (var b in buildings)
            posList.Add(layout.Positions[b]);

        // Pairwise minimum distance must be at least one shape diameter
        // (Vogel's nearest-neighbour distance is ≈ c = shapeDiameter + padding).
        for (int i = 0; i < posList.Count; i++)
        {
            for (int j = i + 1; j < posList.Count; j++)
            {
                float d = posList[i].DistanceTo(posList[j]);
                AssertThat(d).IsGreaterEqual(MockShapeDiameter * 0.95f);
            }
        }

        // Every position must sit inside the reported disk.
        float maxLen = 0f;
        foreach (var p in posList)
            if (p.Length() > maxLen)
                maxLen = p.Length();
        AssertThat(maxLen).IsLessEqual(layout.CircleRadius + 0.5f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Compute_CircleRadius_MonotonicWithCount()
    {
        var layout2 = LayoutEngine.Compute(new List<Building>
        {
            MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1),
            MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1),
        });
        var layout5 = LayoutEngine.Compute(new List<Building>
        {
            MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1),
            MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1),
            MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1),
            MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1),
            MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1),
        });

        AssertThat(layout5.CircleRadius).IsGreaterEqual(layout2.CircleRadius);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Compute_BoundingBox_EncompassesAllPositions()
    {
        var b1 = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var b2 = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);

        var layout = LayoutEngine.Compute(new List<Building> { b1, b2 });

        AssertThat(layout.BoundingBox.HasPoint(layout.Positions[b1])).IsTrue();
        AssertThat(layout.BoundingBox.HasPoint(layout.Positions[b2])).IsTrue();
    }

    // ── Incremental placement tests ─────────────────────────────────

    [TestCase]
    [RequireGodotRuntime]
    public void ComputePositionForNew_FirstBuilding_PlacedAtCenter()
    {
        var building = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var result = LayoutEngine.ComputePositionForNew(
            building, new List<Building>(), new Dictionary<Building, Vector2>(), 0f);

        AssertThat(result.NewPosition.Length()).IsLess(1f);
        AssertThat(result.NewCircleRadius).IsGreater(0f);
        AssertThat(result.UpdatedPositions.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ComputePositionForNew_AppendsAtNextIndex()
    {
        // After laying out two buildings, the third should appear at the
        // sunflower position for index 2 — not the gap midpoint.
        var b1 = MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var b2 = MockBuildingFactory.Create("square", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var b3 = MockBuildingFactory.Create("pentagon", new Color(0.5f, 0.5f, 0.5f), 1, 1);

        var layout = LayoutEngine.Compute(new List<Building> { b1, b2 });
        var existingPositions = new Dictionary<Building, Vector2>(layout.Positions);

        var result = LayoutEngine.ComputePositionForNew(
            b3, new List<Building> { b1, b2 }, existingPositions, layout.CircleRadius);

        float c = LayoutEngine.ComputePackingSpacing(MockShapeDiameter);
        Vector2 expected = LayoutEngine.SunflowerPosition(2, c, -Mathf.Pi / 2f);

        AssertThat(Mathf.IsEqualApprox(result.NewPosition.X, expected.X, 0.5f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(result.NewPosition.Y, expected.Y, 0.5f)).IsTrue();
        AssertThat(result.UpdatedPositions.Count).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ComputePositionForNew_RadiusMonotonicallyGrows()
    {
        var buildings = new List<Building>();
        for (int i = 0; i < 5; i++)
            buildings.Add(MockBuildingFactory.Create("hexagon", new Color(0.5f, 0.5f, 0.5f), 1, 1));

        var layout = LayoutEngine.Compute(buildings);
        float originalRadius = layout.CircleRadius;

        var existingPositions = new Dictionary<Building, Vector2>(layout.Positions);

        var newBuilding = MockBuildingFactory.Create("square", new Color(0.5f, 0.5f, 0.5f), 1, 1);
        var result = LayoutEngine.ComputePositionForNew(
            newBuilding, buildings, existingPositions, originalRadius);

        AssertThat(result.NewCircleRadius).IsGreaterEqual(originalRadius);
    }

    // ── Geometry helper tests (pure unit tests) ─────────────────────

    [TestCase]
    public void AngleToPosition_TopAngle_YIsNegative()
    {
        var pos = LayoutEngine.AngleToPosition(-Mathf.Pi / 2f, 100f);
        AssertThat(Mathf.IsEqualApprox(pos.X, 0f, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(pos.Y, -100f, 0.01f)).IsTrue();
    }

    [TestCase]
    public void AngleToPosition_RightAngle_XIsPositive()
    {
        var pos = LayoutEngine.AngleToPosition(0f, 100f);
        AssertThat(Mathf.IsEqualApprox(pos.X, 100f, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(pos.Y, 0f, 0.01f)).IsTrue();
    }

    [TestCase]
    public void PositionToAngle_RoundTrip()
    {
        float angle = 1.23f;
        var pos = LayoutEngine.AngleToPosition(angle, 50f);
        float recovered = LayoutEngine.PositionToAngle(pos);
        AssertThat(Mathf.IsEqualApprox(recovered, angle, 0.001f)).IsTrue();
    }

    [TestCase]
    public void AngleDiff_PositiveDifference()
    {
        float diff = LayoutEngine.AngleDiff(Mathf.Pi, 0f);
        AssertThat(Mathf.IsEqualApprox(diff, Mathf.Pi, 0.001f)).IsTrue();
    }

    [TestCase]
    public void AngleDiff_WrapAround()
    {
        float diff = LayoutEngine.AngleDiff(0.1f, Mathf.Tau - 0.1f);
        AssertThat(Mathf.IsEqualApprox(diff, 0.2f, 0.01f)).IsTrue();
    }

    [TestCase]
    public void ComputeCircleRadius_Single_ReturnsMinRadius()
    {
        float r = LayoutEngine.ComputeCircleRadius(1, MockShapeDiameter);
        AssertThat(r).IsGreater(0f);
        AssertThat(r).IsGreaterEqual(MockShapeDiameter / 2f);
    }

    [TestCase]
    public void ComputeCircleRadius_GrowsWithCount()
    {
        float r2 = LayoutEngine.ComputeCircleRadius(2, MockShapeDiameter);
        float r6 = LayoutEngine.ComputeCircleRadius(6, MockShapeDiameter);
        AssertThat(r6).IsGreater(r2);
    }

    [TestCase]
    public void ComputeCircleRadius_ZeroCount_ReturnsZero()
    {
        float r = LayoutEngine.ComputeCircleRadius(0, MockShapeDiameter);
        AssertThat(Mathf.IsEqualApprox(r, 0f, 0.001f)).IsTrue();
    }

    [TestCase]
    public void ComputeCircleRadius_ContinuousMonotonic()
    {
        float prev = 0f;
        for (int n = 1; n < 50; n++)
        {
            float r = LayoutEngine.ComputeCircleRadius(n, MockShapeDiameter);
            AssertThat(r).IsGreaterEqual(prev);
            prev = r;
        }
    }

    [TestCase]
    public void SunflowerPosition_IndexZero_AtOrigin()
    {
        var pos = LayoutEngine.SunflowerPosition(0, 100f, 0f);
        AssertThat(pos.LengthSquared()).IsLess(1e-6f);
    }

    [TestCase]
    public void SunflowerPosition_IndexOne_AtPackingDistance()
    {
        var pos = LayoutEngine.SunflowerPosition(1, 100f, 0f);
        AssertThat(Mathf.IsEqualApprox(pos.Length(), 100f, 0.001f)).IsTrue();
    }

    [TestCase]
    public void ComputePackingSpacing_EqualsDiameterPlusPadding()
    {
        float c = LayoutEngine.ComputePackingSpacing(MockShapeDiameter);
        AssertThat(Mathf.IsEqualApprox(c, ExpectedPackingC, 0.001f)).IsTrue();
    }
}
