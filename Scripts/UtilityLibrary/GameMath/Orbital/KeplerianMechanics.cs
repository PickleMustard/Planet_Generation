using System;
using Godot;
using Structures.Logistics;
using UtilityLibrary;

namespace UtilityLibrary.GameMath.Orbital;

public static class KeplerianMechanics
{
    /*
      public static Vector3 PropagateKepler(
          Vector3 positionAtT0,
          Vector3 velocityAtT0,
          float mu,
          float deltaTime
      )
      {
          float radius = positionAtT0.Length();
          float velocity = velocityAtT0.Length();
          float radialVelocity = positionAtT0.Dot(velocityAtT0) / radius;
  
          float alpha = 2f / radius - Mathf.Pow(velocity, 2f) / mu;
  
          float muSqrt = Mathf.Sqrt(mu);
          float chi = 0f;
  
          if (alpha > 1e-10f)
              chi = muSqrt * deltaTime * alpha;
          else if (alpha < -1e-10f)
          {
              float a = 1f / alpha;
              chi = (
                  Mathf.Sqrt(-a)
                  * Mathf.Log(
                      (-2f * mu * alpha * deltaTime) / (positionAtT0.Dot(velocityAtT0))
                          + Mathf.Sqrt(-mu * a) * (1f - radius * alpha)
                  )
              );
          }
          else
              chi = muSqrt * deltaTime / radius;
  
          float z,
              C,
              S = 0f;
          for (int i = 0; i < 100; i++)
          {
              z = alpha * chi * chi;
              C = CalculateStumpffC(z);
              S = CalculateStumpffS(z);
  
              float radiusOverMuSqrt = radius * radialVelocity / muSqrt;
              float oneMinusAR = 1f - alpha * radius;
  
              float F =
                  (radiusOverMuSqrt * chi * chi * C)
                  + (oneMinusAR * chi * chi * chi * S)
                  + (radius * chi)
                  - (muSqrt * deltaTime);
  
              float FPrime =
                  (radiusOverMuSqrt * chi * (1f - z * S)) + (oneMinusAR * chi * chi * C) + radius;
              if (Mathf.Abs(F) < 1e-30f)
                  break;
              float chiNew = chi - F / FPrime;
              if (Mathf.Abs(chiNew - chi) < (1e-10f * Mathf.Max(1f, Mathf.Abs(chi))))
              {
                  chi = chiNew;
                  break;
              }
              chi = chiNew;
          }
          z = alpha * chi * chi;
          C = CalculateStumpffC(z);
          S = CalculateStumpffS(z);
          float f = 1f - (chi * chi / radius) * C;
          float g = deltaTime - (chi * chi * chi / muSqrt) * S;
  
          Vector3 newPosition = f * positionAtT0 + g * velocityAtT0;
          float fDot = muSqrt / (newPosition.Length() * radius) * chi * (z * S - 1f);
          float gDot = 1f - (chi * chi / newPosition.Length()) * C;
  
          Vector3 newVelocity = fDot * positionAtT0 + gDot * velocityAtT0;
          return newPosition;
      }
  */
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

        // 2. Node vector: n = Y_up × h (lies in XZ plane, points toward ascending node)
        Vector3 n = Vector3.Up.Cross(h);
        float nMag = n.Length();

        // 3. Eccentricity vector: e = (v × h) / μ − r̂
        Vector3 eVec = velocity.Cross(h) / mu - rHat;
        float e = eVec.Length();

        // 4. Semi-major axis from vis-viva: a = 1 / (2/r − v²/μ)
        float visVivaInverse = 2f / r - v * v / mu;
        float a;
        if (Mathf.Abs(visVivaInverse) < EPSILON)
        {
            // Near-parabolic
            a = r;
        }
        else
        {
            a = 1f / visVivaInverse;
            if (a < 0f)
            {
                // Hyperbolic — keep negative a for correctness
                // (mean anomaly calculation will use hyperbolic anomaly)
            }
        }

        // 5. Inclination: angle between h and Y-up
        float inclination = Mathf.Acos(Mathf.Clamp(hHat.Y, -1f, 1f));

        // 6. RAAN (Ω): angle of node vector from +X in the XZ plane
        float raan = 0f;
        if (nMag > EPSILON)
        {
            Vector3 nHat = n / nMag;
            raan = Mathf.Acos(Mathf.Clamp(nHat.X, -1f, 1f));
            if (nHat.Z < 0f)
                raan = Mathf.Tau - raan;
        }

        // 7. Argument of periapsis (ω): angle from node vector to eccentricity vector
        float argPeriapsis = 0f;
        if (e > EPSILON)
        {
            if (nMag > EPSILON)
            {
                float dotNE = n.Dot(eVec) / (nMag * e);
                argPeriapsis = Mathf.Acos(Mathf.Clamp(dotNE, -1f, 1f));
                // If periapsis is below reference plane (eVec.Y < 0), ω is in [π, 2π)
                if (eVec.Y < 0f)
                    argPeriapsis = Mathf.Tau - argPeriapsis;
            }
            else
            {
                // Equatorial orbit: use longitude of periapsis from +X axis
                argPeriapsis = Mathf.Atan2(eVec.Z, eVec.X);
                if (argPeriapsis < 0f)
                    argPeriapsis += Mathf.Tau;
            }
        }

        // 8. True anomaly (ν): angle from eccentricity vector to position
        float trueAnomaly = 0f;
        if (e > EPSILON)
        {
            float dotER = eVec.Dot(position) / (e * r);
            trueAnomaly = Mathf.Acos(Mathf.Clamp(dotER, -1f, 1f));
            // If moving away from periapsis (r·v < 0 means approaching), sign convention
            if (position.Dot(velocity) < 0f)
                trueAnomaly = Mathf.Tau - trueAnomaly;
        }
        else
        {
            // Circular orbit: use argument of latitude
            if (nMag > EPSILON)
            {
                float dotNR = n.Dot(position) / (nMag * r);
                trueAnomaly = Mathf.Acos(Mathf.Clamp(dotNR, -1f, 1f));
                if (position.Y < 0f)
                    trueAnomaly = Mathf.Tau - trueAnomaly;
            }
            else
            {
                // Circular equatorial: true longitude from +X
                trueAnomaly = Mathf.Atan2(position.Z, position.X);
                if (trueAnomaly < 0f)
                    trueAnomaly += Mathf.Tau;
            }
        }

        // 9-10. Mean anomaly from true anomaly and eccentricity
        float meanAnomaly = 0f;
        if (a > 0f && e < 1f)
        {
            // Elliptical: E = 2 * atan2(sqrt(1-e) * sin(ν/2), sqrt(1+e) * cos(ν/2))
            float halfNu = trueAnomaly / 2f;
            float eccentricAnomaly = 2f * Mathf.Atan2(
                Mathf.Sqrt(1f - e) * Mathf.Sin(halfNu),
                Mathf.Sqrt(1f + e) * Mathf.Cos(halfNu)
            );
            meanAnomaly = eccentricAnomaly - e * Mathf.Sin(eccentricAnomaly);
        }
        else if (a < 0f && e > 1f)
        {
            // Hyperbolic: H from tanh(H/2) = sqrt((e-1)/(e+1)) * tan(ν/2)
            float tanHalfNu = Mathf.Tan(trueAnomaly / 2f);
            float tanhHalfH = Mathf.Sqrt((e - 1f) / (e + 1f)) * tanHalfNu;
            // atanh(x) = 0.5 * ln((1+x)/(1-x))
            float clampedTanh = Mathf.Clamp(tanhHalfH, -0.9999f, 0.9999f);
            float halfH = 0.5f * Mathf.Log((1f + clampedTanh) / (1f - clampedTanh));
            float H = 2f * halfH;
            meanAnomaly = e * (float)Math.Sinh(H) - H;
        }

        // Normalize mean anomaly to [0, 2π)
        meanAnomaly %= Mathf.Tau;
        if (meanAnomaly < 0f)
            meanAnomaly += Mathf.Tau;

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
                trueAnomaly = 2.0 * Math.Atan2(
                    Math.Sqrt(1.0 + e) * Math.Sin(E / 2.0),
                    Math.Sqrt(1.0 - e) * Math.Cos(E / 2.0)
                );
            }
            else if (e > 1.0 + 1e-6)
            {
                // Hyperbolic
                double H = SolveKeplerHyperbolic(M, e);
                trueAnomaly = 2.0 * Math.Atan2(
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
        if (M < 0) M += 2.0 * Math.PI;

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
        return x;
    }
}
