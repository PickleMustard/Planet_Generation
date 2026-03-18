using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using UtilityLibrary;

namespace Constructables.ArtificialSatellites;

/// <summary>
/// Provides trajectory planning functionality for logistics units.
/// Generates and ranks multiple trajectory options between celestial bodies
/// using Lambert solver calculations with central body gravitational parameters.
/// </summary>
public class TrajectoryPlanner
{
    // ============ Configuration ============

    /// <summary>
    /// Default number of trajectory options to generate.
    /// </summary>
    public int DefaultNumOptions { get; set; } = 5;

    /// <summary>
    /// Minimum time of flight in seconds.
    /// </summary>
    public float MinTOF { get; set; } = 100f;

    /// <summary>
    /// Maximum time of flight in seconds (default: 1 day).
    /// </summary>
    public float MaxTOF { get; set; } = 86400f;

    /// <summary>
    /// Safety margin for delta-v calculations (0.9 = 90% of max usable).
    /// </summary>
    public float SafetyMargin { get; set; } = 0.9f;

    /// <summary>
    /// Whether to include retrograde trajectory options.
    /// </summary>
    public bool IncludeRetrograde { get; set; } = false;

    /// <summary>
    /// Maximum number of complete revolutions to consider.
    /// </summary>
    public int MaxRevolutions { get; set; } = 0;

    // ============ Singleton Instance ============

    private static TrajectoryPlanner? _instance;

    /// <summary>
    /// Singleton instance for global access.
    /// </summary>
    public static TrajectoryPlanner Instance => _instance ??= new TrajectoryPlanner();

    // ============ Constructor ============

    public TrajectoryPlanner()
    {
        GameLogger.Info("TrajectoryPlanner: Initialized");
    }

    // ============ Main Public Methods ============

    /// <summary>
    /// Generates trajectory options between origin and destination celestial bodies.
    /// Uses the central body (origin's parent or dominant gravitational point) for μ.
    /// Returns options ranked by MostEfficient (lowest delta-v) by default.
    /// </summary>
    /// <param name="unit">The logistics unit making the transfer.</param>
    /// <param name="origin">Origin celestial body.</param>
    /// <param name="destination">Destination celestial body.</param>
    /// <param name="departureTime">Departure time in seconds from now.</param>
    /// <param name="numOptions">Number of options to generate.</param>
    /// <param name="rankingCriteria">Criteria for ranking the options.</param>
    /// <returns>List of trajectory options ranked by the specified criteria.</returns>
    public List<TrajectorySolution> GetOptions(
        LogisticsUnit unit,
        CelestialBody origin,
        CelestialBody destination,
        float departureTime = 0f,
        int numOptions = 0,
        TrajectorySolution.RankingCriteria rankingCriteria = TrajectorySolution.RankingCriteria.MostEfficient
    )
    {
        if (unit == null)
        {
            GameLogger.Error("TrajectoryPlanner.GetOptions: LogisticsUnit is null");
            return new List<TrajectorySolution>();
        }

        if (origin == null || destination == null)
        {
            GameLogger.Error("TrajectoryPlanner.GetOptions: Origin or destination is null");
            return new List<TrajectorySolution>();
        }

        if (origin == destination)
        {
            GameLogger.Warning("TrajectoryPlanner.GetOptions: Origin and destination are the same");
            return new List<TrajectorySolution>();
        }

        if (numOptions <= 0)
        {
            numOptions = DefaultNumOptions;
        }

        GameLogger.Info(
            $"TrajectoryPlanner.GetOptions: Planning route from {origin.Name} to {destination.Name}, " +
            $"departure in {departureTime:F1}s, generating {numOptions} options"
        );

        // Find the central body for gravitational parameter
        CelestialBody centralBody = FindCentralBody(origin);
        float mu = GetGravitationalParameter(centralBody);

        if (mu <= 0f)
        {
            GameLogger.Error(
                $"TrajectoryPlanner.GetOptions: Invalid gravitational parameter {mu} " +
                $"for central body {centralBody?.Name}"
            );
            return new List<TrajectorySolution>();
        }

        // Predict origin position at departure time (same for all options)
        Vector3 originPos = PredictBodyPosition(origin, departureTime);

        // Get orbital velocities from origin and destination bodies
        Vector3 originOrbitalVelocity = origin.Velocity;
        Vector3 destOrbitalVelocity = destination.Velocity;

        // Pre-calculate mass/engine values (same for all options)
        float totalMass = unit.GetTotalMass();
        float exhaustVelocity = unit.CurrentEngine?.ExhaustVelocity ?? 300f * 9.81f;

        // Generate ToF values across the range
        float minTof = MinTOF;
        float maxTof = MaxTOF;
        float tofStep;
        if (numOptions == 1)
        {
            tofStep = 0f;
            minTof = (minTof + maxTof) / 2f;
        }
        else
        {
            tofStep = (maxTof - minTof) / (numOptions - 1);
        }

        // Generate Lambert solutions with per-ToF destination position prediction
        var options = new List<TrajectorySolution>();

        for (int i = 0; i < numOptions; i++)
        {
            float tof = minTof + (tofStep * i);

            // Predict destination position at THIS specific arrival time
            Vector3 destPos = PredictBodyPosition(destination, departureTime + tof);

            // Solve Lambert's problem for this specific geometry and ToF
            var solutions = OrbitalMath.SolveLambert(
                originPos, destPos, tof, mu, MaxRevolutions, IncludeRetrograde);

            foreach (var solution in solutions)
            {
                // Set the actual time of flight on the solution
                solution.TimeOfFlight = tof;

                // Set extended properties
                solution.OriginBody = origin;
                solution.DestinationBody = destination;
                solution.DepartureTime = departureTime;
                solution.GravitationalParameter = mu;
                solution.PredictedOriginPosition = originPos;
                solution.PredictedDestinationPosition = destPos;

                // Set orbital velocities and recalculate delta-v correctly:
                // ΔV = |v_lambert_depart - v_orbital_origin| + |v_lambert_arrive - v_orbital_dest|
                solution.OriginOrbitalVelocity = originOrbitalVelocity;
                solution.DestinationOrbitalVelocity = destOrbitalVelocity;
                solution.RecalculateDeltaV();

                // Calculate fuel required (using corrected delta-v)
                solution.CalculateFuelRequired(totalMass, exhaustVelocity);

                options.Add(solution);
            }
        }

        // Sort by delta-v (lowest first) before filtering
        options.Sort((a, b) => a.DeltaVRequired.CompareTo(b.DeltaVRequired));

        // Filter by available delta-v
        float availableDeltaV = GetAvailableDeltaV(unit) * SafetyMargin;
        options = FilterByAvailableDeltaV(options, availableDeltaV);

        if (options.Count == 0)
        {
            GameLogger.Warning(
                $"TrajectoryPlanner.GetOptions: No viable trajectory options within delta-v budget " +
                $"of {availableDeltaV:F2} m/s"
            );
            return options;
        }

        // Calculate scores and rank
        TrajectorySolution.CalculateScores(options);
        options = TrajectorySolution.RankBy(options, rankingCriteria);

        GameLogger.Info(
            $"TrajectoryPlanner.GetOptions: Generated {options.Count} viable trajectory options, " +
            $"ranked by {rankingCriteria}"
        );

        return options;
    }

    /// <summary>
    /// Gets a quick estimate of the optimal trajectory without generating multiple options.
    /// Useful for simple route planning.
    /// </summary>
    /// <param name="unit">The logistics unit.</param>
    /// <param name="origin">Origin celestial body.</param>
    /// <param name="destination">Destination celestial body.</param>
    /// <param name="departureTime">Departure time in seconds from now.</param>
    /// <returns>A single trajectory solution or null if failed.</returns>
    public TrajectorySolution? GetQuickTrajectory(
        LogisticsUnit unit,
        CelestialBody origin,
        CelestialBody destination,
        float departureTime = 0f
    )
    {
        var options = GetOptions(unit, origin, destination, departureTime, 1);
        return options.Count > 0 ? options[0] : null;
    }

    // ============ Private Helper Methods ============

    /// <summary>
    /// Finds the most gravitationally dominant body in the system relative to the given body.
    /// Uses gravitational influence calculation similar to FindDominantBody in PlanetSystemGenerator.
    /// </summary>
    /// <param name="origin">The body to find the central body for.</param>
    /// <returns>The most gravitationally dominant body.</returns>
    private CelestialBody FindCentralBody(CelestialBody origin)
    {
        if (origin == null)
        {
            GameLogger.Warning("TrajectoryPlanner.FindCentralBody: Origin body is null");
            return origin;
        }

        // Get all celestial bodies in the system via the "CelestialBody" group
        var bodies = origin.GetTree().GetNodesInGroup("CelestialBody");

        if (bodies == null || bodies.Count == 0)
        {
            GameLogger.Warning("TrajectoryPlanner.FindCentralBody: No bodies found in system");
            return origin;
        }

        float maxInfluence = 0f;
        CelestialBody? dominantBody = null;
        Vector3 testPosition = origin.GlobalPosition;

        foreach (Node node in bodies)
        {
            if (node is CelestialBody body && body != origin)
            {
                float distanceSq = testPosition.DistanceSquaredTo(body.GlobalPosition);

                // Avoid division by zero for very close or overlapping bodies
                if (distanceSq > 0.001f)
                {
                    // Calculate gravitational influence (acceleration) at origin's position
                    // Similar to FindDominantBody in PlanetSystemGenerator:
                    // influence = G × mass / distance²
                    float influence = OrbitalMath.GRAVITATIONAL_CONSTANT * body.Mass / distanceSq;

                    if (influence > maxInfluence)
                    {
                        maxInfluence = influence;
                        dominantBody = body;
                    }
                }
            }
        }

        // If no other body has significant gravitational influence, 
        // use the origin itself (it's either isolated or is the central body)
        if (dominantBody == null)
        {
            GameLogger.Debug($"TrajectoryPlanner.FindCentralBody: Using {origin.Name} as central body (no dominant body found)");
            return origin;
        }

        GameLogger.Debug($"TrajectoryPlanner.FindCentralBody: Found dominant body {dominantBody.Name} for {origin.Name}");
        return dominantBody;
    }

    /// <summary>
    /// Calculates the gravitational parameter (μ = GM) for a celestial body.
    /// </summary>
    /// <param name="centralBody">The central body.</param>
    /// <returns>Gravitational parameter in m³/s².</returns>
    private float GetGravitationalParameter(CelestialBody centralBody)
    {
        if (centralBody == null || centralBody.Mass <= 0f)
        {
            GameLogger.Warning(
                $"TrajectoryPlanner.GetGravitationalParameter: Invalid body or mass - " +
                $"body: {centralBody?.Name}, mass: {centralBody?.Mass}"
            );
            return 0f;
        }

        // μ = G × M (using OrbitalMath's gravitational constant)
        float mu = OrbitalMath.GRAVITATIONAL_CONSTANT * centralBody.Mass;

        GameLogger.Debug(
            $"TrajectoryPlanner.GetGravitationalParameter: μ = {mu:E2} m³/s² for {centralBody.Name} " +
            $"(mass: {centralBody.Mass:E2} kg)"
        );

        return mu;
    }

    /// <summary>
    /// Predicts the future position of a celestial body at a given time from now.
    /// Uses simplified orbital propagation.
    /// </summary>
    /// <param name="body">The celestial body.</param>
    /// <param name="timeFromNow">Time in seconds from now.</param>
    /// <returns>Predicted position vector.</returns>
    private Vector3 PredictBodyPosition(CelestialBody body, float timeFromNow)
    {
        if (body == null)
        {
            return Vector3.Zero;
        }

        // Get current position
        Vector3 currentPos = body.GlobalPosition;

        // If body has velocity, estimate future position
        // This is a simplified prediction - for accurate results, we'd use full orbital mechanics
        Vector3 velocity = body.Velocity;

        if (velocity.LengthSquared() > 0.001f)
        {
            // Linear prediction (sufficient for nearby bodies over short timescales)
            // For more accuracy, we'd integrate the orbital equation
            Vector3 predictedPos = currentPos + velocity * timeFromNow;

            GameLogger.Debug(
                $"TrajectoryPlanner.PredictBodyPosition: {body.Name} at t+{timeFromNow:F1}s: " +
                $"{currentPos} -> {predictedPos}"
            );

            return predictedPos;
        }

        // No velocity - return current position
        return currentPos;
    }

    /// <summary>
    /// Estimates the average time of flight using a simple Hohmann transfer approximation.
    /// </summary>
    /// <param name="r1">Initial position.</param>
    /// <param name="r2">Final position.</param>
    /// <param name="mu">Gravitational parameter.</param>
    /// <returns>Estimated time of flight in seconds.</parameter>
    private float EstimateAverageTOF(Vector3 r1, Vector3 r2, float mu)
    {
        float distance = r1.DistanceTo(r2);

        if (distance <= 0f || mu <= 0f)
        {
            return (MinTOF + MaxTOF) / 2f;
        }

        // Simple estimate using circular orbit approximation
        // For a rough estimate, assume semi-major axis = distance / 2
        float semiMajorAxis = distance / 2f;

        // Kepler's third law: T = 2π × √(a³/μ)
        float period = 2f * Mathf.Pi * Mathf.Sqrt(Mathf.Pow(semiMajorAxis, 3) / mu);

        // Hohmann transfer is roughly half an orbit
        float estimatedTOF = period / 2f;

        // Clamp to configured range
        return Mathf.Clamp(estimatedTOF, MinTOF, MaxTOF);
    }

    /// <summary>
    /// Filters trajectory options by available delta-v budget.
    /// </summary>
    private List<TrajectorySolution> FilterByAvailableDeltaV(
        List<TrajectorySolution> options,
        float availableDeltaV
    )
    {
        if (options == null || options.Count == 0)
        {
            return new List<TrajectorySolution>();
        }

        var filtered = TrajectorySolution.FilterByDeltaV(options, availableDeltaV);

        GameLogger.Debug(
            $"TrajectoryPlanner.FilterByAvailableDeltaV: Filtered to {filtered.Count} options " +
            $"within budget of {availableDeltaV:F2} m/s"
        );

        return filtered;
    }

    /// <summary>
    /// Gets the available delta-v budget for a logistics unit.
    /// Uses Tsiolkovsky rocket equation: Δv = Isp × g₀ × ln(m_initial / m_final)
    /// </summary>
    private float GetAvailableDeltaV(LogisticsUnit unit)
    {
        if (unit == null)
        {
            return 0f;
        }

        float totalMass = unit.GetTotalMass();
        float fuelMass = unit.Fuel;

        if (fuelMass <= 0f || totalMass <= 0f)
        {
            return 0f;
        }

        float dryMass = totalMass - fuelMass;

        // If no engine, return 0
        if (unit.CurrentEngine == null)
        {
            GameLogger.Warning("TrajectoryPlanner.GetAvailableDeltaV: No engine installed");
            return 0f;
        }

        float exhaustVelocity = unit.CurrentEngine.ExhaustVelocity;

        if (exhaustVelocity <= 0f)
        {
            return 0f;
        }

        // Tsiolkovsky: Δv = Isp × g₀ × ln(m_initial / m_final)
        // Where m_initial = dry mass + fuel
        // m_final = dry mass only (using all fuel)
        float deltaV = exhaustVelocity * Mathf.Log(totalMass / dryMass);

        GameLogger.Debug(
            $"TrajectoryPlanner.GetAvailableDeltaV: {deltaV:F2} m/s available " +
            $"(mass: {totalMass:F2}kg, fuel: {fuelMass:F2}kg, dry: {dryMass:F2}kg, " +
            $"exhaust: {exhaustVelocity:F2}m/s)"
        );

        return deltaV;
    }
}
