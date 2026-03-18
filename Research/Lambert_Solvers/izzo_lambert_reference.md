# Izzo Lambert Solver - Implementation Reference
## For C# Porting

This document provides a comprehensive reference for porting the Izzo Lambert solver 
to C#, based on the poliastro (Python/numba) and PyKEP (C++) implementations.

---

## Algorithm Overview

The Izzo algorithm solves Lambert's problem by:
1. Computing geometric parameters from position vectors
2. Finding the x parameter using Householder iterations
3. Computing y from x
4. Reconstructing velocity vectors

---

## Key Variables and Parameters

### Input Parameters
- `r1`, `r2`: Initial and final position vectors (3D)
- `tof`: Time of flight
- `mu`: Gravitational parameter (GM)
- `M`: Number of full revolutions (0 for single-arc)
- `prograde` / `cw`: Direction flag (prograde=true, retrograde=false)

### Geometric Parameters

| Variable | Description | Formula |
|----------|-------------|---------|
| `c` | Chord length | `\|r2 - r1\|` |
| `s` | Semi-perimeter | `(c + \|r1\| + \|r2\|) / 2` |
| `lambda` (ll) | Geometry parameter | `sqrt(1 - c/s)` with sign handling |
| `lambda2` | lambda squared | `lambda * lambda` |
| `lambda3` | lambda cubed | `lambda2 * lambda` |
| `T` | Non-dimensional TOF | `sqrt(2*mu/s^3) * tof` |

### Sign Handling for Lambda

The lambda parameter sign is critical for determining the transfer direction:

```cpp
// From PyKEP implementation
double lambda2 = 1.0 - m_c / m_s;
m_lambda = sqrt(lambda2);

if (ih[2] < 0.0)  // Transfer angle > 180 degrees
{
    m_lambda = -m_lambda;
    // Compute tangential vectors...
}
```

In poliastro (Python):
```python
if i_h[2] < 0:
    ll = -ll
    i_t1, i_t2 = cross(i_r1, i_h), cross(i_r2, i_h)
else:
    i_t1, i_t2 = cross(i_h, i_r1), cross(i_h, i_r2)

# Apply prograde/retrograde
ll, i_t1, i_t2 = (ll, i_t1, i_t2) if prograde else (-ll, -i_t1, -i_t2)
```

---

## The x and y Parameters

### Relationship between x and y

The algorithm uses a transformation where:
- `x` is the main variable to solve for
- `y` is computed from `x` and `lambda`

```
y = sqrt(1 - lambda^2 * (1 - x^2))
```

For elliptic orbits: -1 < x < 1
For hyperbolic orbits: x > 1 or x < -1

### Computing psi (auxiliary angle)

The psi (ψ) value is computed differently based on orbit type:

```cpp
// From PyKEP
if (-1 <= x && x < 1) {
    // Elliptic: use arccos
    psi = acos(x * y + lambda * (1 - x*x));
} else if (x > 1) {
    // Hyperbolic: use asinh
    psi = asinh((y - x * lambda) * sqrt(x*x - 1));
} else {
    // Parabolic
    psi = 0.0;
}
```

---

## Time of Flight Equation

### Main TOF Function

The TOF is computed from x using different formulas:

```python
# Poliastro - Python/numba
def _tof_equation_y(x, y, T0, ll, M):
    # Near-parabolic case using hypergeometric function
    if M == 0 and sqrt(0.6) < x < sqrt(1.4):
        eta = y - ll * x
        S_1 = (1 - ll - x * eta) * 0.5
        Q = 4 / 3 * hyp2f1b(S_1)  # Hypergeometric function
        T_ = (eta**3 * Q + 4 * ll * eta) * 0.5
    else:
        # General case
        psi = _compute_psi(x, y, ll)
        T_ = (psi + M*pi)/sqrt(abs(1-x**2)) - x + ll*y
        T_ = T_ / (1 - x**2)
    
    return T_ - T0
```

### PyKEP Version (Battin Series)

PyKEP uses the Battin series formulation which is more numerically stable:

```cpp
// From PyKEP - x2tof function
void lambert_problem::x2tof(double &tof, const double x, const int N)
{
    double K = m_lambda * m_lambda;
    double E = x * x - 1.0;
    double rho = fabs(E);
    double z = sqrt(1 + K * E);
    
    if (dist < battin) { // Near parabolic - use Battin series
        double eta = z - m_lambda * x;
        double S1 = 0.5 * (1.0 - m_lambda - x * eta);
        double Q = hypergeometricF(S1, 1e-11);
        Q = 4.0 / 3.0 * Q;
        tof = (eta * eta * eta * Q + 4.0 * m_lambda * eta) / 2.0 
              + N * M_PI / pow(rho, 1.5);
    } else {
        // Lancaster form
        double y = sqrt(rho);
        double g = x * z - m_lambda * E;
        // ... elliptic vs hyperbolic handling
    }
}
```

---

## Derivatives for Householder Iteration

The Householder method requires first, second, and third derivatives of TOF with respect to x.

### First Derivative (dT/dx)

```cpp
// From PyKEP
double DT = (3.0 * T * x - 2.0 + 2.0 * l3 * x / y) / (1.0 - x * x);
```

### Second Derivative (d²T/dx²)

```cpp
// From PyKEP
double DDT = (3.0 * T + 5.0 * x * DT + 2.0 * (1.0 - l2) * l3 / (y*y*y)) 
             / (1.0 - x * x);
```

### Third Derivative (d³T/dx³)

```cpp
// From PyKEP
double DDDT = (7.0 * x * DDT + 8.0 * DT 
               - 6.0 * (1.0 - l2) * l2 * l3 * x / (y*y*y*y*y)) 
              / (1.0 - x * x);
```

Where:
- `l2 = lambda^2`
- `l3 = lambda^3`
- `y` is computed from x and lambda

---

## Initial Guess

### Single Revolution (M=0)

```python
# Poliastro
def _initial_guess(T, ll, M, lowpath):
    if M == 0:
        T_0 = arccos(ll) + ll * sqrt(1 - ll**2)  # Eq 19
        T_1 = 2 * (1 - ll**3) / 3                 # Eq 21
        
        if T >= T_0:
            x_0 = (T_0 / T)**(2/3) - 1
        elif T < T_1:
            x_0 = 5/2 * T_1/T * (T_1 - T) / (1 - ll**5) + 1
        else:
            # Intermediate region
            x_0 = exp(log(2) * log(T/T_0) / log(T_1/T_0)) - 1
        
        return x_0
```

### Multi-Revolution (M > 0)

```python
# Poliastro
def _initial_guess(T, ll, M, lowpath):
    else:
        # Two possible solutions for multi-revolution
        x_0l = (((M*pi + pi) / (8*T))**(2/3) - 1) / \
               (((M*pi + pi) / (8*T))**(2/3) + 1)
        x_0r = (((8*T) / (M*pi))**(2/3) - 1) / \
               (((8*T) / (M*pi))**(2/3) + 1)
        
        # Select based on desired path (low vs high)
        if lowpath:
            x_0 = max(x_0l, x_0r)
        else:
            x_0 = min(x_0l, x_0r)
        
        return x_0
```

---

## Householder Iteration

The Householder method provides quartic convergence (faster than Newton's cubic).

```cpp
// From PyKEP
int lambert_problem::householder(const double T, double &x0, const int N, 
                                  const double eps, const int iter_max)
{
    int it = 0;
    double err = 1.0;
    double tof, delta, DT, DDT, DDDT;
    
    while ((err > eps) && (it < iter_max)) {
        x2tof(tof, x0, N);           // Compute TOF for current x
        dTdx(DT, DDT, DDDT, x0, tof); // Compute derivatives
        
        delta = tof - T;              // Residual
        double DT2 = DT * DT;
        
        // Householder step (quartic)
        xnew = x0 - delta * (DT2 - delta*DDT/2.0) 
                    / (DT*(DT2 - delta*DDT) + DDDT*delta*delta/6.0);
        
        err = fabs(x0 - xnew);
        x0 = xnew;
        it++;
    }
    return it;
}
```

---

## Velocity Reconstruction

Once x and y are found, velocities are computed:

```cpp
// From PyKEP - reconstructing velocities
double gamma = sqrt(mu * s / 2.0);
double rho = (R1 - R2) / c;
double sigma = sqrt(1 - rho * rho);
double y = sqrt(1.0 - lambda2 + lambda2 * x * x);

// Radial components
vr1 = gamma * ((lambda * y - x) - rho * (lambda * y + x)) / R1;
vr2 = -gamma * ((lambda * y - x) + rho * (lambda * y + x)) / R2;

// Tangential component
vt = gamma * sigma * (y + lambda * x);
vt1 = vt / R1;
vt2 = vt / R2;

// Final velocity vectors: v = vr * ir + vt * it
v1 = vr1 * ir1 + vt1 * it1;
v2 = vr2 * ir2 + vt2 * it2;
```

In Python (poliastro):
```python
# Velocity reconstruction
V_r1 = gamma * ((ll * y - x) - rho * (ll * y + x)) / r1_norm
V_r2 = -gamma * ((ll * y - x) + rho * (ll * y + x)) / r2_norm
V_t1 = gamma * sigma * (y + ll * x) / r1_norm
V_t2 = gamma * sigma * (y + ll * x) / r2_norm

v1 = V_r1 * (r1 / r1_norm) + V_t1 * i_t1
v2 = V_r2 * (r2 / r2_norm) + V_t2 * i_t2
```

---

## Multi-Revolution Handling

### Maximum Number of Revolutions

The maximum number of revolutions is determined by:

```cpp
// From PyKEP
m_Nmax = static_cast<int>(T / M_PI);  // Based on TOF
double T00 = acos(lambda) + lambda * sqrt(1 - lambda2);
double T0 = T00 + m_Nmax * M_PI;

// If TOF is below minimum for this N, reduce Nmax
if (T < T0 && m_Nmax > 0) {
    // Find minimum TOF using Halley method
    // If T < T_min, decrement m_Nmax
}
```

### Solutions for Each Revolution

For each revolution number N (0 to Nmax), there are typically 2 solutions:
- "Left" branch (low path)
- "Right" branch (high path)

```cpp
// Finding multi-revolution solutions
for (int i = 1; i <= m_Nmax; i++) {
    // Left branch
    tmp = pow((i * M_PI + M_PI) / (8.0 * T), 2.0/3.0);
    m_x[2*i - 1] = (tmp - 1) / (tmp + 1);
    householder(T, m_x[2*i - 1], i, 1e-8, 15);
    
    // Right branch  
    tmp = pow((8.0 * T) / (i * M_PI), 2.0/3.0);
    m_x[2*i] = (tmp - 1) / (tmp + 1);
    householder(T, m_x[2*i], i, 1e-8, 15);
}
```

---

## Hypergeometric Function

The hypergeometric function F(z) is used in the Battin series:

```cpp
// From PyKEP
double lambert_problem::hypergeometricF(double z, double tol)
{
    double Sj = 1.0;   // Sum
    double Cj = 1.0;   // Current term
    double err = 1.0;
    double Cj1, Sj1;
    int j = 0;
    
    while (err > tol) {
        // Continued fraction expansion
        Cj1 = Cj * (3.0 + j) * (1.0 + j) / (2.5 + j) * z / (j + 1);
        Sj1 = Sj + Cj1;
        err = fabs(Cj1);
        Sj = Sj1;
        Cj = Cj1;
        j++;
    }
    return Sj;
}
```

---

## Summary of Key Functions to Implement in C#

1. **Geometry Setup**
   - Compute chord c, semi-perimeter s
   - Compute lambda with proper sign handling
   - Compute unit vectors ir1, ir2, ih, it1, it2

2. **TOF Equation** (`x2tof`)
   - Handle near-parabolic case with Battin series
   - Handle elliptic case (Lagrange form)
   - Handle hyperbolic case

3. **Derivatives** (`dTdx`)
   - dT/dx, d2T/dx2, d3T/dx3

4. **Initial Guess** (`initial_guess`)
   - Single revolution case
   - Multi-revolution case

5. **Householder Iteration** (`householder`)
   - Quartic convergence method

6. **Velocity Reconstruction** (`reconstruct`)
   - Compute radial and tangential components
   - Transform to Cartesian coordinates

---

## References

- Original Paper: Dario Izzo, "Revisiting Lambert's Problem", AIAA 2004-4984
- Poliastro: https://github.com/poliastro/poliastro
- PyKEP: https://github.com/esa/pykep

---

## Source Files

- **Poliastro (Python/numba)**: `src/poliastro/iod/izzo.py`
- **PyKEP (C++)**: 
  - Header: `include/keplerian_toolbox/lambert_problem.hpp`
  - Implementation: `src/lambert_problem.cpp`
