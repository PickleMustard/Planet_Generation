using GdUnit4;
using Godot;
using UI.SystemBoard;
using static GdUnit4.Assertions;

namespace Tests.UI.SystemBoard;

/// <summary>
/// Pure geometry tests for <see cref="SystemBoardShapes"/> — octagon/triangle
/// vertex generation, XZ projection, velocity-to-heading, point-in-polygon hit
/// testing, and the bulged-Bézier route arc. No Godot runtime required.
/// </summary>
[TestSuite]
public class SystemBoardShapesTest
{
    private const float Eps = 1e-3f;

    // ── RegularPolygon ──────────────────────────────────────────────

    [TestCase]
    public void RegularPolygon_Octagon_HasEightVerticesAtRadius()
    {
        var c = new Vector2(10f, -5f);
        const float r = 20f;
        var verts = SystemBoardShapes.RegularPolygon(c, r, 8);

        AssertThat(verts.Length).IsEqual(8);
        foreach (var v in verts)
            AssertThat(v.DistanceTo(c)).IsEqualApprox(r, Eps);
    }

    [TestCase]
    public void RegularPolygon_VerticesEvenlySpaced()
    {
        var c = Vector2.Zero;
        var verts = SystemBoardShapes.RegularPolygon(c, 15f, 8);
        float expectedStep = Mathf.Tau / 8f;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector2 a = verts[i];
            Vector2 b = verts[(i + 1) % verts.Length];
            float delta = Mathf.Abs(Mathf.AngleDifference(a.Angle(), b.Angle()));
            AssertThat(delta).IsEqualApprox(expectedStep, 1e-2f);
        }
    }

    [TestCase]
    public void RegularPolygon_FewerThanThreeSides_ClampsToTriangle()
    {
        var verts = SystemBoardShapes.RegularPolygon(Vector2.Zero, 10f, 1);
        AssertThat(verts.Length).IsEqual(3);
    }

    // ── Triangle ────────────────────────────────────────────────────

    [TestCase]
    public void Triangle_ApexPointsAlongHeading()
    {
        var c = new Vector2(3f, 7f);
        const float r = 12f;
        float heading = Mathf.Pi / 4f;
        var tri = SystemBoardShapes.Triangle(c, r, heading);

        Vector2 expectedApex = c + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * r;
        AssertThat(tri.Length).IsEqual(3);
        AssertThat(tri[0].DistanceTo(expectedApex)).IsLess(Eps);
    }

    [TestCase]
    public void Triangle_AllVerticesOnCircumcircle()
    {
        var c = Vector2.Zero;
        const float r = 9f;
        var tri = SystemBoardShapes.Triangle(c, r, 0f);
        foreach (var v in tri)
            AssertThat(v.Length()).IsEqualApprox(r, Eps);
    }

    // ── Projection / heading ────────────────────────────────────────

    [TestCase]
    public void ProjectXZ_DropsY()
    {
        var p = new Vector3(4f, 99f, -6f);
        AssertThat(SystemBoardShapes.ProjectXZ(p)).IsEqual(new Vector2(4f, -6f));
    }

    [TestCase]
    public void HeadingFromVelocityXZ_MatchesAtan2()
    {
        var vel = new Vector3(1f, 50f, 1f);
        float expected = Mathf.Atan2(1f, 1f);
        AssertThat(SystemBoardShapes.HeadingFromVelocityXZ(vel)).IsEqualApprox(expected, Eps);
    }

    [TestCase]
    public void HeadingFromVelocityXZ_ZeroVelocity_ReturnsZeroFallback()
    {
        var vel = new Vector3(0f, 12f, 0f);
        AssertThat(SystemBoardShapes.HeadingFromVelocityXZ(vel)).IsEqual(0f);
    }

    // ── PointInPolygon ──────────────────────────────────────────────

    [TestCase]
    public void PointInPolygon_CenterInsideOctagon()
    {
        var c = new Vector2(5f, 5f);
        var oct = SystemBoardShapes.RegularPolygon(c, 20f, 8);
        AssertThat(SystemBoardShapes.PointInPolygon(c, oct)).IsTrue();
    }

    [TestCase]
    public void PointInPolygon_FarPointOutside()
    {
        var oct = SystemBoardShapes.RegularPolygon(Vector2.Zero, 20f, 8);
        AssertThat(SystemBoardShapes.PointInPolygon(new Vector2(1000f, 1000f), oct)).IsFalse();
    }

    [TestCase]
    public void PointInPolygon_InsideTriangleApexRegion()
    {
        var tri = SystemBoardShapes.Triangle(Vector2.Zero, 30f, 0f);
        // A point just inside the apex direction but near the centroid.
        AssertThat(SystemBoardShapes.PointInPolygon(new Vector2(2f, 0f), tri)).IsTrue();
        AssertThat(SystemBoardShapes.PointInPolygon(new Vector2(50f, 0f), tri)).IsFalse();
    }

    // ── BulgedBezier ────────────────────────────────────────────────

    [TestCase]
    public void BulgedBezier_EndpointsMatch()
    {
        var a = new Vector2(0f, 0f);
        var b = new Vector2(100f, 0f);
        var pts = SystemBoardShapes.BulgedBezier(a, b, 30f, 16);

        AssertThat(pts.Length).IsEqual(16);
        AssertThat(pts[0].DistanceTo(a)).IsLess(Eps);
        AssertThat(pts[pts.Length - 1].DistanceTo(b)).IsLess(Eps);
    }

    [TestCase]
    public void BulgedBezier_MidpointBowsPerpendicular()
    {
        var a = new Vector2(0f, 0f);
        var b = new Vector2(100f, 0f);
        var pts = SystemBoardShapes.BulgedBezier(a, b, 40f, 17);

        // Middle sample should be displaced off the chord (non-zero Y).
        Vector2 mid = pts[pts.Length / 2];
        AssertThat(Mathf.Abs(mid.Y)).IsGreater(1f);
    }
}
