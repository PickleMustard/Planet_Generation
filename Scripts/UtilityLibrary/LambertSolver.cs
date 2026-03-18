using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using UtilityLibrary;

namespace UtilityLibrary;

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
    public static TrajectorySolution Solve(
        Vector3 r1,
        Vector3 r2,
        float timeOfFlight,
        float mu,
        bool prograde = true,
        int revolutions = 0,
        bool lowPath = true
    )
    {
        GameLogger.EnterFunction("LambertSolver.Solve",
            $"r1={r1}, r2={r2}, TOF={timeOfFlight}, mu={mu}, prograde={prograde}, revs={revolutions}");

        // Validate inputs
        float r1Mag = r1.Length();
        float r2Mag = r2.Length();

        if (r1Mag < 1e-6f || r2Mag < 1e-6f)
        {
            GameLogger.Warning("LambertSolver: Degenerate case - position vector near zero");
            return CreateZeroSolution();
        }

        if (timeOfFlight <= 0f)
        {
            GameLogger.Warning("LambertSolver: Time of flight must be positive");
            return CreateZeroSolution();
        }

        if (mu <= 0f)
        {
            GameLogger.Warning("LambertSolver: Gravitational parameter must be positive");
            return CreateZeroSolution();
        }

        // Check collinearity
        Vector3 crossProduct = r1.Cross(r2);
        if (crossProduct.LengthSquared() < 1e-12f)
        {
            GameLogger.Warning("LambertSolver: Nearly collinear vectors, using degenerate solver");
            return SolveDegenerate(r1, r2, timeOfFlight, mu, prograde, revolutions);
        }

        // ================================================================
        // 1. Compute geometry
        // ================================================================
        Vector3 chord = r2 - r1;
        float cNorm = chord.Length();
        float s = (r1Mag + r2Mag + cNorm) * 0.5f; // Semi-perimeter

        // Unit vectors
        Vector3 ir1 = r1 / r1Mag;
        Vector3 ir2 = r2 / r2Mag;
        Vector3 ih = ir1.Cross(ir2).Normalized();

        // ================================================================
        // 2. Compute lambda and tangential directions
        // ================================================================
        float lambda2 = 1f - Mathf.Min(1f, cNorm / s);
        float lambda = Mathf.Sqrt(lambda2);

        Vector3 it1, it2;

        // Handle transfer angle direction based on angular momentum z-component
        if (ih.Y < 0f) // Using Y as "up" in Godot's coordinate system
        {
            lambda = -lambda;
            it1 = ir1.Cross(ih);
            it2 = ir2.Cross(ih);
        }
        else
        {
            it1 = ih.Cross(ir1);
            it2 = ih.Cross(ir2);
        }
        it1 = it1.Normalized();
        it2 = it2.Normalized();

        // Handle prograde/retrograde
        if (!prograde)
        {
            lambda = -lambda;
            it1 = -it1;
            it2 = -it2;
        }

        // ================================================================
        // 3. Non-dimensional time of flight
        // ================================================================
        float T = Mathf.Sqrt(2f * mu / (s * s * s)) * timeOfFlight;

        // ================================================================
        // 4. Compute reference times and check feasibility
        // ================================================================
        float lambda3 = lambda * lambda2;
        float T00 = Mathf.Acos(lambda) + lambda * Mathf.Sqrt(1f - lambda2);
        float T1 = 2f / 3f * (1f - lambda3);

        // Check maximum feasible revolutions
        int nMax = (int)(T / Mathf.Pi);

        if (nMax > 0 && revolutions > 0)
        {
            // Refine nMax by checking minimum T for multi-rev
            float tMin = T00 + nMax * Mathf.Pi;
            if (T < tMin)
            {
                float xTMin = FindMinimumTofX(lambda, lambda2, nMax);
                float actualTMin = ComputeTimeOfFlight(xTMin, lambda, lambda2, nMax);
                if (T < actualTMin)
                {
                    nMax -= 1;
                }
            }
        }

        if (revolutions > nMax)
        {
            GameLogger.Warning(
                $"LambertSolver: Requested {revolutions} revolutions but max feasible is {nMax}");
            return CreateZeroSolution();
        }

        // ================================================================
        // 5. Initial guess for x
        // ================================================================
        float x0;
        if (revolutions == 0)
        {
            x0 = InitialGuessSingleRev(T, lambda, lambda2, lambda3, T00, T1);
        }
        else
        {
            x0 = InitialGuessMultiRev(T, revolutions, lowPath);
        }

        // ================================================================
        // 6. Householder iteration to find x
        // ================================================================
        float tolerance = revolutions == 0 ? HouseholderTolerance : MultiRevTolerance;
        float x = HouseholderIteration(x0, T, lambda, lambda2, revolutions, tolerance, MaxHouseholderIterations);

        // Compute y from x
        float y = ComputeY(x, lambda);

        // ================================================================
        // 7. Reconstruct terminal velocities
        // ================================================================
        float gamma = Mathf.Sqrt(mu * s / 2f);
        float rho = (r1Mag - r2Mag) / cNorm;
        float sigma = Mathf.Sqrt(1f - rho * rho);

        // Radial velocity components
        float lxy = lambda * y;
        float vr1 = gamma * ((lxy - x) - rho * (lxy + x)) / r1Mag;
        float vr2 = -gamma * ((lxy - x) + rho * (lxy + x)) / r2Mag;

        // Tangential velocity component
        float vt = gamma * sigma * (y + lambda * x);
        float vt1 = vt / r1Mag;
        float vt2 = vt / r2Mag;

        // Construct velocity vectors: v = vr * ir + vt * it
        Vector3 v1 = vr1 * ir1 + vt1 * it1;
        Vector3 v2 = vr2 * ir2 + vt2 * it2;

        // ================================================================
        // 8. Compute orbital parameters
        // ================================================================
        float semiMajorAxis = s / (2f * (1f - x * x));
        float eccentricity = ComputeEccentricity(r1, v1, mu);

        TransferType transferType = revolutions == 0
            ? TransferType.Direct
            : TransferType.MultiRev;

        var solution = new TrajectorySolution(
            v1,
            v2,
            timeOfFlight,
            semiMajorAxis,
            eccentricity,
            revolutions,
            transferType
        );

        GameLogger.ExitFunction("LambertSolver.Solve", solution.GetDescription());
        return solution;
    }

    /// <summary>
    /// Solves Lambert's problem and returns ALL feasible solutions across revolution counts.
    /// For 0 revolutions: 1 solution.
    /// For each revolution N (1..maxRevolutions): up to 2 solutions (low-path and high-path).
    /// Total: up to 2*maxRevolutions + 1 solutions.
    /// </summary>
    /// <param name="r1">Initial position vector (m)</param>
    /// <param name="r2">Final position vector (m)</param>
    /// <param name="timeOfFlight">Time of flight in seconds</param>
    /// <param name="mu">Gravitational parameter (m³/s²)</param>
    /// <param name="prograde">True for prograde orbit, false for retrograde</param>
    /// <param name="maxRevolutions">Maximum number of complete revolutions to consider</param>
    /// <returns>List of all feasible TrajectorySolution objects</returns>
    public static List<TrajectorySolution> SolveAll(
        Vector3 r1,
        Vector3 r2,
        float timeOfFlight,
        float mu,
        bool prograde = true,
        int maxRevolutions = 3
    )
    {
        var solutions = new List<TrajectorySolution>();

        // 0-revolution solution
        var zeroRev = Solve(r1, r2, timeOfFlight, mu, prograde, 0, true);
        if (zeroRev.InitialVelocity.LengthSquared() > 0f)
        {
            solutions.Add(zeroRev);
        }

        // Multi-revolution solutions (two branches per revolution count)
        for (int rev = 1; rev <= maxRevolutions; rev++)
        {
            // Low path (left branch)
            var lowPath = Solve(r1, r2, timeOfFlight, mu, prograde, rev, true);
            if (lowPath.InitialVelocity.LengthSquared() > 0f)
            {
                solutions.Add(lowPath);
            }

            // High path (right branch)
            var highPath = Solve(r1, r2, timeOfFlight, mu, prograde, rev, false);
            if (highPath.InitialVelocity.LengthSquared() > 0f)
            {
                solutions.Add(highPath);
            }
        }

        return solutions;
    }

    /// <summary>
    /// Finds the best (minimum delta-v) multi-revolution solution.
    /// Backwards-compatible wrapper that returns a single best solution.
    /// </summary>
    public static TrajectorySolution SolveMultiRev(
        Vector3 r1,
        Vector3 r2,
        float timeOfFlight,
        float mu,
        bool prograde = true,
        int maxRevolutions = 3
    )
    {
        var allSolutions = SolveAll(r1, r2, timeOfFlight, mu, prograde, maxRevolutions);

        if (allSolutions.Count == 0)
        {
            return Solve(r1, r2, timeOfFlight, mu, prograde, 0);
        }

        // Find minimum delta-v solution
        TrajectorySolution best = allSolutions[0];
        foreach (var sol in allSolutions)
        {
            if (sol.DeltaVRequired > 0f && sol.DeltaVRequired < best.DeltaVRequired)
            {
                best = sol;
            }
        }

        return best;
    }

    // ================================================================
    // Time of Flight Equation and Derivatives
    // ================================================================

    /// <summary>
    /// Computes y = sqrt(1 - λ²(1 - x²)).
    /// </summary>
    private static float ComputeY(float x, float lambda)
    {
        float val = 1f - lambda * lambda * (1f - x * x);
        return Mathf.Sqrt(Mathf.Max(0f, val));
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

        float K = lambda2;        // λ²
        float E = x * x - 1f;     // x² - 1
        float rho = Mathf.Abs(E);
        float z = Mathf.Sqrt(1f + K * E);

        // Use Battin series when very close to parabolic
        if (dist < battinThreshold)
        {
            float eta = z - lambda * x;
            float S1 = 0.5f * (1f - lambda - x * eta);
            float Q = 4f / 3f * HypergeometricF(S1, 1e-11f);
            float tof = (eta * eta * eta * Q + 4f * lambda * eta) / 2f
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
            if (logArg <= 0f) logArg = 1e-10f; // Prevent NaN from log of non-positive
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
            if (lambda < 0f) beta = -beta;
            return a * Mathf.Sqrt(a) * ((alfa - Mathf.Sin(alfa)) - (beta - Mathf.Sin(beta)) + 2f * Mathf.Pi * N) / 2f;
        }
        else // Hyperbolic
        {
            float alfa = 2f * Acosh(x);
            float sinhArg = Mathf.Sqrt(Mathf.Clamp(-lambda2 / a, 0f, float.MaxValue));
            float beta = 2f * Asinh(sinhArg);
            if (lambda < 0f) beta = -beta;
            return -a * Mathf.Sqrt(-a) * ((beta - Sinh(beta)) - (alfa - Sinh(alfa))) / 2f;
        }
    }

    /// <summary>
    /// Computes the first, second, and third derivatives of T(x) with respect to x.
    /// These are the exact analytical derivatives from Izzo's algorithm:
    ///   dT/dx   = (3Tx - 2 + 2λ³x/y) / (1 - x²)
    ///   d²T/dx² = (3T + 5x·dT + 2(1-λ²)λ³/y³) / (1 - x²)
    ///   d³T/dx³ = (7x·d²T + 8·dT - 6(1-λ²)λ⁵x/y⁵) / (1 - x²)
    /// </summary>
    private static void ComputeDerivatives(
        float x, float lambda, float lambda2, float T,
        out float DT, out float DDT, out float DDDT)
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

    // ================================================================
    // Iteration Methods
    // ================================================================

    /// <summary>
    /// Householder iteration (quartic convergence) to find the root of T(x) - T_target = 0.
    /// Uses the formula: x_new = x - f(f'² - f·f''/2) / (f'(f'² - f·f'') + f'''·f²/6)
    /// </summary>
    private static float HouseholderIteration(
        float x0, float T, float lambda, float lambda2, int N,
        float tolerance, int maxIterations)
    {
        float x = x0;
        for (int i = 0; i < maxIterations; i++)
        {
            float tofComputed = ComputeTimeOfFlight(x, lambda, lambda2, N);
            float delta = tofComputed - T;

            ComputeDerivatives(x, lambda, lambda2, tofComputed, out float DT, out float DDT, out float DDDT);

            if (Mathf.Abs(DT) < 1e-12f)
            {
                GameLogger.Warning("LambertSolver: Zero derivative in Householder iteration");
                break;
            }

            // Householder step (quartic convergence)
            float DT2 = DT * DT;
            float xNew = x - delta * (DT2 - delta * DDT / 2f)
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
            ComputeDerivatives(x, lambda, lambda2, tMin, out float DT, out float DDT, out float DDDT);

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
        float T, float lambda, float lambda2, float lambda3, float T00, float T1)
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

    // ================================================================
    // Utility Functions
    // ================================================================

    /// <summary>
    /// Computes eccentricity from position and velocity at a point.
    /// </summary>
    private static float ComputeEccentricity(Vector3 r, Vector3 v, float mu)
    {
        float rMag = r.Length();
        if (rMag < 1e-10f || mu < 1e-10f) return 0f;

        float vSq = v.LengthSquared();
        float rvDot = r.Dot(v);

        // Eccentricity vector: e = ((v² - μ/r)r - (r·v)v) / μ
        Vector3 eVec = (r * (vSq - mu / rMag) - v * rvDot) / mu;
        return eVec.Length();
    }

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
            if (Mathf.Abs(Cj1) < tol) break;
            Cj = Cj1;
            j++;
        }

        return Sj;
    }

    /// <summary>
    /// Inverse hyperbolic cosine: acosh(x) = ln(x + sqrt(x² - 1))
    /// </summary>
    private static float Acosh(float x)
    {
        if (x < 1f) return 0f;
        return (float)Math.Log(x + Math.Sqrt(x * x - 1.0));
    }

    /// <summary>
    /// Inverse hyperbolic sine: asinh(x) = ln(x + sqrt(x² + 1))
    /// </summary>
    private static float Asinh(float x)
    {
        return (float)Math.Log(x + Math.Sqrt(x * x + 1.0));
    }

    /// <summary>
    /// Hyperbolic sine.
    /// </summary>
    private static float Sinh(float x)
    {
        return (float)Math.Sinh(x);
    }
}
