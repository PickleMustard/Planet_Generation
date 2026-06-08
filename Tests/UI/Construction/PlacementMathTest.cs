using GdUnit4;
using Godot;
using UI.Construction;
using static GdUnit4.Assertions;

namespace Tests.UI.Construction;

/// <summary>
/// Pure-math tests for <see cref="PlacementMath"/> (cursor→plane projection and
/// band snapping). No scene tree required.
/// </summary>
[TestSuite]
public class PlacementMathTest
{
    private const float Eps = 1e-3f;

    [TestCase]
    public void ProjectToPlaneY_StraightDown_HitsExpectedPoint()
    {
        // Ray from (5, 10, -3) pointing straight down onto y=0.
        bool ok = PlacementMath.ProjectToPlaneY(
            new Vector3(5f, 10f, -3f), Vector3.Down, 0f, out Vector3 hit);

        AssertThat(ok).IsTrue();
        AssertThat(hit.X).IsEqualApprox(5f, Eps);
        AssertThat(hit.Y).IsEqualApprox(0f, Eps);
        AssertThat(hit.Z).IsEqualApprox(-3f, Eps);
    }

    [TestCase]
    public void ProjectToPlaneY_Parallel_ReturnsFalse()
    {
        bool ok = PlacementMath.ProjectToPlaneY(
            new Vector3(0f, 10f, 0f), Vector3.Right, 0f, out _);
        AssertThat(ok).IsFalse();
    }

    [TestCase]
    public void ProjectToPlaneY_PlaneBehindRay_ReturnsFalse()
    {
        // Pointing up but plane is below ⇒ intersection is behind the origin.
        bool ok = PlacementMath.ProjectToPlaneY(
            new Vector3(0f, 10f, 0f), Vector3.Up, 0f, out _);
        AssertThat(ok).IsFalse();
    }

    [TestCase]
    public void NearestBand_PicksClosestRadius()
    {
        var bands = new[] { 10f, 20f, 35f };
        int idx = PlacementMath.NearestBand(22f, bands, out float snapped);
        AssertThat(idx).IsEqual(1);
        AssertThat(snapped).IsEqualApprox(20f, Eps);
    }

    [TestCase]
    public void NearestBand_Empty_ReturnsMinusOne()
    {
        int idx = PlacementMath.NearestBand(15f, System.Array.Empty<float>(), out float snapped);
        AssertThat(idx).IsEqual(-1);
        AssertThat(snapped).IsEqualApprox(15f, Eps);
    }
}
