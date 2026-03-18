# Izzo Lambert Solver Algorithm - Comprehensive Technical Summary

## Overview

The Izzo Lambert solver is an efficient algorithm for solving Lambert's problem, developed by Dario Izzo at the European Space Agency (ESA). The algorithm was first published around 2010-2013 and then refined in the 2014 paper "Revisiting Lambert's Problem" (arXiv:1403.2705). It is widely used in astrodynamics and is implemented in libraries like PyKEP (ESA's Python library) and poliastro.

---

## 1. Problem Formulation

### Lambert's Problem
Given:
- Initial position vector **r₁** at time t₁
- Final position vector **r₂** at time t₂
- Gravitational parameter μ
- Time of flight Δt = t₂ - t₁
- Number of complete revolutions M (usually 0 for single-revolution)
- Direction (prograde/retrograde)

Find:
- Initial velocity vector **v₁** at r₁
- Final velocity vector **v₂** at r₂

---

## 2. Key Mathematical Formulations

### 2.1 Geometry Setup

Given position vectors r₁ and r₂:

```cpp
// Chord vector
c = r₂ - r₁
c_norm = |c|

// Semiperimeter
s = (|r₁| + |r₂| + c_norm) / 2

// Unit position vectors
i_r1 = r₁ / |r₁|
i_r2 = r₂ / |r₂|

// Angular momentum direction (normal to transfer plane)
i_h = cross(i_r1, i_r2)
i_h = i_h / |i_h|
```

### 2.2 Lambda (λ) Parameter

The lambda parameter is computed as:

```cpp
// From PyKEP implementation:
lambda2 = 1 - c_norm / s           // λ²
lambda = sqrt(lambda2)             // λ (positive for prograde)

// For retrograde, lambda is negated
if (ih[2] < 0.0) {
    lambda = -lambda;  // Transfer angle > 180°
}

// For explicit retrograde motion (cw flag):
if (cw) {
    lambda = -lambda;
}
```

**Key insight**: The formula is `λ = sqrt(1 - c/s)`, NOT `sqrt(c/s)`. This is crucial for correct implementation.

### 2.3 Non-Dimensional Time of Flight

```cpp
// Non-dimensional time of flight T
T = sqrt(2 * mu / s³) * tof
```

### 2.4 Universal Variable x

The universal variable x is related to the orbital geometry:

- **x = 1** corresponds to parabolic orbit
- **x > 1** corresponds to hyperbolic orbit  
- **0 < x < 1** corresponds to elliptic orbit
- **x < 0** corresponds to "negative" semi-major axis (not physical, but part of the solution space)

The variable x is related to the semi-major axis a by:

```
a = s / (2 * (1 - x²))
```

### 2.5 Time of Flight Equation T(x)

The time of flight is computed as a function of x using different formulas depending on the orbital regime:

**For elliptic orbit (0 ≤ x < 1):**
```cpp
// Using Lancaster-Blanchard form
alpha = 2 * acos(x)
beta = 2 * asin(sqrt(lambda² / a))
tof = (a * sqrt(a) * ((alpha - sin(alpha)) - (beta - sin(beta)) + 2π * M)) / 2
```

**For hyperbolic orbit (x > 1):**
```cpp
alpha = 2 * acosh(x)
beta = 2 * asinh(sqrt(-lambda² / a))
tof = (-a * sqrt(-a) * ((beta - sinh(beta)) - (alpha - sinh(alpha))) / 2
```

**Unified form (used in poliastro):**
```cpp
// y is the second universal variable
y = sqrt(1 - λ² * (1 - x²))

// For single revolution (M=0) near elliptic region:
if (M == 0 && sqrt(0.6) < x && x < sqrt(1.4)) {
    eta = y - λ * x
    S1 = (1 - λ - x * eta) / 2
    Q = 4/3 * hyper2f1(S1)  // Hypergeometric function
    T_ = (eta³ * Q + 4 * λ * eta) / 2
} else {
    // General form using psi
    psi = compute_psi(x, y, λ)
    T_ = (psi + M*π)/sqrt(|1-x²)| - x + λ*y) / (1 - x²)
}

return T_ - T0  // where T0 is the target non-dimensional TOF
```

---

## 3. Initial Guess for x

The algorithm uses analytical approximations to generate initial guesses for Householder iteration:

### Single Revolution (M = 0)

```cpp
T0 = arccos(λ) + λ * sqrt(1 - λ²)        // Eq. 19 in paper
T1 = 2 * (1 - λ³) / 3                     // Eq. 21 in paper

if (T >= T0) {
    // Long transfer (more than half orbit)
    x0 = (T0 / T)^(2/3) - 1
} else if (T < T1) {
    // Very short transfer
    x0 = 5/2 * T1 / T * (T1 - T) / (1 - λ⁵) + 1
} else {
    // Medium transfer - use interpolation
    x0 = exp(log(2) * log(T/T0) / log(T1/T0)) - 1
}
```

### Multi-Revolution (M > 0)

```cpp
// Left branch (prograde/short way)
x_left = ((M*π + π) / (8 * T))^(2/3) - 1) / (((M*π + π) / (8 * T))^(2/3) + 1)

// Right branch (retrograde/long way)  
x_right = ((8 * T) / (M * π))^(2/3) - 1) / ((8 * T) / (M * π))^(2/3) + 1)
```

---

## 4. Householder Iteration

The algorithm uses Householder's method (a generalization of Newton's method) to find the root of the TOF equation:

```cpp
// Householder iteration (3rd order)
delta = tof(x) - T_target    // Residual
DT = dTdx(x)                 // First derivative
DDT = dTdx2(x)               // Second derivative
DDDT = dTdx3(x)              // Third derivative

x_new = x - delta * (DT² - delta*DDT/2) / 
             (DT * (DT² - delta*DDT) + DDDT * delta² / 6)
```

### Derivatives Required

```cpp
// First derivative dT/dx
DT = (3 * T * x - 2 + 2 * λ³ * x / y) / (1 - x²)

// Second derivative d²T/dx²
DDT = (3 * T + 5 * x * DT + 2 * (1 - λ²) * λ³ / y³) / (1 - x²)

// Third derivative d³T/dx³
DDDT = (7 * x * DDT + 8 * DT - 6 * (1 - λ²) * λ⁵ * x / y⁵) / (1 - x²)
```

### Convergence Criteria

- Default tolerance: 1e-8 (relative error)
- Maximum iterations: 15
- Typical convergence: 2-3 iterations for single revolution

---

## 5. Velocity Reconstruction

Once x is found, the terminal velocities are computed using the Lancaster-Blanchard formulation:

### Geometry Parameters

```cpp
gamma = sqrt(mu * s / 2)
rho = (|r₁| - |r₂|) / c_norm
sigma = sqrt(1 - rho²)
```

### Radial and Tangential Components

```cpp
// y from x
y = sqrt(1 - λ² * (1 - x²))

// Radial velocities at r1 and r2
V_r1 = gamma * ((λ * y - x) - rho * (λ * y + x)) / |r₁|
V_r2 = -gamma * ((λ * y - x) + rho * (λ * y + x)) / |r₂|

// Tangential velocities
V_t = gamma * sigma * (y + λ * x)
V_t1 = V_t / |r₁|
V_t2 = V_t / |r₂|
```

### Computing Tangential Unit Vectors

The tangential direction depends on the transfer geometry:

```cpp
// Prograde case (ih[2] >= 0)
i_t1 = cross(i_h, i_r1)
i_t2 = cross(i_h, i_r2)

// Retrograde case (ih[2] < 0)
i_t1 = cross(i_r1, i_h)
i_t2 = cross(i_r2, i_h)

// Normalize
i_t1 = i_t1 / |i_t1|
i_t2 = i_t2 / |i_t2|
```

### Final Velocity Vectors

```cpp
v₁ = V_r1 * i_r1 + V_t1 * i_t1
v₂ = V_r2 * i_r2 + V_t2 * i_t2
```

---

## 6. Direction Handling (Prograde/Retrograde)

### Automatic Detection

The algorithm automatically detects the transfer direction based on the geometry:

```cpp
// Use the z-component of angular momentum
if (i_h[2] < 0.0) {
    // Transfer angle > 180° (as seen from above z-axis)
    lambda = -lambda;
}
```

### Manual Override

For explicit prograde/retrograde selection:

```cpp
if (!prograde) {
    lambda = -lambda;
    i_t1 = -i_t1;
    i_t2 = -i_t2;
}
```

---

## 7. Multi-Revolution Solutions

The Izzo algorithm naturally handles multi-revolution solutions:

### Maximum Revolutions

```cpp
M_max = floor(T / π)
```

This is the theoretical maximum based on the non-dimensional time of flight.

### Finding All Solutions

For each revolution count M (from 0 to M_max), there are typically two solutions:
1. **Left branch**: "Short way" transfer for that revolution count
2. **Right branch**: "Long way" transfer for that revolution count

The total number of solutions is `2 * M_max + 1`.

### Solution Selection

```cpp
// Iterate through all solutions
for (int i = 0; i <= M_max; i++) {
    // Left solution for i revolutions
    x_left = solve_householder(T, i, "left")
    
    // Right solution for i revolutions  
    x_right = solve_householder(T, i, "right")
}
```

---

## 8. Common Implementation Pitfalls

### 8.1 Lambda Computation

**Pitfall**: Using `sqrt(c/s)` instead of `sqrt(1 - c/s)`

**Correct**:
```cpp
lambda = sqrt(1 - c_norm / s)
```

**Incorrect**:
```cpp
lambda = sqrt(c_norm / s)  // WRONG!
```

### 8.2 Sign of Lambda

**Pitfall**: Not handling the sign of lambda correctly for:
- Transfer angles > 180°
- Retrograde transfers
- The lowpath/highpath option

**Solution**: Always check `ih[2]` (z-component of angular momentum) and negate lambda if negative.

### 8.3 y Computation

**Pitfall**: Wrong formula for y

**Correct**:
```cpp
y = sqrt(1 - λ² * (1 - x²))
```

Note that this is `1 - λ² * (1 - x²)` NOT `(1 - λ²) * (1 - x²)`.

### 8.4 Numerical Issues Near Parabolic Orbit

**Pitfall**: Division by zero or instability when x ≈ 1 (parabolic)

**Solution**: The poliastro implementation uses different TOF formulas:
- Near x = 1: Use Lancaster-Blanchard form with hypergeometric function
- Away from x = 1: Use standard form

### 8.5 Stumpff Functions

**Pitfall**: Using wrong Stumpff function definitions

The algorithm uses c₂(z) and c₃(z) Stumpff functions:
```cpp
c2(z) = (1 - cos(sqrt(z))) / z    // for z > 0
c2(z) = (cosh(sqrt(-z)) - 1) / (-z)  // for z < 0
c2(0) = 1/2

c3(z) = (sqrt(z) - sin(sqrt(z))) / z^(3/2)  // for z > 0
c3(z) = (sinh(sqrt(-z)) - sqrt(-z)) / (-z)^(3/2)  // for z < 0
c3(0) = 1/6
```

### 8.6 Vector Collinearity Check

**Pitfall**: Not checking if r₁ and r₂ are collinear

**Solution**: 
```cpp
if (cross(r1, r2).all() == 0) {
    throw error("Lambert solution cannot be computed for collinear vectors")
}
```

---

## 9. Delta-V Computation

### Correct Formula for Delta-V

When comparing Lambert solutions to existing orbits, the delta-v should be computed as:

```cpp
// Delta-V at point 1
delta_v1 = |v_lambert_1 - v_orbit_1|

// Delta-V at point 2  
delta_v2 = |v_lambert_2 - v_orbit_2|

// Total delta-V
delta_v_total = delta_v1 + delta_v2
```

**NOT** as:
```cpp
// WRONG - This is velocity magnitude, not delta-v
delta_v_wrong = |v_lambert_1| + |v_lambert_2|
```

### Why This Matters

The Lambert solver returns absolute velocities (**v₁**, **v₂**) required to go from r₁ to r₂ in the specified time. To compute the actual propellant required:

1. Subtract the existing orbital velocity at each point
2. Take the magnitude of the difference
3. Sum the magnitudes for total delta-V

This is essential for mission planning where you're already on an orbit and need to execute a maneuver to enter the Lambert transfer trajectory.

---

## 10. Algorithm Summary

```
INPUT: r1, r2, tof, mu, M, prograde, lowpath
OUTPUT: v1, v2

1. Compute geometry:
   c = |r2 - r1|
   s = (|r1| + |r2| + c) / 2
   
2. Compute lambda:
   lambda = sqrt(1 - c/s)
   Adjust sign based on geometry and prograde flag
   
3. Non-dimensional time:
   T = sqrt(2*mu/s³) * tof
   
4. Find x using Householder iteration:
   - Generate initial guess
   - Iterate until convergence
   - Compute y from x
   
5. Reconstruct velocities:
   - Compute gamma, rho, sigma
   - Compute radial/tangential components
   - Combine with unit vectors
   
6. Return v1, v2
```

---

## 11. References

1. Dario Izzo, "Revisiting Lambert's Problem", arXiv:1403.2705, 2014
2. PyKEP library (ESA): https://github.com/esa/pykep
3. poliastro library: https://github.com/poliastro/poliastro
4. Curtis, "Orbital Mechanics for Engineering Students", Chapter 5

---

## 12. Key Implementation Differences: PyKEP vs Poliastro

| Aspect | PyKEP | Poliastro |
|--------|-------|-----------|
| Language | C++ with Python bindings | Python with Numba JIT |
| TOF Formula | Multiple (Lancaster, Battin, Lagrange) | Unified with hyper2f1 |
| Initial Guess | Different formulas per regime | Similar with corrections |
| Multi-rev | Full support | Full support |
| Convergence | Householder (3rd order) | Householder (3rd order) |
| Tolerance | 1e-13 | 1e-8 |

Both implementations are authoritative and produce consistent results. The poliastro version includes bug fixes (e.g., issue #1362 regarding initial guess formula).
