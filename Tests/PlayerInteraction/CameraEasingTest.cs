using GdUnit4;
using PlayerInteraction.Camera;
using static GdUnit4.Assertions;

namespace Tests.PlayerInteraction;

/// <summary>
/// Pure-math tests for <see cref="CameraEasing.SCurve"/>. No scene tree required.
/// </summary>
[TestSuite]
public class CameraEasingTest
{
    private const float Eps = 1e-4f;

    [TestCase]
    public void Endpoints_AreExact()
    {
        AssertThat(CameraEasing.SCurve(0f, 2f, 2f)).IsEqualApprox(0f, Eps);
        AssertThat(CameraEasing.SCurve(1f, 2f, 2f)).IsEqualApprox(1f, Eps);
    }

    [TestCase]
    public void Midpoint_IsHalf_RegardlessOfExponents()
    {
        AssertThat(CameraEasing.SCurve(0.5f, 2f, 4f)).IsEqualApprox(0.5f, Eps);
        AssertThat(CameraEasing.SCurve(0.5f, 1f, 1f)).IsEqualApprox(0.5f, Eps);
    }

    [TestCase]
    public void UnitExponents_AreLinear()
    {
        AssertThat(CameraEasing.SCurve(0.25f, 1f, 1f)).IsEqualApprox(0.25f, Eps);
        AssertThat(CameraEasing.SCurve(0.75f, 1f, 1f)).IsEqualApprox(0.75f, Eps);
    }

    [TestCase]
    public void IsMonotonic_NonDecreasing()
    {
        float prev = -1f;
        for (int i = 0; i <= 20; i++)
        {
            float v = CameraEasing.SCurve(i / 20f, 3f, 2f);
            AssertThat(v >= prev - Eps).IsTrue();
            prev = v;
        }
    }

    [TestCase]
    public void HigherAccelExp_StartsSlower()
    {
        // Larger accel exponent ⇒ less progress early (gentler acceleration).
        float gentle = CameraEasing.SCurve(0.2f, 3f, 2f);
        float steep = CameraEasing.SCurve(0.2f, 1f, 2f);
        AssertThat(gentle < steep).IsTrue();
    }

    [TestCase]
    public void Clamps_OutOfRangeInput()
    {
        AssertThat(CameraEasing.SCurve(-1f, 2f, 2f)).IsEqualApprox(0f, Eps);
        AssertThat(CameraEasing.SCurve(2f, 2f, 2f)).IsEqualApprox(1f, Eps);
    }
}
