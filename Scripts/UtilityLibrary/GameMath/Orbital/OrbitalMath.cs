using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;

namespace UtilityLibrary.GameMath.Orbital;

public static class OrbitalMath
{
    public const float GRAVITATIONAL_CONSTANT = .067394967f;
    public const float STANDARD_GRAVITY = 9.81f;
    public const float NAT_LOG_TWO = 0.69314718055994529f;

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
    /// Calculates positions and velocities for both bodies in a binary system using a
    /// single relative orbit, guaranteeing the barycenter lies exactly at the origin.
    ///
    /// The apogee/perigee define the shape of the relative orbit (the ellipse that the
    /// body-to-body separation vector traces). Each body's distance from the barycenter
    /// is inversely proportional to its mass:
    ///   r_A = r_total × M_B / (M_A + M_B)
    ///   r_B = r_total × M_A / (M_A + M_B)
    ///
    /// This ensures M_A·posA + M_B·posB = 0 for any angle θ, eccentricity, or mass ratio.
    /// </summary>
    /// <param name="pHat">Unit vector defining the periapsis direction in the orbital plane</param>
    /// <param name="qHat">Unit vector perpendicular to pHat in the orbital plane (90° ahead in direction of motion)</param>
    /// <param name="relativeApogee">Farthest body-to-body separation (apoapsis of the relative orbit)</param>
    /// <param name="relativePerigee">Closest body-to-body separation (periapsis of the relative orbit)</param>
    /// <param name="angle">True anomaly in radians</param>
    /// <param name="massA">Mass of body A</param>
    /// <param name="massB">Mass of body B</param>
    /// <returns>Tuple of (positionA, velocityA, positionB, velocityB) centered on the barycenter at origin</returns>
    public static (
        Vector3 posA,
        Vector3 velA,
        Vector3 posB,
        Vector3 velB
    ) CalculateBinaryOrbitalState(
        Vector3 pHat,
        Vector3 qHat,
        float relativeApogee,
        float relativePerigee,
        float angle,
        float massA,
        float massB
    )
    {
        float massTotal = massA + massB;

        // Guard: masses must be positive
        if (massTotal <= 0f)
        {
            GameLogger.Warning(
                $"OrbitalMath.CalculateBinaryOrbitalState: Non-positive total mass {massTotal}, returning origin"
            );
            return (Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero);
        }

        // Relative orbit parameters
        float semiMajorAxis = (relativeApogee + relativePerigee) / 2f;
        float eccentricity = CalculateEccentricity(relativeApogee, relativePerigee);

        // Guard: semi-major axis must be positive
        if (semiMajorAxis <= 0f)
        {
            GameLogger.Warning(
                $"OrbitalMath.CalculateBinaryOrbitalState: Invalid semi-major axis {semiMajorAxis}, returning origin"
            );
            return (Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero);
        }

        // Mass fractions: distance from barycenter is inversely proportional to mass
        float fractionA = massB / massTotal; // body A's share of total separation
        float fractionB = massA / massTotal; // body B's share of total separation

        // Total body-to-body separation at true anomaly θ (conic section formula)
        float oneMinusESq = 1f - eccentricity * eccentricity;
        float semiLatusRectum = semiMajorAxis * oneMinusESq;
        float rTotal = semiLatusRectum / (1f + eccentricity * Mathf.Cos(angle));

        // Direction vector in the orbital plane at angle θ
        Vector3 direction = Mathf.Cos(angle) * pHat + Mathf.Sin(angle) * qHat;

        // Positions: bodies on opposite sides of barycenter (origin)
        Vector3 posA = +rTotal * fractionA * direction;
        Vector3 posB = -rTotal * fractionB * direction;

        // Velocity of the relative orbit via vis-viva: v² = μ(2/r - 1/a)
        // where μ = G·(M_A + M_B) for the relative orbit
        float mu = GRAVITATIONAL_CONSTANT * massTotal;
        float speedRelative = Mathf.Sqrt(mu * (2f / rTotal - 1f / semiMajorAxis));

        // Velocity direction: perpendicular to radial direction in the orbital plane
        // In the perifocal frame: v_dir = -sin(θ)·pHat + (e + cos(θ))·qHat, then normalize
        Vector3 velDirection = (
            -Mathf.Sin(angle) * pHat + (eccentricity + Mathf.Cos(angle)) * qHat
        ).Normalized();

        if (velDirection.LengthSquared() < 0.001f)
        {
            velDirection = qHat;
        }

        // Split relative velocity by mass fractions (same as positions)
        // v_A = +v_rel · (M_B / M_total), v_B = -v_rel · (M_A / M_total)
        // This guarantees M_A·v_A + M_B·v_B = 0 (zero net momentum)
        Vector3 velA = +speedRelative * fractionA * velDirection;
        Vector3 velB = -speedRelative * fractionB * velDirection;

        return (posA, velA, posB, velB);
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
            // For circular orbits E = ν; compute angle of position in the orbital frame
            Vector3 h = position.Cross(velocity);
            Vector3 orbitNormal = h.Normalized();

            // Build a reference P-hat in the orbital plane (same logic as caller)
            Vector3 pHat = orbitNormal.Cross(Vector3.Up).Normalized();
            if (pHat.LengthSquared() < 1e-10f)
            {
                pHat = orbitNormal.Cross(Vector3.Right).Normalized();
            }
            Vector3 qHat = orbitNormal.Cross(pHat).Normalized();

            // Angle of position measured from P-hat within the orbital plane
            Vector3 rNorm = position.Normalized();
            return Mathf.Atan2(rNorm.Dot(qHat), rNorm.Dot(pHat));
        }

        // Calculate eccentricity vector direction
        float r = position.Length();
        float vSq = velocity.LengthSquared();
        float rvDot = position.Dot(velocity);

        Vector3 term1 = position * (vSq - mu / r);
        Vector3 term2 = velocity * rvDot;
        Vector3 eccentricityVector = (term1 - term2) / mu;

        // Calculate the eccentric anomaly using the dot product
        float E = Mathf.Acos((1f - r / semiMajorAxis) / eccentricity);
        if (position.Dot(velocity) < 0f)
            E = 2f * Mathf.Pi - E;

        //// Check if we need to adjust based on velocity direction
        //Vector3 rPlusE = position + eccentricityVector;
        //if (rPlusE.Dot(velocity) < 0f)
        //{
        //    eccentricAnomaly = 2f * Mathf.Pi - eccentricAnomaly;
        //}

        return E;
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
        if (barycenter.Mass <= 0f)
        {
            GameLogger.Warning(
                $"OrbitalMath.CalculateOrbitalStateFromParams: Non-positive barycenter weight {barycenter.Mass}, returning position with zero velocity"
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
        Vector3 orbitNormal = new Vector3(Mathf.Sin(inclinationRad), Mathf.Cos(inclinationRad), 0);
        Vector3 pHat = new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)).Normalized();
        Vector3 qHat = pHat.Cross(orbitNormal).Normalized();
        // Position on orbit at starting angle
        //float r = perigee + (apogee - perigee) * (1 + Mathf.Cos(angleRad)) / 2f;
        float r = semiLatusRectum / (1f + eccentricity * Mathf.Cos(theta));
        Vector3 position =
            barycenter.Position + r * (pHat * Mathf.Cos(theta) + qHat * Mathf.Sin(theta));
        // Calculate orbital velocity using vis-viva equation
        float mu = GRAVITATIONAL_CONSTANT * barycenter.Mass;
        float speed = Mathf.Sqrt(mu * (2f / r - 1f / semiMajorAxis));
        // Velocity direction is perpendicular to position vector in orbital plane
        Vector3 velocityDir = (pHat * Mathf.Sin(theta) + qHat * Mathf.Cos(theta)).Normalized();
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

    /// <summary>
    /// Finds the most gravitationally dominant body in the system relative to the given body.
    /// Uses gravitational influence calculation similar to FindDominantBody in PlanetSystemGenerator.
    /// </summary>
    /// <param name="origin">The body to find the central body for.</param>
    /// <returns>The most gravitationally dominant body.</returns>
    public static IOrbitalBody FindCentralBody(IOrbitalBody origin)
    {
        SceneTree tree = (SceneTree)Engine.GetMainLoop();
        //var barycenter = tree.Root.FindChild("Barycenter", true);
        //if (barycenter != null && barycenter is Barycenter bc)
        //{
        //    return bc;
        //}
        if (origin == null)
        {
            GameLogger.Warning("TrajectoryPlanner.FindCentralBody: Origin body is null");
            return null;
        }

        var bodies = tree.GetNodesInGroup("CelestialBody");

        if (bodies == null || bodies.Count == 0)
        {
            GameLogger.Warning("TrajectoryPlanner.FindCentralBody: No bodies found in system");
            return origin;
        }

        float maxInfluence = 0f;
        CelestialBody? dominantBody = null;
        Vector3 testPosition = origin.BodyPosition;

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
            GameLogger.Debug(
                $"TrajectoryPlanner.FindCentralBody: Using {origin} as central body (no dominant body found)"
            );
            return origin;
        }

        GameLogger.Debug(
            $"TrajectoryPlanner.FindCentralBody: Found dominant body {dominantBody.Name} for {origin}"
        );
        return dominantBody;
    }

    /// <summary>
    /// Calculates the gravitational parameter (μ = GM) for a celestial body.
    /// </summary>
    /// <param name="centralBody">The central body.</param>
    /// <returns>Gravitational parameter in m³/s².</returns>
    public static float GetGravitationalParameter(IOrbitalBody centralBody)
    {
        if (centralBody == null || centralBody.Mass <= 0f)
        {
            GameLogger.Warning(
                $"TrajectoryPlanner.GetGravitationalParameter: Invalid body or mass - "
                    + $"body: {centralBody}, mass: {centralBody?.Mass}"
            );
            return 0f;
        }

        // μ = G × M (using OrbitalMath's gravitational constant)
        float mu = OrbitalMath.GRAVITATIONAL_CONSTANT * centralBody.Mass;

        GameLogger.Debug(
            $"TrajectoryPlanner.GetGravitationalParameter: μ = {mu:E2} m³/s² for {centralBody} "
                + $"(mass: {centralBody.Mass:E2} kg)"
        );

        return mu;
    }
}
