using GdUnit4;
using Godot;
using Structures.Resources;
using UI.PlanetBoard;
using static GdUnit4.Assertions;

namespace Tests.UI.PlanetBoard;

[TestSuite]
public class BuildingShapeGeometryTest
{
    [TestCase]
    public void NormalizeShape_AcceptsKnownAndFallsBackOnUnknown()
    {
        AssertThat(BuildingShapeGeometry.NormalizeShape("hexagon")).IsEqual("hexagon");
        AssertThat(BuildingShapeGeometry.NormalizeShape("HEXAGON")).IsEqual("hexagon");
        AssertThat(BuildingShapeGeometry.NormalizeShape("  pentagon  ")).IsEqual("pentagon");
        AssertThat(BuildingShapeGeometry.NormalizeShape("octagon")).IsEqual("hexagon");
        AssertThat(BuildingShapeGeometry.NormalizeShape(null)).IsEqual("hexagon");
        AssertThat(BuildingShapeGeometry.NormalizeShape("")).IsEqual("hexagon");
    }

    [TestCase]
    public void GetSideCount_MatchesExpectedPolygonOrder()
    {
        AssertThat(BuildingShapeGeometry.GetSideCount("triangle")).IsEqual(3);
        AssertThat(BuildingShapeGeometry.GetSideCount("square")).IsEqual(4);
        AssertThat(BuildingShapeGeometry.GetSideCount("rectangle")).IsEqual(4);
        AssertThat(BuildingShapeGeometry.GetSideCount("pentagon")).IsEqual(5);
        AssertThat(BuildingShapeGeometry.GetSideCount("hexagon")).IsEqual(6);
        // Unknown shapes fall back to hexagon's 6.
        AssertThat(BuildingShapeGeometry.GetSideCount("nonagon")).IsEqual(6);
    }

    [TestCase]
    public void GetVertices_HexagonHasSixVerticesAndTopMatches()
    {
        var verts = BuildingShapeGeometry.GetVertices("hexagon", Vector2.Zero, 100f);
        AssertThat(verts.Length).IsEqual(6);
        // Vertex 0 at -π/2 (top). Y is screen-down so top has y < 0.
        AssertThat(Mathf.IsEqualApprox(verts[0].X, 0f, 1e-3f)).IsTrue();
        AssertThat(Mathf.IsEqualApprox(verts[0].Y, -100f, 1e-3f)).IsTrue();
    }

    [TestCase]
    public void GetEdgeIndex_HexagonTopMapsToEdgeZero()
    {
        AssertThat(BuildingShapeGeometry.GetEdgeIndex("hexagon", BuildingSide.Top)).IsEqual(0);
        AssertThat(BuildingShapeGeometry.GetEdgeIndex("hexagon", BuildingSide.East)).IsEqual(1);
        AssertThat(BuildingShapeGeometry.GetEdgeIndex("hexagon", BuildingSide.South)).IsEqual(2);
        AssertThat(BuildingShapeGeometry.GetEdgeIndex("hexagon", BuildingSide.Bottom)).IsEqual(3);
        AssertThat(BuildingShapeGeometry.GetEdgeIndex("hexagon", BuildingSide.West)).IsEqual(4);
        AssertThat(BuildingShapeGeometry.GetEdgeIndex("hexagon", BuildingSide.North)).IsEqual(5);
    }

    [TestCase]
    public void GetEdgeIndex_SquareNorthFallsBackToTop()
    {
        // North + South aren't dedicated on a square — they fall back to Top + Bottom.
        AssertThat(BuildingShapeGeometry.GetEdgeIndex("square", BuildingSide.North))
            .IsEqual(BuildingShapeGeometry.GetEdgeIndex("square", BuildingSide.Top));
        AssertThat(BuildingShapeGeometry.GetEdgeIndex("square", BuildingSide.South))
            .IsEqual(BuildingShapeGeometry.GetEdgeIndex("square", BuildingSide.Bottom));
    }

    [TestCase]
    public void GetPortAnchor_HexagonTopIsAtTopEdgeMidpoint()
    {
        var anchor = BuildingShapeGeometry.GetPortAnchor("hexagon", BuildingSide.Top, Vector2.Zero, 100f);
        // Top edge is from vertex 0 (top) to vertex 1 (upper-right). Midpoint y is between
        // -100 and -50, so it should sit above center.
        AssertThat(anchor.Y).IsLess(0f);
        AssertThat(anchor.Y).IsGreater(-100f);
    }

    [TestCase]
    public void GetPortNormal_PointsAwayFromCenter()
    {
        var normal = BuildingShapeGeometry.GetPortNormal("hexagon", BuildingSide.East, Vector2.Zero, 100f);
        AssertThat(normal.X).IsGreater(0f);
        AssertThat(Mathf.IsEqualApprox(normal.Length(), 1f, 1e-3f)).IsTrue();
    }
}
