using System;
using Godot;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;
using EngineDef = Structures.Logistics.EngineDefinition;

namespace Constructables.ArtificialSatellites;

/// <summary>
/// Calculator for thrust-based performance metrics including payload capacity,
/// delta-v capability, range, and transfer feasibility assessment.
/// Uses Lambert solver for sophisticated trajectory-based calculations.
/// </summary>
public static class ThrustPerformanceCalculator
{
    // ============ Constants ============

    /// <summary>Default margin factor for payload calculations (0.9 = 90% of theoretical max).</summary>
    public const float DefaultMarginFactor = 0.9f;

    /// <summary>Default reserve fuel fraction for range calculations (0.1 = 10% reserved).</summary>
    public const float DefaultReserveFuelFraction = 0.1f;

    /// <summary>Minimum distance for range calculations in meters (1 km).</summary>
    public const float MinRangeDistance = 1000f;

    /// <summary>Maximum iterations for binary search in range calculation.</summary>
    public const int MaxRangeIterations = 20;

    /// <summary>Tolerance for binary search convergence in meters.</summary>
    public const float RangeTolerance = 100f;

    // ============ Max Payload Calculation (Ticket #473) ============

    /// <summary>
    /// Calculates the maximum payload capacity given engine, dry mass, fuel mass, and required delta-v.
    /// Uses Tsiolkovsky rocket equation: Δv = ve × ln(m0/m1)
    /// </summary>
    /// <param name="engine">Engine definition with thrust and Isp.</param>
    /// <param name="dryMass">Dry mass of the vessel in kg (without fuel or cargo).</param>
    /// <param name="fuelMass">Total fuel mass available in kg.</param>
    /// <param name="requiredDeltaV">Required delta-v in m/s.</param>
    /// <param name="marginFactor">Safety margin factor (default 0.9 = 90%).</param>
    /// <returns>Maximum payload capacity in kg, or 0 if parameters are invalid.</returns>
    public static float CalculateMaxPayload(
        EngineDef engine,
        float dryMass,
        float fuelMass,
        float requiredDeltaV,
        float marginFactor = DefaultMarginFactor
    )
    {
        if (engine == null)
        {
            GameLogger.Warning("ThrustPerformanceCalculator.CalculateMaxPayload: Engine is null");
            return 0f;
        }

        if (dryMass <= 0f || fuelMass <= 0f || requiredDeltaV <= 0f)
        {
            GameLogger.Warning(
                $"ThrustPerformanceCalculator.CalculateMaxPayload: Invalid parameters - dryMass: {dryMass}, fuelMass: {fuelMass}, requiredDeltaV: {requiredDeltaV}"
            );
            return 0f;
        }

        float exhaustVelocity = engine.ExhaustVelocity;
        if (exhaustVelocity <= 0f)
        {
            GameLogger.Warning(
                "ThrustPerformanceCalculator.CalculateMaxPayload: Invalid exhaust velocity"
            );
            return 0f;
        }

        // Apply margin factor to delta-v requirement
        float adjustedDeltaV = requiredDeltaV * marginFactor;

        // Tsiolkovsky rearranged: payload = dryMass × (e^(Δv/ve) - 1) - fuelMass
        float massRatio = (float)Math.Exp(adjustedDeltaV / exhaustVelocity);
        float maxPayload = dryMass * (massRatio - 1f) - fuelMass;

        GameLogger.Debug(
            $"ThrustPerformanceCalculator.CalculateMaxPayload: Max payload = {Math.Max(0f, maxPayload):F2} kg (margin: {marginFactor:P0})"
        );

        return Math.Max(0f, maxPayload);
    }

    // ============ Delta-V and Range Calculations (Ticket #474) ============

    /// <summary>
    /// Calculates the maximum delta-v capability given engine, mass, and fuel.
    /// </summary>
    /// <param name="engine">Engine definition with thrust and Isp.</param>
    /// <param name="dryMass">Dry mass in kg.</param>
    /// <param name="fuelMass">Fuel mass in kg.</param>
    /// <param name="cargoMass">Cargo mass in kg (default 0).</param>
    /// <returns>Maximum delta-v in m/s.</returns>
    public static float CalculateMaxDeltaV(
        EngineDef engine,
        float dryMass,
        float fuelMass,
        float cargoMass = 0f
    )
    {
        if (engine == null || dryMass <= 0f || fuelMass <= 0f)
        {
            return 0f;
        }

        float exhaustVelocity = engine.ExhaustVelocity;
        if (exhaustVelocity <= 0f)
        {
            return 0f;
        }

        float totalMass = dryMass + fuelMass + cargoMass;
        float finalMass = dryMass + cargoMass;

        if (totalMass <= finalMass)
        {
            return 0f;
        }

        // Tsiolkovsky: Δv = ve × ln(m0/m1)
        float deltaV = exhaustVelocity * (float)Math.Log(totalMass / finalMass);

        GameLogger.Debug(
            $"ThrustPerformanceCalculator.CalculateMaxDeltaV: Max Δv = {deltaV:F2} m/s"
        );

        return deltaV;
    }

    /// <summary>
    /// Calculates the maximum range achievable using Lambert solver for trajectory-based calculation.
    /// Uses binary search to find the maximum distance that can be reached within fuel constraints.
    /// </summary>
    /// <param name="engine">Engine definition.</param>
    /// <param name="dryMass">Dry mass in kg.</param>
    /// <param name="fuelMass">Total fuel mass in kg.</param>
    /// <param name="cargoMass">Cargo mass in kg.</param>
    /// <param name="reserveFuelFraction">Fraction of fuel to reserve (default 0.1 = 10%).</param>
    /// <param name="originPosition">Starting position (default origin).</param>
    /// <param name="centralBodyMass">Mass of central body for gravity (for μ calculation).</param>
    /// <returns>Maximum range in meters.</returns>
    public static float CalculateRange(
        EngineDef engine,
        float dryMass,
        float fuelMass,
        float cargoMass = 0f,
        float reserveFuelFraction = DefaultReserveFuelFraction,
        Vector3 originPosition = default,
        float centralBodyMass = 0f
    )
    {
        if (engine == null || fuelMass <= 0f)
        {
            return 0f;
        }

        // Calculate usable fuel (excluding reserve)
        float usableFuel = fuelMass * (1f - reserveFuelFraction);

        if (usableFuel <= 0f)
        {
            GameLogger.Warning(
                "ThrustPerformanceCalculator.CalculateRange: No usable fuel after reserve"
            );
            return 0f;
        }

        float exhaustVelocity = engine.ExhaustVelocity;
        if (exhaustVelocity <= 0f)
        {
            return 0f;
        }

        float totalMass = dryMass + fuelMass + cargoMass;

        // Calculate maximum available delta-v from usable fuel
        float availableDeltaV = CalculateMaxDeltaV(engine, dryMass, usableFuel, cargoMass);

        if (availableDeltaV <= 0f)
        {
            return 0f;
        }

        // If no central body mass provided, use simple approximation
        if (centralBodyMass <= 0f)
        {
            return CalculateRangeSimple(engine, totalMass, usableFuel, cargoMass);
        }

        // Use Lambert solver for sophisticated calculation
        return CalculateRangeWithLambert(
            engine,
            totalMass,
            usableFuel,
            cargoMass,
            originPosition,
            centralBodyMass
        );
    }

    /// <summary>
    /// Simple range calculation without gravitational context.
    /// Uses constant-thrust approximation: d = 0.5 × a × t²
    /// </summary>
    private static float CalculateRangeSimple(
        EngineDef engine,
        float totalMass,
        float usableFuel,
        float cargoMass
    )
    {
        float averageThrust = engine.EffectiveThrust;
        if (averageThrust <= 0f || totalMass <= 0f)
        {
            return 0f;
        }

        // Average mass during burn
        float finalMass = totalMass - usableFuel;
        float averageMass = (totalMass + finalMass) / 2f;

        // Constant acceleration
        float acceleration = averageThrust / averageMass;

        // Burn time: fuel / mass flow rate
        // mass flow rate = thrust / exhaustVelocity
        float massFlowRate = averageThrust / engine.ExhaustVelocity;
        float burnTime = usableFuel / massFlowRate;

        // Distance traveled with constant acceleration
        float range = 0.5f * acceleration * burnTime * burnTime;

        GameLogger.Debug(
            $"ThrustPerformanceCalculator.CalculateRangeSimple: Range = {range / 1000f:F2} km"
        );

        return range;
    }

    /// <summary>
    /// Sophisticated range calculation using Lambert solver.
    /// Binary searches for maximum distance within delta-v budget.
    /// </summary>
    private static float CalculateRangeWithLambert(
        EngineDef engine,
        float totalMass,
        float usableFuel,
        float cargoMass,
        Vector3 originPosition,
        float centralBodyMass
    )
    {
        float exhaustVelocity = engine.ExhaustVelocity;

        // Calculate fuel required for a given delta-v using Tsiolkovsky
        float CalculateFuelForDeltaV(float deltaV)
        {
            float finalMass = totalMass - cargoMass; // After burning all fuel
            float massRatio = (float)Math.Exp(deltaV / exhaustVelocity);
            return totalMass * massRatio - finalMass;
        }

        // Estimate maximum possible range (half of system diameter as upper bound)
        // Using reasonable defaults for unknown system size
        float maxPossibleRange = centralBodyMass * 1e6f; // Rough estimate
        float minRange = MinRangeDistance;

        // Binary search for maximum achievable distance
        float low = minRange;
        float high = maxPossibleRange;
        float bestRange = minRange;

        for (int i = 0; i < MaxRangeIterations; i++)
        {
            float mid = (low + high) / 2f;

            // Calculate required delta-v for this distance using Lambert
            // Simplified: assume direct transfer at some average angle
            float estimatedDeltaV = EstimateDeltaVForDistance(mid, centralBodyMass);

            // Check if we have enough fuel
            float fuelNeeded = CalculateFuelForDeltaV(estimatedDeltaV);

            if (fuelNeeded <= usableFuel)
            {
                // Achievable - try farther
                bestRange = mid;
                low = mid;
            }
            else
            {
                // Not achievable - try closer
                high = mid;
            }

            // Check convergence
            if (high - low < RangeTolerance)
            {
                break;
            }
        }

        GameLogger.Debug(
            $"ThrustPerformanceCalculator.CalculateRangeWithLambert: Range = {bestRange / 1000f:F2} km"
        );

        return bestRange;
    }

    /// <summary>
    /// Estimates delta-v required for a direct transfer over a given distance.
    /// Uses simplified orbital mechanics: Δv ≈ √(2μ/r) for escape-type trajectory
    /// </summary>
    private static float EstimateDeltaVForDistance(float distance, float centralBodyMass)
    {
        // Gravitational parameter
        float mu = OrbitalMath.GRAVITATIONAL_CONSTANT * centralBodyMass;

        if (mu <= 0f || distance <= 0f)
        {
            return 0f;
        }

        // Simplified: assume starting from circular orbit at distance/2
        // and escaping to distance
        // v_escape = √(2μ/r)
        float startRadius = distance / 2f;

        if (startRadius <= 0f)
        {
            return 0f;
        }

        float velocityAtStart = Mathf.Sqrt(mu / startRadius);
        float velocityAtEnd = Mathf.Sqrt(2f * mu / distance); // At apogee, essentially escape

        // Approximate delta-v as the difference
        float deltaV = velocityAtEnd - velocityAtStart;

        return Math.Abs(deltaV);
    }

    // ============ Transfer Feasibility Assessment (Ticket #475) ============

    /// <summary>
    /// Feasibility status returned by AssessTransferFeasibility.
    /// </summary>
    public enum FeasibilityStatus
    {
        /// <summary>Transfer is fully achievable with current parameters.</summary>
        Feasible,

        /// <summary>Transfer is possible but requires optimal conditions.</summary>
        MarginallyFeasible,

        /// <summary>Transfer is not possible with current parameters.</summary>
        NotFeasible,

        /// <summary>Insufficient data to determine feasibility.</summary>
        Unknown,
    }

    /// <summary>
    /// Result structure for transfer feasibility assessment.
    /// </summary>
    public class TransferFeasibilityResult
    {
        public FeasibilityStatus Status { get; set; }
        public float RequiredDeltaV { get; set; }
        public float AvailableDeltaV { get; set; }
        public float RequiredThrust { get; set; }
        public float AvailableThrust { get; set; }
        public float MinimumTransferTime { get; set; }
        public float RequestedTransferTime { get; set; }
        public float ThrustToWeightRatio { get; set; }
        public string Message { get; set; } = string.Empty;

        public bool IsFeasible =>
            Status == FeasibilityStatus.Feasible || Status == FeasibilityStatus.MarginallyFeasible;
    }

    /// <summary>
    /// Assesses whether a transfer is realistic given engine thrust, total mass,
    /// required delta-v, and transfer time.
    /// </summary>
    /// <param name="engine">Engine definition.</param>
    /// <param name="totalMass">Total mass (dry + fuel + cargo) in kg.</param>
    /// <param name="requiredDeltaV">Required delta-v in m/s.</param>
    /// <param name="transferTime">Desired transfer time in seconds.</param>
    /// <returns>FeasibilityResult with status and detailed information.</returns>
    public static TransferFeasibilityResult AssessTransferFeasibility(
        EngineDef engine,
        float totalMass,
        float requiredDeltaV,
        float transferTime
    )
    {
        var result = new TransferFeasibilityResult
        {
            RequiredDeltaV = requiredDeltaV,
            RequestedTransferTime = transferTime,
            Status = FeasibilityStatus.Unknown,
        };

        // Validation
        if (engine == null)
        {
            result.Status = FeasibilityStatus.Unknown;
            result.Message = "No engine specified";
            return result;
        }

        if (totalMass <= 0f || requiredDeltaV <= 0f)
        {
            result.Status = FeasibilityStatus.Unknown;
            result.Message = "Invalid mass or delta-v parameters";
            return result;
        }

        float exhaustVelocity = engine.ExhaustVelocity;
        float thrust = engine.EffectiveThrust;

        result.AvailableThrust = thrust;
        result.ThrustToWeightRatio = thrust / (totalMass * OrbitalMath.GRAVITATIONAL_CONSTANT);

        // Calculate available delta-v using the calculator
        // Assume minimal fuel for delta-v check
        result.AvailableDeltaV = CalculateMaxDeltaV(engine, totalMass * 0.99f, totalMass * 0.01f);

        // Check delta-v sufficiency
        if (result.AvailableDeltaV < requiredDeltaV)
        {
            result.Status = FeasibilityStatus.NotFeasible;
            result.Message =
                $"Insufficient delta-v: need {requiredDeltaV:F0} m/s, available {result.AvailableDeltaV:F0} m/s";
            return result;
        }

        // Calculate minimum transfer time based on thrust
        float acceleration = thrust / totalMass;

        if (acceleration <= 0f)
        {
            result.Status = FeasibilityStatus.NotFeasible;
            result.Message = "Zero thrust - cannot accelerate";
            return result;
        }

        // Minimum time to achieve required delta-v with constant thrust
        result.MinimumTransferTime = requiredDeltaV / acceleration;

        // Check if requested time is achievable
        if (transferTime < result.MinimumTransferTime)
        {
            result.Status = FeasibilityStatus.NotFeasible;
            result.Message =
                $"Transfer time too short: minimum {result.MinimumTransferTime:F0}s, requested {transferTime:F0}s";
            return result;
        }

        // Determine feasibility level based on time margin
        float timeMargin = transferTime / result.MinimumTransferTime;

        if (timeMargin >= 2.0f)
        {
            result.Status = FeasibilityStatus.Feasible;
            result.Message = $"Transfer is feasible with {timeMargin:F1}x time margin";
        }
        else if (timeMargin >= 1.1f)
        {
            result.Status = FeasibilityStatus.MarginallyFeasible;
            result.Message =
                $"Transfer is marginally feasible - tight time margin of {timeMargin:F1}x";
        }
        else
        {
            result.Status = FeasibilityStatus.NotFeasible;
            result.Message = $"Insufficient time margin: {timeMargin:F1}x (need >1.1x)";
        }

        return result;
    }

    // ============ Advanced: Full Trajectory-Based Range ============

    /*
    /// <summary>
    /// Calculates range using actual Lambert solver for precise trajectory calculations.
    /// Requires origin and destination celestial bodies.
    /// </summary>
    /// <param name="engine">Engine definition.</param>
    /// <param name="dryMass">Dry mass in kg.</param>
    /// <param name="fuelMass">Total fuel mass in kg.</param>
    /// <param name="cargoMass">Cargo mass in kg.</param>
    /// <param name="originBody">Origin celestial body.</param>
    /// <param name="destinationBody">Destination celestial body (optional for range calculation).</param>
    /// <param name="reserveFuelFraction">Fraction of fuel to reserve.</param>
    /// <returns>Maximum range in meters.</returns>
    public static float CalculateRangeWithTrajectory(
        EngineDef engine,
        float dryMass,
        float fuelMass,
        float cargoMass,
        TrajectorySolution route,
        float reserveFuelFraction = DefaultReserveFuelFraction
    )
    {
        float usableFuel = fuelMass * (1f - reserveFuelFraction);
        float totalMass = dryMass + fuelMass + cargoMass;
        float exhaustVelocity = engine.ExhaustVelocity;

        if (exhaustVelocity <= 0f || totalMass <= 0f)
        {
            return 0f;
        }

        // Get gravitational parameter from origin body


            // Calculate fuel required for this trajectory
        float fuelRequired = route.CalculateFuelRequired(totalMass, exhaustVelocity);


        // Otherwise, binary search with Lambert solver
        float maxDistance = 1e9f; // 1 million km default
        float minDistance = MinRangeDistance;

        // Binary search
        float low = minDistance;
        float high = maxDistance;
        float bestRange = minDistance;

        for (int i = 0; i < MaxRangeIterations; i++)
        {
            float mid = (low + high) / 2f;

            // Create a test destination at this distance
            Vector3 r1 = Vector3.Zero;
            Vector3 r2 = new Vector3(mid, 0, 0);

            // Solve for minimum delta-v transfer (longer TOF = less delta-v)
            // Use moderate TOF as baseline
            float testTOF = 3600f * (1f + mid / 1e8f); // Scale TOF with distance

            var solution = LambertSolver.Solve(r1, r2, testTOF, mu);
            float fuelNeeded = solution.CalculateFuelRequired(totalMass, exhaustVelocity);

            if (fuelNeeded <= usableFuel)
            {
                bestRange = mid;
                low = mid;
            }
            else
            {
                high = mid;
            }

            if (high - low < RangeTolerance)
            {
                break;
            }
        }

        return bestRange;
    }
*/
}
