# Izzo Lambert Solver: Comprehensive Research Summary

## Table of Contents
1. [What is the Izzo Lambert Solver?](#1-what-is-the-izzo-lambert-solver)
2. [Key Algorithmic Steps](#2-key-algorithmic-steps)
3. [Reference Implementations](#3-reference-implementations)
4. [Edge Cases and Important Considerations](#4-edge-cases-and-important-considerations)
5. [Citations](#5-citations)

---

## 1. What is the Izzo Lambert Solver?

### Overview

The **Izzo Lambert Solver** is a highly efficient algorithm for solving **Lambert's Problem** (also known as the **Orbital Boundary Value Problem**), which is a fundamental problem in astrodynamics and orbital mechanics.

### What is Lambert's Problem?

Lambert's Problem is defined as:
- **Given**: Initial position (r₁), final position (r₂), and time of flight (Δt)
- **Find**: The trajectory (orbital elements and velocity vectors) that connects r₁ to r₂ in the specified time

This is essential for:
- Interplanetary trajectory planning
- Orbital rendezvous calculations
- Mission design for spacecraft
- Transfer orbit computations

### Historical Context and Development

The Izzo algorithm was developed by **Dario Izzo** at the European Space Agency (ESA). The original work built upon the Lancaster and Blanchard approach to Lambert's problem. The algorithm has gone through iterations:

- **2010-2013**: Initial versions appeared in various papers and software
- **2014**: The final, refined version was published in "Revisiting Lambert's Problem" (arXiv:1403.2705)

The 2014 version is approximately **1.8 times faster** than previous versions while maintaining numerical accuracy comparable to Gooding's procedure.

### Key Innovations

The Izzo algorithm introduces several key innovations:

1. **New Variable Representation**: Uses a variable that represents all problem classes under L-similarity (Laplacian similarity), simplifying the mathematical formulation

2. **Time of Flight Curves**: The TOF curves in the new variable have two oblique asymptotes, making them amenable to piecewise continuous line approximation

3. **Simple Initial Guess**: Uses and inverts a simple approximation to provide an efficient initial guess

4. **Householder Iteration**: Applies Householder's method (rather than Newton-Raphson) for rapid convergence - typically converges in only **2 iterations** for the single-revolution case

5. **Unified Treatment**: Handles elliptic, parabolic, and hyperbolic orbits in a unified framework

---

## 2. Key Algorithmic Steps

### Step 1: Problem Setup and Geometry Calculation

```csharp
// Calculate geometric parameters
float c = (r1 - r2).Length();                    // Chord length
float s = (r1Mag + r2Mag + c) / 2f;              // Semi-perimeter
float lambda = Mathf.Sqrt(1f - c / s);            // Lambda parameter
```

The algorithm uses the **chord length (c)**, **semi-perimeter (s)**, and **lambda parameter** to characterize the geometry of the transfer.

### Step 2: Determine Transfer Direction

```csharp
Vector3 r1CrossR2 = r1.Cross(r2);
int direction = prograde ? 1 : -1;
// Use cross product sign to determine actual orbital plane
if (r1CrossR2.Length() > 1e-6f)
{
    int crossSign = r1CrossR2.Y > 0 ? 1 : -1;
    direction = crossSign == (prograde ? 1 : -1) ? 1 : -1;
}
```

The algorithm must determine:
- **Prograde vs. Retrograde**: Whether the transfer orbits in the same direction as the central body's rotation
- **Multi-revolution solutions**: Number of complete revolutions (0 for direct transfer)

### Step 3: Initial Guess for Universal Variable x

```csharp
// Initial guess based on Kepler equation approximation
float a = s / 2f * (1f + (float)Math.Pow(lambdaSimple(c, s), 3));
float x0 = Mathf.Pow(3f * tof / a, 1f / 3f) - 1f;
x0 = Mathf.Clamp(x0, -0.999f, 100f);
```

The **universal variable x** is related to the semi-major axis:
- **x = 1**: Parabolic orbit
- **x > 1**: Hyperbolic orbit  
- **0 < x < 1**: Elliptic orbit
- **x < 0**: Elliptic orbit (with perihelion inside the chord)

### Step 4: Householder Iteration

```csharp
for (int i = 0; i < MaxIterations; i++)
{
    float tofCalc = ComputeTimeOfFlight(x, ...);
    float dTofDx = ComputeDTimeOfFlightDx(x, ...);
    float d2TofDx2 = ComputeD2TimeOfFlightDx2(x, ...);
    
    // Householder update formula
    float h = (tofCalc - tof) / dTofDx;
    float h2 = h * d2TofDx2 / (2f * dTofDx);
    float xNew = x - h * (1f + h * d2TofDx2 / (2f * dTofDx)) / 
                        (1f + h * d2TofDx2 / dTofDx);
    
    if (Mathf.Abs(xNew - x) < Tolerance) return xNew;
}
```

The **Householder method** is an extension of Newton-Raphson that uses second-order derivatives for faster convergence. It typically converges in 2 iterations.

### Step 5: Time of Flight Computation

The TOF is computed using different formulas depending on the orbital type:

```csharp
if (x > 1f) // Hyperbolic case
{
    float sqrtX = Mathf.Sqrt(x);
    float asinhX = Mathf.Asinh(sqrtX);
    term2 = (x * x - 1f) * (x * x + 7f) * asinhX / (3f * x * sqrtX);
}
else if (x < -0.9f) // Near parabolic
{
    float sqrtX = Mathf.Sqrt(1f - x);
    term2 = (1f - x * x) * (1f + 7f * x) * Mathf.Atanh(sqrtX) / (3f * sqrtX);
}
else // Elliptic case
{
    float sqrtX = Mathf.Sqrt(1f - x);
    float acosX = Mathf.Acos(x);
    term2 = (1f - x * x) * (1f + 7f * x) * acosX / (3f * sqrtX);
}

// Add multi-revolution term
float revTerm = 2f * Mathf.Pi * revolutions * s / sqrtMu;

return (term1 * Mathf.Sqrt(term2) + revTerm) / (3f * sqrtMu);
```

### Step 6: Orbit Reconstruction

Once x is found, compute velocity vectors:

```csharp
// Calculate alpha and beta parameters
if (x > 1f) // Hyperbolic
{
    alpha = 2f * asinh(sqrtX) / sqrtX;
}
else if (x < -0.9f) // Near parabolic
{
    alpha = 2f * tanh(sqrtX) / sqrtX;
}
else // Elliptic
{
    alpha = 2f * acos(x) / sqrtX;
    beta = 2f * asin(x * sqrtX) / sqrtX;
}

// Radial and tangential components
float radial1 = (s * (x + 1f) - r1Mag) / (r1Mag * beta);
float tangential1 = (s * (x + 1f)) / (r1Mag * alpha);

// Construct velocity vectors using orbital plane geometry
Vector3 v1 = radial1 * r1Hat + tangential1 * tangent1;
v1 *= Mathf.Sqrt(mu / s);  // Scale by sqrt(mu/s)
```

---

## 3. Reference Implementations

### Primary Academic Paper

1. **"Revisiting Lambert's Problem"** (2014)
   - Author: Dario Izzo
   - arXiv: arXiv:1403.2705
   - Published in: Celestial Mechanics and Dynamical Astronomy, 2014
   - DOI: https://doi.org/10.1007/s10569-014-9587-y
   - URL: https://arxiv.org/abs/1403.2705

### Software Libraries

| Library | Language | Notes |
|---------|----------|-------|
| **PaGMO/PyGMO** | C++/Python | ESA-developed, includes original Izzo implementation |
| **poliastro** | Python | Open-source astrodynamics library, uses Izzo algorithm |
| **pykep** | C++/Python | ESA's library, contains original lambert solver |
| **ORBIT** | MATLAB | Academic orbital mechanics toolbox |

### Implementation Details

#### poliastro (Python)
- Uses `lambert_izzo` function
- Accelerated with Numba (JIT compilation)
- Available at: https://github.com/poliastro/poliastro

#### PaGMO/PyGMO (C++/Python)
- Original implementation from ESA
- Used for GTOP (Global Trajectory Optimization Problems)
- Available at: https://github.com/esa/pagmo

### Key Code References

The implementation in your project (`LambertSolver.cs`) follows the algorithm and includes:
- Householder iteration for convergence
- Handling of elliptic, parabolic, and hyperbolic cases
- Multi-revolution support
- Degenerate case handling

---

## 4. Edge Cases and Important Considerations

### 4.1 Degenerate Geometries

| Case | Description | Handling |
|------|-------------|----------|
| **Near-zero positions** | r₁ or r₂ very close to origin | Check for r < 1e-6, return zero solution |
| **Aligned positions** | r₁ and r₂ nearly collinear | Use simplified circular orbit approximation |
| **Zero time of flight** | Δt ≈ 0 | Return zero/direct transfer solution |
| **λ → 0** | Nearly 180° transfer (antinodal) | Use degenerate case solver |

### 4.2 Orbital Regimes

| Regime | x value | Formula Type |
|--------|---------|--------------|
| Elliptic | 0 < x < 1 | `acos(x)` - trigonometric |
| Parabolic | x = 1 | Limiting case |
| Hyperbolic | x > 1 | `asinh(x)` - hyperbolic |
| Near-parabolic | x ≈ 1 | `atanh(x)` - special handling |

### 4.3 Multi-Revolution Solutions

```csharp
// The time of flight includes a term for complete revolutions
float revTerm = 2f * Mathf.Pi * revolutions * s / sqrtMu;
```

- **rev = 0**: Direct transfer (single revolution)
- **rev > 0**: Multi-revolution transfers (more energy, longer transfer time)
- Different revolution counts yield different ΔV requirements

### 4.4 Numerical Precision

| Parameter | Typical Value | Notes |
|-----------|---------------|-------|
| Tolerance | 1e-8 | Convergence criterion |
| Max Iterations | 50 | Prevents infinite loops |
| Min Position | 1e-6 | Degenerate case detection |

### 4.5 Prograde vs. Retrograde

The algorithm must correctly handle:
- **Prograde**: Orbit in same direction as central body rotation
- **Retrograde**: Orbit in opposite direction
- Determined by the sign of r₁ × r₂ (cross product)

### 4.6 Common Pitfalls

1. **Wrong sign in direction**: Ensure cross product sign matches expected orbit direction
2. **Near-parabolic instability**: Use special formulas when |x| ≈ 1
3. **Scaling issues**: Ensure μ (gravitational parameter) is in consistent units
4. **Velocity reconstruction**: The scale factor `√(μ/s)` is critical for correct magnitudes

### 4.7 Performance Considerations

- **Convergence**: Typically 2 iterations with Householder method
- **Complexity**: O(n) per iteration, much faster than Gooding's procedure
- **Memory**: Minimal - only a few float variables needed
- **Accuracy**: Comparable to Gooding's method, better than 1e-8 typically

---

## 5. Citations

### Primary Source

1. **Izzo, D. (2014). "Revisiting Lambert's Problem"**
   - arXiv:1403.2705
   - Celestial Mechanics and Dynamical Astronomy, 121(3), 239-252
   - DOI: https://doi.org/10.1007/s10569-014-9587-y
   - URL: https://arxiv.org/abs/1403.2705

### Software Implementations

2. **poliastro - Astrodynamics in Python**
   - GitHub: https://github.com/poliastro/poliastro
   - Documentation: https://docs.poliastro.space/

3. **PaGMO/PyGMO - ESA Optimization Library**
   - GitHub: https://github.com/esa/pagmo
   - Used for GTOP: https://www.esa.int/gsp/ACT/projects/gtop/

### Related Work

4. **Lancaster, E.R., & Blanchard, R.C. (1969). "A Unified Form of Lambert's Problem"**
   - NASA TN D-5368
   - Foundational work that Izzo builds upon

5. **Gooding, R.H. (1990). "A Procedure for the Solution of Lambert's Orbital Boundary-Value Problem"**
   - Celestial Mechanics and Dynamical Astronomy, 48(2), 145-165
   - Alternative solver, used for comparison in Izzo's paper

---

## Summary

The Izzo Lambert solver is a **highly efficient, numerically stable algorithm** for solving Lambert's problem. It combines:

- **Efficiency**: ~1.8x faster than previous versions, converges in ~2 iterations
- **Accuracy**: Comparable to Gooding's established method
- **Robustness**: Handles degenerate cases and all orbital regimes
- **Simplicity**: Unified treatment of elliptic, parabolic, and hyperbolic orbits

The algorithm is widely used in:
- Spacecraft trajectory optimization
- Mission planning tools
- Astrodynamics software (poliastro, PaGMO, ORKit)

---

*Research compiled: March 14, 2026*
