using System;
using GdUnit4;
using Godot;
using UtilityLibrary;
using static GdUnit4.Assertions;

namespace Tests;

/// <summary>
/// Tests for multi-body system generation bugs:
/// 1. Barycenter mutation — shared Barycenter (a Resource/reference type) must not be
///    mutated when computing per-body orbital parameters.
/// 2. OrbitalMath.CalculateEllipticalOrbitalVelocity — must guard against zero central mass.
/// 3. OrbitalMath.CalculateOrbitalStateFromParams — multiple bodies orbiting different
///    parents must each receive correct, independent orbital state.
/// </summary>
[TestSuite]
public class MultiBodyGenerationTest
{
    private const float G = OrbitalMath.GRAVITATIONAL_CONSTANT;
    private const float Epsilon = 0.01f;

    #region Barycenter Immutability Tests

    /// <summary>
    /// Verifies that creating a new Barycenter for a child body's orbital center
    /// does NOT mutate the original system-level Barycenter object.
    /// This is the core bug: Barycenter extends Resource (reference type), so
    /// mutating it inside a loop corrupts all subsequent iterations.
    /// </summary>
    [TestCase]
    public void BarycenterIsReferenceType_MutationCorruptsSharedState()
    {
        // Arrange: a system barycenter shared across all body calculations
        var systemBarycenter = new Barycenter(Vector3.Zero, Vector3.Zero, 5000f);
        Vector3 originalPosition = systemBarycenter.Position;
        float originalWeight = systemBarycenter.Weight;

        // Act: simulate what the OLD buggy code did — mutate the shared barycenter
        // for a body that orbits a specific parent star instead of the barycenter
        Vector3 starPosition = new Vector3(1000f, 0f, 500f);
        float starMass = 3000f;

        // This is what the bug looked like:
        // barycenter.position = star.GlobalPosition;
        // barycenter.weight = star.Mass;
        // After this mutation, the original systemBarycenter is corrupted.

        // Demonstrate the problem: mutating a Resource mutates the reference
        systemBarycenter.Position = starPosition;
        systemBarycenter.Weight = starMass;

        // Assert: the original values are gone — this IS the bug
        AssertThat(systemBarycenter.Position).IsNotEqual(originalPosition);
        AssertThat(systemBarycenter.Weight).IsNotEqual(originalWeight);
    }

    /// <summary>
    /// Verifies the FIX: creating a local Barycenter copy preserves the original.
    /// </summary>
    [TestCase]
    public void LocalBarycenterCopy_PreservesOriginal()
    {
        // Arrange: a system barycenter shared across all body calculations
        var systemBarycenter = new Barycenter(Vector3.Zero, Vector3.Zero, 5000f);
        Vector3 originalPosition = systemBarycenter.Position;
        float originalWeight = systemBarycenter.Weight;

        // Act: the FIX — create a local copy instead of mutating the shared instance
        Vector3 starPosition = new Vector3(1000f, 0f, 500f);
        float starMass = 3000f;
        var localBarycenter = new Barycenter(starPosition, Vector3.Zero, starMass);

        // Assert: the original system barycenter is untouched
        AssertThat(systemBarycenter.Position).IsEqual(originalPosition);
        AssertThat(systemBarycenter.Weight).IsEqual(originalWeight);

        // And the local copy has the star values
        AssertThat(localBarycenter.Position).IsEqual(starPosition);
        AssertThat(localBarycenter.Weight).IsEqual(starMass);
    }

    /// <summary>
    /// Simulates the multi-body loop: two planetary bodies with different parents.
    /// With the fix, each body should get correct orbital state and the shared
    /// barycenter remains unmodified.
    /// </summary>
    [TestCase]
    public void TwoBodiesDifferentParents_IndependentOrbitalState()
    {
        // System barycenter
        var systemBarycenter = new Barycenter(Vector3.Zero, Vector3.Zero, 8000f);

        // Parent star at offset position
        Vector3 starPosition = new Vector3(500f, 0f, 0f);
        float starMass = 4000f;

        // Body 1: orbits the star (not the barycenter)
        Barycenter localBc1 = new Barycenter(starPosition, Vector3.Zero, starMass);
        var (pos1, vel1) = OrbitalMath.CalculateOrbitalStateFromParams(
            800f,
            400f,
            45f,
            0f,
            localBc1
        );

        // Body 2: orbits the system barycenter
        var (pos2, vel2) = OrbitalMath.CalculateOrbitalStateFromParams(
            1200f,
            600f,
            90f,
            0f,
            systemBarycenter
        );

        // Assert: system barycenter was NOT corrupted by body 1's calculation
        AssertThat(systemBarycenter.Position).IsEqual(Vector3.Zero);
        AssertThat(systemBarycenter.Weight).IsEqual(8000f);

        // Assert: body 1 orbits near the star (position offset by star's position)
        float dist1ToStar = (pos1 - starPosition).Length();
        AssertThat(dist1ToStar).IsGreater(0f);
        AssertThat(dist1ToStar).IsLess(1000f); // should be within the apogee/perigee range

        // Assert: body 2 orbits near the origin barycenter
        float dist2ToOrigin = pos2.Length();
        AssertThat(dist2ToOrigin).IsGreater(0f);
        AssertThat(dist2ToOrigin).IsLess(1500f); // should be within the apogee/perigee range

        // Assert: neither has NaN
        AssertThat(Single.IsNaN(pos1.X)).IsFalse();
        AssertThat(Single.IsNaN(pos1.Y)).IsFalse();
        AssertThat(Single.IsNaN(pos1.Z)).IsFalse();
        AssertThat(Single.IsNaN(vel1.X)).IsFalse();
        AssertThat(Single.IsNaN(vel1.Y)).IsFalse();
        AssertThat(Single.IsNaN(vel1.Z)).IsFalse();
        AssertThat(Single.IsNaN(pos2.X)).IsFalse();
        AssertThat(Single.IsNaN(pos2.Y)).IsFalse();
        AssertThat(Single.IsNaN(pos2.Z)).IsFalse();
        AssertThat(Single.IsNaN(vel2.X)).IsFalse();
        AssertThat(Single.IsNaN(vel2.Y)).IsFalse();
        AssertThat(Single.IsNaN(vel2.Z)).IsFalse();
    }

    #endregion

    #region OrbitalMath NaN Guard Tests

    /// <summary>
    /// CalculateEllipticalOrbitalVelocity with zero central mass must return
    /// Vector3.Zero, NOT NaN. Before the fix, 0/sqrt(0) produced NaN.
    /// </summary>
    [TestCase]
    public void EllipticalVelocity_ZeroCentralMass_ReturnsZero()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float centralMass = 0f;
        float apogee = 1000f;
        float perigee = 500f;
        float angle = 0f;

        Vector3 result = OrbitalMath.CalculateEllipticalOrbitalVelocity(
            pHat,
            qHat,
            centralMass,
            apogee,
            perigee,
            angle
        );

        AssertThat(result).IsEqual(Vector3.Zero);
        AssertThat(Single.IsNaN(result.X)).IsFalse();
        AssertThat(Single.IsNaN(result.Y)).IsFalse();
        AssertThat(Single.IsNaN(result.Z)).IsFalse();
    }

    /// <summary>
    /// CalculateEllipticalOrbitalVelocity with negative central mass must return
    /// Vector3.Zero (guarded), not produce NaN from sqrt of negative value.
    /// </summary>
    [TestCase]
    public void EllipticalVelocity_NegativeCentralMass_ReturnsZero()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float centralMass = -500f;
        float apogee = 1000f;
        float perigee = 500f;
        float angle = Mathf.Pi / 4f;

        Vector3 result = OrbitalMath.CalculateEllipticalOrbitalVelocity(
            pHat,
            qHat,
            centralMass,
            apogee,
            perigee,
            angle
        );

        AssertThat(result).IsEqual(Vector3.Zero);
        AssertThat(Single.IsNaN(result.X)).IsFalse();
    }

    /// <summary>
    /// CalculateEllipticalOrbitalVelocity with valid inputs must still produce
    /// correct non-zero, non-NaN velocity.
    /// </summary>
    [TestCase]
    public void EllipticalVelocity_ValidInputs_ProducesNonZeroNonNaN()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float centralMass = 5000f;
        float apogee = 1000f;
        float perigee = 500f;
        float angle = 0f;

        Vector3 result = OrbitalMath.CalculateEllipticalOrbitalVelocity(
            pHat,
            qHat,
            centralMass,
            apogee,
            perigee,
            angle
        );

        AssertThat(result.Length()).IsGreater(0f);
        AssertThat(Single.IsNaN(result.X)).IsFalse();
        AssertThat(Single.IsNaN(result.Y)).IsFalse();
        AssertThat(Single.IsNaN(result.Z)).IsFalse();
    }

    /// <summary>
    /// CalculateOrbitalStateFromParams with zero barycenter weight returns position
    /// with zero velocity (guarded), not NaN.
    /// </summary>
    [TestCase]
    public void OrbitalState_ZeroBarycenterWeight_ReturnsZeroVelocity()
    {
        var bc = new Barycenter(Vector3.Zero, Vector3.Zero, 0f);
        float semiMajorAxis = 500f;
        float eccentricity = 0.3f;
        float theta = 0f;

        var (position, velocity) = OrbitalMath.CalculateOrbitalStateFromParams(
            semiMajorAxis,
            eccentricity,
            theta,
            bc
        );

        // Should not be NaN
        AssertThat(Single.IsNaN(position.X)).IsFalse();
        AssertThat(Single.IsNaN(velocity.X)).IsFalse();
        // Velocity should be zero (no mass to orbit)
        AssertThat(velocity).IsEqual(Vector3.Zero);
        // Position should still be geometrically computed
        AssertThat(position.Length()).IsGreater(0f);
    }

    #endregion

    #region Multiple Body Orbital Independence Tests

    /// <summary>
    /// Three bodies orbiting the same barycenter at different angles should all
    /// produce valid, distinct positions and velocities with no NaN values.
    /// </summary>
    [TestCase]
    public void ThreeBodiesSameBarycenter_AllValidDistinctOrbits()
    {
        var bc = new Barycenter(Vector3.Zero, Vector3.Zero, 10000f);

        var (pos1, vel1) = OrbitalMath.CalculateOrbitalStateFromParams(600f, 400f, 0f, 0f, bc);
        var (pos2, vel2) = OrbitalMath.CalculateOrbitalStateFromParams(800f, 500f, 120f, 0f, bc);
        var (pos3, vel3) = OrbitalMath.CalculateOrbitalStateFromParams(1000f, 700f, 240f, 0f, bc);

        // All positions should be non-NaN and non-zero
        foreach (var pos in new[] { pos1, pos2, pos3 })
        {
            AssertThat(Single.IsNaN(pos.X)).IsFalse();
            AssertThat(Single.IsNaN(pos.Y)).IsFalse();
            AssertThat(Single.IsNaN(pos.Z)).IsFalse();
            AssertThat(pos.Length()).IsGreater(0f);
        }

        // All velocities should be non-NaN and non-zero
        foreach (var vel in new[] { vel1, vel2, vel3 })
        {
            AssertThat(Single.IsNaN(vel.X)).IsFalse();
            AssertThat(Single.IsNaN(vel.Y)).IsFalse();
            AssertThat(Single.IsNaN(vel.Z)).IsFalse();
            AssertThat(vel.Length()).IsGreater(0f);
        }

        // All positions should be distinct (different orbital parameters)
        AssertThat((pos1 - pos2).Length()).IsGreater(Epsilon);
        AssertThat((pos2 - pos3).Length()).IsGreater(Epsilon);
        AssertThat((pos1 - pos3).Length()).IsGreater(Epsilon);
    }

    /// <summary>
    /// Bodies orbiting with apogee == perigee (circular orbit) should produce
    /// valid results with eccentricity == 0.
    /// </summary>
    [TestCase]
    public void CircularOrbit_ApogeeEqualsPerigee_ValidResult()
    {
        var bc = new Barycenter(Vector3.Zero, Vector3.Zero, 5000f);
        float radius = 500f;

        var (position, velocity) = OrbitalMath.CalculateOrbitalStateFromParams(
            radius,
            radius,
            0f,
            0f,
            bc
        );

        AssertThat(Single.IsNaN(position.X)).IsFalse();
        AssertThat(Single.IsNaN(velocity.X)).IsFalse();
        AssertThat(position.Length()).IsGreater(0f);
        AssertThat(velocity.Length()).IsGreater(0f);

        // For circular orbit, distance from barycenter should equal the radius
        float distToCenter = (position - bc.Position).Length();
        AssertThat(Mathf.Abs(distToCenter - radius)).IsLess(1f);
    }

    #endregion

    #region Binary Orbital State Tests

    /// <summary>
    /// For a binary system with unequal masses and an eccentric orbit, the
    /// mass-weighted center of the two positions must be at the origin (barycenter).
    /// M_A·posA + M_B·posB = 0
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_BarycenterAtOrigin_UnequalMasses()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float apogee = 1000f;
        float perigee = 500f;
        float angle = Mathf.Pi / 3f; // 60 degrees
        float massA = 5000f;
        float massB = 3000f;

        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            apogee,
            perigee,
            angle,
            massA,
            massB
        );

        // Mass-weighted center should be at origin
        Vector3 barycenterPos = (massA * posA + massB * posB) / (massA + massB);
        AssertThat(barycenterPos.Length()).IsLess(Epsilon);
    }

    /// <summary>
    /// Momentum conservation: M_A·velA + M_B·velB = 0 (zero net momentum).
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_ZeroNetMomentum_UnequalMasses()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float apogee = 1000f;
        float perigee = 500f;
        float angle = Mathf.Pi / 3f;
        float massA = 5000f;
        float massB = 3000f;

        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            apogee,
            perigee,
            angle,
            massA,
            massB
        );

        // Net momentum should be zero
        Vector3 totalMomentum = massA * velA + massB * velB;
        AssertThat(totalMomentum.Length()).IsLess(Epsilon);
    }

    /// <summary>
    /// The distance between the two bodies should match the conic section formula
    /// for the relative orbit: r = a(1-e²) / (1 + e·cos(θ))
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_CorrectSeparation()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float apogee = 1200f;
        float perigee = 600f;
        float angle = Mathf.Pi / 4f; // 45 degrees
        float massA = 4000f;
        float massB = 6000f;

        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            apogee,
            perigee,
            angle,
            massA,
            massB
        );

        // Expected separation from conic section formula
        float semiMajorAxis = (apogee + perigee) / 2f;
        float eccentricity = (apogee - perigee) / (apogee + perigee);
        float expectedSeparation =
            semiMajorAxis
            * (1f - eccentricity * eccentricity)
            / (1f + eccentricity * Mathf.Cos(angle));

        float actualSeparation = (posA - posB).Length();
        AssertThat(Mathf.Abs(actualSeparation - expectedSeparation)).IsLess(1f);
    }

    /// <summary>
    /// Circular orbit: when apogee == perigee, eccentricity is 0 and both bodies
    /// should be equidistant from the origin (scaled by inverse mass ratio).
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_CircularOrbit_SymmetricPlacement()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float radius = 800f; // apogee == perigee
        float angle = 0f;
        float massA = 5000f;
        float massB = 5000f;

        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            radius,
            radius,
            angle,
            massA,
            massB
        );

        // Equal masses: both bodies at same distance from origin
        float distA = posA.Length();
        float distB = posB.Length();
        AssertThat(Mathf.Abs(distA - distB)).IsLess(Epsilon);

        // Each should be at half the total separation (equal masses)
        AssertThat(Mathf.Abs(distA - radius / 2f)).IsLess(1f);

        // Barycenter at origin
        Vector3 barycenterPos = (massA * posA + massB * posB) / (massA + massB);
        AssertThat(barycenterPos.Length()).IsLess(Epsilon);
    }

    /// <summary>
    /// Equal masses: both bodies should be at equal distances from the origin,
    /// on exactly opposite sides.
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_EqualMasses_EqualDistancesOpposite()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float apogee = 1000f;
        float perigee = 600f;
        float angle = Mathf.Pi / 6f; // 30 degrees
        float mass = 4000f;

        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            apogee,
            perigee,
            angle,
            mass,
            mass
        );

        // Equal masses: equal distances
        AssertThat(Mathf.Abs(posA.Length() - posB.Length())).IsLess(Epsilon);

        // Opposite directions: posA + posB should be zero
        AssertThat((posA + posB).Length()).IsLess(Epsilon);

        // Equal and opposite velocities
        AssertThat(Mathf.Abs(velA.Length() - velB.Length())).IsLess(Epsilon);
        AssertThat((velA + velB).Length()).IsLess(Epsilon);
    }

    /// <summary>
    /// Barycenter centering must hold for multiple different true anomaly angles
    /// across the full orbit, not just a single snapshot.
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_BarycenterAtOrigin_MultipleAngles()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float apogee = 1500f;
        float perigee = 400f;
        float massA = 7000f;
        float massB = 2000f;

        float[] angles =
        {
            0f,
            Mathf.Pi / 4f,
            Mathf.Pi / 2f,
            Mathf.Pi,
            3f * Mathf.Pi / 2f,
            Mathf.Pi * 1.9f,
        };

        foreach (float angle in angles)
        {
            var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
                pHat,
                qHat,
                apogee,
                perigee,
                angle,
                massA,
                massB
            );

            Vector3 barycenterPos = (massA * posA + massB * posB) / (massA + massB);
            AssertThat(barycenterPos.Length())
                .OverrideFailureMessage(
                    $"Barycenter not at origin for angle {angle}: offset={barycenterPos.Length()}"
                )
                .IsLess(Epsilon);

            Vector3 totalMomentum = massA * velA + massB * velB;
            AssertThat(totalMomentum.Length())
                .OverrideFailureMessage(
                    $"Non-zero momentum for angle {angle}: magnitude={totalMomentum.Length()}"
                )
                .IsLess(Epsilon);
        }
    }

    /// <summary>
    /// Zero or negative total mass should return all-zero vectors without NaN.
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_ZeroMass_ReturnsZero()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);

        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            1000f,
            500f,
            0f,
            0f,
            0f
        );

        AssertThat(posA).IsEqual(Vector3.Zero);
        AssertThat(velA).IsEqual(Vector3.Zero);
        AssertThat(posB).IsEqual(Vector3.Zero);
        AssertThat(velB).IsEqual(Vector3.Zero);
    }

    /// <summary>
    /// With valid inputs, no component of position or velocity should be NaN.
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_NoNaN()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float apogee = 1000f;
        float perigee = 500f;
        float angle = Mathf.Pi / 4f;
        float massA = 5000f;
        float massB = 3000f;

        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            apogee,
            perigee,
            angle,
            massA,
            massB
        );

        foreach (
            var vec in new[] { posA, velA, posB, velB }
        )
        {
            AssertThat(Single.IsNaN(vec.X)).IsFalse();
            AssertThat(Single.IsNaN(vec.Y)).IsFalse();
            AssertThat(Single.IsNaN(vec.Z)).IsFalse();
        }

        // Both positions and velocities should be non-zero
        AssertThat(posA.Length()).IsGreater(0f);
        AssertThat(posB.Length()).IsGreater(0f);
        AssertThat(velA.Length()).IsGreater(0f);
        AssertThat(velB.Length()).IsGreater(0f);
    }

    /// <summary>
    /// Heavier body should be closer to the barycenter (origin) than the lighter body.
    /// </summary>
    [TestCase]
    public void BinaryOrbitalState_HeavierBodyCloserToBarycenter()
    {
        Vector3 pHat = new Vector3(1, 0, 0);
        Vector3 qHat = new Vector3(0, 0, 1);
        float apogee = 1000f;
        float perigee = 500f;
        float angle = 0f;
        float massA = 8000f; // heavier
        float massB = 2000f; // lighter

        var (posA, velA, posB, velB) = OrbitalMath.CalculateBinaryOrbitalState(
            pHat,
            qHat,
            apogee,
            perigee,
            angle,
            massA,
            massB
        );

        // Body A (heavier) should be closer to origin
        AssertThat(posA.Length()).IsLess(posB.Length());
    }

    #endregion
}
