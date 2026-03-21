using GdUnit4;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using static GdUnit4.Assertions;

namespace Tests.Logistics;

/// <summary>
/// Tests for TrajectorySolution's DepartureDeltaV / ArrivalDeltaV split
/// and the updated RecalculateDeltaV() method.
/// </summary>
[TestSuite]
public class TrajectorySolutionDeltaVTest
{
    private const float Tolerance = 0.01f;

    [TestCase]
    [RequireGodotRuntime]
    public void Constructor_SplitsDeltaV_FromVelocityMagnitudes()
    {
        // Arrange
        Vector3 v1 = new Vector3(0f, 0f, 8000f);
        Vector3 v2 = new Vector3(0f, 0f, -7000f);

        // Act
        var solution = new TrajectorySolution(
            v1, v2, 3600f, 7000000f, 0.1f, 0, TransferType.Direct
        );

        // Assert
        AssertThat(solution.DepartureDeltaV).IsEqualApprox(v1.Length(), Tolerance);
        AssertThat(solution.ArrivalDeltaV).IsEqualApprox(v2.Length(), Tolerance);
        AssertThat(solution.DeltaVRequired)
            .IsEqualApprox(solution.DepartureDeltaV + solution.ArrivalDeltaV, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RecalculateDeltaV_PersistsSplit_WithOrbitalVelocities()
    {
        // Arrange
        Vector3 v1 = new Vector3(0f, 0f, 8000f);
        Vector3 v2 = new Vector3(0f, 0f, -7000f);
        Vector3 originOrbV = new Vector3(0f, 0f, 7580f);
        Vector3 destOrbV = new Vector3(0f, 0f, -7580f);

        var solution = new TrajectorySolution(
            v1, v2, 3600f, 7000000f, 0.1f, 0, TransferType.Direct
        );

        // Act
        solution.OriginOrbitalVelocity = originOrbV;
        solution.DestinationOrbitalVelocity = destOrbV;
        solution.RecalculateDeltaV();

        // Assert
        float expectedDeparture = (v1 - originOrbV).Length();
        float expectedArrival = (v2 - destOrbV).Length();

        AssertThat(solution.DepartureDeltaV).IsEqualApprox(expectedDeparture, Tolerance);
        AssertThat(solution.ArrivalDeltaV).IsEqualApprox(expectedArrival, Tolerance);
        AssertThat(solution.DeltaVRequired)
            .IsEqualApprox(expectedDeparture + expectedArrival, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RecalculateDeltaV_Fallback_WhenNoOrbitalVelocities()
    {
        // Arrange — don't set orbital velocities (they remain Vector3.Zero)
        Vector3 v1 = new Vector3(100f, 0f, 0f);
        Vector3 v2 = new Vector3(0f, 80f, 0f);

        var solution = new TrajectorySolution(
            v1, v2, 1000f, 5000f, 0.5f, 0, TransferType.Direct
        );

        // Act
        solution.RecalculateDeltaV();

        // Assert — should use raw velocity magnitudes
        AssertThat(solution.DepartureDeltaV).IsEqualApprox(v1.Length(), Tolerance);
        AssertThat(solution.ArrivalDeltaV).IsEqualApprox(v2.Length(), Tolerance);
        AssertThat(solution.DeltaVRequired)
            .IsEqualApprox(v1.Length() + v2.Length(), Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DeparturePlusArrival_AlwaysEqualsDeltaVRequired()
    {
        // Test with a variety of velocity combinations
        Vector3[] departures = {
            new Vector3(500f, 0f, 0f),
            new Vector3(0f, 3000f, 0f),
            new Vector3(100f, 200f, 300f),
        };
        Vector3[] arrivals = {
            new Vector3(0f, 0f, 400f),
            new Vector3(-2000f, 0f, 0f),
            new Vector3(-150f, -250f, -350f),
        };

        for (int i = 0; i < departures.Length; i++)
        {
            var sol = new TrajectorySolution(
                departures[i], arrivals[i], 1000f, 10000f, 0.3f, 0, TransferType.Direct
            );

            AssertThat(sol.DepartureDeltaV + sol.ArrivalDeltaV)
                .IsEqualApprox(sol.DeltaVRequired, Tolerance);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DefaultConstructor_AllZeros()
    {
        var solution = new TrajectorySolution();

        AssertThat(solution.DepartureDeltaV).IsEqualApprox(0f, Tolerance);
        AssertThat(solution.ArrivalDeltaV).IsEqualApprox(0f, Tolerance);
        AssertThat(solution.DeltaVRequired).IsEqualApprox(0f, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetDescription_IncludesDeltaVSplit()
    {
        Vector3 v1 = new Vector3(0f, 0f, 500f);
        Vector3 v2 = new Vector3(0f, 0f, -300f);

        var solution = new TrajectorySolution(
            v1, v2, 1000f, 5000f, 0.2f, 0, TransferType.Direct
        );

        string desc = solution.GetDescription();
        AssertThat(desc.Contains("depart:")).IsTrue();
        AssertThat(desc.Contains("arrive:")).IsTrue();
    }
}
