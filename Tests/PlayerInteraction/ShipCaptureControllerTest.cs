using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Tests.PlayerInteraction;

/// <summary>
/// Pure-math tests for <see cref="ShipCaptureController"/>'s static geometry helpers.
/// No scene tree required (Godot Vector3/Mathf are managed structs).
/// </summary>
[TestSuite]
public class ShipCaptureControllerTest
{
    private const float Eps = 1e-3f;

    // --- angle of attack / exit detection ---

    [TestCase]
    public void StraightOutVelocity_WantsToLeave()
    {
        Vector3 nOut = Vector3.Up;
        Vector3 v = Vector3.Up * 5f; // straight out
        AssertThat(ShipCaptureController.RadialDot(v, nOut)).IsEqualApprox(1f, Eps);
        AssertThat(ShipCaptureController.WantsToLeave(v, nOut, 35f)).IsTrue();
    }

    [TestCase]
    public void TangentialVelocity_DoesNotLeave()
    {
        Vector3 nOut = Vector3.Up;
        Vector3 v = Vector3.Right * 5f; // tangential
        AssertThat(ShipCaptureController.RadialDot(v, nOut)).IsEqualApprox(0f, Eps);
        AssertThat(ShipCaptureController.WantsToLeave(v, nOut, 35f)).IsFalse();
    }

    [TestCase]
    public void InwardVelocity_DoesNotLeave()
    {
        Vector3 nOut = Vector3.Up;
        Vector3 v = Vector3.Down * 5f; // straight in
        AssertThat(ShipCaptureController.WantsToLeave(v, nOut, 35f)).IsFalse();
    }

    [TestCase]
    public void ZeroVelocity_RadialDotIsZero()
    {
        AssertThat(ShipCaptureController.RadialDot(Vector3.Zero, Vector3.Up)).IsEqual(0f);
    }

    // --- tangential bend ---

    [TestCase]
    public void BendFullStrength_RemovesRadialComponent()
    {
        Vector3 nOut = Vector3.Up;
        Vector3 v = new Vector3(3f, 4f, 0f); // tangential 3, radial 4
        Vector3 bent = ShipCaptureController.BendTangential(v, nOut, 1f); // full bend
        AssertThat(bent.Dot(nOut)).IsEqualApprox(0f, Eps); // radial gone
        AssertThat(bent.X).IsEqualApprox(3f, Eps); // tangential preserved
    }

    [TestCase]
    public void PartialBend_ShrinksRadialKeepsTangential()
    {
        Vector3 nOut = Vector3.Up;
        Vector3 v = new Vector3(3f, 4f, 0f);
        Vector3 bent = ShipCaptureController.BendTangential(v, nOut, 0.5f);
        // radial component halved, tangential unchanged
        AssertThat(bent.Y).IsEqualApprox(2f, Eps);
        AssertThat(bent.X).IsEqualApprox(3f, Eps);
    }

    // --- no-entry deceleration ---

    [TestCase]
    public void DecelOutsideBand_NoChange()
    {
        Vector3 nOut = Vector3.Up;
        Vector3 v = Vector3.Down * 10f;
        // dist well outside noEntry+band
        Vector3 result = ShipCaptureController.ApplyNoEntryDecel(v, nOut, 100f, 10f, 3f, 0.01f, 0.016f);
        AssertThat(result).IsEqual(v);
    }

    [TestCase]
    public void DecelInsideBand_RemovesInwardComponent()
    {
        Vector3 nOut = Vector3.Up;
        Vector3 v = new Vector3(2f, -10f, 0f); // tangential 2, inward 10
        Vector3 result = ShipCaptureController.ApplyNoEntryDecel(v, nOut, 10.5f, 10f, 3f, 0.01f, 0.016f);
        // inward (negative radial) removed; only damped tangential remains, never inward
        AssertThat(result.Dot(nOut)).IsGreaterEqual(-Eps);
    }

    // --- no-entry hard clamp ---

    [TestCase]
    public void Clamp_CandidateInsideShell_PushedToEdge()
    {
        Vector3 shipPos = new Vector3(0f, 11f, 0f);
        Vector3 bodyPos = Vector3.Zero;
        float noEntry = 10f;
        Vector3 finalDelta = new Vector3(0f, -5f, 0f); // would land at y=6, inside shell
        Vector3 vel = new Vector3(0f, -5f, 0f);
        var (delta, v) = ShipCaptureController.ClampNoEntry(shipPos, finalDelta, bodyPos, noEntry, vel);
        Vector3 landed = shipPos + delta;
        AssertThat(landed.DistanceTo(bodyPos)).IsEqualApprox(noEntry, Eps); // sits on shell
        AssertThat(v.Dot(Vector3.Up)).IsGreaterEqual(-Eps); // inward velocity zeroed
    }

    [TestCase]
    public void Clamp_CandidateOutsideShell_Unchanged()
    {
        Vector3 shipPos = new Vector3(0f, 20f, 0f);
        Vector3 bodyPos = Vector3.Zero;
        float noEntry = 10f;
        Vector3 finalDelta = new Vector3(0f, -2f, 0f); // lands at y=18, outside
        Vector3 vel = new Vector3(0f, -2f, 0f);
        var (delta, v) = ShipCaptureController.ClampNoEntry(shipPos, finalDelta, bodyPos, noEntry, vel);
        AssertThat(delta).IsEqual(finalDelta);
        AssertThat(v).IsEqual(vel);
    }
}
