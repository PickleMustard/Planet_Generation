using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Structures.Enums;
using Structures.Resources;

namespace Tests.Structures.Resources;

[TestSuite]
public class BuildingShape2DTest
{
    private static BuildingShape2D BuildSquare()
    {
        return new BuildingShape2D
        {
            Id = "square_test",
            Vertices = new[]
            {
                new Vector2(0.707f, -0.707f),
                new Vector2(0.707f, 0.707f),
                new Vector2(-0.707f, 0.707f),
                new Vector2(-0.707f, -0.707f),
            },
            Sides = new List<BuildingShape2D.SideSpec>
            {
                new() { Slots = { new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Import, StateOfMatter = StateOfMatter.Solid } } },
                new() { Slots =
                    {
                        new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Export, StateOfMatter = StateOfMatter.Solid },
                        new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Export, StateOfMatter = StateOfMatter.Solid },
                    } },
                new(),
                new() { Slots = { new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Flex, StateOfMatter = StateOfMatter.Solid } } },
            },
        };
    }

    [TestCase]
    public void SideCount_EqualsVertexCount()
    {
        var s = BuildSquare();
        AssertThat(s.SideCount).IsEqual(4);
        AssertThat(s.VertexCount).IsEqual(4);
    }

    [TestCase]
    public void GetEdge_ReturnsVerticesScaledAndOffset()
    {
        var s = BuildSquare();
        var (a, b) = s.GetEdge(0, new Vector2(100, 100), 10f);
        AssertThat(Mathf.IsEqualApprox(a.X, 100 + 7.07f, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(a.Y, 100 - 7.07f, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(b.X, 100 + 7.07f, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(b.Y, 100 + 7.07f, 0.01f)).IsTrue();
    }

    [TestCase]
    public void GetSlotAnchor_SingleSlot_LandsAtEdgeMidpoint()
    {
        var s = BuildSquare();
        var anchor = s.GetSlotAnchor(0, 0, Vector2.Zero, 100f);
        // Side 0 has 1 slot — t = 1/2 — midpoint of (0.707, -0.707)→(0.707, 0.707)
        AssertThat(Mathf.IsEqualApprox(anchor.X, 70.7f, 0.1f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(anchor.Y, 0f, 0.1f)).IsTrue();
    }

    [TestCase]
    public void GetSlotAnchor_MultipleSlots_DistributesAlongEdge()
    {
        var s = BuildSquare();
        var a0 = s.GetSlotAnchor(1, 0, Vector2.Zero, 100f);
        var a1 = s.GetSlotAnchor(1, 1, Vector2.Zero, 100f);
        // Side 1 has 2 slots: t1 = 1/3, t2 = 2/3 along (0.707, 0.707)→(-0.707, 0.707)
        AssertThat(Mathf.IsEqualApprox(a0.X, 70.7f - 2f * 70.7f / 3f, 0.1f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(a1.X, 70.7f - 4f * 70.7f / 3f, 0.1f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(a0.Y, 70.7f, 0.1f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(a1.Y, 70.7f, 0.1f)).IsTrue();
    }

    [TestCase]
    public void GetSlotNormal_PointsOutwardFromCenter()
    {
        var s = BuildSquare();
        var normal = s.GetSlotNormal(0, 0, Vector2.Zero, 100f);
        // Side 0 is right edge; outward normal should point right (X≈1, Y≈0)
        AssertThat(Mathf.IsEqualApprox(normal.X, 1f, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(normal.Y, 0f, 0.01f)).IsTrue();
    }

    [TestCase]
    public void GetScaledVertices_AppliesCenterAndRadius()
    {
        var s = BuildSquare();
        var verts = s.GetScaledVertices(new Vector2(50, 50), 10f);
        AssertThat(verts.Length).IsEqual(4);
        AssertThat(Mathf.IsEqualApprox(verts[0].X, 50 + 7.07f, 0.01f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(verts[0].Y, 50 - 7.07f, 0.01f)).IsTrue();
    }
}
