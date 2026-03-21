using System;
using System.Collections.Generic;
using Constructables.ArtificialSatellites;
using GdUnit4;
using Godot;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using static GdUnit4.Assertions;

namespace Tests.Logistics;

/// <summary>
/// Tests for the orbit-band-aware Lambert solver integration.
/// Validates that TrajectoryPlanner uses ship actual position/velocity
/// and targets destination orbit bands correctly.
/// </summary>
[TestSuite]
public class TrajectoryPlannerOrbitBandTest
{
    private const float Tolerance = 0.01f;

    // ========================================================================
    // CelestialBody orbit band utility tests
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void GetOrbitBandRadius_ValidBand_ReturnsCorrectRadius()
    {
        // Arrange
        var body = CreateTestCelestialBody(mass: 10000f, radius: 10f);
        body.InitializeOrbitSystem();

        // Act
        float bandRadius = body.GetOrbitBandRadius(0);

        // Assert — band 0 multiplier is 1.5, so radius = 10 * 1.5 = 15
        AssertThat(bandRadius).IsGreater(0f);
        AssertThat(bandRadius).IsEqualApprox(15f, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetOrbitBandRadius_InvalidBand_ReturnsNegative()
    {
        var body = CreateTestCelestialBody(mass: 10000f, radius: 10f);
        body.InitializeOrbitSystem();

        float result = body.GetOrbitBandRadius(99);

        AssertThat(result).IsEqual(-1f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetOrbitalSpeedForBand_InnerBandFasterThanOuter()
    {
        var body = CreateTestCelestialBody(mass: 10000f, radius: 10f);
        body.InitializeOrbitSystem();

        // Only test if we have at least 2 bands
        if (body.GetBandCount() < 2) return;

        float speed0 = body.GetOrbitalSpeedForBand(0);
        float speed1 = body.GetOrbitalSpeedForBand(1);

        AssertThat(speed0).IsGreater(0f);
        AssertThat(speed1).IsGreater(0f);
        AssertThat(speed0).IsGreater(speed1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetOrbitalSpeedForBand_InvalidBand_ReturnsNegative()
    {
        var body = CreateTestCelestialBody(mass: 10000f, radius: 10f);
        body.InitializeOrbitSystem();

        float result = body.GetOrbitalSpeedForBand(-1);

        AssertThat(result).IsEqual(-1f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetClosestBandForApproach_ReturnsValidBand()
    {
        var body = CreateTestCelestialBody(mass: 10000f, radius: 10f);
        body.InitializeOrbitSystem();

        int band = body.GetClosestBandForApproach(5.0f);

        AssertThat(band).IsGreaterEqual(0);
        AssertThat(band).IsLess(body.GetBandCount());
    }

    // ========================================================================
    // TrajectorySolution orbit band metadata tests
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void TrajectorySolution_DefaultBandIndices_AreNegativeOne()
    {
        var solution = new TrajectorySolution();

        AssertThat(solution.OriginBandIndex).IsEqual(-1);
        AssertThat(solution.DestinationBandIndex).IsEqual(-1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TrajectorySolution_BandIndices_CanBeSet()
    {
        var solution = new TrajectorySolution
        {
            OriginBandIndex = 1,
            DestinationBandIndex = 2,
        };

        AssertThat(solution.OriginBandIndex).IsEqual(1);
        AssertThat(solution.DestinationBandIndex).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RecalculateDeltaV_WithShipVelocity_DifferentFromPlanetVelocity()
    {
        // Arrange: Lambert solution with ship orbital velocity instead of planet velocity
        Vector3 lambertDepart = new Vector3(0f, 0f, 8000f);
        Vector3 lambertArrive = new Vector3(0f, 0f, -7000f);

        var solution = new TrajectorySolution(
            lambertDepart, lambertArrive, 3600f, 7000000f, 0.1f, 0, TransferType.Direct
        );

        // Ship velocity includes orbital velocity around planet + planet velocity
        Vector3 shipVelocity = new Vector3(100f, 0f, 7580f); // 100 m/s from orbit band
        Vector3 destBandVelocity = new Vector3(-50f, 0f, -7580f); // Destination band velocity

        solution.OriginOrbitalVelocity = shipVelocity;
        solution.DestinationOrbitalVelocity = destBandVelocity;

        // Act
        solution.RecalculateDeltaV();

        // Assert
        float expectedDepart = (lambertDepart - shipVelocity).Length();
        float expectedArrive = (lambertArrive - destBandVelocity).Length();

        AssertThat(solution.DepartureDeltaV).IsEqualApprox(expectedDepart, Tolerance);
        AssertThat(solution.ArrivalDeltaV).IsEqualApprox(expectedArrive, Tolerance);
        AssertThat(solution.DeltaVRequired)
            .IsEqualApprox(expectedDepart + expectedArrive, Tolerance);
    }

    // ========================================================================
    // LogisticsUnit GetOrbitalVelocity integration tests
    // ========================================================================

    [TestCase]
    public void GetOrbitalVelocity_SpeedMatchesRadiusTimesAngularSpeed()
    {
        // Arrange: multiple configurations
        var testCases = new (float radius, float speed)[]
        {
            (1000f, 0.1f),
            (5000f, 0.05f),
            (15f, 0.333f), // Typical orbit band radius for a 10-unit planet
        };

        foreach (var (radius, speed) in testCases)
        {
            var unit = CreateTestUnit(radius, speed, 0f);
            Vector3 velocity = unit.GetOrbitalVelocity();

            float expectedLinearSpeed = radius * speed;
            AssertThat(velocity.Length()).IsEqualApprox(expectedLinearSpeed, Tolerance);
        }
    }

    // ========================================================================
    // Helper methods
    // ========================================================================

    private CelestialBody CreateTestCelestialBody(float mass = 10000f, float radius = 10f)
    {
        var mesh = new UnifiedCelestialMesh { size = (int)radius };
        var bodyDict = new Godot.Collections.Dictionary
        {
            { "type", "RockyPlanet" },
            {
                "template",
                new Godot.Collections.Dictionary
                {
                    { "mass", mass },
                    { "velocity", Vector3.Zero },
                    { "size", (float)(int)radius },
                }
            },
        };

        var body = new CelestialBody(bodyDict, mesh);
        body.Radius = radius;
        return body;
    }

    private LogisticsUnit CreateTestUnit(
        float orbitalRadius = 1000f,
        float orbitalSpeed = 0.1f,
        float orbitalAngle = 0f
    )
    {
        var unit = new LogisticsUnit();

        // Use reflection to set private orbital state fields
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(LogisticsUnit).GetField("_orbitalRadius", flags)?.SetValue(unit, orbitalRadius);
        typeof(LogisticsUnit).GetField("_orbitalSpeed", flags)?.SetValue(unit, orbitalSpeed);
        typeof(LogisticsUnit).GetField("_orbitalAngle", flags)?.SetValue(unit, orbitalAngle);
        typeof(LogisticsUnit).GetField("_isInitialized", flags)?.SetValue(unit, true);

        return unit;
    }
}
