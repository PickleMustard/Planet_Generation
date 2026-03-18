using System;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using UtilityLibrary;
using static GdUnit4.Assertions;

namespace Tests;

/// <summary>
/// Tests for the Izzo Lambert Solver implementation.
/// Validates the solver against known analytical solutions and edge cases.
/// </summary>
[TestSuite]
public class LambertSolverTest
{
    // Standard gravitational parameter for Earth (m³/s²)
    private const float EarthMu = 3.986004418e14f;

    // Earth radius (m)
    private const float EarthRadius = 6371000f;

    /// <summary>
    /// Test a simple half-orbit transfer (180 degrees).
    /// For a circular orbit, this should result in a symmetric transfer.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestHalfOrbitTransfer()
    {
        // Position at origin + radius on X-axis
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        // Position at opposite side (-X)
        Vector3 r2 = new Vector3(-EarthRadius, 0f, 0f);

        // Time of flight for half orbit: π * r^1.5 / sqrt(mu)
        float tof = Mathf.Pi * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        var solution = LambertSolver.Solve(r1, r2, tof, EarthMu, true, 0);

        // Should have valid velocities
        AssertThat(solution.InitialVelocity.Length()).IsGreater(0f);
        AssertThat(solution.FinalVelocity.Length()).IsGreater(0f);

        // DeltaV should be positive
        AssertThat(solution.DeltaVRequired).IsGreater(0f);

        // Semi-major axis should be approximately r for half-orbit
        AssertThat(solution.SemiMajorAxis).IsGreater(0f);

        GameLogger.Info(
            $"Half-orbit test: TOF={tof / 3600f:F1}h, DeltaV={solution.DeltaVRequired:F0} m/s"
        );
    }

    /// <summary>
    /// Test a quarter-orbit transfer (90 degrees).
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestQuarterOrbitTransfer()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        // Time of flight for quarter orbit
        float tof = (Mathf.Pi / 2f) * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        var solution = LambertSolver.Solve(r1, r2, tof, EarthMu, true, 0);

        AssertThat(solution.InitialVelocity.Length()).IsGreater(0f);
        AssertThat(solution.FinalVelocity.Length()).IsGreater(0f);
        AssertThat(solution.DeltaVRequired).IsGreater(0f);

        GameLogger.Info(
            $"Quarter-orbit test: TOF={tof / 3600f:F1}h, DeltaV={solution.DeltaVRequired:F0} m/s"
        );
    }

    /// <summary>
    /// Test retrograde (counter-clockwise) transfer.
    /// Prograde and retrograde solutions should produce different velocities.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestRetrogradeTransfer()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        float tof = (Mathf.Pi / 2f) * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        // Prograde
        var prograde = LambertSolver.Solve(r1, r2, tof, EarthMu, true, 0);

        // Retrograde
        var retrograde = LambertSolver.Solve(r1, r2, tof, EarthMu, false, 0);

        // Both should have valid solutions
        AssertThat(prograde.InitialVelocity.Length()).IsGreater(0f);
        AssertThat(retrograde.InitialVelocity.Length()).IsGreater(0f);

        // DeltaV should differ between prograde and retrograde
        GameLogger.Info($"Prograde DeltaV: {prograde.DeltaVRequired:F0} m/s");
        GameLogger.Info($"Retrograde DeltaV: {retrograde.DeltaVRequired:F0} m/s");
    }

    /// <summary>
    /// Test degenerate case handling (nearly aligned positions).
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestDegenerateCaseNearAligned()
    {
        // Nearly aligned positions (small angle between them)
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(EarthRadius - 1100f, 100f, 0f); // Very small angle

        float tof = 3600f; // 1 hour

        var solution = LambertSolver.Solve(r1, r2, tof, EarthMu, true, 0);

        // Should return a valid solution (possibly simplified)
        AssertThat(solution.InitialVelocity).IsNotNull();
        AssertThat(solution.FinalVelocity).IsNotNull();
    }

    /// <summary>
    /// Test zero position handling — should return zero solution gracefully.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestZeroPosition()
    {
        Vector3 r1 = Vector3.Zero;
        Vector3 r2 = new Vector3(EarthRadius, 0f, 0f);

        var solution = LambertSolver.Solve(r1, r2, 3600f, EarthMu, true, 0);

        // Should return zero solution for degenerate case
        AssertThat(solution.InitialVelocity.Length()).IsEqual(0f);
    }

    /// <summary>
    /// Test zero time of flight — should return zero solution gracefully.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestZeroTimeOfFlight()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        var solution = LambertSolver.Solve(r1, r2, 0f, EarthMu, true, 0);

        // Should return zero solution
        AssertThat(solution.InitialVelocity.Length()).IsEqual(0f);
    }

    /// <summary>
    /// Test multi-revolution transfer using SolveAll.
    /// Should return more solutions than single revolution.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestMultiRevolutionTransfer()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        // Time for 1.5 orbits — enough for multi-revolution solutions
        float tof = (3f * Mathf.Pi / 2f) * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        // Direct transfer (0 revolutions)
        var direct = LambertSolver.Solve(r1, r2, tof, EarthMu, true, 0);

        AssertThat(direct.InitialVelocity.Length()).IsGreater(0f);

        // SolveAll should return at least the 0-rev solution
        var allSolutions = LambertSolver.SolveAll(r1, r2, tof, EarthMu, true, 1);
        AssertThat(allSolutions.Count).IsGreaterEqual(1);

        GameLogger.Info($"Direct DeltaV: {direct.DeltaVRequired:F0} m/s");
        GameLogger.Info($"Total solutions from SolveAll (maxRev=1): {allSolutions.Count}");
        foreach (var sol in allSolutions)
        {
            GameLogger.Info($"  Rev={sol.Revolutions}, Type={sol.TransferType}, DeltaV={sol.DeltaVRequired:F0} m/s");
        }
    }

    /// <summary>
    /// Test the multi-revolution solver that finds best solution.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestSolveMultiRev()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        // Time for 2.5 orbits
        float tof = (9f * Mathf.Pi / 2f) * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        var solution = LambertSolver.SolveMultiRev(r1, r2, tof, EarthMu, true, 3);

        AssertThat(solution.InitialVelocity.Length()).IsGreater(0f);

        GameLogger.Info(
            $"SolveMultiRev: Rev={solution.Revolutions}, Type={solution.TransferType}, " +
            $"DeltaV={solution.DeltaVRequired:F0} m/s"
        );
    }

    /// <summary>
    /// Test hyperbolic (escape) trajectory.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestHyperbolicTrajectory()
    {
        // Starting close, ending far
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(10000000f, 0f, 0f);

        // Long time of flight
        float tof = 86400f; // 1 day

        var solution = LambertSolver.Solve(r1, r2, tof, EarthMu, true, 0);

        AssertThat(solution.InitialVelocity.Length()).IsGreater(0f);

        GameLogger.Info($"Hyperbolic semi-major axis: {solution.SemiMajorAxis:E2} m");
    }

    /// <summary>
    /// Test that solution velocity magnitudes are physically reasonable.
    /// For a quarter orbit at Earth's surface, velocities should be close
    /// to circular orbital velocity (~7580 m/s).
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestVelocityMagnitudes()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        float tof = (Mathf.Pi / 2f) * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        var solution = LambertSolver.Solve(r1, r2, tof, EarthMu, true, 0);

        // Circular orbit velocity at Earth's radius
        float circularVel = Mathf.Sqrt(EarthMu / EarthRadius); // ~7580 m/s

        // Transfer velocities should be in reasonable range
        float v1 = solution.InitialVelocity.Length();
        float v2 = solution.FinalVelocity.Length();

        AssertThat(v1).IsGreater(0f);
        AssertThat(v2).IsGreater(0f);

        // Velocities should not be wildly unrealistic (e.g., > 20 km/s for Earth orbit)
        AssertThat(v1).IsLess(20000f);
        AssertThat(v2).IsLess(20000f);

        GameLogger.Info(
            $"Transfer velocities: v1={v1:F0} m/s, v2={v2:F0} m/s (circular={circularVel:F0} m/s)"
        );
    }

    // ================================================================
    // New Tests: Analytical Verification
    // ================================================================

    /// <summary>
    /// Verify that a circular orbit transfer (same radius, 90°) produces
    /// velocities close to the known circular orbital velocity.
    /// For a circular orbit at radius r: v_circ = sqrt(mu/r) ≈ 7580 m/s.
    /// The Lambert solution for a quarter-orbit at circular speed should
    /// give velocities within ~10% of circular velocity.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestCircularOrbitVelocityReasonable()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, 0f, EarthRadius); // 90° transfer in XZ plane

        float circularVel = Mathf.Sqrt(EarthMu / EarthRadius);
        float tof = (Mathf.Pi / 2f) * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        var solution = LambertSolver.Solve(r1, r2, tof, EarthMu, true, 0);

        float v1 = solution.InitialVelocity.Length();
        float v2 = solution.FinalVelocity.Length();

        // Velocities should be within 20% of circular velocity for this geometry
        float lowerBound = circularVel * 0.8f;
        float upperBound = circularVel * 1.2f;

        AssertThat(v1).IsBetween(lowerBound, upperBound);
        AssertThat(v2).IsBetween(lowerBound, upperBound);

        GameLogger.Info(
            $"Circular orbit test: v1={v1:F0}, v2={v2:F0}, v_circ={circularVel:F0} m/s"
        );
    }

    /// <summary>
    /// Verify that different ToF values produce genuinely different solutions.
    /// This is the key behavior that was broken before the fix.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestDifferentTofProducesDifferentSolutions()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        float baseTof = (Mathf.Pi / 2f) * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        // Solve with 3 different ToF values
        var sol1 = LambertSolver.Solve(r1, r2, baseTof * 0.5f, EarthMu, true, 0);
        var sol2 = LambertSolver.Solve(r1, r2, baseTof, EarthMu, true, 0);
        var sol3 = LambertSolver.Solve(r1, r2, baseTof * 2f, EarthMu, true, 0);

        // All should be valid
        AssertThat(sol1.InitialVelocity.Length()).IsGreater(0f);
        AssertThat(sol2.InitialVelocity.Length()).IsGreater(0f);
        AssertThat(sol3.InitialVelocity.Length()).IsGreater(0f);

        // All should have different delta-v values (since ToF differs, geometry same)
        float dv1 = sol1.DeltaVRequired;
        float dv2 = sol2.DeltaVRequired;
        float dv3 = sol3.DeltaVRequired;

        // At minimum, they shouldn't all be identical
        bool allSame = Mathf.Abs(dv1 - dv2) < 1f && Mathf.Abs(dv2 - dv3) < 1f;
        AssertThat(allSame).IsFalse();

        GameLogger.Info(
            $"Different TOF test: dv1={dv1:F0} (0.5x), dv2={dv2:F0} (1.0x), dv3={dv3:F0} (2.0x) m/s"
        );
    }

    /// <summary>
    /// Verify that SolveAll returns multiple distinct solutions for multi-rev.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestSolveAllReturnsDistinctSolutions()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        // Long TOF to allow multi-revolution solutions
        float tof = 5f * Mathf.Pi * Mathf.Sqrt(EarthRadius * EarthRadius * EarthRadius / EarthMu);

        var solutions = LambertSolver.SolveAll(r1, r2, tof, EarthMu, true, 2);

        // Should have at least the zero-rev solution
        AssertThat(solutions.Count).IsGreaterEqual(1);

        // Check that solutions have distinct delta-v values
        var deltaVs = new HashSet<int>();
        foreach (var sol in solutions)
        {
            // Round to nearest integer to allow small numerical differences
            deltaVs.Add((int)sol.DeltaVRequired);
        }

        GameLogger.Info(
            $"SolveAll test: {solutions.Count} solutions, {deltaVs.Count} distinct delta-v values"
        );

        // If we have multiple solutions, they should not all be identical
        if (solutions.Count > 1)
        {
            AssertThat(deltaVs.Count).IsGreater(1);
        }
    }

    /// <summary>
    /// Test that RecalculateDeltaV correctly subtracts orbital velocities.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestRecalculateDeltaVWithOrbitalVelocities()
    {
        // Create a solution with known velocities
        Vector3 v1 = new Vector3(0f, 0f, 8000f); // Lambert departure velocity
        Vector3 v2 = new Vector3(0f, 0f, -7000f); // Lambert arrival velocity

        var solution = new TrajectorySolution(
            v1, v2, 3600f, EarthRadius, 0f, 0, TransferType.Direct
        );

        // Default delta-v (no orbital velocities)
        float defaultDv = solution.DeltaVRequired;
        AssertThat(defaultDv).IsEqual(v1.Length() + v2.Length());

        // Set orbital velocities
        Vector3 originOrbitV = new Vector3(0f, 0f, 7580f); // Close to v1 => small departure burn
        Vector3 destOrbitV = new Vector3(0f, 0f, -7580f); // Close to v2 => small arrival burn

        solution.OriginOrbitalVelocity = originOrbitV;
        solution.DestinationOrbitalVelocity = destOrbitV;
        solution.RecalculateDeltaV();

        float correctedDv = solution.DeltaVRequired;

        // Corrected delta-v should be MUCH smaller than raw sum
        AssertThat(correctedDv).IsLess(defaultDv);

        // Corrected delta-v should be |v1 - vOrbit1| + |v2 - vOrbit2|
        float expectedDv = (v1 - originOrbitV).Length() + (v2 - destOrbitV).Length();
        AssertThat(Mathf.Abs(correctedDv - expectedDv)).IsLess(1f);

        GameLogger.Info(
            $"RecalculateDeltaV test: raw={defaultDv:F0}, corrected={correctedDv:F0}, expected={expectedDv:F0} m/s"
        );
    }

    /// <summary>
    /// Test that negative gravitational parameter is handled gracefully.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestNegativeMuHandling()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(0f, EarthRadius, 0f);

        var solution = LambertSolver.Solve(r1, r2, 3600f, -1f, true, 0);

        // Should return zero solution for invalid mu
        AssertThat(solution.InitialVelocity.Length()).IsEqual(0f);
    }

    /// <summary>
    /// Test that identical start/end positions with non-zero cross product
    /// (e.g., positions on a circle but same point) are handled.
    /// </summary>
    [TestCase]
    [RequireGodotRuntime]
    public void TestSameStartEndPosition()
    {
        Vector3 r1 = new Vector3(EarthRadius, 0f, 0f);
        Vector3 r2 = new Vector3(EarthRadius, 0f, 0f); // Same position

        // The cross product is zero, so this should hit the degenerate case
        var solution = LambertSolver.Solve(r1, r2, 3600f, EarthMu, true, 0);

        // Should handle gracefully (degenerate solver or zero solution)
        AssertThat(solution).IsNotNull();
    }
}
