using System;
using Godot;
using Godot.Collections;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using Structures.Logistics;

namespace UtilityLibrary.GameMath.Orbital;

/// <summary>
/// Lambert solver implementing the Izzo algorithm for solving Lambert's problem.
/// Ported from Dario Izzo's PyKEP (ESA) and poliastro reference implementations.
///
/// Handles 0-rev and multi-revolution transfers, prograde and retrograde orbits.
/// Uses Householder iterations (quartic convergence) to find the universal variable x,
/// then reconstructs terminal velocities using the gamma/rho/sigma formulation.
///
/// Reference: D. Izzo, "Revisiting Lambert's Problem", arXiv:1403.2705 (2014)
/// </summary>
public static class LambertSolver
{
    private const int MaxHouseholderIterations = 15;
    private const float HouseholderTolerance = 1e-5f;
    private const float MultiRevTolerance = 1e-8f;
    private const int MaxHalleyIterations = 12;
    private const float HalleyTolerance = 1e-13f;

    /// <summary>
    /// Solves Lambert's problem to find the transfer trajectory between two positions.
    /// Returns a single TrajectorySolution for the specified revolution count and path type.
    /// </summary>
    /// <param name="r1">Initial position vector (m)</param>
    /// <param name="r2">Final position vector (m)</param>
    /// <param name="timeOfFlight">Time of flight in seconds</param>
    /// <param name="mu">Gravitational parameter (m³/s²)</param>
    /// <param name="prograde">True for prograde orbit, false for retrograde</param>
    /// <param name="revolutions">Number of complete revolutions (0 for direct transfer)</param>
    /// <param name="lowPath">True for low-path (left branch), false for high-path (right branch).
    /// Only matters for multi-revolution (revolutions > 0).</param>
    /// <returns>TrajectorySolution containing the transfer orbit parameters</returns>
    public static Array<TrajectorySolution> Solve(
        Vector3 r1,
        Vector3 r2,
        float timeOfFlight,
        float mu,
        bool prograde = true,
        int revolutions = 0,
        bool lowPath = true
    )
    {
        if (timeOfFlight <= 0f)
        {
            GameLogger.Warning("LambertSolver: Time of flight must be positive");
            throw new ArgumentException("Time of flight must be position");
        }
        if (mu <= 0f)
        {
            GameLogger.Warning("LambertSolver: Gravitational parameter must be positive");
            throw new ArgumentException("Gravitational parameter must be positive");
        }

        Array<TrajectorySolution> trajectorySolutions = new Array<TrajectorySolution>();

        float chordLength = (r2 - r1).Length();
        float distanceA = r1.Length();
        float distanceB = r2.Length();
        float semiPerimeter = (chordLength + distanceA + distanceB) / 2f;
        Vector3 distanceANormalized = r1.Normalized();
        Vector3 distanceBNormalized = r2.Normalized();
        Vector3 angularMomentum = r1.Cross(r2).Normalized();

        if (angularMomentum.Y == 0f)
        {
            GameLogger.Warning(
                "LamberSolver: angularMomentum has no Y component, cannot determine winding"
            );
            throw new ArithmeticException(
                "LamberSolver: angularMomentum has no Y component, cannot determine winding"
            );
        }

        float lambda2 = 1f - chordLength / semiPerimeter;
        float lambda = Mathf.Sqrt(lambda2);

        Vector3 instantaneousVelocityA = angularMomentum.Cross(distanceANormalized).Normalized();
        Vector3 instantaneousVelocityB = angularMomentum.Cross(distanceBNormalized).Normalized();

        if (angularMomentum.Y < 0f)
        {
            lambda = -lambda;
            instantaneousVelocityA = -instantaneousVelocityA;
            instantaneousVelocityB = -instantaneousVelocityB;
        }
        if (!prograde)
        {
            lambda = -lambda;
            instantaneousVelocityA = -instantaneousVelocityA;
            instantaneousVelocityB = -instantaneousVelocityB;
        }

        float lambda3 = lambda * lambda2;
        float T =
            Mathf.Sqrt(2f * mu / (semiPerimeter * semiPerimeter * semiPerimeter)) * timeOfFlight;

        float maxNumberOfRevolutions = (T / Mathf.Pi);
        maxNumberOfRevolutions = Mathf.Max(maxNumberOfRevolutions, revolutions);
        float T00 = Mathf.Acos(lambda) + lambda * Mathf.Sqrt(1f - lambda2);
        float T0 = (T00 + maxNumberOfRevolutions * Mathf.Pi);
        float T1 = 2f / 3f * (1f - lambda3);
        float DT = 0f,
            DDT = 0f,
            DDDT = 0f;
        if (maxNumberOfRevolutions > 0)
        {
            if (T < T0)
            {
                int it = 0;
                float error = 1f,
                    TMin = T0,
                    xOld = 0f,
                    xNew = 0f;
                while (true)
                {
                    dTdx(T, xOld, lambda, ref DT, ref DDT, ref DDDT);
                    if (DT != 0f)
                    {
                        xNew = xOld - DT * DDT / (DDT * DDT - DT * DDT / 2f);
                    }
                    error = Mathf.Abs(xOld - xNew);
                    if ((error < 1e-13f) || (it > 12))
                    {
                        break;
                    }
                    x2tof(ref TMin, xNew, maxNumberOfRevolutions, lambda);
                    xOld = xNew;
                    it++;
                }
                if (TMin > T)
                {
                    maxNumberOfRevolutions--;
                }
            }
            maxNumberOfRevolutions = Mathf.Min(revolutions, maxNumberOfRevolutions);
        }

        //Find solutions in x,y
        //Initial Guess
        Array<float> initialGuess = new Array<float>();
        if (T >= T00)
        {
            initialGuess.Add(-(T - T00) / (T - T00 + 4f));
        }
        else if (T <= T1)
        {
            initialGuess.Add(T1 * (T1 - T) / (2f / 5f * (1f - lambda2 * lambda3) * T) + 1);
        }
        else
        {
            initialGuess.Add(
                Mathf.Pow((T / T00), OrbitalMath.NAT_LOG_TWO / Mathf.Log(T1 / T00)) - 1f
            );
        }
        //Single revolution solution
        Array<float> solutions = new Array<float>();
        //int size = Mathf.RoundToInt(maxNumberOfRevolutions * 2f + 1f);
        //solutions.Resize(size);
        float singleRevSolution = householder(T, initialGuess[0], 0, 1e-5f, 15, lambda);
        solutions.Add(singleRevSolution);

        //Multi-revolution solutions
        float tmp = 0f;
        int numRevolutions = Mathf.RoundToInt(maxNumberOfRevolutions) + 1;
        for (int i = 1; i < numRevolutions; i++)
        {
            //Left Householder Iteration
            tmp = Mathf.Pow(((float)i * Mathf.Pi + Mathf.Pi) / (8f * T), 2f / 3f);
            initialGuess.Add((tmp - 1f) / (tmp + 1f));
            solutions.Add(householder(T, initialGuess[2 * i - 1], i, 1e-8f, 15, lambda));
            //Right Householder Iteration
            tmp = Mathf.Pow((8f * T) / ((float)i * Mathf.Pi), 2f / 3f);
            initialGuess.Add((tmp - 1f) / (tmp + 1f));
            solutions.Add(householder(T, initialGuess[2 * i], i, 1e-8f, 15, lambda));
        }

        float gamma = Mathf.Sqrt(mu * semiPerimeter / 2f);
        float rho = (distanceA - distanceB) / chordLength;
        float sigma = Mathf.Sqrt(1f - rho * rho);
        for (int i = 0; i < solutions.Count; i++)
        {
            float y = Mathf.Sqrt(1f - lambda2 + lambda2 * solutions[i] * solutions[i]);
            float vrs =
                gamma
                * ((lambda * y - solutions[i]) - rho * (lambda * y + solutions[i]))
                / distanceA;
            float vrf =
                -gamma
                * ((lambda * y - solutions[i]) + rho * (lambda * y + solutions[i]))
                / distanceB;
            float vt = gamma * sigma * (y + lambda * solutions[i]);
            float vts = vt / distanceA;
            float vtf = vt / distanceB;
            Vector3 v0 = vrs * distanceANormalized + vts * instantaneousVelocityA;
            Vector3 v1 = vrf * distanceBNormalized + vtf * instantaneousVelocityB;

            int numRevs = (i + 1) / 2;
            TrajectorySolution trajectorySolution = new TrajectorySolution(
                v0,
                v1,
                timeOfFlight,
                semiPerimeter,
                0f,
                numRevs,
                numRevs == 0 ? TransferType.Direct : TransferType.MultiRev
            );
            trajectorySolutions.Add(trajectorySolution);
        }
        return trajectorySolutions;
    }

    private static float householder(float T, float x0, int N, float eps, int iterMax, float lambda)
    {
        int it = 0;
        float error = 1f;
        float xNew = 0f;
        float tof = 0f,
            delta = 0f,
            DT = 0f,
            DDT = 0f,
            DDDT = 0f;
        while ((error > eps) && (it < iterMax))
        {
            x2tof(ref tof, x0, N, lambda);
            dTdx(T, x0, lambda, ref DT, ref DDT, ref DDDT);
            delta = tof - T;
            float DT2 = DT * DT;
            xNew =
                x0
                - delta
                    * (DT2 - delta * DDT / 2f)
                    / (DT * (DT2 - delta * DDT) + DDDT * delta * delta / 6f);
            error = Mathf.Abs(x0 - xNew);
            x0 = xNew;
            it++;
        }
        return x0;
    }

    private static void dTdx(
        float T,
        float x,
        float lambda,
        ref float DT,
        ref float DDT,
        ref float DDDT
    )
    {
        float l2 = lambda * lambda;
        float l3 = l2 * lambda;
        float umx2 = 1f - x * x;
        float y = Mathf.Sqrt(1f - l2 * umx2);
        float y2 = y * y;
        float y3 = y2 * y;

        DT = 1f / umx2 * (3f * T * x - 2f + 2f * l3 * x / y);
        DDT = 1f / umx2 * (3f * T + 5f * x * DT + 2f * (1f - l2) * l3 / y3);
        DDDT = 1f / umx2 * (7f * x * DDT + 8f * DT - 6f * (1f - l2) * l2 * l3 * x / y3 / y2);
    }

    private static void x2tof(ref float tof, float x, float N, float lambda)
    {
        float battin = .01f;
        float lagrange = .2f;
        float dist = Mathf.Abs(x - 1f);
        if (dist < lagrange && dist > battin)
        {
            x2tof2(ref tof, x, N, lambda);
            return;
        }
        float K = lambda * lambda;
        float E = x * x - 1f;
        float rho = Mathf.Abs(E);
        float z = Mathf.Sqrt(1f + K * E);
        if (dist < battin)
        {
            float eta = z - lambda * x;
            float S1 = 0.5f * (1f - lambda - x * eta);
            float Q = hypergeometricF(S1, 1e-11f);
            Q = 4f / 3f * Q;
            tof =
                (eta * eta * eta * Q + 4f * lambda * eta) / 2f
                + N * Mathf.Pi / Mathf.Pow(rho, 1.5f);
            return;
        }
        else
        {
            float y = Mathf.Sqrt(rho);
            float g = x * z - lambda * E;
            float d = 0f;
            if (E < 0f)
            {
                float l = Mathf.Acos(g);
                d = N * Mathf.Pi + l;
            }
            else
            {
                float f = y * (z - lambda * x);
                d = Mathf.Log(f + g);
            }
            tof = (x - lambda * z - d / y) / E;
            return;
        }
    }

    private static void x2tof2(ref float tof, float x, float N, float lambda)
    {
        float a = 1f / (1f - x * x);
        if (a > 0) //ellipse
        {
            float alfa = 2f * Mathf.Acos(x);
            float beta = 2f * Mathf.Asin(Mathf.Sqrt(lambda * lambda / a));
            if (lambda < 0f)
                beta = -beta;
            tof = (
                (
                    a
                    * Mathf.Sqrt(a)
                    * ((alfa - Mathf.Sin(alfa)) - (beta - Mathf.Sin(beta)) + 2f * Mathf.Pi * N)
                ) / 2f
            );
        }
        else
        {
            float alfa = 2f * Mathf.Acosh(x);
            float beta = 2f * Mathf.Asinh(Mathf.Sqrt(-lambda * lambda / a));
            if (lambda < 0f)
                beta = -beta;
            tof = (
                -a * Mathf.Sqrt(-a) * ((beta - Mathf.Sinh(beta)) - (alfa - Mathf.Sinh(alfa))) / 2f
            );
        }
    }

    private static float hypergeometricF(float z, float tol)
    {
        float Sj = 1f;
        float Cj = 1f;
        float err = 1f;
        float Cj1 = 0f;
        float Sj1 = 0f;
        float j = 0;
        while (err > tol)
        {
            Cj1 = Cj * (3f + j) * (1f + j) / (2.5f + j) * z / (j + 1f);
            Sj1 = Sj + Cj1;
            err = Mathf.Abs(Cj1);
            Sj = Sj1;
            Cj = Cj1;
            j += 1f;
        }
        return Sj;
    }

    /// <summary>
    /// Computes the non-dimensional time of flight T(x) for a given x value.
    /// Uses three different formulations depending on proximity to x=1 (parabolic):
    ///   - Battin series (very close to x=1)
    ///   - Lagrange form (close to x=1)
    ///   - Lancaster form (general case)
    /// </summary>
    private static float ComputeTimeOfFlight(float x, float lambda, float lambda2, int N)
    {
        float dist = Mathf.Abs(x - 1f);
        const float battinThreshold = 0.01f;
        const float lagrangeThreshold = 0.2f;

        // Use Lagrange form when moderately close to parabolic
        if (dist < lagrangeThreshold && dist > battinThreshold)
        {
            return ComputeTofLagrange(x, lambda, lambda2, N);
        }

        float K = lambda2; // λ²
        float E = x * x - 1f; // x² - 1
        float rho = Mathf.Abs(E);
        float z = Mathf.Sqrt(1f + K * E);

        // Use Battin series when very close to parabolic
        if (dist < battinThreshold)
        {
            float eta = z - lambda * x;
            float S1 = 0.5f * (1f - lambda - x * eta);
            float Q = 4f / 3f * HypergeometricF(S1, 1e-11f);
            float tof =
                (eta * eta * eta * Q + 4f * lambda * eta) / 2f
                + N * Mathf.Pi / Mathf.Pow(rho, 1.5f);
            return tof;
        }

        // Lancaster form (general case)
        float y = Mathf.Sqrt(rho);
        float g = x * z - lambda * E;
        float d;

        if (E < 0f) // Elliptic
        {
            float l = Mathf.Acos(Mathf.Clamp(g, -1f, 1f));
            d = N * Mathf.Pi + l;
        }
        else // Hyperbolic
        {
            float f = y * (z - lambda * x);
            float logArg = f + g;
            if (logArg <= 0f)
                logArg = 1e-10f; // Prevent NaN from log of non-positive
            d = (float)Math.Log(logArg);
        }

        return (x - lambda * z - d / y) / E;
    }

    /// <summary>
    /// Lagrange form of the TOF equation.
    /// Used when x is moderately close to 1 (parabolic region).
    /// </summary>
    private static float ComputeTofLagrange(float x, float lambda, float lambda2, int N)
    {
        float a = 1f / (1f - x * x);

        if (a > 0f) // Elliptic
        {
            float alfa = 2f * Mathf.Acos(Mathf.Clamp(x, -1f, 1f));
            float sinArg = Mathf.Sqrt(Mathf.Clamp(lambda2 / a, 0f, 1f));
            float beta = 2f * Mathf.Asin(Mathf.Clamp(sinArg, -1f, 1f));
            if (lambda < 0f)
                beta = -beta;
            return a
                * Mathf.Sqrt(a)
                * ((alfa - Mathf.Sin(alfa)) - (beta - Mathf.Sin(beta)) + 2f * Mathf.Pi * N)
                / 2f;
        }
        else // Hyperbolic
        {
            float alfa = 2f * Mathf.Acosh(x);
            float sinhArg = Mathf.Sqrt(Mathf.Clamp(-lambda2 / a, 0f, float.MaxValue));
            float beta = 2f * Mathf.Asinh(sinhArg);
            if (lambda < 0f)
                beta = -beta;
            return -a
                * Mathf.Sqrt(-a)
                * ((beta - Mathf.Sinh(beta)) - (alfa - Mathf.Sinh(alfa)))
                / 2f;
        }
    }

    /// <summary>
    /// Computes the first, second, and third derivatives of T(x) with respect to x.
    /// These are the exact analytical derivatives from Izzo's algorithm:
    ///   dT/dx   = (3Tx - 2 + 2λ³x/y) / (1 - x²)
    ///   d²T/dx² = (3T + 5x·dT + 2(1-λ²)λ³/y³) / (1 - x²)
    ///   d³T/dx³ = (7x·d²T + 8·dT - 6(1-λ²)λ⁵x/y⁵) / (1 - x²)
    /// </summary>
    /*
    private static void ComputeDerivatives(
        float x,
        float lambda,
        float lambda2,
        float T,
        out float DT,
        out float DDT,
        out float DDDT
    )
    {
        float l3 = lambda2 * lambda;
        float umx2 = 1f - x * x;
        float y = ComputeY(x, lambda);
        float y2 = y * y;
        float y3 = y2 * y;
        float y5 = y3 * y2;

        // Guard against degenerate cases
        if (Mathf.Abs(umx2) < 1e-12f || Mathf.Abs(y) < 1e-12f)
        {
            DT = 0f;
            DDT = 0f;
            DDDT = 0f;
            return;
        }

        float inv_umx2 = 1f / umx2;

        DT = inv_umx2 * (3f * T * x - 2f + 2f * l3 * x / y);
        DDT = inv_umx2 * (3f * T + 5f * x * DT + 2f * (1f - lambda2) * l3 / y3);
        DDDT = inv_umx2 * (7f * x * DDT + 8f * DT - 6f * (1f - lambda2) * lambda2 * l3 * x / y5);
    }
    */

    // ================================================================
    // Iteration Methods
    // ================================================================

    /// <summary>
    /// Householder iteration (quartic convergence) to find the root of T(x) - T_target = 0.
    /// Uses the formula: x_new = x - f(f'² - f·f''/2) / (f'(f'² - f·f'') + f'''·f²/6)
    /// </summary>
    /*
    private static float HouseholderIteration(
        float x0,
        float T,
        float lambda,
        float lambda2,
        int N,
        float tolerance,
        int maxIterations
    )
    {
        float x = x0;
        for (int i = 0; i < maxIterations; i++)
        {
            float tofComputed = ComputeTimeOfFlight(x, lambda, lambda2, N);
            float delta = tofComputed - T;

            ComputeDerivatives(
                x,
                lambda,
                lambda2,
                tofComputed,
                out float DT,
                out float DDT,
                out float DDDT
            );

            if (Mathf.Abs(DT) < 1e-12f)
            {
                GameLogger.Warning("LambertSolver: Zero derivative in Householder iteration");
                break;
            }

            // Householder step (quartic convergence)
            float DT2 = DT * DT;
            float xNew =
                x
                - delta
                    * (DT2 - delta * DDT / 2f)
                    / (DT * (DT2 - delta * DDT) + DDDT * delta * delta / 6f);

            if (Mathf.Abs(xNew - x) < tolerance)
            {
                GameLogger.Debug($"LambertSolver: Householder converged after {i + 1} iterations");
                return xNew;
            }

            x = xNew;
        }

        GameLogger.Warning("LambertSolver: Householder iteration did not converge");
        return x;
    }

    /// <summary>
    /// Finds the x value that minimizes T(x) for a given revolution count.
    /// Uses Halley's method (cubic convergence) on the first derivative.
    /// </summary>
    private static float FindMinimumTofX(float lambda, float lambda2, int N)
    {
        float x = 0.0f; // Start from parabolic
        float tMin = ComputeTimeOfFlight(x, lambda, lambda2, N);

        for (int i = 0; i < MaxHalleyIterations; i++)
        {
            ComputeDerivatives(
                x,
                lambda,
                lambda2,
                tMin,
                out float DT,
                out float DDT,
                out float DDDT
            );

            if (Mathf.Abs(DDT) < 1e-12f)
            {
                break;
            }

            // Halley step: x_new = x - 2·DT·DDT / (2·DDT² - DT·DDDT)
            float xNew = x - 2f * DT * DDT / (2f * DDT * DDT - DT * DDDT);

            if (Mathf.Abs(xNew - x) < HalleyTolerance)
            {
                return xNew;
            }

            x = xNew;
            tMin = ComputeTimeOfFlight(x, lambda, lambda2, N);
        }

        return x;
    }

    // ================================================================
    // Initial Guess
    // ================================================================

    /// <summary>
    /// Computes the initial guess for x for single-revolution (M=0) transfers.
    /// Uses the T0/T1 reference times from Izzo's paper (Equations 19, 21).
    /// </summary>
    private static float InitialGuessSingleRev(
        float T,
        float lambda,
        float lambda2,
        float lambda3,
        float T00,
        float T1
    )
    {
        if (T >= T00)
        {
            // Long transfer: x₀ = (T₀/T)^(2/3) - 1
            return Mathf.Pow(T00 / T, 2f / 3f) - 1f;
        }
        else if (T <= T1)
        {
            // Very short transfer: x₀ = (5/2)·(T₁/T)·(T₁-T)/(1-λ⁵) + 1
            float lambda5 = lambda2 * lambda3;
            return 5f / 2f * T1 / T * (T1 - T) / (1f - lambda5) + 1f;
        }
        else
        {
            // Medium transfer: interpolation between T1 and T00
            // Corrected formula from poliastro issue #1362
            return Mathf.Exp(Mathf.Log(2f) * Mathf.Log(T / T00) / Mathf.Log(T1 / T00)) - 1f;
        }
    }

    /// <summary>
    /// Computes the initial guess for x for multi-revolution transfers.
    /// Uses left/right branch selection from Izzo's algorithm.
    /// </summary>
    private static float InitialGuessMultiRev(float T, int N, bool lowPath)
    {
        // Left branch (low path)
        float tmpLeft = Mathf.Pow((N * Mathf.Pi + Mathf.Pi) / (8f * T), 2f / 3f);
        float x0Left = (tmpLeft - 1f) / (tmpLeft + 1f);

        // Right branch (high path)
        float tmpRight = Mathf.Pow((8f * T) / (N * Mathf.Pi), 2f / 3f);
        float x0Right = (tmpRight - 1f) / (tmpRight + 1f);

        // Select based on desired path type
        return lowPath ? Mathf.Max(x0Left, x0Right) : Mathf.Min(x0Left, x0Right);
    }
    */

    // ================================================================
    // Degenerate Case Handler
    // ================================================================

    /// <summary>
    /// Solves the degenerate case where r1 and r2 are nearly collinear.
    /// Uses a circular orbit approximation.
    /// </summary>
    private static TrajectorySolution SolveDegenerate(
        Vector3 r1,
        Vector3 r2,
        float timeOfFlight,
        float mu,
        bool prograde,
        int revolutions
    )
    {
        float r1Mag = r1.Length();
        float r2Mag = r2.Length();

        // Use simplified circular orbit approximation
        float avgRadius = (r1Mag + r2Mag) / 2f;

        // Calculate velocity for a circular orbit at average radius
        float circularVelocity = Mathf.Sqrt(mu / avgRadius);

        // Determine transfer angle
        float cosAngle = Mathf.Clamp(r1.Dot(r2) / (r1Mag * r2Mag), -1f, 1f);
        float angularDistance = Mathf.Acos(cosAngle);

        if (angularDistance < 1e-6f)
        {
            angularDistance = Mathf.Pi; // Default to 180° for truly collinear
        }

        if (!prograde)
        {
            angularDistance = 2f * Mathf.Pi - angularDistance;
        }

        // Calculate velocity vectors
        Vector3 r1Hat = r1.Normalized();
        Vector3 normal = r1.Cross(r2).Normalized();

        if (normal.LengthSquared() < 1e-6f)
        {
            normal = Vector3.Up;
        }

        Vector3 tangent1 = normal.Cross(r1Hat).Normalized();
        if (tangent1.LengthSquared() < 1e-6f)
        {
            tangent1 = new Vector3(1f, 0f, 0f);
        }

        Vector3 r2Hat = r2.Normalized();
        Vector3 tangent2 = normal.Cross(r2Hat).Normalized();
        if (tangent2.LengthSquared() < 1e-6f)
        {
            tangent2 = new Vector3(1f, 0f, 0f);
        }

        Vector3 v1 = tangent1 * circularVelocity;
        Vector3 v2 = tangent2 * circularVelocity;

        return new TrajectorySolution(
            v1,
            v2,
            timeOfFlight,
            avgRadius,
            0f,
            revolutions,
            revolutions == 0 ? TransferType.Direct : TransferType.MultiRev
        );
    }

    /// <summary>
    /// Creates a zero solution for degenerate cases.
    /// </summary>
    private static TrajectorySolution CreateZeroSolution()
    {
        return new TrajectorySolution(
            Vector3.Zero,
            Vector3.Zero,
            0f,
            0f,
            0f,
            0,
            TransferType.Direct
        );
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
    public static Array<TrajectorySolution> GetTrajectoryOptions(
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
        var options = new Array<TrajectorySolution>();

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
            var solutions = Solve(r1, r2, tof, mu);

            // Add all solutions from different revolution counts
            foreach (var solution in solutions)
            {
                // Set the actual time of flight on the solution
                solution.TimeOfFlight = tof;
                options.Add(solution);
            }
        }

        // Sort by DeltaVRequired (lowest delta-v first)
        //options.Sort((a, b) => a.DeltaVRequired.CompareTo(b.DeltaVRequired));

        return options;
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
    public static Array<GravityAssistOpportunity> DetectGravityAssists(
        Vector3 startPos,
        Vector3 endPos,
        Array<CelestialBody> bodies,
        float maxTimeOfFlight
    )
    {
        var opportunities = new Array<GravityAssistOpportunity>();

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
            float gravitationalParameter = OrbitalMath.GRAVITATIONAL_CONSTANT * bodyMass;
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
        //opportunities.Sort((a, b) => b.DeltaVSavings.CompareTo(a.DeltaVSavings));

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

    // ================================================================
    // Utility Functions
    // ================================================================

    /// <summary>
    /// Hypergeometric function F(z) used in the Battin series expansion.
    /// Computes the continued fraction: F = 1 + c₁z + c₂z² + ...
    /// </summary>
    private static float HypergeometricF(float z, float tol)
    {
        float Sj = 1f;
        float Cj = 1f;
        int j = 0;

        for (int iter = 0; iter < 100; iter++)
        {
            float coefficient = (3f + j) * (1f + j) / ((2.5f + j) * (j + 1));
            float Cj1 = Cj * coefficient * z;
            Sj += Cj1;
            if (Mathf.Abs(Cj1) < tol)
                break;
            Cj = Cj1;
            j++;
        }

        return Sj;
    }
}
