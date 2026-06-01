using System;
using Godot;
using Structures.Logistics;

namespace UtilityLibrary.GameMath.Orbital;

public static class KeplerianMechanics
{
    public static (Vector3, Vector3) PropagateKepler(
        Vector3 positionAtTime0,
        Vector3 velocityAtTime0,
        float mu,
        float tof
    )
    {
        double mud = (double)mu;
        double R0 = positionAtTime0.Length();
        double Rf = 0.0;
        double V02 = velocityAtTime0.Length();
        GD.Print($"PropagateKepler: R0={R0:F2}, V02={V02:F2}");
        if (R0 <= .3 && V02 <= .3)
        { //Roughly no movement
            return (positionAtTime0, velocityAtTime0);
        }
        double DX = 0.0;
        double energy = (V02 / 2.0 - mud / R0);
        double a = -mud / 2.0 / energy;

        double sqrtA = 0,
            F = 0,
            G = 0,
            Ft = 0,
            Gt = 0,
            s0 = 0,
            c0 = 0,
            sigma0 = (positionAtTime0.Dot(velocityAtTime0)) / Math.Sqrt(mud);

        if (a > 0)
        {
            sqrtA = Math.Sqrt(a);
            double DM = Math.Sqrt(mud / (a * a * a)) * tof;
            double sinDM = Math.Sin(DM);
            double cosDM = Math.Cos(DM);
            double DMCropped = Math.Atan2(sinDM, cosDM);
            if (DMCropped < 0)
                DMCropped += 2 * Math.PI;
            s0 = sigma0 / sqrtA;
            c0 = (1 - R0 / a);
            double initialGuess =
                DMCropped
                + c0 * sinDM
                - s0 * (1 - cosDM)
                + (c0 * cosDM - s0 * sinDM) * (c0 * sinDM + s0 * cosDM - s0)
                + .5 * (c0 * sinDM + s0 * cosDM - s0) * (2 * Math.Pow(c0 * cosDM - s0 * sinDM, 2))
                - (c0 * sinDM + s0 * cosDM - s0) * (c0 * sinDM + s0 * cosDM);
            double DE = NewtonRaphson(
                (double DE) =>
                {
                    return -DM
                        + DE
                        + sigma0 / sqrtA * (1f - Math.Cos(DE))
                        - (1f - R0 / a) * Math.Sin(DE);
                },
                (double DE) =>
                {
                    return 1f + sigma0 / sqrtA * Math.Sin(DE) - (1f - R0 / a) * Math.Cos(DE);
                },
                initialGuess
            );

            Rf = a + (R0 - a) * Math.Cos(DE) + sigma0 * sqrtA * Math.Sin(DE);
            F = 1f - a / R0 * (1f - Math.Cos(DE));
            G =
                a * sigma0 / Math.Sqrt(mu) * (1f - Math.Cos(DE))
                + R0 * Math.Sqrt(a / mu) * Math.Sin(DE);
            Ft = -Math.Sqrt(mu * a) / (Rf * R0) * Math.Sin(DE);
            Gt = 1f - a / Rf * (1f - Math.Cos(DE));
            DX = DE;
        }

        Vector3 newPosition = new Vector3(
            (float)(F * positionAtTime0.X + G * velocityAtTime0.X),
            (float)(F * positionAtTime0.Y + G * velocityAtTime0.Y),
            (float)(F * positionAtTime0.Z + G * velocityAtTime0.Z)
        );
        Vector3 newVelocity = new Vector3(
            (float)(Ft * positionAtTime0.X + Gt * velocityAtTime0.X),
            (float)(Ft * positionAtTime0.Y + Gt * velocityAtTime0.Y),
            (float)(Ft * positionAtTime0.Z + Gt * velocityAtTime0.Z)
        );
        return (newPosition, newVelocity);
    }

    /// <summary>
    /// Derives all six classical Keplerian orbital elements plus mean anomaly
    /// from position and velocity state vectors.
    /// Adapted for Godot's Y-up coordinate system (XZ is the reference plane).
    /// </summary>
    /// <param name="position">Position vector relative to the central body.</param>
    /// <param name="velocity">Velocity vector relative to the central body.</param>
    /// <param name="mu">Gravitational parameter (G * M) of the central body.</param>
    /// <returns>Classical orbital elements with all angles in degrees.</returns>
    public static ClassicalOrbitalElements DeriveClassicalElements(
        Vector3 position,
        Vector3 velocity,
        float mu
    )
    {
        const float EPSILON = 1e-10f;

        float r = position.Length();
        float v = velocity.Length();

        if (r < EPSILON || mu < EPSILON)
        {
            GameLogger.Warning(
                "KeplerianMechanics.DeriveClassicalElements: Degenerate input "
                    + $"(r={r}, mu={mu}), returning zero elements"
            );
            return default;
        }

        Vector3 rHat = position / r;

        // 1. Angular momentum: h = r × v
        Vector3 h = position.Cross(velocity);
        float hMag = h.Length();

        if (hMag < EPSILON)
        {
            // Rectilinear trajectory — cannot define an orbital plane
            GameLogger.Warning(
                "KeplerianMechanics.DeriveClassicalElements: Rectilinear trajectory (h ≈ 0), "
                    + "returning elements with zero angles"
            );
            float aRect = 1f / (2f / r - v * v / mu);
            return new ClassicalOrbitalElements { SemiMajorAxis = aRect };
        }

        Vector3 hHat = h / hMag;
        float p = h.LengthSquared() / mu;

        // 2. Node vector: n = Y_up × h (lies in XZ plane, points toward ascending node)
        Vector3 n = Vector3.Up.Cross(h);
        float nMag = n.Length();

        // 3. Eccentricity vector: e = (v × h) / μ − r̂
        Vector3 eVec = velocity.Cross(h) / mu - rHat;
        float e = eVec.Length();

        // 4. Semi-major axis from vis-viva: a = 1 / (2/r − v²/μ)
        float a = p / (1f - e * e);

        // 5. Inclination: angle between h and Y-up
        float inclination = Mathf.Acos(h.Y / hMag);

        // 6. RAAN (Ω): angle of node vector from +X in the XZ plane
        float raan = 0f;
        if (nMag > EPSILON)
        {
            float cosRaan = Mathf.Clamp(n.X / nMag, -1f, 1f);
            raan = Mathf.Acos(cosRaan);
            if (n.Z < 0f)
                raan = Mathf.Tau - raan;
        }

        // 7. Argument of periapsis (ω): angle from node vector to eccentricity vector
        float argPeriapsis = 0f;
        if (e > EPSILON)
        {
            if (nMag > EPSILON)
            {
                float cosArgP = Mathf.Clamp(n.Dot(eVec) / (nMag * e), -1f, 1f);
                argPeriapsis = Mathf.Acos(cosArgP);
                if (eVec.Y < 0f)
                    argPeriapsis = Mathf.Tau - argPeriapsis;
            }
            else
            {
                // Equatorial orbit: longitude of periapsis from +X
                argPeriapsis = Mathf.Atan2(eVec.Z, eVec.X);
                if (argPeriapsis < 0f)
                    argPeriapsis += Mathf.Tau;
            }
        }

        // 8. True anomaly (ν): angle from eccentricity vector to position
        float cosf = eVec.Dot(position) / (e * r);
        float sinf = position.Dot(velocity) * hMag / (e * r * mu);
        float f = Mathf.Atan2(sinf, cosf);
        if (f < 0f)
        {
            f += 2f * Mathf.Pi;
        }
        float trueAnomaly = f;

        // 9-10. Mean anomaly from true anomaly and eccentricity
        float E = 2f * Mathf.Atan(Mathf.Sqrt((1f - e) / (1f + e)) * Mathf.Tan(f / 2f));
        float meanAnomaly = (E - e * Mathf.Sin(E));

        return new ClassicalOrbitalElements
        {
            SemiMajorAxis = a,
            Eccentricity = e,
            InclinationDeg = Mathf.RadToDeg(inclination),
            AscendingNodeLongitudeDeg = Mathf.RadToDeg(raan),
            ArgumentOfPeriapsisDeg = Mathf.RadToDeg(argPeriapsis),
            TrueAnomalyDeg = Mathf.RadToDeg(trueAnomaly),
            MeanAnomalyDeg = Mathf.RadToDeg(meanAnomaly),
        };
    }

    /// <summary>
    /// Propagates an orbit defined by classical orbital elements forward in time.
    /// Handles elliptical (e &lt; 1), hyperbolic (e &gt; 1), and circular (e ≈ 0) orbits.
    /// Adapted for Godot's Y-up coordinate system (XZ is the reference plane).
    /// </summary>
    /// <param name="semiMajorAxis">Semi-major axis in meters (negative for hyperbolic).</param>
    /// <param name="eccentricity">Eccentricity of the orbit.</param>
    /// <param name="inclinationDeg">Inclination in degrees.</param>
    /// <param name="raanDeg">Right ascension of the ascending node in degrees.</param>
    /// <param name="argPeriapsisDeg">Argument of periapsis in degrees.</param>
    /// <param name="meanAnomalyAtEpochDeg">Mean anomaly at epoch (t=0) in degrees.</param>
    /// <param name="mu">Gravitational parameter (G * M) of the central body.</param>
    /// <param name="elapsedTime">Time elapsed since epoch in seconds.</param>
    /// <returns>Position vector in the central-body-centered inertial frame.</returns>
    public static Vector3 PropagateFromElements(
        float semiMajorAxis,
        float eccentricity,
        float inclinationDeg,
        float raanDeg,
        float argPeriapsisDeg,
        float meanAnomalyAtEpochDeg,
        float mu,
        float elapsedTime
    )
    {
        double a = semiMajorAxis;
        double e = eccentricity;
        double mud = mu;

        // Guard: near-parabolic or degenerate
        if (Math.Abs(a) < 1e-6 || mu < 1e-10f)
        {
            GameLogger.Warning(
                $"PropagateFromElements: Degenerate orbit (a={a}, mu={mu}), returning zero"
            );
            return Vector3.Zero;
        }

        // 1. Mean motion: n = sqrt(mu / |a^3|)
        double absA = Math.Abs(a);
        double n = Math.Sqrt(mud / (absA * absA * absA));

        // 2. Mean anomaly at elapsed time
        double M0 = (double)Mathf.DegToRad(meanAnomalyAtEpochDeg);
        double M = M0 + n * elapsedTime;

        // 3. Solve Kepler's equation for true anomaly
        double trueAnomaly;
        try
        {
            if (e < 1.0 - 1e-6)
            {
                // Elliptical
                double E = SolveKeplerElliptical(M, e);
                trueAnomaly =
                    2.0
                    * Math.Atan2(
                        Math.Sqrt(1.0 + e) * Math.Sin(E / 2.0),
                        Math.Sqrt(1.0 - e) * Math.Cos(E / 2.0)
                    );
            }
            else if (e > 1.0 + 1e-6)
            {
                // Hyperbolic
                double H = SolveKeplerHyperbolic(M, e);
                trueAnomaly =
                    2.0
                    * Math.Atan2(
                        Math.Sqrt(e + 1.0) * Math.Sinh(H / 2.0),
                        Math.Sqrt(e - 1.0) * Math.Cosh(H / 2.0)
                    );
            }
            else
            {
                // Near-parabolic: fall back to linear interpolation
                GameLogger.Warning(
                    $"PropagateFromElements: Near-parabolic orbit (e={e:F6}), using M as true anomaly approximation"
                );
                trueAnomaly = M;
            }
        }
        catch (Exception ex)
        {
            GameLogger.Warning(
                $"PropagateFromElements: Kepler solver failed ({ex.Message}), falling back to mean anomaly"
            );
            trueAnomaly = M;
        }

        // 4. Compute radius: r = a(1 - e^2) / (1 + e * cos(nu))
        double p = a * (1.0 - e * e); // semi-latus rectum (positive for both elliptical and hyperbolic)
        double cosNu = Math.Cos(trueAnomaly);
        double r = p / (1.0 + e * cosNu);

        // 5. Position in perifocal frame (Y-up: orbit in XZ plane, periapsis along +X)
        double sinNu = Math.Sin(trueAnomaly);
        double xPerifocal = r * cosNu;
        double zPerifocal = r * sinNu;
        // yPerifocal = 0 (in the orbital plane)

        // 6. Rotate perifocal to inertial frame using RAAN, inclination, argPeriapsis
        // Convention matches DeriveClassicalElements (Y-up, XZ reference plane):
        //   - RAAN (Omega): rotation about Y-axis
        //   - Inclination (i): rotation about the node line (X-axis after RAAN rotation)
        //   - ArgPeriapsis (omega): rotation about the orbit normal (Y-axis in tilted frame)
        double raanRad = (double)Mathf.DegToRad(raanDeg);
        double incRad = (double)Mathf.DegToRad(inclinationDeg);
        double omegaRad = (double)Mathf.DegToRad(argPeriapsisDeg);

        double cosO = Math.Cos(raanRad);
        double sinO = Math.Sin(raanRad);
        double cosI = Math.Cos(incRad);
        double sinI = Math.Sin(incRad);
        double cosW = Math.Cos(omegaRad);
        double sinW = Math.Sin(omegaRad);

        // Perifocal unit vectors (P-hat = periapsis direction, Q-hat = 90deg ahead) in inertial frame
        // For Y-up: R_Y(RAAN) * R_X(inc) * R_Y(argPeri)
        // P-hat components (where perifocal X maps to):
        double Px = cosO * cosW - sinO * cosI * sinW;
        double Py = sinI * sinW;
        double Pz = sinO * cosW + cosO * cosI * sinW;

        // Q-hat components (where perifocal Z maps to, since orbit is in XZ):
        double Qx = -cosO * sinW - sinO * cosI * cosW;
        double Qy = sinI * cosW;
        double Qz = -sinO * sinW + cosO * cosI * cosW;

        // Position in inertial frame = xPerifocal * P-hat + zPerifocal * Q-hat
        double posX = xPerifocal * Px + zPerifocal * Qx;
        double posY = xPerifocal * Py + zPerifocal * Qy;
        double posZ = xPerifocal * Pz + zPerifocal * Qz;

        return new Vector3((float)posX, (float)posY, (float)posZ);
    }

    /// <summary>
    /// Solves Kepler's equation for elliptical orbits: M = E - e*sin(E).
    /// </summary>
    private static double SolveKeplerElliptical(double M, double e)
    {
        // Normalize M to [0, 2*pi)
        M = M % (2.0 * Math.PI);
        if (M < 0)
            M += 2.0 * Math.PI;

        // Initial guess: first-order approximation
        double E0 = M + e * Math.Sin(M);

        return NewtonRaphson(
            (double E) => E - e * Math.Sin(E) - M,
            (double E) => 1.0 - e * Math.Cos(E),
            E0
        );
    }

    /// <summary>
    /// Solves Kepler's equation for hyperbolic orbits: M = e*sinh(H) - H.
    /// </summary>
    private static double SolveKeplerHyperbolic(double M, double e)
    {
        // Initial guess: standard heuristic for hyperbolic Kepler equation
        double H0 = Math.Sign(M) * Math.Log(2.0 * Math.Abs(M) / e + 1.8);

        return NewtonRaphson(
            (double H) => e * Math.Sinh(H) - H - M,
            (double H) => e * Math.Cosh(H) - 1.0,
            H0
        );
    }

    public static double NewtonRaphson(
        Func<double, double> f,
        Func<double, double> df,
        double x0,
        double tolerance = 1e-10f,
        int maxIterations = 100
    )
    {
        double x = x0;
        for (int i = 0; i < maxIterations; i++)
        {
            double fx = f(x);
            double dfx = df(x);
            if (Math.Abs(dfx) < 1e-15f)
            {
                throw new ArithmeticException(
                    $"Derivative near zero at x = {x}. Method cannot continue"
                );
            }
            double xNew = x - fx / dfx;
            if (Math.Abs(xNew - x) < tolerance)
            {
                return xNew;
            }
            x = xNew;
        }
        throw new Exception("Failed to find root");
    }
}
