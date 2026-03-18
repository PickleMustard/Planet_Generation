# Orbital Mechanics Research: Multi-Body Systems Around a Barycenter

This document provides implementation-ready formulas for computing stable orbital velocities in multi-body systems orbiting a shared barycenter, suitable for C#/Godot implementation.

---

## 1. Two-Body Problem Around a Barycenter

### 1.1 Barycenter Calculation

Given two bodies with masses `M1` and `M2` at position vectors `r1` and `r2`, the barycenter position is:

```csharp
// Barycenter position
Vector3 barycenter = (M1 * r1 + M2 * r2) / (M1 + M2);

// Relative position vector (body 2 relative to body 1)
Vector3 r = r2 - r1;
```

### 1.2 Orbital Elements from State Vectors

Given position vector `r` and velocity vector `v` relative to the barycenter, with known gravitational parameter `μ = G * (M1 + M2)`:

**Specific Angular Momentum:**
```csharp
Vector3 h = r.Cross(v);  // h = r × v
float hMag = h.Length();
```

**Eccentricity Vector (points toward periapsis):**
```csharp
Vector3 eVec = (v.Cross(h) / μ) - (r.Normalized());
float e = eVec.Length();
```

**Semi-major Axis:**
```csharp
float vMag = v.Length();
float rMag = r.Length();
float a = 1.0f / (2.0f / rMag - vMag * vMag / μ);
```

**Specific Orbital Energy:**
```csharp
float energy = vMag * vMag / 2.0f - μ / rMag;
```

**True Anomaly (angle from periapsis):**
```csharp
float trueAnomaly = Mathf.Atan2(r.Cross(v).Dot(eVec), eVec.Dot(h));
```

---

## 2. Perifocal Frame Construction

The perifocal frame is a 2D coordinate system in the orbital plane, with:
- **Origin**: At the focus (barycenter)
- **pHat axis**: Points toward periapsis (direction of eccentricity vector)
- **qHat axis**: Perpendicular to pHat, in the orbital plane, 90° ahead in direction of motion

### 2.1 Computing pHat and qHat from Position/Mass

Given position vectors relative to barycenter:

```csharp
// For body orbiting around barycenter:
// r1 = position of body 1 relative to barycenter
// r2 = position of body 2 relative to barycenter
// M1, M2 = masses

// The relative position vector between bodies
Vector3 r = r2 - r1;

// Compute orbital angular momentum (perpendicular to orbital plane)
Vector3 h = r.Cross(v);  // v is velocity of body2 relative to body1

// pHat: direction of eccentricity vector (points to periapsis)
Vector3 pHat = eVec.Normalized();

// qHat: perpendicular to both h and pHat (in orbital plane)
Vector3 qHat = h.Cross(pHat).Normalized();
```

### 2.2 Computing Perifocal Frame Directly from State Vectors

Given position `r` and velocity `v`:

```csharp
Vector3 pHat = ((v.Cross(r.Cross(v))) / (μ * rMag) - (r / rMag)).Normalized();
Vector3 h = r.Cross(v);
Vector3 qHat = h.Cross(pHat).Normalized();
```

### 2.3 C# Implementation Structure

```csharp
public struct PerifocalFrame
{
    public Vector3 Origin;      // Barycenter position
    public Vector3 pHat;        // Periapsis direction
    public Vector3 qHat;        // Perpendicular to pHat in orbital plane
    
    public static PerifocalFrame FromStateVectors(
        Vector3 r,      // Position relative to barycenter
        Vector3 v,      // Velocity relative to barycenter
        float mu)       // Gravitational parameter G*(M1+M2)
    {
        // Specific angular momentum
        Vector3 h = r.Cross(v);
        
        // Eccentricity vector
        Vector3 eVec = (v.Cross(h) / mu) - (r.Normalized());
        
        // pHat points toward periapsis (eccentricity direction)
        Vector3 pHat = eVec.Normalized();
        
        // qHat perpendicular to both h and pHat
        Vector3 qHat = h.Cross(pHat).Normalized();
        
        return new PerifocalFrame
        {
            Origin = Vector3.Zero,  // At barycenter
            pHat = pHat,
            qHat = qHat
        };
    }
}
```

---

## 3. Velocity Calculation for Elliptical Orbits

### 3.1 Vis-Viva Equation

The vis-viva equation gives the orbital speed at any point:

```
v² = μ × (2/r - 1/a)

where:
- v = orbital speed (magnitude)
- μ = G × (M1 + M2) = gravitational parameter (m³/s²)
- r = current distance from focus (m)
- a = semi-major axis (m)
```

**C# Implementation:**
```csharp
float CalculateOrbitalVelocity(float mu, float r, float a)
{
    return Mathf.Sqrt(mu * (2.0f / r - 1.0f / a));
}
```

### 3.2 Determining Semi-Major Axis from Positions

For a two-body system, you can compute `a` from the positions and velocities:

```csharp
float CalculateSemiMajorAxis(Vector3 r, Vector3 v, float mu)
{
    float rMag = r.Length();
    float vMag = v.Length();
    
    // From vis-viva: energy = -μ/(2a) = v²/2 - μ/r
    // Solving for a:
    return 1.0f / (2.0f / rMag - vMag * vMag / mu);
}
```

Alternatively, from apoapsis and periapsis distances:
```csharp
float CalculateSemiMajorAxis(float rApoapsis, float rPeriapsis)
{
    return (rApoapsis + rPeriapsis) / 2.0f;
}
```

### 3.3 Velocity Magnitude at Any Point

The velocity magnitude varies around the orbit:

```csharp
public enum OrbitPoint { Periapsis, Apoapsis, Current }

float GetVelocityAtPoint(float mu, float a, float e, OrbitPoint point)
{
    switch (point)
    {
        case OrbitPoint.Periapsis:
            // r_p = a(1-e)
            float rP = a * (1.0f - e);
            return Mathf.Sqrt(mu * (2.0f / rP - 1.0f / a));
            
        case OrbitPoint.Apoapsis:
            // r_a = a(1+e)
            float rA = a * (1.0f + e);
            return Mathf.Sqrt(mu * (2.0f / rA - 1.0f / a));
            
        default:
            throw new System.ArgumentException("Use CalculateVelocityAtPosition");
    }
}

float CalculateVelocityAtPosition(float mu, float a, float r)
{
    return Mathf.Sqrt(mu * (Mathf.Abs(2.0f / r - 1.0f / a)));
}
```

### 3.4 Velocity Vector in Perifocal Frame

The velocity vector in perifocal coordinates:

```
v = v_r × pHat + v_θ × qHat

where:
- v_r = radial velocity component (toward/away from focus)
- v_θ = tangential velocity component (perpendicular to radial)
```

```csharp
Vector3 CalculatePerifocalVelocityVector(
    float mu, 
    float a, 
    float e, 
    float trueAnomaly)
{
    float r = a * (1.0f - e * e) / (1.0f + e * Mathf.Cos(trueAnomaly));
    
    // Velocity components in perifocal frame
    float vTheta = Mathf.Sqrt(mu * a) / r;  // Tangential component
    float vr = Mathf.Sqrt(mu / a) * e * Mathf.Sin(trueAnomaly) / 
               Mathf.Sqrt(1.0f - e * e);     // Radial component
    
    // Convert to 3D vector
    return vr * pHat + vTheta * qHat;
}
```

---

## 4. Circular vs. Elliptical Orbit Assumptions

### 4.1 Why Circular Formula Fails for Elliptical Orbits

**Circular orbit formula:** `v = √(μ/r)`

This assumes:
- Constant orbital radius `r` (perfect circle)
- Constant velocity magnitude

**Problem:** For elliptical orbits:
- `r` varies between `r_p` (periapsis) and `r_a` (apoapsis)
- Velocity is NOT constant - faster at periapsis, slower at apoapsis

### 4.2 Velocity Comparison Example

For an orbit with `a = 1000 km`, `e = 0.5`:

| Location | r (km) | v using √(μ/r) | v using vis-viva |
|----------|--------|----------------|------------------|
| Periapsis | 500 | Incorrect | Correct |
| Apoapsis | 1500 | Incorrect | Correct |
| Semi-major axis | 1000 | Correct | Correct |

### 4.3 The Key Difference

**Circular formula:** `v = √(μ/r)` 
- Assumes orbit is a circle with radius `r`
- Only valid when `e ≈ 0` (nearly circular orbits)

**Vis-viva equation:** `v = √(μ × (2/r - 1/a))`
- Works for ANY elliptical orbit (`0 ≤ e < 1`)
- Accounts for varying distance `r` AND orbit shape via `a`

```csharp
// WRONG for elliptical orbits:
float wrongVelocity = Mathf.Sqrt(mu / r);

// CORRECT for elliptical orbits:
float correctVelocity = Mathf.Sqrt(mu * (2.0f / r - 1.0f / a));
```

### 4.4 When Circular Approximation is Acceptable

The circular formula `v = √(μ/r)` is acceptable when:
- Eccentricity `e < 0.01` (nearly circular)
- You only need approximate velocities
- You're at a point where `r ≈ a`

---

## 5. Gravitational Parameter Selection

### 5.1 The Three Options

Given two bodies with masses `M1` and `M2`:

| Option | Formula | When to Use |
|--------|---------|-------------|
| **Total mass** | `μ = G × (M1 + M2)` | For orbit relative to barycenter (binary system) |
| **Primary only** | `μ = G × M_primary` | When M₂ ≪ M₁ (satellite around planet) |
| **Other body** | `μ = G × M_other` | Not typically used directly |

### 5.2 For Binary Systems (Your Use Case)

For a binary star system or two bodies orbiting a shared barycenter:

```csharp
// Gravitational parameter for two-body orbit around barycenter
float mu = G * (M1 + M2);  // CORRECT for binary systems
```

**Why NOT use just one mass:**
- If you use `μ = G × M1`, you're treating body 2 as a massless test particle
- This only works when M2 ≪ M1 (e.g., satellite around Earth)
- For binary systems (comparable masses), you MUST use total mass

### 5.3 The Reduced Mass Concept

The reduced mass `μ_reduced` is a mathematical tool for simplifying the two-body problem:

```
μ_reduced = (M1 × M2) / (M1 + M2)
```

This is used when you want to treat the relative motion as a single body problem:

```csharp
float reducedMass = (M1 * M2) / (M1 + M2);

// The relative acceleration becomes:
// a = F / μ_reduced
// But the gravitational force is still F = G * M1 * M2 / r²
```

**For velocity calculations, use total mass gravitational parameter**, NOT reduced mass.

---

## 6. Complete Implementation Example

```csharp
using Godot;

public class OrbitalMechanics : Godot.Object
{
    // Gravitational constant (m³/(kg·s²))
    public const float G = 6.67430e-11f;
    
    /// <summary>
    /// Calculate orbital velocity using vis-viva equation
    /// </summary>
    /// <param name="mu">Gravitational parameter G*(M1+M2)</param>
    /// <param name="r">Current distance from focus</param>
    /// <param name="a">Semi-major axis</param>
    /// <returns>Orbital velocity magnitude</returns>
    public static float VisVivaVelocity(float mu, float r, float a)
    {
        return Mathf.Sqrt(mu * (2.0f / r - 1.0f / a));
    }
    
    /// <summary>
    /// Calculate semi-major axis from position and velocity
    /// </summary>
    public static float CalculateSemiMajorAxis(Vector3 r, Vector3 v, float mu)
    {
        float rMag = r.Length();
        float vMag = v.Length();
        return 1.0f / (2.0f / rMag - vMag * vMag / mu);
    }
    
    /// <summary>
    /// Calculate eccentricity from state vectors
    /// </summary>
    public static float CalculateEccentricity(Vector3 r, Vector3 v, float mu)
    {
        Vector3 vCrossH = v.Cross(r.Cross(v));
        Vector3 eVec = vCrossH / mu - r.Normalized();
        return eVec.Length();
    }
    
    /// <summary>
    /// Construct perifocal frame from state vectors
    /// </summary>
    public static (Vector3 pHat, Vector3 qHat) ConstructPerifocalFrame(
        Vector3 r, Vector3 v, float mu)
    {
        // Specific angular momentum
        Vector3 h = r.Cross(v);
        
        // Eccentricity vector
        Vector3 eVec = (v.Cross(h) / mu) - (r.Normalized());
        Vector3 pHat = eVec.Normalized();
        
        // qHat perpendicular to both h and pHat
        Vector3 qHat = h.Cross(pHat).Normalized();
        
        return (pHat, qHat);
    }
    
    /// <summary>
    /// Get velocity vector in perifocal frame
    /// </summary>
    public static Vector3 GetPerifocalVelocity(
        float mu, float a, float e, float trueAnomaly,
        Vector3 pHat, Vector3 qHat)
    {
        float r = a * (1.0f - e * e) / (1.0f + e * Mathf.Cos(trueAnomaly));
        
        // Radial and tangential components
        float vTheta = Mathf.Sqrt(mu * a) / r;
        float vr = Mathf.Sqrt(mu / a) * e * Mathf.Sin(trueAnomaly) / 
                   Mathf.Sqrt(1.0f - e * e);
        
        return vr * pHat + vTheta * qHat;
    }
}
```

---

## 7. Summary of Key Formulas

| Concept | Formula |
|---------|---------|
| **Gravitational parameter** | `μ = G × (M1 + M2)` |
| **Barycenter position** | `r_bary = (M1×r1 + M2×r2) / (M1 + M2)` |
| **Vis-viva equation** | `v = √(μ × (2/r - 1/a))` |
| **Circular orbit (special case)** | `v = √(μ/r)` when `e = 0` |
| **Semi-major axis** | `a = 1 / (2/r - v²/μ)` |
| **Eccentricity** | `e = |(v×h)/μ - r/|r||` |
| **Periapsis distance** | `r_p = a × (1 - e)` |
| **Apoapsis distance** | `r_a = a × (1 + e)` |
| **Specific angular momentum** | `h = r × v` |
| **Orbital period** | `T = 2π × √(a³/μ)` |

---

## References

- [Vis-viva equation - Wikipedia](https://en.wikipedia.org/wiki/Vis-viva_equation)
- [Orbital mechanics - Wikipedia](https://en.wikipedia.org/wiki/Orbital_mechanics)
- [Barycenter (astronomy) - Wikipedia](https://en.wikipedia.org/wiki/Barycenter_(astronomy))
- [Reduced mass - Wikipedia](https://en.wikipedia.org/wiki/Reduced_mass)
- [Orbital elements - Wikipedia](https://en.wikipedia.org/wiki/Orbital_elements)
- [Orbital state vectors - Wikipedia](https://en.wikipedia.org/wiki/Orbital_state_vectors)
