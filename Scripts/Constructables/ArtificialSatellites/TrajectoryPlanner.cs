using System;
using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using UtilityLibrary;
using UtilityLibrary.GameMath.Orbital;

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
    /// Maximum delta V available after escaping host body's gravity well.
    /// </summary>
    public float MaxAvailableDeltaV { get; set; } = 1000f;

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
    ///
    /// The solver accounts for:
    /// - Ship's actual position in its orbit band (not planet center)
    /// - Ship's orbital velocity around its parent body + parent's orbital velocity
    /// - Target orbit band position and velocity at the destination
    /// </summary>
    /// <param name="unit">The logistics unit making the transfer.</param>
    /// <param name="origin">Origin celestial body.</param>
    /// <param name="destination">Destination celestial body.</param>
    /// <param name="departureTime">Departure time in seconds from now.</param>
    /// <param name="numOptions">Number of options to generate.</param>
    /// <param name="rankingCriteria">Criteria for ranking the options.</param>
    /// <param name="targetBandIndex">Target orbit band at destination (-1 = auto-select closest).</param>
    /// <returns>List of trajectory options ranked by the specified criteria.</returns>
    public List<TrajectorySolution> GetOptions(
        LogisticsUnit unit,
        IOrbitalBody origin,
        IOrbitalBody destination,
        float departureTime = 0f,
        int numOptions = 0,
        TrajectorySolution.RankingCriteria rankingCriteria =
            TrajectorySolution.RankingCriteria.MostEfficient,
        int targetBandIndex = -1
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
            $"TrajectoryPlanner.GetOptions: Planning route from {origin} to {destination}, "
                + $"departure in {departureTime:F1}s, generating {numOptions} options, "
                + $"target band: {(targetBandIndex >= 0 ? targetBandIndex.ToString() : "auto")}"
        );

        // Find the central body for gravitational parameter
        IOrbitalBody centralBody = OrbitalMath.FindCentralBody(origin);
        float mu = OrbitalMath.GetGravitationalParameter(centralBody);
        float chordLength = (destination.BodyPosition - origin.BodyPosition).Length();
        float semiPerimeter =
            (origin.BodyPosition.Length() + destination.BodyPosition.Length() + chordLength) / 2f;
        float semiMajorAxis = semiPerimeter / 2f;
        float lambda = Mathf.Sqrt(1f - (chordLength / semiPerimeter));

        if (mu <= 0f)
        {
            GameLogger.Error(
                $"TrajectoryPlanner.GetOptions: Invalid gravitational parameter {mu} "
                    + $"for central body {centralBody}"
            );
            return new List<TrajectorySolution>();
        }

        GameLogger.Info(
            $"TrajectoryPlanner.GetOptions: Geometric inputs - "
                + $"originPos={origin.BodyPosition}, destinationPos={destination.BodyPosition}, "
                + $"chordLength={chordLength:F2}m, semiPerimeter={semiPerimeter:F2}m, "
                + $"semiMajorAxis={semiMajorAxis:F2}m, lambda={lambda:F6}, mu={mu:E2}"
        );

        // Ship's total velocity relative to central body:
        // ship orbital velocity around parent + parent orbital velocity around central body
        //Vector3 shipVelocityRelative = unit.GetOrbitalVelocityRelativeTo(centralBody);
        Vector3 shipVelocity = unit.Velocity + origin.Velocity;

        GameLogger.Info(
            $"TrajectoryPlanner.GetOptions: Velocity inputs - "
                + $"unitVelocity={unit.Velocity}, originVelocity={origin.Velocity}, "
                + $"combinedShipVelocity={shipVelocity}, speed={shipVelocity.Length():F2}m/s"
        );

        //MaxAvailableDeltaV = Mathf.Sqrt(
        //    Mathf.Pow(shipVelocity.Length() + unit.GetAvailableDeltaV(), 2f)
        //        - (2f * mu) / shipPosRelative.Length()
        //);
        MaxAvailableDeltaV = shipVelocity.Length() + unit.GetAvailableDeltaV();

        GameLogger.Info(
            $"TrajectoryPlanner.GetOptions: DeltaV budget - "
                + $"shipSpeed={shipVelocity.Length():F2}m/s, availableDeltaV={unit.GetAvailableDeltaV():F2}m/s, "
                + $"maxAvailableDeltaV={MaxAvailableDeltaV:F2}m/s"
        );

        int originBandIndex = unit.BandIndex;

        // ---- Destination: determine target orbit band ----

        // Auto-select target band if not specified
        int resolvedTargetBand = targetBandIndex;
        if (resolvedTargetBand < 0)
        {
            // Default to closest band (band 0 if no bands available)
            resolvedTargetBand = destination.GetClosestBandForApproach(shipVelocity.Length());
        }

        // Validate target band exists
        if (resolvedTargetBand >= destination.GetBandCount())
        {
            GameLogger.Warning(
                $"TrajectoryPlanner.GetOptions: Target band {resolvedTargetBand} exceeds "
                    + $"available bands ({destination.GetBandCount()}), defaulting to 0"
            );
            resolvedTargetBand = 0;
        }

        // Pre-calculate mass/engine values (same for all options)
        float totalMass = unit.GetTotalMass();
        float exhaustVelocity = unit.CurrentEngine?.ExhaustVelocity ?? 300f * 9.81f;

        // Generate ToF values across the range
        var (minTof, maxTof) = CalculateTOFBounds(
            origin.BodyPosition,
            destination.BodyPosition,
            shipVelocity.Length(),
            MaxAvailableDeltaV,
            mu
        );

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

        GameLogger.Info(
            $"TrajectoryPlanner.GetOptions: TOF bounds calculation - "
                + $"minTof={minTof:F2}s, maxTof={maxTof:F2}s, tofStep={tofStep:F2}s, numOptions={numOptions}"
        );

        // Generate Lambert solutions with per-ToF destination position prediction
        var options = new List<TrajectorySolution>();

        for (int i = 0; i < numOptions; i++)
        {
            float tof = minTof + (tofStep * i);

            Vector3 centralBodyPos = centralBody.BodyPosition;
            Vector3 centralBodyVel = centralBody.Velocity;

            Vector3 originPos = origin.BodyPosition;
            Vector3 originVel = origin.Velocity;

            (Vector3 destinationPos, Vector3 destinationVel) = KeplerianMechanics.PropagateKepler(
                destination.BodyPosition,
                destination.Velocity,
                mu,
                tof
            );

            Vector3 shipPosGlobal = unit.GlobalPosition;
            float arrivalTime = departureTime + tof;
            var (destPosGlobal, destVelocityRelative) = CalculateTargetOrbitBandState(
                destination,
                centralBody,
                resolvedTargetBand,
                mu,
                tof
            );
            Vector3 destPosRelative = destPosGlobal - centralBodyPos;

            try
            {
                var solutions = LambertSolver.Solve(
                    shipPosGlobal,
                    destinationPos,
                    tof,
                    mu,
                    revolutions: 2
                );

                GameLogger.Info(
                    $"TrajectoryPlanner.GetOptions: Lambert solver found {solutions.Count} solution(s)"
                );

                foreach (var solution in solutions)
                {
                    // Set the actual time of flight on the solution
                    solution.TimeOfFlight = tof;

                    // Set extended properties
                    solution.OriginBody = origin;
                    solution.DestinationBody = destination;
                    solution.DepartureTime = departureTime;
                    solution.GravitationalParameter = mu;
                    // Store global positions — ship's actual position, not planet center
                    solution.PredictedOriginPosition = shipPosGlobal;
                    solution.PredictedDestinationPosition = destinationPos;

                    // Store orbit band metadata
                    solution.OriginBandIndex = originBandIndex;
                    solution.DestinationBandIndex = resolvedTargetBand;

                    // Set orbital velocities and recalculate delta-v correctly:
                    // ΔV = |v_lambert_depart - v_ship| + |v_lambert_arrive - v_dest_band|
                    solution.OriginOrbitalVelocity = shipVelocity;
                    solution.DestinationOrbitalVelocity = destVelocityRelative;
                    solution.RecalculateDeltaV();

                    // Calculate fuel required (using corrected delta-v)
                    solution.CalculateFuelRequired(totalMass, exhaustVelocity);

                    // Derive the remaining classical orbital elements for the transfer orbit
                    var elements = KeplerianMechanics.DeriveClassicalElements(
                        shipPosGlobal,
                        solution.InitialVelocity,
                        mu
                    );
                    solution.SemiMajorAxis = elements.SemiMajorAxis;
                    solution.Eccentricity = elements.Eccentricity;
                    solution.Inclination = elements.InclinationDeg;
                    solution.AscendingNodeLongitude = elements.AscendingNodeLongitudeDeg;
                    solution.ArgumentOfPeriapsis = elements.ArgumentOfPeriapsisDeg;
                    solution.MeanAnomaly = elements.MeanAnomalyDeg;

                    options.Add(solution);
                    GameLogger.Info(
                        $"TrajectorySolution generated: TimeOfFlight {tof}, Origin {origin} Destination {destination}; PredictedOriginPosition {shipPosGlobal}"
                    );
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error in generating solutions: {e.Message}\n{e.StackTrace}");
                GameLogger.Error($"Error in generating solutions: {e.Message}\n{e.StackTrace}");
            }
        }

        // Sort by delta-v (lowest first) before filtering
        //options.Sort((a, b) => a.DeltaVRequired.CompareTo(b.DeltaVRequired));

        //// Filter by available delta-v
        float availableDeltaV = GetAvailableDeltaV(unit) * SafetyMargin;
        //options = FilterByAvailableDeltaV(options, availableDeltaV);

        if (options.Count == 0)
        {
            GameLogger.Warning(
                $"TrajectoryPlanner.GetOptions: No viable trajectory options within delta-v budget "
                    + $"of {availableDeltaV:F2} m/s"
            );
            return options;
        }

        // Calculate scores and rank
        //TrajectorySolution.CalculateScores(options);
        //options = TrajectorySolution.RankBy(options, rankingCriteria);

        GameLogger.Info(
            $"TrajectoryPlanner.GetOptions: Generated {options.Count} viable trajectory options, "
                + $"ranked by {rankingCriteria}"
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
    /// Calculates the position and velocity of a point in a destination body's orbit band
    /// at a given arrival time. This accounts for:
    /// 1. The planet's predicted position at arrival
    /// 2. The position within the orbit band (circular orbit around planet)
    /// 3. The velocity in the orbit band + planet's orbital velocity
    /// </summary>
    /// <param name="destinationBody">The destination celestial body.</param>
    /// <param name="centralBody">The central body for reference frame.</param>
    /// <param name="targetBandIndex">Target orbit band index.</param>
    /// <param name="arrivalTime">Time from now when the ship arrives.</param>
    /// <returns>Tuple of (global position, velocity relative to central body).</returns>
    private (Vector3 position, Vector3 velocity) CalculateTargetOrbitBandState(
        IOrbitalBody destinationBody,
        IOrbitalBody centralBody,
        int targetBandIndex,
        float mu,
        float tof
    )
    {
        Vector3 centralBodyVel = centralBody.Velocity;

        // 1. Get planet's predicted position at arrival
        //Vector3 planetPosGlobal = PredictBodyPosition(destinationBody, arrivalTime);
        (Vector3 planetPosGlobal, Vector3 planetVelGlobal) = KeplerianMechanics.PropagateKepler(
            destinationBody.BodyPosition,
            destinationBody.Velocity,
            mu,
            tof
        );
        Vector3 planetVelRelative = destinationBody.Velocity - centralBodyVel;

        // 2. Get orbit band radius and speed
        float bandRadius = destinationBody.GetOrbitBandRadius(targetBandIndex);
        float bandAngularSpeed = destinationBody.GetOrbitalSpeedForBand(targetBandIndex);

        // If band data is unavailable, fall back to planet center
        if (bandRadius <= 0f || bandAngularSpeed <= 0f)
        {
            GameLogger.Warning(
                $"TrajectoryPlanner.CalculateTargetOrbitBandState: "
                    + $"Could not get band {targetBandIndex} data for {destinationBody}, "
                    + $"falling back to planet center"
            );
            return (planetPosGlobal, planetVelRelative);
        }

        // 3. Calculate optimal arrival angle to minimize delta-v.
        // The optimal arrival direction is along the transfer trajectory.
        // We approximate this by choosing the angle on the destination side
        // of the orbit band that faces the origin (the central body approach direction).
        float arrivalAngle = CalculateOptimalArrivalAngle(
            planetPosGlobal,
            centralBody.BodyPosition,
            bandRadius
        );

        // 4. Calculate position in orbit band (circular orbit in XZ plane around planet)
        Vector3 bandOffset = new Vector3(
            Mathf.Cos(arrivalAngle) * bandRadius,
            0f,
            Mathf.Sin(arrivalAngle) * bandRadius
        );
        Vector3 bandPosGlobal = planetPosGlobal + bandOffset;

        // 5. Calculate velocity in orbit band (tangent to circular orbit)
        float bandLinearSpeed = bandRadius * bandAngularSpeed;
        Vector3 bandOrbitalVelocity = new Vector3(
            -bandLinearSpeed * Mathf.Sin(arrivalAngle),
            0f,
            bandLinearSpeed * Mathf.Cos(arrivalAngle)
        );

        // 6. Total velocity relative to central body: band orbital velocity + planet orbital velocity
        Vector3 totalVelocityRelative = bandOrbitalVelocity + planetVelRelative;

        GameLogger.Debug(
            $"TrajectoryPlanner.CalculateTargetOrbitBandState: "
                + $"band {targetBandIndex} r={bandRadius:F0}, ω={bandAngularSpeed:F3} rad/s, "
                + $"angle={arrivalAngle:F3} rad, "
                + $"band v={bandOrbitalVelocity.Length():F2} m/s, "
                + $"total v={totalVelocityRelative.Length():F2} m/s"
        );

        return (bandPosGlobal, totalVelocityRelative);
    }

    /// <summary>
    /// Calculates the optimal arrival angle in the destination orbit band
    /// to minimize delta-v. The optimal point is where the orbit band velocity
    /// is most aligned with the transfer trajectory direction.
    /// </summary>
    /// <param name="planetPos">Predicted planet position at arrival.</param>
    /// <param name="centralBodyPos">Central body position.</param>
    /// <param name="bandRadius">Orbit band radius.</param>
    /// <returns>Optimal arrival angle in radians.</returns>
    private float CalculateOptimalArrivalAngle(
        Vector3 planetPos,
        Vector3 centralBodyPos,
        float bandRadius
    )
    {
        // The approach direction from central body to destination planet (in XZ plane)
        Vector3 approachDir = (planetPos - centralBodyPos);
        approachDir.Y = 0f;

        if (approachDir.LengthSquared() < 1e-6f)
        {
            return 0f;
        }

        approachDir = approachDir.Normalized();

        // The optimal arrival point is where the orbital velocity (tangent to orbit)
        // is most aligned with the approach direction. For a prograde orbit,
        // velocity at angle θ is (-sin θ, 0, cos θ). We want this aligned with approachDir.
        // The angle where the tangent aligns with approach is:
        // θ = atan2(-approachDir.X, approachDir.Z)
        // But we actually want the arrival POSITION to be on the approach side,
        // so the ship flies "into" the orbit. The position angle that places
        // the orbital velocity tangent closest to the incoming transfer arc
        // direction is offset by π/2 from the approach direction.
        float approachAngle = Mathf.Atan2(approachDir.Z, approachDir.X);

        // Place arrival point 90° ahead of the approach direction (prograde matching)
        float arrivalAngle = approachAngle + Mathf.Pi / 2f;

        // Normalize to [0, 2π)
        arrivalAngle %= Mathf.Tau;
        if (arrivalAngle < 0f)
        {
            arrivalAngle += Mathf.Tau;
        }

        return arrivalAngle;
    }

    /// <summary>
    /// Predicts the future position of a celestial body at a given time from now.
    /// Uses simplified orbital propagation.
    /// </summary>
    /// <param name="body">The celestial body.</param>
    /// <param name="timeFromNow">Time in seconds from now.</param>
    /// <returns>Predicted position vector.</returns>
    private Vector3 PredictBodyPosition(IOrbitalBody body, float timeFromNow)
    {
        if (body == null)
        {
            return Vector3.Zero;
        }

        // Get current position
        Vector3 currentPos = body.BodyPosition;

        // If body has velocity, estimate future position
        // This is a simplified prediction - for accurate results, we'd use full orbital mechanics
        Vector3 velocity = body.Velocity;

        if (velocity.LengthSquared() > 0.001f)
        {
            // Linear prediction (sufficient for nearby bodies over short timescales)
            // For more accuracy, we'd integrate the orbital equation
            Vector3 predictedPos = currentPos + velocity * timeFromNow;

            GameLogger.Debug(
                $"TrajectoryPlanner.PredictBodyPosition: {body} at t+{timeFromNow:F1}s: "
                    + $"{currentPos} -> {predictedPos}"
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

    private (float, float) CalculateTOFBounds(
        Vector3 originPosition,
        Vector3 destinationPosition,
        float currentVelocity,
        float deltaV,
        float mu
    )
    {
        GameLogger.Info(
            $"TrajectoryPlanner.CalculateTOFBounds: Inputs - "
                + $"originPosition={originPosition}, destinationPosition={destinationPosition}, "
                + $"currentVelocity={currentVelocity:F2}m/s, deltaV={deltaV:F2}m/s, mu={mu:E2}"
        );

        float minTOF = Single.MaxValue;
        float maxTOF = Single.MinValue;
        Vector3 r1 = originPosition;
        Vector3 r2 = destinationPosition;
        float distance = r1.DistanceTo(r2);

        GameLogger.Info(
            $"TrajectoryPlanner.CalculateTOFBounds: Distance calculation - "
                + $"r1={r1}, r2={r2}, distance={distance:F2}m, |r1|={r1.Length():F2}m, |r2|={r2.Length():F2}m"
        );

        float vMax = currentVelocity + deltaV;
        if (vMax >= 1e-10f)
            minTOF = .5f * distance / vMax;

        GameLogger.Info(
            $"TrajectoryPlanner.CalculateTOFBounds: Minimum TOF calculation - "
                + $"vMax={vMax:F2}m/s, minTOF={minTOF:F2}s"
        );

        float r1Mag = originPosition.Length();
        float r2Mag = destinationPosition.Length();
        float aTransfer = (r1Mag + r2Mag) / 2f;
        float hohmannTOF = Mathf.Pi * Mathf.Sqrt(Mathf.Pow(aTransfer, 3) / (mu));
        maxTOF = 2f * hohmannTOF;

        GameLogger.Info(
            $"TrajectoryPlanner.CalculateTOFBounds: Maximum TOF (Hohmann-based) - "
                + $"r1Sun={r1Mag}, r2Sun={r2Mag}, aTransfer={aTransfer}, |aTransfer|={aTransfer:F2}m, "
                + $"hohmannTOF={hohmannTOF:F2}s, maxTOF={maxTOF:F2}s"
        );

        if (minTOF > maxTOF)
        {
            float originalMinTOF = minTOF;
            minTOF = maxTOF * .1f;
            GameLogger.Info(
                $"TrajectoryPlanner.CalculateTOFBounds: Adjusted minTOF - "
                    + $"originalMinTOF={originalMinTOF:F2}s > maxTOF, new minTOF={minTOF:F2}s"
            );
        }

        GameLogger.Info(
            $"TrajectoryPlanner.CalculateTOFBounds: Final bounds - minTOF={minTOF:F2}s, maxTOF={maxTOF:F2}s"
        );

        return (minTOF, maxTOF);
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
            $"TrajectoryPlanner.FilterByAvailableDeltaV: Filtered to {filtered.Count} options "
                + $"within budget of {availableDeltaV:F2} m/s"
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
            $"TrajectoryPlanner.GetAvailableDeltaV: {deltaV:F2} m/s available "
                + $"(mass: {totalMass:F2}kg, fuel: {fuelMass:F2}kg, dry: {dryMass:F2}kg, "
                + $"exhaust: {exhaustVelocity:F2}m/s)"
        );

        return deltaV;
    }
}
