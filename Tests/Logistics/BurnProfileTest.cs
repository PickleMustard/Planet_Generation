using GdUnit4;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using static GdUnit4.Assertions;

using EngineDef = Structures.Logistics.EngineDefinition;

namespace Tests.Logistics;

/// <summary>
/// Tests for BurnProfile calculation, phase timing, fuel rates, and edge cases.
/// Requires Godot runtime because BurnProfile uses Godot.Mathf and GameLogger.
/// </summary>
[TestSuite]
public class BurnProfileTest
{
    // Test engine: Isp 300s, Thrust 1000N
    // ExhaustVelocity = 300 * 0.67394967 ≈ 202.18
    private const float TestIsp = 300f;
    private const float TestThrust = 1000f;
    private const float TestExhaustVelocity = TestIsp * 0.67394967f; // ≈ 202.18
    private const float TestTotalMass = 1000f; // 1000 kg
    private const float Tolerance = 0.01f;

    /// <summary>
    /// Helper to create a TrajectorySolution with the specified departure/arrival ΔV.
    /// </summary>
    private static TrajectorySolution CreateTrajectory(
        float departureDv,
        float arrivalDv,
        float tof
    )
    {
        var trajectory = new TrajectorySolution
        {
            DepartureDeltaV = departureDv,
            ArrivalDeltaV = arrivalDv,
            DeltaVRequired = departureDv + arrivalDv,
            TimeOfFlight = tof,
            TransferType = TransferType.Direct,
        };
        return trajectory;
    }

    /// <summary>
    /// Helper to create a default test engine.
    /// </summary>
    private static EngineDef CreateEngine()
    {
        return new EngineDef(TestIsp, TestThrust);
    }

    // ========================================================================
    // PHASE DURATION TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void CalculatesCorrectPhaseDurations()
    {
        // Arrange: departureDV = 50, arrivalDV = 30, TOF = 500s
        // acceleration = 1000 / 1000 = 1.0 m/s²
        // accelTime = 50 / 1.0 = 50s, decelTime = 30 / 1.0 = 30s
        // coastTime = 500 - 50 - 30 = 420s
        var trajectory = CreateTrajectory(50f, 30f, 500f);
        var engine = CreateEngine();

        // Act
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass);

        // Assert
        AssertThat(profile).IsNotNull();
        AssertThat(profile!.AccelBurnDuration).IsEqualApprox(50f, Tolerance);
        AssertThat(profile.DecelBurnDuration).IsEqualApprox(30f, Tolerance);
        AssertThat(profile.CoastDuration).IsEqualApprox(420f, Tolerance);
        AssertThat(profile.TotalDuration).IsEqualApprox(500f, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void HighDeltaV_ZeroCoast_ContinuousBurn()
    {
        // Arrange: burn times exceed TOF → continuous burn, coast = 0
        // acceleration = 1.0 m/s², departureDV = 300, arrivalDV = 200
        // rawAccel = 300s, rawDecel = 200s, rawTotal = 500s, TOF = 400s
        // scale = 400/500 = 0.8
        // accelTime = 300 * 0.8 = 240, decelTime = 200 * 0.8 = 160
        var trajectory = CreateTrajectory(300f, 200f, 400f);
        var engine = CreateEngine();

        // Act
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass);

        // Assert
        AssertThat(profile).IsNotNull();
        AssertThat(profile!.CoastDuration).IsEqualApprox(0f, Tolerance);
        AssertThat(profile.AccelBurnDuration + profile.DecelBurnDuration)
            .IsEqualApprox(400f, Tolerance);
        // Ratio should be preserved: 300:200 = 3:2
        float ratio = profile.AccelBurnDuration / profile.DecelBurnDuration;
        AssertThat(ratio).IsEqualApprox(1.5f, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LowDeltaV_LongCoast()
    {
        // Arrange: small ΔV → brief burns, long coast
        // acceleration = 1.0, departureDV = 5, arrivalDV = 5
        // accelTime = 5s, decelTime = 5s, coastTime = 10000 - 10 = 9990s
        var trajectory = CreateTrajectory(5f, 5f, 10000f);
        var engine = CreateEngine();

        // Act
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass);

        // Assert
        AssertThat(profile).IsNotNull();
        AssertThat(profile!.CoastDuration).IsEqualApprox(9990f, Tolerance);
        AssertThat(profile.AccelBurnDuration).IsEqualApprox(5f, Tolerance);
        AssertThat(profile.DecelBurnDuration).IsEqualApprox(5f, Tolerance);
    }

    // ========================================================================
    // PHASE BOUNDARY TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void GetPhaseAtTime_ReturnsCorrectPhases()
    {
        // Arrange: accel 50s, coast 420s, decel 30s, total 500s
        var trajectory = CreateTrajectory(50f, 30f, 500f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        // Assert - Acceleration phase [0, 50)
        AssertThat(profile.GetPhaseAtTime(0f)).IsEqual(TransitPhase.Accelerating);
        AssertThat(profile.GetPhaseAtTime(25f)).IsEqual(TransitPhase.Accelerating);
        AssertThat(profile.GetPhaseAtTime(49.9f)).IsEqual(TransitPhase.Accelerating);

        // Coasting phase [50, 470)
        AssertThat(profile.GetPhaseAtTime(50f)).IsEqual(TransitPhase.Coasting);
        AssertThat(profile.GetPhaseAtTime(250f)).IsEqual(TransitPhase.Coasting);
        AssertThat(profile.GetPhaseAtTime(469.9f)).IsEqual(TransitPhase.Coasting);

        // Deceleration phase [470, 500]
        AssertThat(profile.GetPhaseAtTime(470f)).IsEqual(TransitPhase.Decelerating);
        AssertThat(profile.GetPhaseAtTime(490f)).IsEqual(TransitPhase.Decelerating);
        AssertThat(profile.GetPhaseAtTime(500f)).IsEqual(TransitPhase.Decelerating);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetPhaseAtTime_ContinuousBurn_NoCoastPhase()
    {
        // Arrange: burns fill entire TOF, no coast
        var trajectory = CreateTrajectory(300f, 200f, 400f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        // At start: accelerating
        AssertThat(profile.GetPhaseAtTime(0f)).IsEqual(TransitPhase.Accelerating);
        // At midpoint (240s is accel end for a 3:2 ratio over 400s)
        AssertThat(profile.GetPhaseAtTime(profile.AccelEndTime - 1f)).IsEqual(TransitPhase.Accelerating);
        // Just past accel → decel (since coast is 0, DecelStartTime == AccelEndTime)
        AssertThat(profile.GetPhaseAtTime(profile.DecelStartTime + 1f)).IsEqual(TransitPhase.Decelerating);
        // At end
        AssertThat(profile.GetPhaseAtTime(400f)).IsEqual(TransitPhase.Decelerating);
    }

    // ========================================================================
    // FUEL RATE TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void GetFuelRateAtTime_ZeroDuringCoast()
    {
        // Arrange: accel 50s, coast 420s, decel 30s
        var trajectory = CreateTrajectory(50f, 30f, 500f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        // Assert - fuel rate during coast is zero
        AssertThat(profile.GetFuelRateAtTime(100f)).IsEqualApprox(0f, Tolerance);
        AssertThat(profile.GetFuelRateAtTime(250f)).IsEqualApprox(0f, Tolerance);

        // Fuel rates during burns are positive
        AssertThat(profile.GetFuelRateAtTime(0f)).IsGreater(0f);
        AssertThat(profile.GetFuelRateAtTime(490f)).IsGreater(0f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FuelRates_AreConsistent_WithBudgetAndDuration()
    {
        // Arrange
        var trajectory = CreateTrajectory(50f, 30f, 500f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        // Assert: rate * duration = budget
        float accelFuelFromRate = profile.AccelFuelRate * profile.AccelBurnDuration;
        AssertThat(accelFuelFromRate).IsEqualApprox(profile.AccelFuelBudget, 0.1f);

        float decelFuelFromRate = profile.DecelFuelRate * profile.DecelBurnDuration;
        AssertThat(decelFuelFromRate).IsEqualApprox(profile.DecelFuelBudget, 0.1f);
    }

    // ========================================================================
    // FUEL BUDGET TESTS (Tsiolkovsky)
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void FuelBudgets_UseTsiolkovskySequentially()
    {
        // Arrange
        float departureDv = 50f;
        float arrivalDv = 30f;
        var trajectory = CreateTrajectory(departureDv, arrivalDv, 500f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        // Manual Tsiolkovsky: fuel = mass * (1 - e^(-dv / ve))
        float departureFuel = TestTotalMass * (1f - Mathf.Exp(-departureDv / TestExhaustVelocity));
        float massAfterDeparture = TestTotalMass - departureFuel;
        float arrivalFuel = massAfterDeparture * (1f - Mathf.Exp(-arrivalDv / TestExhaustVelocity));

        // Assert
        AssertThat(profile.AccelFuelBudget).IsEqualApprox(departureFuel, 0.5f);
        AssertThat(profile.DecelFuelBudget).IsEqualApprox(arrivalFuel, 0.5f);
        AssertThat(profile.TotalFuelBudget).IsEqualApprox(departureFuel + arrivalFuel, 0.5f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TotalFuelBudget_EqualsAccelPlusDecel()
    {
        var trajectory = CreateTrajectory(80f, 40f, 1000f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        AssertThat(profile.TotalFuelBudget)
            .IsEqualApprox(profile.AccelFuelBudget + profile.DecelFuelBudget, Tolerance);
    }

    // ========================================================================
    // PHASE BOUNDARY CONSISTENCY TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void AccelEndTime_EqualsAccelBurnDuration()
    {
        var trajectory = CreateTrajectory(60f, 40f, 800f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        AssertThat(profile.AccelEndTime).IsEqualApprox(profile.AccelBurnDuration, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DecelStartTime_Equals_TOF_MinusDecelDuration()
    {
        var trajectory = CreateTrajectory(60f, 40f, 800f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        AssertThat(profile.DecelStartTime)
            .IsEqualApprox(profile.TotalDuration - profile.DecelBurnDuration, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PhaseDurations_SumToTotalDuration()
    {
        var trajectory = CreateTrajectory(75f, 25f, 600f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        float sum = profile.AccelBurnDuration + profile.CoastDuration + profile.DecelBurnDuration;
        AssertThat(sum).IsEqualApprox(profile.TotalDuration, Tolerance);
    }

    // ========================================================================
    // VALIDATION / EDGE CASE TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void Calculate_NullTrajectory_ReturnsNull()
    {
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(null!, engine, TestTotalMass);
        AssertThat(profile).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Calculate_NullEngine_ReturnsNull()
    {
        var trajectory = CreateTrajectory(50f, 30f, 500f);
        var profile = BurnProfile.Calculate(trajectory, null!, TestTotalMass);
        AssertThat(profile).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Calculate_ZeroMass_ReturnsNull()
    {
        var trajectory = CreateTrajectory(50f, 30f, 500f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, 0f);
        AssertThat(profile).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Calculate_ZeroTOF_ReturnsNull()
    {
        var trajectory = CreateTrajectory(50f, 30f, 0f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass);
        AssertThat(profile).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Calculate_ZeroDeltaV_ZeroFuelBudget()
    {
        // No velocity change needed — no fuel consumed
        var trajectory = CreateTrajectory(0f, 0f, 500f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass);

        AssertThat(profile).IsNotNull();
        AssertThat(profile!.TotalFuelBudget).IsEqualApprox(0f, Tolerance);
        AssertThat(profile.AccelBurnDuration).IsEqualApprox(0f, Tolerance);
        AssertThat(profile.DecelBurnDuration).IsEqualApprox(0f, Tolerance);
        AssertThat(profile.CoastDuration).IsEqualApprox(500f, Tolerance);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GetDescription_ReturnsNonEmptyString()
    {
        var trajectory = CreateTrajectory(50f, 30f, 500f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        string desc = profile.GetDescription();
        AssertThat(desc).IsNotEmpty();
        AssertThat(desc.Contains("Accel")).IsTrue();
        AssertThat(desc.Contains("Decel")).IsTrue();
    }

    // ========================================================================
    // ASYMMETRIC BURN TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void AsymmetricDeltaV_ProducesAsymmetricBurnDurations()
    {
        // Departure ΔV much larger than arrival
        var trajectory = CreateTrajectory(100f, 10f, 1000f);
        var engine = CreateEngine();
        var profile = BurnProfile.Calculate(trajectory, engine, TestTotalMass)!;

        // Accel burn should be ~10x longer than decel burn
        AssertThat(profile.AccelBurnDuration).IsGreater(profile.DecelBurnDuration * 5f);
        // Accel fuel budget should be larger than decel
        AssertThat(profile.AccelFuelBudget).IsGreater(profile.DecelFuelBudget);
    }
}
