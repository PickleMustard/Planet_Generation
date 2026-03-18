using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;

namespace UtilityLibrary;

public static class OrbitalMath
{
    public const float GRAVITATIONAL_CONSTANT = 6.7394967f;

    public static Vector3 CalculateOrbitalPosition(
        Vector3 pHat,
        Vector3 qHat,
        float apogee,
        float perigee,
        float angle,
        float eccentricity = 0f
    )
    {
        float semiMajorAxis = (apogee + perigee) / 2f;
        float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(1f - eccentricity * eccentricity);
        float focalDistance = semiMajorAxis * eccentricity;

        float radius =
            semiMajorAxis
            * (1 - eccentricity * eccentricity)
            / (1 + eccentricity * Mathf.Cos(angle));
        Vector3 resultVector = radius * Mathf.Cos(angle) * pHat + radius * Mathf.Sin(angle) * qHat;
        return resultVector;
    }

    public static Vector3 CalculateOrbitalVelocity(
        float centralMass,
        Vector3 position,
        bool clockwise = false
    )
    {
        float distance = position.Length();
        if (distance <= 0f)
            return Vector3.Zero;

        float orbitalSpeed = Mathf.Sqrt(GRAVITATIONAL_CONSTANT * centralMass / distance);
        Vector3 tangentDirection = new Vector3(-position.Z, 0, position.X).Normalized();

        if (!clockwise)
            tangentDirection = -tangentDirection;

        return tangentDirection * orbitalSpeed;
    }

    public static float CalculateEccentricity(float apogee, float perigee)
    {
        float denominator = apogee + perigee;
        if (denominator <= 0f)
            return 1f; // Default to circular orbit to avoid division by zero

        return (apogee - perigee) / denominator;
    }

    public static (Vector3, Vector3) CalculateOrbitalFrame(
        Vector3 pHat,
        Vector3 qHat,
        float apogee,
        float perigee,
        float angle,
        float massA,
        float massB
    )
    {
        float massTotal = massB + massA;
        Vector3 nHat = pHat.Cross(qHat).Normalized();
        float semiMajorAxis = (apogee + perigee) / 2f;
        float eccentricity = CalculateEccentricity(apogee, perigee);
        float angularSpeed = Mathf.Sqrt(
            GRAVITATIONAL_CONSTANT * (massTotal) / (semiMajorAxis * semiMajorAxis * semiMajorAxis)
        );
        //Vector3 angularVelocity = angularSpeed * nHat;
        float oneMinusESq = 1f - eccentricity * eccentricity;
        float hSquared = GRAVITATIONAL_CONSTANT * massTotal * semiMajorAxis * oneMinusESq;

        float constants = (GRAVITATIONAL_CONSTANT * massTotal) / Mathf.Sqrt(hSquared);
        Vector3 angularVelocity =
            angularSpeed * (-Mathf.Sin(angle) * pHat + (eccentricity + Mathf.Cos(angle)) * qHat);
        float separation =
            (semiMajorAxis * (1f - eccentricity * eccentricity))
            / (1f + eccentricity * Mathf.Cos(angle));
        Vector3 position =
            separation * (massB / massTotal) * (Mathf.Cos(angle) * pHat + Mathf.Sin(angle) * qHat);
        return (position, angularVelocity);
    }

    /// <summary>
    /// Calculates orbital velocity at a specific position on an elliptical orbit.
    /// Uses the vis-viva equation: v² = GM(2/r - 1/a)
    /// </summary>
    /// <param name="centralMass">Mass of the parent body</param>
    /// <param name="apogee">Farthest point from parent</param>
    /// <param name="perigee">Closest point to parent</param>
    /// <param name="position">Current position of the satellite</param>
    /// <param name="clockwise">Whether orbit is clockwise (default: false = counter-clockwise)</param>
    /// <returns>Orbital velocity vector at the given position</returns>
    public static Vector3 CalculateEllipticalOrbitalVelocity(
        Vector3 pHat,
        Vector3 qHat,
        float centralMass,
        float apogee,
        float perigee,
        float angle,
        bool clockwise = false
    )
    {
        float eccentricity = CalculateEccentricity(apogee, perigee);
        float semiMajorAxis = apogee / (1.0f + eccentricity);

        if (semiMajorAxis <= 0f)
        {
            GameLogger.Warning(
                $"OrbitalMath.CalculateEllipticalOrbitalVelocity: Invalid semi-major axis {semiMajorAxis}, returning zero velocity"
            );
            return Vector3.Zero;
        }

        // Guard: centralMass must be positive to avoid 0/sqrt(0) = NaN
        if (centralMass <= 0f)
        {
            GameLogger.Warning(
                $"OrbitalMath.CalculateEllipticalOrbitalVelocity: Non-positive centralMass {centralMass}, returning zero velocity"
            );
            return Vector3.Zero;
        }

        float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(1f - eccentricity * eccentricity);
        float focalDistance = semiMajorAxis * eccentricity;

        float radius =
            semiMajorAxis
            * (1 - eccentricity * eccentricity)
            / (1 + eccentricity * Mathf.Cos(angle));
        Vector3 resultVector = radius * Mathf.Cos(angle) * pHat + radius * Mathf.Sin(angle) * qHat;

        // Specific angular momentum: h = sqrt(G * M * a * (1 - e²))
        // Velocity constant: mu / h = (G * M) / sqrt(G * M * a * (1 - e²))
        float oneMinusESq = 1f - eccentricity * eccentricity;
        float hSquared = GRAVITATIONAL_CONSTANT * centralMass * semiMajorAxis * oneMinusESq;
        if (hSquared <= 0f)
        {
            GameLogger.Warning(
                $"OrbitalMath.CalculateEllipticalOrbitalVelocity: Non-positive h² ({hSquared}), returning zero velocity"
            );
            return Vector3.Zero;
        }

        float constants = (GRAVITATIONAL_CONSTANT * centralMass) / Mathf.Sqrt(hSquared);
        Vector3 resultantVector =
            constants * (-Mathf.Sin(angle) * pHat + (eccentricity + Mathf.Cos(angle)) * qHat);
        return clockwise ? resultantVector : -resultantVector;
    }

    /// <summary>
    /// Solves Lambert's problem to find transfer trajectories between two positions.
    /// Returns all feasible solutions including multi-revolution left/right branches.
    /// For 0 revolutions: 1 solution.
    /// For each revolution N (1..maxRevolutions): up to 2 solutions (low-path and high-path).
    /// </summary>
    /// <param name="r1">Start position vector</param>
    /// <param name="r2">End position vector</param>
    /// <param name="tof">Time of flight in seconds</param>
    /// <param name="mu">Gravitational parameter (m³/s²)</param>
    /// <param name="maxRevolutions">Maximum number of complete revolutions to consider</param>
    /// <param name="retrograde">True for retrograde orbit, false for prograde</param>
    /// <returns>List of all feasible TrajectorySolution objects</returns>
    public static List<TrajectorySolution> SolveLambert(
        Vector3 r1,
        Vector3 r2,
        float tof,
        float mu,
        int maxRevolutions = 0,
        bool retrograde = false
    )
    {
        // prograde = !retrograde
        return LambertSolver.SolveAll(r1, r2, tof, mu, !retrograde, maxRevolutions);
    }

    /// <summary>
    /// Generates multiple trajectory transfer options between start and end positions
    /// for various time-of-flight values. Returns a list of TrajectorySolution.
    /// </summary>
    /// <param name="r1">Start position vector</param>
    /// <param name="r2">End position vector</param>
    /// <param name="mu">Gravitational parameter (m³/s²)</param>
    /// <param name="minTof">Minimum time of flight in seconds</param>
    /// <param name="maxTof">Maximum time of flight in seconds</param>
    /// <param name="numOptions">Number of trajectory options to generate</param>
    /// <param name="retrograde">True for retrograde orbit, false for prograde</param>
    /// <param name="maxRevolutions">Maximum number of complete revolutions to consider per TOF</param>
    /// <returns>List of TrajectorySolution for various time-of-flight values</returns>
    public static List<TrajectorySolution> GetTrajectoryOptions(
        Vector3 r1,
        Vector3 r2,
        float mu,
        float minTof,
        float maxTof,
        int numOptions = 5,
        bool retrograde = false,
        int maxRevolutions = 0
    )
    {
        var options = new List<TrajectorySolution>();

        if (numOptions <= 0)
        {
            GameLogger.Warning(
                "OrbitalMath.GetTrajectoryOptions: numOptions must be positive, returning empty list"
            );
            return options;
        }

        if (minTof <= 0f || maxTof <= 0f || maxTof < minTof)
        {
            GameLogger.Warning(
                "OrbitalMath.GetTrajectoryOptions: Invalid TOF range, using default range"
            );
            minTof = 100f;
            maxTof = 1000f;
        }

        // Handle single-option case to avoid division by zero
        float tofStep;
        if (numOptions == 1)
        {
            tofStep = 0f;
            minTof = (minTof + maxTof) / 2f; // Use midpoint for single option
        }
        else
        {
            // Generate time-of-flight values across the range
            tofStep = (maxTof - minTof) / (numOptions - 1);
        }

        for (int i = 0; i < numOptions; i++)
        {
            float tof = minTof + (tofStep * i);

            // Solve Lambert's problem for this time of flight
            var solutions = SolveLambert(r1, r2, tof, mu, maxRevolutions, retrograde);

            // Add all solutions from different revolution counts
            foreach (var solution in solutions)
            {
                // Set the actual time of flight on the solution
                solution.TimeOfFlight = tof;
                options.Add(solution);
            }
        }

        // Sort by DeltaVRequired (lowest delta-v first)
        options.Sort((a, b) => a.DeltaVRequired.CompareTo(b.DeltaVRequired));

        return options;
    }

    /// <summary>
    /// Propagates a position and velocity forward in time using Keplerian orbital mechanics.
    /// Uses the solution of Kepler's equation to determine position at a specified time.
    /// </summary>
    /// <param name="positionAtT0">Position vector at time t=0</param>
    /// <param name="velocityAtT0">Velocity vector at time t=0</param>
    /// <param name="mu">Gravitational parameter (μ = GM)</param>
    /// <param name="deltaTime">Time increment to propagate forward</param>
    /// <returns>Position vector at time t=deltaTime</returns>
    public static Vector3 GetPositionOnOrbit(
        Vector3 positionAtT0,
        Vector3 velocityAtT0,
        float mu,
        float deltaTime
    )
    {
        // Validate inputs
        float r = positionAtT0.Length();
        if (r <= 0f)
        {
            GameLogger.Error("OrbitalMath.GetPositionOnOrbit: Initial position has zero length");
            return Vector3.Zero;
        }

        if (mu <= 0f)
        {
            GameLogger.Error("OrbitalMath.GetPositionOnOrbit: Invalid gravitational parameter mu");
            return Vector3.Zero;
        }

        // Calculate specific angular momentum: h = r × v
        Vector3 angularMomentum = positionAtT0.Cross(velocityAtT0);
        float hMag = angularMomentum.Length();

        if (hMag <= 1e-10f)
        {
            GameLogger.Error(
                "OrbitalMath.GetPositionOnOrbit: Orbit degenerate (collinear position and velocity)"
            );
            return Vector3.Zero;
        }

        // Calculate velocity squared
        float vSq = velocityAtT0.LengthSquared();

        // Calculate eccentricity vector: e = ((v² - μ/r)r - (r·v)v) / μ
        float rvDot = positionAtT0.Dot(velocityAtT0);
        Vector3 term1 = positionAtT0 * (vSq - mu / r);
        Vector3 term2 = velocityAtT0 * rvDot;
        Vector3 eccentricityVector = (term1 - term2) / mu;
        float eccentricity = eccentricityVector.Length();

        // Handle near-circular orbits
        if (eccentricity < 1e-10f)
        {
            eccentricity = 0f;
            eccentricityVector = Vector3.Zero;
        }

        // Calculate semi-major axis using vis-viva equation: a = μ / (2μ/r - v²)
        float semiMajorAxis = mu / (2f * mu / r - vSq);

        // Handle parabolic/hyperbolic orbits (non-elliptical)
        if (semiMajorAxis <= 0f)
        {
            GameLogger.Warning(
                "OrbitalMath.GetPositionOnOrbit: Non-elliptical orbit detected, using simplified propagation"
            );
            return PropagateHyperbolicOrbit(positionAtT0, velocityAtT0, mu, deltaTime);
        }

        // Calculate mean motion: n = √(μ/a³)
        float meanMotion = Mathf.Sqrt(mu / Mathf.Pow(semiMajorAxis, 3));

        // Calculate eccentric anomaly at t=0 using the eccentric anomaly formula
        float eccentricAnomaly = CalculateEccentricAnomaly(
            positionAtT0,
            velocityAtT0,
            semiMajorAxis,
            mu,
            eccentricity
        );

        // Calculate mean anomaly: M = E - e*sin(E)
        float meanAnomaly = eccentricAnomaly - eccentricity * Mathf.Sin(eccentricAnomaly);

        // Propagate mean anomaly to new time
        meanAnomaly += meanMotion * deltaTime;

        // Normalize mean anomaly to [0, 2π]
        meanAnomaly = NormalizeAngle(meanAnomaly);

        // Solve Kepler's equation M = E - e*sin(E) for eccentric anomaly using Newton-Raphson
        float newEccentricAnomaly = SolveKeplerEquation(meanAnomaly, eccentricity);

        // Calculate radius at new position: r = a(1 - e*cos(E))
        float newRadius = semiMajorAxis * (1f - eccentricity * Mathf.Cos(newEccentricAnomaly));

        // Convert eccentric anomaly to true anomaly
        float cosE = Mathf.Cos(newEccentricAnomaly);
        float sinE = Mathf.Sin(newEccentricAnomaly);
        float trueAnomaly = Mathf.Atan2(
            Mathf.Sqrt(1f - eccentricity * eccentricity) * sinE,
            cosE - eccentricity
        );

        // Calculate position in orbital plane
        Vector3 orbitNormal = angularMomentum.Normalized();
        Vector3 eccentricityDirection;

        if (eccentricity > 1e-10f)
        {
            eccentricityDirection = eccentricityVector.Normalized();
        }
        else
        {
            // For circular orbits, use any vector perpendicular to angular momentum
            eccentricityDirection = orbitNormal.Cross(Vector3.Up).Normalized();
            if (eccentricityDirection.LengthSquared() < 1e-10f)
            {
                eccentricityDirection = orbitNormal.Cross(Vector3.Right).Normalized();
            }
        }

        // Calculate third basis vector (perpendicular to e and h)
        Vector3 thirdVector = orbitNormal.Cross(eccentricityDirection).Normalized();

        // Position in orbital plane: r = r*cos(ν)*ê + r*sin(ν)*q̂
        Vector3 newPosition =
            eccentricityDirection * (newRadius * Mathf.Cos(trueAnomaly))
            + thirdVector * (newRadius * Mathf.Sin(trueAnomaly));

        return newPosition;
    }

    /// <summary>
    /// Calculates the eccentric anomaly from position and velocity vectors.
    /// </summary>
    private static float CalculateEccentricAnomaly(
        Vector3 position,
        Vector3 velocity,
        float semiMajorAxis,
        float mu,
        float eccentricity
    )
    {
        if (eccentricity < 1e-10f)
        {
            // For circular orbits, calculate true anomaly directly
            float radius = position.Length();
            float trueAnomaly = Mathf.Atan2(
                position.Dot(Vector3.Right),
                position.Dot(Vector3.Forward)
            );
            return trueAnomaly;
        }

        // Calculate eccentricity vector direction
        float r = position.Length();
        float vSq = velocity.LengthSquared();
        float rvDot = position.Dot(velocity);

        Vector3 term1 = position * (vSq - mu / r);
        Vector3 term2 = velocity * rvDot;
        Vector3 eccentricityVector = (term1 - term2) / mu;

        // Calculate the eccentric anomaly using the dot product
        float cosE =
            position.Normalized().Dot((position - eccentricityVector).Normalized())
            / (1f - eccentricity);
        cosE = Mathf.Clamp(cosE, -1f, 1f);
        float eccentricAnomaly = Mathf.Acos(cosE);

        // Check if we need to adjust based on velocity direction
        Vector3 rPlusE = position + eccentricityVector;
        if (rPlusE.Dot(velocity) < 0f)
        {
            eccentricAnomaly = 2f * Mathf.Pi - eccentricAnomaly;
        }

        return eccentricAnomaly;
    }

    /// <summary>
    /// Solves Kepler's equation M = E - e*sin(E) using Newton-Raphson iteration.
    /// </summary>
    private static float SolveKeplerEquation(float meanAnomaly, float eccentricity)
    {
        // Handle negative mean anomaly
        if (meanAnomaly < 0f)
        {
            meanAnomaly = 2f * Mathf.Pi + (meanAnomaly % (2f * Mathf.Pi));
        }

        // Normalize to [0, 2π]
        meanAnomaly = meanAnomaly % (2f * Mathf.Pi);

        // Initial guess: E ≈ M for small eccentricity
        float eccentricAnomaly = meanAnomaly;

        // Newton-Raphson iteration
        const int maxIterations = 50;
        const float tolerance = 1e-8f;

        for (int i = 0; i < maxIterations; i++)
        {
            float f = eccentricAnomaly - eccentricity * Mathf.Sin(eccentricAnomaly) - meanAnomaly;
            float fPrime = 1f - eccentricity * Mathf.Cos(eccentricAnomaly);

            if (Mathf.Abs(fPrime) < 1e-10f)
            {
                break; // Avoid division by zero
            }

            float delta = f / fPrime;
            eccentricAnomaly -= delta;

            if (Mathf.Abs(delta) < tolerance)
            {
                break;
            }
        }

        return eccentricAnomaly;
    }

    /// <summary>
    /// Propagates a hyperbolic orbit (non-elliptical) using simplified approach.
    /// </summary>
    private static Vector3 PropagateHyperbolicOrbit(
        Vector3 positionAtT0,
        Vector3 velocityAtT0,
        float mu,
        float deltaTime
    )
    {
        // For hyperbolic orbits, use a simpler propagation based on position delta
        // This is an approximation that works for small time steps
        float r = positionAtT0.Length();
        Vector3 radialDirection = positionAtT0.Normalized();

        // Calculate approximate acceleration
        float aMag = mu / (r * r);
        Vector3 acceleration = -radialDirection * aMag;

        // Simple Euler integration (first-order approximation)
        Vector3 newPosition =
            positionAtT0 + velocityAtT0 * deltaTime + 0.5f * acceleration * deltaTime * deltaTime;

        return newPosition;
    }

    /// <summary>
    /// Normalizes an angle to [0, 2π].
    /// </summary>
    private static float NormalizeAngle(float angle)
    {
        angle = angle % (2f * Mathf.Pi);
        if (angle < 0f)
        {
            angle += 2f * Mathf.Pi;
        }
        return angle;
    }

    /// <summary>
    /// Detects gravity assist opportunities between a start and end position using available celestial bodies.
    /// A gravity assist opportunity exists when a celestial body can provide a significant trajectory
    /// deflection that reduces the total delta-v required for the journey.
    /// </summary>
    /// <param name="startPos">Starting position vector in meters.</param>
    /// <param name="endPos">Ending position vector in meters.</param>
    /// <param name="bodies">List of available celestial bodies that can be used for gravity assists.</param>
    /// <param name="maxTimeOfFlight">Maximum time of flight in seconds to consider for assist timing.</param>
    /// <returns>List of GravityAssistOpportunity structs representing viable gravity assist options.</returns>
    public static List<GravityAssistOpportunity> DetectGravityAssists(
        Vector3 startPos,
        Vector3 endPos,
        List<CelestialBody> bodies,
        float maxTimeOfFlight
    )
    {
        var opportunities = new List<GravityAssistOpportunity>();

        if (bodies == null || bodies.Count == 0)
        {
            GameLogger.Debug("OrbitalMath.DetectGravityAssists: No celestial bodies provided");
            return opportunities;
        }

        if (maxTimeOfFlight <= 0f)
        {
            GameLogger.Warning(
                "OrbitalMath.DetectGravityAssists: Invalid maxTimeOfFlight, must be positive"
            );
            return opportunities;
        }

        // Calculate the direct trajectory direction
        Vector3 directVector = endPos - startPos;
        float totalDistance = directVector.Length();

        if (totalDistance <= 0f)
        {
            GameLogger.Warning(
                "OrbitalMath.DetectGravityAssists: Start and end positions are identical"
            );
            return opportunities;
        }

        Vector3 directDirection = directVector.Normalized();

        // Evaluate each celestial body for gravity assist potential
        foreach (CelestialBody body in bodies)
        {
            if (body == null)
                continue;

            Vector3 bodyPosition = body.GlobalPosition;

            // Calculate the minimum distance from the body to the direct trajectory line
            Vector3 toBody = bodyPosition - startPos;
            float projectionDistance = toBody.Dot(directDirection);

            // Only consider bodies that are between start and end positions (within the flight path)
            if (projectionDistance < 0f || projectionDistance > totalDistance)
                continue;

            // Calculate perpendicular distance from body to direct trajectory
            Vector3 projectedPoint = startPos + directDirection * projectionDistance;
            float perpendicularDistance = (bodyPosition - projectedPoint).Length();

            // Get the body's gravitational influence radius (sphere of influence)
            float soiRadius = GetSphereOfInfluence(body);

            // Check if the body is within reasonable range of the trajectory
            if (perpendicularDistance > soiRadius * 2f)
                continue;

            // Calculate approach time based on position in trajectory
            float approachTime = projectionDistance / 1000f; // Approximate velocity of 1 km/s
            approachTime = Mathf.Clamp(approachTime, 0f, maxTimeOfFlight);

            // Calculate gravity assist parameters
            float bodyMass = body.Mass;
            float gravitationalParameter = GRAVITATIONAL_CONSTANT * bodyMass;
            float bodySize = body.Mesh?.size ?? 1000f;

            // Calculate the deflection angle based on closest approach distance
            float closestApproach = Mathf.Max(perpendicularDistance, bodySize * 2f);
            float deflectionAngle = CalculateDeflectionAngle(
                gravitationalParameter,
                closestApproach,
                1000f
            );

            // Calculate potential delta-v savings
            float velocityMagnitude = 1000f; // Approximate velocity
            float deltaVSavings = CalculateDeltaVSavings(velocityMagnitude, deflectionAngle);

            // Calculate exit velocity after gravity assist
            Vector3 exitVelocity = CalculateExitVelocity(
                directDirection,
                deflectionAngle,
                velocityMagnitude
            );

            // Only add if there's meaningful delta-v savings
            if (deltaVSavings > 10f) // Minimum 10 m/s savings
            {
                var opportunity = new GravityAssistOpportunity(
                    body,
                    approachTime,
                    deltaVSavings,
                    deflectionAngle,
                    exitVelocity
                );
                opportunities.Add(opportunity);

                GameLogger.Debug(
                    $"OrbitalMath.DetectGravityAssists: Found assist - Body: {body.Name}, "
                        + $"Distance: {perpendicularDistance:F0}m, Deflection: {Mathf.RadToDeg(deflectionAngle):F1}°, "
                        + $"ΔV Savings: {deltaVSavings:F2} m/s"
                );
            }
        }

        // Sort by delta-v savings (highest savings first)
        opportunities.Sort((a, b) => b.DeltaVSavings.CompareTo(a.DeltaVSavings));

        return opportunities;
    }

    /// <summary>
    /// Gets the sphere of influence radius for a celestial body.
    /// The SOI is the region around a body where its gravity dominates over other bodies.
    /// </summary>
    /// <param name="body">The celestial body.</param>
    /// <returns>The sphere of influence radius in meters.</returns>
    private static float GetSphereOfInfluence(CelestialBody body)
    {
        // Approximate SOI using Hill sphere formula: r = a * (m/M)^(1/3)
        // where a is semi-major axis, m is body mass, M is parent mass
        // For simplicity, we use a scaled value based on body mesh size
        float bodySize = body.Mesh?.size ?? 1000f;
        float bodyMass = body.Mass;

        if (bodyMass <= 0f || bodySize <= 0f)
            return 10000f; // Default 10km SOI

        // Simplified SOI calculation - scales with mass^(1/3) * size
        float soi = Mathf.Pow(bodyMass, 1f / 3f) * bodySize * 10f;
        return Mathf.Max(soi, bodySize * 5f); // Minimum 5x body size
    }

    /// <summary>
    /// Calculates the trajectory deflection angle achievable from a gravity assist.
    /// </summary>
    /// <param name="gravitationalParameter">Gravitational parameter (μ = GM) in m³/s².</param>
    /// <param name="closestApproach">Closest approach distance in meters.</param>
    /// <param name="velocity">Incoming velocity in m/s.</param>
    /// <returns>Deflection angle in radians.</returns>
    private static float CalculateDeflectionAngle(
        float gravitationalParameter,
        float closestApproach,
        float velocity
    )
    {
        if (closestApproach <= 0f || velocity <= 0f)
            return 0f;

        // Deflection angle formula: δ = 2 * arcsin(μ / (μ + v² * rp))
        // where rp is closest approach distance
        float vSquared = velocity * velocity;
        float muPlusVrSquared = gravitationalParameter + vSquared * closestApproach;

        if (muPlusVrSquared <= 0f)
            return 0f;

        float sinHalfAngle = gravitationalParameter / muPlusVrSquared;
        sinHalfAngle = Mathf.Clamp(sinHalfAngle, -1f, 1f);
        float deflectionAngle = 2f * Mathf.Asin(sinHalfAngle);

        return Mathf.Clamp(deflectionAngle, 0f, Mathf.Pi);
    }

    /// <summary>
    /// Calculates the delta-v savings from a gravity assist based on deflection angle.
    /// </summary>
    /// <param name="velocity">Incoming velocity in m/s.</param>
    /// <param name="deflectionAngle">Deflection angle in radians.</param>
    /// <returns>Delta-v savings in m/s.</returns>
    private static float CalculateDeltaVSavings(float velocity, float deflectionAngle)
    {
        if (deflectionAngle <= 0f || velocity <= 0f)
            return 0f;

        // Delta-v from gravity assist: Δv = 2 * v * sin(δ/2)
        // This represents the velocity change from the gravity turn
        float deltaV = 2f * velocity * Mathf.Sin(deflectionAngle / 2f);
        return deltaV;
    }

    /// <summary>
    /// Calculates the exit velocity vector after a gravity assist.
    /// </summary>
    /// <param name="incomingDirection">Incoming velocity direction.</param>
    /// <param name="deflectionAngle">Deflection angle in radians.</param>
    /// <param name="velocityMagnitude">Velocity magnitude in m/s.</param>
    /// <returns>Exit velocity vector.</returns>
    private static Vector3 CalculateExitVelocity(
        Vector3 incomingDirection,
        float deflectionAngle,
        float velocityMagnitude
    )
    {
        if (deflectionAngle <= 0f)
            return incomingDirection * velocityMagnitude;

        // Calculate a perpendicular vector for the deflection
        Vector3 up = Vector3.Up;
        if (Mathf.Abs(incomingDirection.Dot(up)) > 0.99f)
        {
            up = Vector3.Right;
        }

        Vector3 perpendicular = incomingDirection.Cross(up).Normalized();
        if (perpendicular.LengthSquared() < 0.01f)
        {
            return incomingDirection * velocityMagnitude;
        }

        // Apply the deflection - rotate the velocity vector by the deflection angle
        // around the perpendicular axis
        float cosAngle = Mathf.Cos(deflectionAngle);
        float sinAngle = Mathf.Sin(deflectionAngle);

        // Rotate in the plane defined by incoming direction and perpendicular
        Vector3 componentAlongPerp = perpendicular * Mathf.Sin(deflectionAngle);
        Vector3 componentAlongIncoming = incomingDirection * cosAngle;

        Vector3 newDirection = (componentAlongIncoming + componentAlongPerp).Normalized();
        return newDirection * velocityMagnitude;
    }

    // ============================================================================
    // Phase 1: Most Influential Body Logic
    // ============================================================================

    private const float COMPETITIVE_THRESHOLD = 10f; // One order of magnitude

    /// <summary>
    /// Calculates the system center point (barycenter) based on gravitational influence.
    /// If one body is significantly more influential than all others, returns that body alone.
    /// If multiple bodies are competitive (within threshold), calculates mass-weighted center.
    /// </summary>
    /// <param name="bodies">List of celestial bodies to analyze</param>
    /// <returns>Tuple containing: center point position, total mass for orbital calculations,
    ///          and list of indices of dominant/competitive bodies</returns>
    public static (
        Vector3 centerPoint,
        float totalMass,
        List<int> dominantIndices
    ) CalculateSystemCenter(List<CelestialBody> bodies)
    {
        if (bodies == null || bodies.Count == 0)
            return (Vector3.Zero, 0f, new List<int>());

        if (bodies.Count == 1)
            return (bodies[0].GlobalPosition, bodies[0].Mass, new List<int> { 0 });

        // Calculate total gravitational influence for each body
        // Influence = sum of (G * other_mass / distance^2) for all other bodies
        List<float> totalInfluences = new List<float>(bodies.Count);

        for (int i = 0; i < bodies.Count; i++)
        {
            float influence = 0f;
            Vector3 posI = bodies[i].GlobalPosition;

            for (int j = 0; j < bodies.Count; j++)
            {
                if (i == j)
                    continue;

                Vector3 posJ = bodies[j].GlobalPosition;
                float distSq = posI.DistanceSquaredTo(posJ);

                if (distSq > 0.001f) // Avoid division by zero
                {
                    influence += GRAVITATIONAL_CONSTANT * bodies[j].Mass / distSq;
                }
            }

            totalInfluences.Add(influence);
        }

        // Find maximum influence and identify competitive bodies
        float maxInfluence = 0f;
        foreach (float inf in totalInfluences)
        {
            if (inf > maxInfluence)
                maxInfluence = inf;
        }

        List<int> competitiveIndices = new List<int>();
        for (int i = 0; i < bodies.Count; i++)
        {
            if (totalInfluences[i] >= maxInfluence / COMPETITIVE_THRESHOLD)
            {
                competitiveIndices.Add(i);
            }
        }

        // Calculate barycenter for competitive bodies
        Vector3 centerPoint = Vector3.Zero;
        float totalMass = 0f;

        if (competitiveIndices.Count == 1)
        {
            // Single dominant body
            int idx = competitiveIndices[0];
            centerPoint = bodies[idx].GlobalPosition;
            totalMass = bodies[idx].Mass;
        }
        else
        {
            // Multiple competitive bodies - calculate barycenter
            foreach (int idx in competitiveIndices)
            {
                float mass = bodies[idx].Mass;
                centerPoint += bodies[idx].GlobalPosition * mass;
                totalMass += mass;
            }

            if (totalMass > 0f)
            {
                centerPoint /= totalMass;
            }
        }

        return (centerPoint, totalMass, competitiveIndices);
    }

    /// <summary>
    /// Calculates the system center from Dictionary-based body data.
    /// Expected Dictionary keys: "position" (Vector3), "mass" (float)
    /// </summary>
    public static (
        Vector3 centerPoint,
        float totalMass,
        List<int> dominantIndices
    ) CalculateSystemCenterFromDicts(
        Godot.Collections.Array<Godot.Collections.Dictionary> bodyDicts
    )
    {
        if (bodyDicts == null || bodyDicts.Count == 0)
            return (Vector3.Zero, 0f, new List<int>());

        if (bodyDicts.Count == 1)
        {
            Vector3 pos = bodyDicts[0]["position"].AsVector3();
            float mass = bodyDicts[0]["mass"].AsSingle();
            return (pos, mass, new List<int> { 0 });
        }

        // Convert to position/mass arrays for calculation
        List<Vector3> positions = new List<Vector3>();
        List<float> masses = new List<float>();

        foreach (var dict in bodyDicts)
        {
            positions.Add(dict["position"].AsVector3());
            masses.Add(dict["mass"].AsSingle());
        }

        // Calculate total gravitational influence for each body
        List<float> totalInfluences = new List<float>(bodyDicts.Count);

        for (int i = 0; i < bodyDicts.Count; i++)
        {
            float influence = 0f;

            for (int j = 0; j < bodyDicts.Count; j++)
            {
                if (i == j)
                    continue;

                float distSq = positions[i].DistanceSquaredTo(positions[j]);

                if (distSq > 0.001f)
                {
                    influence += GRAVITATIONAL_CONSTANT * masses[j] / distSq;
                }
            }

            totalInfluences.Add(influence);
        }

        // Find maximum influence
        float maxInfluence = 0f;
        foreach (float inf in totalInfluences)
        {
            if (inf > maxInfluence)
                maxInfluence = inf;
        }

        // Identify competitive bodies
        List<int> competitiveIndices = new List<int>();
        for (int i = 0; i < bodyDicts.Count; i++)
        {
            if (totalInfluences[i] >= maxInfluence / COMPETITIVE_THRESHOLD)
            {
                competitiveIndices.Add(i);
            }
        }

        // Calculate barycenter
        Vector3 centerPoint = Vector3.Zero;
        float totalMass = 0f;

        if (competitiveIndices.Count == 1)
        {
            int idx = competitiveIndices[0];
            centerPoint = positions[idx];
            totalMass = masses[idx];
        }
        else
        {
            foreach (int idx in competitiveIndices)
            {
                float mass = masses[idx];
                centerPoint += positions[idx] * mass;
                totalMass += mass;
            }

            if (totalMass > 0f)
            {
                centerPoint /= totalMass;
            }
        }

        return (centerPoint, totalMass, competitiveIndices);
    }

    /// <summary>
    /// Finds the index of the most gravitationally influential body relative to a test position.
    /// Uses the test position to calculate influence from each body.
    /// </summary>
    /// <param name="testPosition">Position to calculate influence from</param>
    /// <param name="bodies">List of celestial bodies</param>
    /// <returns>Index of the most influential body, or -1 if none found</returns>
    public static int GetMostInfluentialBodyIndex(
        Vector3 testPosition,
        Godot.Collections.Array<CelestialBody> bodies
    )
    {
        if (bodies == null || bodies.Count == 0)
            return -1;

        float maxInfluence = 0f;
        int dominantIndex = -1;

        for (int i = 0; i < bodies.Count; i++)
        {
            float distanceSq = testPosition.DistanceSquaredTo(bodies[i].GlobalPosition);

            if (distanceSq > 0.001f)
            {
                float influence = GRAVITATIONAL_CONSTANT * bodies[i].Mass / distanceSq;

                if (influence > maxInfluence)
                {
                    maxInfluence = influence;
                    dominantIndex = i;
                }
            }
        }

        return dominantIndex;
    }

    /// <summary>
    /// Finds the index of the most gravitationally influential body relative to a test position.
    /// Dictionary format: "position" (Vector3), "mass" (float)
    /// </summary>
    public static int GetMostInfluentialBodyIndex(
        Vector3 testPosition,
        Godot.Collections.Array<Godot.Collections.Dictionary> bodyDicts
    )
    {
        if (bodyDicts == null || bodyDicts.Count == 0)
            return -1;

        float maxInfluence = 0f;
        int dominantIndex = -1;

        for (int i = 0; i < bodyDicts.Count; i++)
        {
            Vector3 pos = bodyDicts[i]["position"].AsVector3();
            float distanceSq = testPosition.DistanceSquaredTo(pos);

            if (distanceSq > 0.001f)
            {
                float mass = bodyDicts[i]["mass"].AsSingle();
                float influence = GRAVITATIONAL_CONSTANT * mass / distanceSq;

                if (influence > maxInfluence)
                {
                    maxInfluence = influence;
                    dominantIndex = i;
                }
            }
        }

        return dominantIndex;
    }

    /// <summary>
    /// Calculates orbital position and velocity from orbital parameters using standard orbital mechanics.
    /// Uses the vis-viva equation to compute speed and constructs the velocity vector in the
    /// perifocal frame, then rotates both position and velocity into the inertial reference frame
    /// using the classical orbital element Euler angles (Ω, i, ω).
    /// </summary>
    /// <param name="apogee">Farthest point from orbital center</param>
    /// <param name="perigee">Closest point to orbital center</param>
    /// <param name="startingAngle">True anomaly (θ) — starting angle in degrees</param>
    /// <param name="verticalOffset">Vertical offset/inclination in degrees (legacy parameter, applied additively to inclination)</param>
    /// <param name="bc">Barycenter containing the position of the orbital center and parent mass</param>
    /// <param name="ascendingNodeLongitude">Right ascension of the ascending node (Ω) in degrees. Rotates the node line in the reference plane. Default 0.</param>
    /// <param name="inclination">Orbital inclination (i) in degrees. Tilts the orbital plane relative to the reference plane. Default 0.</param>
    /// <param name="argumentOfPeriapsis">Argument of periapsis (ω) in degrees. Rotates periapsis within the orbital plane. Default 0.</param>
    /// <returns>Tuple containing position and velocity vectors in the inertial reference frame</returns>
    public static (Vector3 position, Vector3 velocity) CalculateOrbitalStateFromParams(
        float apogee,
        float perigee,
        float startingAngle,
        float verticalOffset,
        Barycenter bc,
        float ascendingNodeLongitude = 0f,
        float argumentOfPeriapsis = 0f
    )
    {
        // Calculate semi-major axis from apogee/perigee
        float semiMajorAxis = (apogee + perigee) / 2f;

        // Calculate eccentricity
        float eccentricity = CalculateEccentricity(apogee, perigee);

        // Convert true anomaly to radians
        float theta = Mathf.DegToRad(startingAngle);
        return CalculateOrbitalStateFromParams(
            semiMajorAxis,
            eccentricity,
            theta,
            bc,
            ascendingNodeLongitude,
            verticalOffset,
            argumentOfPeriapsis
        );
    }

    public static (Vector3 position, Vector3 velocity) CalculateOrbitalStateFromParams(
        float semiMajorAxis,
        float eccentricity,
        float theta,
        Barycenter barycenter,
        float ascendingNodeLongitude = 0f,
        float inclination = 0f,
        float argumentOfPeriapsis = 0f
    )
    {
        // Guard: semi-major axis must be positive for a valid orbit
        if (semiMajorAxis <= 0f)
        {
            GameLogger.Warning(
                $"OrbitalMath.CalculateOrbitalStateFromParams: Invalid semi-major axis {semiMajorAxis}, returning origin"
            );
            return (barycenter.Position, Vector3.Zero);
        }

        // Guard: barycenter weight (central mass) must be positive to compute velocity
        if (barycenter.Weight <= 0f)
        {
            GameLogger.Warning(
                $"OrbitalMath.CalculateOrbitalStateFromParams: Non-positive barycenter weight {barycenter.Weight}, returning position with zero velocity"
            );
            // We can still compute position geometrically, just not velocity
            float pGeom = semiMajorAxis * (1f - eccentricity * eccentricity);
            float rGeom = pGeom / (1f + eccentricity * Mathf.Cos(theta));
            Vector3 geomPos =
                barycenter.Position
                + new Vector3(rGeom * Mathf.Cos(theta), 0f, rGeom * Mathf.Sin(theta));
            return (geomPos, Vector3.Zero);
        }

        // Calculate semi-major axis from apogee/perigee
        //float semiMajorAxis = (apogee + perigee) / 2f;
        // Calculate eccentricity
        //float eccentricity = CalculateEccentricity(apogee, perigee);
        // Convert angles to radians
        //float angleRad = Mathf.DegToRad(startingAngle);
        float inclinationRad = Mathf.DegToRad(inclination);
        float semiLatusRectum = semiMajorAxis * (1f - eccentricity * eccentricity);
        // Create orbital plane basis vectors
        Vector3 orbitNormal = new Vector3(0, Mathf.Sin(inclinationRad), Mathf.Cos(inclinationRad));
        Vector3 pHat = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)).Normalized();
        Vector3 qHat = pHat.Cross(orbitNormal).Normalized();
        // Position on orbit at starting angle
        //float r = perigee + (apogee - perigee) * (1 + Mathf.Cos(angleRad)) / 2f;
        float r = semiLatusRectum / (1f + eccentricity * Mathf.Cos(theta));
        Vector3 position =
            barycenter.Position + r * (pHat * Mathf.Cos(theta) + qHat * Mathf.Sin(theta));
        // Calculate orbital velocity using vis-viva equation
        float mu = GRAVITATIONAL_CONSTANT * barycenter.Weight;
        float speed = Mathf.Sqrt(mu * (2f / r - 1f / semiMajorAxis));
        // Velocity direction is perpendicular to position vector in orbital plane
        Vector3 velocityDir = (-pHat * Mathf.Sin(theta) + qHat * Mathf.Cos(theta)).Normalized();
        if (velocityDir.LengthSquared() < 0.001f)
        {
            velocityDir = new Vector3(0, 1, 0);
        }
        Vector3 velocity = speed * velocityDir;

        return (position, velocity);
    }

    /// <summary>
    /// Derives orbital elements for a body at a given position relative to a focus point.
    /// When an existing velocity is provided, full orbital elements are computed from state vectors.
    /// When no velocity is provided, a circular orbit (e=0, a=r) is assumed.
    /// </summary>
    /// <param name="relativePosition">Position vector relative to the focus (barycenter)</param>
    /// <param name="mu">Gravitational parameter: G * M_other</param>
    /// <param name="existingVelocity">Optional existing velocity to derive elements from</param>
    /// <returns>Tuple of (semiMajorAxis, eccentricity, pHat, qHat, trueAnomaly)</returns>
    public static (
        float semiMajorAxis,
        float eccentricity,
        Vector3 pHat,
        Vector3 qHat,
        float trueAnomaly
    ) DeriveOrbitalElements(Vector3 relativePosition, float mu, Vector3? existingVelocity = null)
    {
        float r = relativePosition.Length();
        Vector3 rHat = relativePosition.Normalized();

        if (existingVelocity.HasValue && existingVelocity.Value.LengthSquared() > 1e-10f)
        {
            Vector3 v = existingVelocity.Value;
            float vSq = v.LengthSquared();

            // Specific angular momentum: h = r × v
            Vector3 h = relativePosition.Cross(v);
            float hMag = h.Length();

            if (hMag <= 1e-10f)
            {
                // Degenerate: velocity is collinear with position — fall through to circular default
                GameLogger.Warning(
                    "OrbitalMath.DeriveOrbitalElements: Collinear position and velocity, defaulting to circular orbit"
                );
            }
            else
            {
                // Eccentricity vector: e_vec = (v × h) / μ − r̂
                Vector3 eccentricityVector = v.Cross(h) / mu - rHat;
                float eccentricity = eccentricityVector.Length();

                // Semi-major axis from vis-viva: a = 1 / (2/r − v²/μ)
                float visVivaInverse = 2f / r - vSq / mu;
                if (Mathf.Abs(visVivaInverse) < 1e-10f)
                {
                    // Parabolic — treat as very large circular orbit
                    GameLogger.Warning(
                        "OrbitalMath.DeriveOrbitalElements: Near-parabolic orbit detected, defaulting to circular"
                    );
                }
                else
                {
                    float semiMajorAxis = 1f / visVivaInverse;
                    if (semiMajorAxis <= 0f)
                    {
                        // Hyperbolic orbit — clamp to the current radius for a bound orbit
                        GameLogger.Warning(
                            "OrbitalMath.DeriveOrbitalElements: Hyperbolic orbit detected, clamping semi-major axis to current radius"
                        );
                        semiMajorAxis = r;
                        eccentricity = 0f;
                    }

                    // Construct perifocal frame
                    Vector3 hHat = h.Normalized();
                    Vector3 pHat;
                    Vector3 qHat;

                    if (eccentricity > 1e-6f)
                    {
                        // pHat points toward periapsis (along eccentricity vector)
                        pHat = eccentricityVector.Normalized();
                    }
                    else
                    {
                        // Near-circular: pHat along radial direction
                        pHat = rHat;
                    }

                    // qHat completes the right-handed perifocal frame: qHat = hHat × pHat
                    qHat = hHat.Cross(pHat).Normalized();

                    // True anomaly: angle between pHat (periapsis) and position
                    float cosNu = Mathf.Clamp(pHat.Dot(rHat), -1f, 1f);
                    float sinNu = Mathf.Clamp(qHat.Dot(rHat), -1f, 1f);
                    float trueAnomaly = Mathf.Atan2(sinNu, cosNu);

                    return (semiMajorAxis, eccentricity, pHat, qHat, trueAnomaly);
                }
            }
        }

        // Default: circular orbit (no existing velocity or degenerate case)
        // a = r, e = 0, body is at trueAnomaly = 0 (periapsis by convention)
        Vector3 defaultPHat = rHat;

        // Determine orbital plane normal (default to XZ plane with Y-up)
        Vector3 orbitNormal = Vector3.Up;
        if (Mathf.Abs(defaultPHat.Dot(orbitNormal)) > 0.99f)
        {
            orbitNormal = Vector3.Right;
        }

        // qHat perpendicular to pHat in the orbital plane: qHat = orbitNormal × pHat
        Vector3 defaultQHat = orbitNormal.Cross(defaultPHat).Normalized();

        // Guard against degenerate cross product
        if (defaultQHat.LengthSquared() < 1e-6f)
        {
            defaultQHat = Vector3.Forward.Cross(defaultPHat).Normalized();
        }

        return (r, 0f, defaultPHat, defaultQHat, 0f);
    }

    /// <summary>
    /// Calculates stable orbital velocities for multiple bodies around a shared barycenter.
    /// Derives orbital elements (semi-major axis, eccentricity, perifocal frame) from each
    /// body's position, then uses the vis-viva equation for correct velocity magnitude and
    /// the perifocal frame for proper velocity direction.
    /// </summary>
    /// <param name="positions">List of body positions</param>
    /// <param name="masses">List of body masses</param>
    /// <param name="barycenter">The shared barycenter position</param>
    /// <returns>List of velocity vectors for each body</returns>
    public static List<Vector3> CalculateStableVelocitiesForDominantBodies(
        List<Vector3> positions,
        List<float> masses,
        Barycenter barycenter
    )
    {
        var velocities = new List<Vector3>();

        if (positions == null || masses == null || positions.Count == 0 || masses.Count == 0)
        {
            GameLogger.Warning(
                "OrbitalMath.CalculateStableVelocitiesForDominantBodies: Invalid input lists"
            );
            return velocities;
        }

        if (positions.Count != masses.Count)
        {
            GameLogger.Error(
                "OrbitalMath.CalculateStableVelocitiesForDominantBodies: Position and mass counts must match"
            );
            return velocities;
        }

        // Calculate total mass with minimum mass guard
        float totalMass = 0f;
        for (int i = 0; i < masses.Count; i++)
        {
            float mass = masses[i];
            if (mass <= 0f)
            {
                mass = 1.0f;
                GameLogger.Warning(
                    "OrbitalMath.CalculateStableVelocitiesForDominantBodies: Invalid mass detected, using minimum mass of 1.0f"
                );
            }
            totalMass += mass;
        }

        if (totalMass <= 0f)
        {
            GameLogger.Error(
                "OrbitalMath.CalculateStableVelocitiesForDominantBodies: Total mass must be positive"
            );
            return velocities;
        }

        // Calculate velocity for each body using perifocal frame and vis-viva
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 radialVector = positions[i] - barycenter.Position;
            float r = radialVector.Length();

            // Guard: if distance to barycenter is negligible, assign zero velocity
            if (r <= 0.001f)
            {
                velocities.Add(Vector3.Zero);
                continue;
            }

            // For dominant bodies orbiting a shared barycenter, the effective
            // gravitational parameter must account for the reduced two-body problem.
            // The actual gravitational source is at distance d (inter-body separation),
            // not r (distance to barycenter). Since d = r * M_total / M_other:
            //   mu_eff = G * M_other^3 / M_total^2
            // This ensures the vis-viva velocity is consistent with the N-body simulation
            // where F = G*m_i*m_j / d_ij^2.
            float otherMass = totalMass - masses[i];
            if (otherMass <= 0f)
            {
                otherMass = 1.0f;
            }
            float effectiveMass = (otherMass * otherMass * otherMass) / (totalMass * totalMass);
            float mu = effectiveMass * GRAVITATIONAL_CONSTANT;

            // Derive orbital elements from position relative to barycenter
            var (semiMajorAxis, eccentricity, pHat, qHat, trueAnomaly) = DeriveOrbitalElements(
                radialVector,
                mu
            );

            // Vis-viva equation: v² = μ_eff * (2/r − 1/a)
            float visViva = mu * (2f / r - 1f / semiMajorAxis);

            // Guard: vis-viva should be positive for a bound orbit
            // If negative (hyperbolic), clamp to circular velocity
            if (visViva <= 0f)
            {
                GameLogger.Warning(
                    $"OrbitalMath.CalculateStableVelocitiesForDominantBodies: Negative vis-viva for body {i}, clamping to circular velocity"
                );
                visViva = mu / r;
            }

            float speed = Mathf.Sqrt(visViva);

            // Velocity direction in perifocal frame:
            // v_dir = (−sin(ν) * p̂ + (e + cos(ν)) * q̂).Normalized()
            Vector3 velocityDir = (
                -Mathf.Sin(trueAnomaly) * pHat + (eccentricity + Mathf.Cos(trueAnomaly)) * qHat
            ).Normalized();

            // Guard against degenerate direction
            if (velocityDir.LengthSquared() < 1e-6f)
            {
                // Fall back to simple tangent perpendicular to radial
                Vector3 up = Vector3.Up;
                if (Mathf.Abs(radialVector.Normalized().Dot(up)) > 0.99f)
                {
                    up = Vector3.Right;
                }
                velocityDir = radialVector.Cross(up).Normalized();
            }

            velocities.Add(velocityDir * speed);
        }

        return velocities;
    }

    /// <summary>
    /// Distributes bodies evenly around a barycenter with mass-weighted distances.
    /// Heavier bodies are placed closer to the barycenter, lighter bodies farther.
    /// </summary>
    /// <param name="positions">Input positions (used only for count, not values)</param>
    /// <param name="masses">List of body masses</param>
    /// <param name="barycenter">The barycenter position</param>
    /// <param name="baseSeparation">Base separation distance</param>
    /// <returns>New list of positions with symmetric distribution</returns>
    public static List<Vector3> PlaceDominantBodiesSymmetrically(
        List<Vector3> positions,
        List<float> masses,
        Barycenter barycenter,
        float baseSeparation
    )
    {
        const float MINIMUM_SEPARATION = 100f;

        var newPositions = new List<Vector3>();

        if (positions == null || masses == null || positions.Count == 0 || masses.Count == 0)
        {
            GameLogger.Warning("OrbitalMath.PlaceDominantBodiesSymmetrically: Invalid input lists");
            return newPositions;
        }

        if (positions.Count != masses.Count)
        {
            GameLogger.Error(
                "OrbitalMath.PlaceDominantBodiesSymmetrically: Position and mass counts must match"
            );
            return newPositions;
        }

        int numBodies = positions.Count;

        if (numBodies == 1)
        {
            // Guard: if single body (N=1), return position at origin (0,0,0)
            newPositions.Add(barycenter.Position);
            return newPositions;
        }

        // Calculate total mass
        float totalMass = 0f;
        foreach (float mass in masses)
        {
            totalMass += mass;
        }

        if (totalMass <= 0f)
        {
            GameLogger.Error(
                "OrbitalMath.PlaceDominantBodiesSymmetrically: Total mass must be positive"
            );
            return newPositions;
        }

        // Calculate sum of (totalMass / mass_i) for normalization
        float massRatioSum = 0f;
        foreach (float mass in masses)
        {
            if (mass > 0f)
            {
                massRatioSum += totalMass / mass;
            }
        }

        if (massRatioSum <= 0f)
        {
            GameLogger.Error(
                "OrbitalMath.PlaceDominantBodiesSymmetrically: Invalid mass ratio sum"
            );
            return newPositions;
        }

        // Angular spacing between bodies
        float angularStep = 2f * Mathf.Pi / numBodies;

        // Place each body
        for (int i = 0; i < numBodies; i++)
        {
            float mass = masses[i];

            if (mass <= 0f)
            {
                // Invalid mass - place at maximum distance
                newPositions.Add(
                    barycenter.Position
                        + new Vector3(Mathf.Cos(angularStep * i), 0, Mathf.Sin(angularStep * i))
                            * baseSeparation
                            * numBodies
                );
                continue;
            }

            // Distance inversely proportional to mass
            // r_i = baseSeparation * (totalMass / mass_i) / sum(totalMass/mass_j)
            float distance = baseSeparation * (totalMass / mass) / massRatioSum;

            // Guard: clamp calculated distance to be at least MINIMUM_SEPARATION
            distance = Mathf.Max(distance, MINIMUM_SEPARATION);

            // Calculate angle for this body (distribute evenly around 360 degrees)
            float angle = angularStep * i;

            // Position in XZ plane relative to barycenter
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
            newPositions.Add(barycenter.Position + offset);
        }

        return newPositions;
    }
}
