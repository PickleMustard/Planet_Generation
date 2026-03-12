# Spherical Harmonics Deformation for Procedural Mesh Generation

## 1. What are Spherical Harmonics and How are They Used for Mesh Deformation?

**Spherical Harmonics (SH)** are special mathematical functions defined on the surface of a sphere. They form a complete set of orthogonal basis functions that can be used to represent any function on a sphere's surface, similar to how sines and cosines can represent periodic signals in 1D.

### For Mesh Deformation:
Spherical harmonics are used to deform spherical meshes by:
1. **Radial Displacement**: The harmonic function value at each point on the sphere's surface determines how much to displace that vertex along the radial direction
2. **Shape Representation**: By summing weighted spherical harmonic basis functions, you can approximate any arbitrary shape on a sphere's surface
3. **Smooth Variation**: Low-order harmonics produce smooth, large-scale deformations, perfect for creating natural-looking irregular bodies

The radial distance at a given point (θ, φ) on a deformed sphere is calculated as:

```
r(θ, φ) = r₀ + Δr(θ, φ)
```

where:
- `r₀` is the base radius of the sphere
- `Δr(θ, φ)` is the displacement calculated from the spherical harmonics expansion

**Sources:**
- Wikipedia: Spherical Harmonics
- Wolfram MathWorld: Spherical Harmonic
- Wikipedia: Table of Spherical Harmonics

---

## 2. Combining Low-Order Spherical Harmonics for Asteroid/Body Shapes

To create believable asteroid-like shapes, combine spherical harmonics of different degrees (ℓ) and orders (m):

### Key Principles:
- **ℓ=0 (l=0)**: Constant term, represents overall scaling of the sphere
- **ℓ=1 (dipole terms)**: Creates bulges/indentations, representing elongation
- **ℓ=2 (quadrupole terms)**: Creates oblateness/pear-shapes, representing flattening
- **ℓ=3 (octupole terms)**: Creates more complex 3-lobe patterns
- **ℓ=4 (hexadecapole terms)**: Creates intricate variations with up to 8 lobes

### Coefficient Selection for Asteroid Bodies:
For realistic asteroid shapes, use the following coefficient ranges:

| Harmonic | Typical Coefficient Range | Effect on Shape |
|-----------|---------------------|----------------|
| ℓ=0, m=0 | 0.1 to 0.5 | Base radius modifier |
| ℓ=1, m=-1,0,1 | -0.3 to 0.3 | Large-scale elongation |
| ℓ=2, m=-2,-1,0,1,2 | -0.2 to 0.2 | Oblateness, flattening |
| ℓ=3, m=-3,-2,-1,0,1,2,3 | -0.1 to 0.15 | Multi-lobe protrusions |
| ℓ=4, m=-4,-3,-2,-1,0,1,2,3,4 | -0.05 to 0.1 | Fine detail variations |

### Example Combination for a Pot-Shaped Asteroid:
```
Δr(θ, φ) = A₀₀ × Y₀₀(θ, φ)
            + A₁₀ × Y₁₀(θ, φ)
            + A₂₀ × Y₂₀(θ, φ)
            + A₂₂ × Y₂₂(θ, φ)
            + A₃₀ × Y₃₀(θ, φ) + A₃₁ × Y₃₁(θ, φ)
```

Where coefficients are randomly selected from the ranges above.

**Sources:**
- Wolfram MathWorld: Spherical Harmonic
- Practical implementation in procedural generation based on harmonic frequency analysis

---

## 3. Mathematical Formula for Real Spherical Harmonics Yₗ^ᵐ(θ, φ)

The real spherical harmonics (used for real-valued displacements) are defined as:

### Complex Spherical Harmonics:
```
Yₗ^ᵐ(θ, φ) = √((2ℓ+1)/(4π) × ((ℓ-m)!/((ℓ+m)!)) × Pₗ^ᵐ(cosθ) × e^(i m φ)
```

### Real Spherical Harmonics (Cosine and Sine forms):
For real-valued functions, separate into cosine and sine components:

```
Yₗ^ᵐ(θ, φ) = √((2ℓ+1)/(4π)) × ((ℓ-m)!/((ℓ+m)!) × Pₗ^ᵐ(cosθ) × {cos(mφ) for m ≥ 0}
Yₗ^ᵐ(θ, φ) = √((2ℓ+1)/(4π)) × ((ℓ-m)!/((ℓ+m)!) × Pₗ^ᵐ(cosθ) × sin(|m|φ) for m < 0}
```

Where:
- `ℓ` (ell or l) = degree (band index), ℓ ≥ 0
- `m` = order (within degree), -ℓ ≤ m ≤ ℓ
- `Pₗ^ᵐ(x)` = associated Legendre polynomial of degree ℓ and order m
- `θ` (theta) = polar angle [0, π] (colatitude/inclination)
- `φ` (phi) = azimuthal angle [0, 2π) (longitude)
- `(ℓ-m)!` and `(ℓ+m)!` = factorials

**Normalization Note**: The normalization factor √((2ℓ+1)/(4π) ensures orthonormality.

**Sources:**
- Wikipedia: Spherical Harmonics (Complex and Real Forms section)
- Wolfram MathWorld: Spherical Harmonic
- Wikipedia: Table of Spherical Harmonics

---

## 4. Converting Vertex Position (x,y,z) to (θ, φ)

For a unit sphere vertex (or vertex normalized to unit radius), use spherical coordinate conversions:

### Conversions from Cartesian to Spherical:
```
r = √(x² + y² + z²)  // Radial distance
θ = arccos(z / r)         // Polar angle (colatitude)
φ = arctan2(y, x)        // Azimuthal angle
```

### Important Notes:
- **arctan2(y, x)** computes the angle correctly in all quadrants:
  - Returns angle in range [0, 2π) or [−π, π] depending on implementation
  - In Godot/Math.NET: Use `Mathf.Atan2(y, x)` for standard 2-argument arctangent
- **Division by zero**: Handle vertices on the poles (where x=y=0) by checking denominators
- **Range**: θ ∈ [0, π], φ ∈ [0, 2π) for physics convention
- For a mesh already on a unit sphere, r=1, so r can be omitted in the spherical harmonics evaluation

**Example in C# (Godot/Unity):**
```csharp
// Convert vertex on unit sphere to spherical coordinates
Vector3 position = vertex.Position;
float r = position.Length;  // Should be ~1.0 for unit sphere
float theta = Mathf.Acos(position.Y / r);
float phi = Mathf.Atan2(position.X, position.Z);

// Handle phi to be in [0, 2π]
if (phi < 0f) phi += 2f * Mathf.Pi;
```

**Sources:**
- Wikipedia: Spherical coordinate system
- Wikipedia: Coordinate system conversions

---

## 5. Coefficient Ranges for Asteroid-Like Bodies

### Recommended Ranges for Different Asteroid Types:

| Asteroid Type | ℓ Range | Coefficient Amplitude |
|---------------|----------|----------------------|
| Small Rocky | 0-3 | 0.05 - 0.25 |
| Medium Rocky | 0-4 | 0.1 - 0.3 |
| Large Rocky | 0-5 | 0.15 - 0.4 |
| Irregular/Fragment | 3-6 | 0.1 - 0.3 |
| Comet Core | 2-4 | 0.1 - 0.2 |

### Practical Guidelines:
1. **Base Radius (ℓ=0)**: Typically 0.8-1.2× the target radius
2. **Amplitude Decay**: Higher-order harmonics should generally have smaller amplitudes
   - Rule of thumb: `Amplitude(ℓ) ≈ BaseAmplitude / (ℓ + 1)^α` where α ≈ 1.5-2.0
3. **Randomness**: Use Gaussian distribution or uniform random for coefficient selection
4. **Preserve Volume**: Ensure the average displacement doesn't significantly change the body's volume

### Example Coefficient Generation in C#:
```csharp
// Pseudocode for generating asteroid coefficients
struct HarmonicCoefficients
{
    public int Degree { get; set; }      // ℓ value
    public int Order { get; set; }      // m value
    public float Amplitude { get; set; }  // Coefficient aₗ^ᵐ
}

// Generate coefficients for a medium rocky asteroid
List<HarmonicCoefficients> coeffs = new List<HarmonicCoefficients>();

// ℓ=0: Base radius (always include)
coeffs.Add(new HarmonicCoefficients { Degree = 0, Order = 0, Amplitude = baseRadius });

// ℓ=1: Dipole terms (3 coefficients: m=-1,0,1)
coeffs.Add(new HarmonicCoefficients { Degree = 1, Order = -1, Amplitude = Random.Range(-0.25f, 0.25f) });
coeffs.Add(new HarmonicCoefficients { Degree = 1, Order = 0, Amplitude = Random.Range(-0.2f, 0.2f) });
coeffs.Add(new HarmonicCoefficients { Degree = 1, Order = 1, Amplitude = Random.Range(-0.25f, 0.25f) });

// ℓ=2: Quadrupole terms (5 coefficients: m=-2,-1,0,1,2)
for (int m = -2; m <= 2; m++)
{
    float amp = Random.Range(-0.15f, 0.15f);
    coeffs.Add(new HarmonicCoefficients { Degree = 2, Order = m, Amplitude = amp });
}
```

**Sources:**
- Practical procedural generation literature
- Statistical analysis of asteroid shapes

---

## 6. Layering Small-Scale Noise (Perlin/Simplex) on Top of Spherical Harmonics

### The Concept:
Spherical harmonics provide the **macroscopic structure** (large-scale features), while noise functions add **microscopic detail** (surface roughness).

### Implementation Approach:
```
final_radius = base_radius + sh_displacement + noise_displacement

where:
- sh_displacement = Σ(aₗ^ᵐ × Yₗ^ᵐ(θ, φ))  // Spherical harmonics contribution
- noise_displacement = noise_scale × Noise(position)    // High-frequency detail
```

### Two Main Strategies:

#### Strategy A: Independent Noise Addition
```csharp
// Evaluate spherical harmonics first
float shValue = EvaluateSphericalHarmonics(theta, phi, coefficients);

// Add noise independently
float noiseValue = Noise3D(position.X * noiseFreq, position.Y * noiseFreq, position.Z * noiseFreq);
noiseValue *= noiseAmplitude;

// Combine
float displacement = shValue + noiseValue;
```

#### Strategy B: Noise-Modulated Harmonics
```csharp
// Use noise to vary the SH coefficients spatially
float modulation = Noise3D(theta * modulationFreq, phi * modulationFreq, 0f) * modulationStrength;

// Apply to SH evaluation
float shValue = EvaluateSphericalHarmonics(theta, phi, coefficients * (1f + modulation));
```

### Recommended Parameters:
| Parameter | Suggested Value | Effect |
|-----------|----------------|---------|
| Noise Frequency | 2-8 | Scale of surface detail |
| Noise Amplitude | 0.01-0.1 | Depth of surface features |
| SH Max Degree | 3-5 | Balance between smoothness and complexity |
| Noise Layer Count | 1-3 octaves | Multi-frequency detail |

### Godot 4 C# Implementation Example:
```csharp
using Godot;
using System.Collections.Generic;

public partial class AsteroidMesh : MeshInstance3D
{
    [Export] public int shMaxDegree = 4;        // Maximum SH degree to use
    [Export] public float shAmplitude = 0.3f;      // Overall SH strength
    [Export] public float noiseAmplitude = 0.05f;  // Detail noise strength
    [Export] public float noiseFrequency = 4.0f;   // Scale of noise
    [Export] public int noiseOctaves = 3;        // Noise layers

    private List<SHCoefficients> coefficients;

    public override void _Ready()
    {
        GenerateCoefficients();
        DeformMesh();
    }

    private void GenerateCoefficients()
    {
        coefficients = new List<SHCoefficients>();
        RandomNumberGenerator rng = new RandomNumberGenerator();

        // ℓ=0: Base radius
        coefficients.Add(new SHCoefficients(0, 0, 1.0f));

        // ℓ=1 to shMaxDegree
        for (int l = 1; l <= shMaxDegree; l++)
        {
            float amplitude = shAmplitude / Mathf.Pow(l + 1, 1.5f);

            // m=-l to l
            for (int m = -l; m <= l; m++)
            {
                float coeff = rng.RandfRange(-amplitude, amplitude);
                coefficients.Add(new SHCoefficients(l, m, coeff));
            }
        }
    }

    private float EvaluateSphericalHarmonics(float theta, float phi)
    {
        float sum = 0f;

        foreach (var coeff in coefficients)
        {
            float Ylm = RealSphericalHarmonic(coeff.Degree, coeff.Order, theta, phi);
            sum += coeff.Amplitude * Ylm;
        }

        return sum;
    }

    private float RealSphericalHarmonic(int l, int m, float theta, float phi)
    {
        // Normalization factor
        float K = Mathf.Sqrt((2f * l + 1) / (4f * Mathf.Pi));

        // Associated Legendre polynomial P_l^m(cosθ)
        float P = AssociatedLegendre(l, m, Mathf.Cos(theta));

        // Angular function
        if (m < 0)
        {
            return K * P * Mathf.Sin(Mathf.Abs(m) * phi);
        }
        else if (m > 0)
        {
            // Use symmetry: Y_l^(-m) = (-1)^m × Y_l^m
            return Mathf.Pow(-1, m) * K * P * Mathf.Sin(m * phi);
        }
        else // m == 0
        {
            return K * P;
        }
    }

    private float AssociatedLegendre(int l, int m, float x)
    {
        // Simplified implementation for low orders
        if (l == 0 && m == 0) return 1f;
        if (l == 1 && m == 0) return x;
        if (l == 1 && Math.Abs(m) == 1) return -Mathf.Sqrt(1 - x * x) / 2f;
        if (l == 2 && m == 0) return (3f * x * x - 1f) / 2f;
        // ... add more cases as needed or use general formula
        return 1f; // Fallback
    }

    private float Noise3D(float x, float y, float z)
    {
        // Simplified 3D noise (replace with actual implementation)
        float value = Mathf.Sin(x) * Mathf.Cos(y) * Mathf.Sin(z);
        return value;
    }

    private void DeformMesh()
    {
        MeshData3D meshData = Mesh.Instance.GetSurface(this.Mesh);
        Vector3[] vertices = meshData.SurfaceGetArrays(Mesh.ArrayType.Vertex)[0];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];

            // Convert to spherical coordinates (assuming unit sphere)
            float r = vertex.Length;
            float theta = Mathf.Acos(vertex.Y / r);
            float phi = Mathf.Atan2(vertex.X, vertex.Z);
            if (phi < 0f) phi += 2f * Mathf.Pi;

            // Evaluate spherical harmonics displacement
            float shDisplacement = EvaluateSphericalHarmonics(theta, phi);

            // Add noise detail
            float noiseDetail = Noise3D(
                vertex.X * noiseFrequency,
                vertex.Y * noiseFrequency,
                vertex.Z * noiseFrequency
            ) * noiseAmplitude;

            // Total displacement (radial)
            float displacement = shDisplacement + noiseDetail;

            // Apply displacement radially
            Vector3 direction = vertex.Normalized;
            vertices[i] = vertex + direction * displacement;
        }

        meshData.SurfaceSetArrays(Mesh.ArrayType.Vertex, vertices);
    }
}

// Helper structure
public struct SHCoefficients
{
    public int Degree;
    public int Order;
    public float Amplitude;
}
```

**Sources:**
- Real-time mesh deformation techniques in computer graphics
- Multi-scale procedural generation literature

---

## 7. C# and Godot 4 Specific Implementations and Considerations

### Math Library Considerations:
- **System.Math**: Provides `double` precision, but convert to `float` for Godot
- **Mathf**: Godot's math library with `float` precision
- Use `Mathf.Sqrt()`, `Mathf.Pow()`, `Mathf.Pi`, `Mathf.Cos()`, `Mathf.Sin()`, `Mathf.Acos()`, `Mathf.Atan2()`

### Performance Optimizations:
1. **Precompute Values**: Cache spherical harmonics evaluations if vertices are reused
2. **Vectorization**: Process vertices in batches or use compute shaders for GPU acceleration
3. **LOD**: Reduce SH degree for distant objects (use ℓ=2 instead of ℓ=5)
4. **Asynchronous Generation**: Generate large asteroids on background threads

### Godot-Specific APIs:
```csharp
// Godot 4 C# specific mesh manipulation
MeshData3D meshData = Mesh.Instance.GetSurface(mesh);
Vector3[] vertices = meshData.SurfaceGetArrays(Mesh.ArrayType.Vertex)[0];
Vector3[] normals = meshData.SurfaceGetArrays(Mesh.ArrayType.Normal)[0];
int[] indices = meshData.SurfaceGetArrays(Mesh.ArrayType.Index)[0];

// After modification:
meshData.SurfaceSetArrays(Mesh.ArrayType.Vertex, vertices);
meshData.SurfaceSetArrays(Mesh.ArrayType.Normal, normals);
meshData.SurfaceSetArrays(Mesh.ArrayType.Index, indices);
```

### Shader-Based Implementation (For Performance):
```glsl
// Vertex shader example for SH deformation
shader_type spatial;
render_mode blend;

uniform sampler2D noiseTexture;  // For detail noise
uniform float shCoefficients[16];  // Precomputed SH coefficients
uniform int shDegree;
uniform float shStrength;

varying vec3 vertexPosition;

void vertex() {
    // Convert to spherical coordinates
    vec3 pos = normalize(vertexPosition);
    float theta = acos(pos.y);
    float phi = atan(pos.x, pos.z);
    
    // Evaluate spherical harmonics (simplified in shader)
    float shDisplacement = 0.0;
    int index = 0;
    for (int l = 0; l <= shDegree; l++) {
        for (int m = -l; m <= l; m++) {
            // Real spherical harmonic evaluation would go here
            float Ylm = evaluateRealSH(l, m, theta, phi);
            shDisplacement += shCoefficients[index++] * Ylm;
        }
    }
    
    // Add noise
    float detail = texture(noiseTexture, phi * 0.1, theta * 0.1).r * 0.05;
    
    // Apply radial displacement
    float totalDisplacement = shStrength * shDisplacement + detail;
    vec4 displacedPos = vec4(vertexPosition + normalize(vertexPosition) * totalDisplacement, 1.0);
    
    POSITION = PROJECTION_MATRIX * MODEL_MATRIX * displacedPos;
}
```

**Sources:**
- Godot 4 Shader documentation
- GPU-based mesh deformation techniques

### Randomness and Determinism:
```csharp
// Use Godot's RandomNumberGenerator for reproducible results
RandomNumberGenerator rng = new RandomNumberGenerator();
rng.Seed = 42;  // Set seed for reproducibility
float randomValue = rng.RandfRange(min, max);
```

**Sources:**
- Godot 4 C# API documentation
- Procedural generation best practices

---

## 8. Typical Approach: Radial Displacement Using SH Evaluation

### The Standard Pipeline:

1. **Generate Base Mesh**: Create a spherical mesh (icosphere, UV sphere, or subdivided cube)
2. **Generate SH Coefficients**: Create random coefficients within desired ranges
3. **For Each Vertex**:
   - Convert vertex position to spherical coordinates (θ, φ)
   - Evaluate spherical harmonics: `Δr = Σ(aₗ^ᵐ × Yₗ^ᵐ(θ, φ))`
   - Apply displacement radially: `new_pos = old_pos + Δr × (old_pos / |old_pos|)`
4. **Optional Detail Layer**: Add noise function for small-scale features
5. **Recompute Normals**: After vertex displacement, recalculate surface normals for proper lighting
6. **Update Mesh**: Replace mesh vertices and normals with deformed values

### Pseudocode:
```
function DeformSphericalMesh(mesh, shCoefficients):
    vertices = mesh.GetVertices()
    
    for each vertex in vertices:
        // Step 1: Convert to spherical coordinates
        r = length(vertex)  // Radius from origin
        theta = arccos(vertex.y / r)  // Polar angle
        phi = atan2(vertex.x, vertex.z)   // Azimuthal angle
        
        // Step 2: Evaluate SH displacement
        displacement = 0
        for (l, m, amplitude) in shCoefficients:
            Ylm = RealSphericalHarmonic(l, m, theta, phi)
            displacement += amplitude * Ylm
        
        // Step 3: Apply radial displacement
        direction = vertex / r  // Unit normal vector
        vertex += direction * displacement
    
    // Step 4: Update mesh
    mesh.SetVertices(vertices)
    mesh.RecalculateNormals()
```

### Why This Works:
- **Radial Displacement**: Using the displacement as a radial offset from the center preserves the sphere-like topology
- **Orthonormality**: Spherical harmonics form an orthonormal basis, meaning different degrees affect independent features
- **Continuity**: The sum of smooth spherical harmonics produces a continuously differentiable surface
- **Intuitive Control**: Each degree (ℓ) affects specific frequency/wavelength of deformation

**Sources:**
- Geometric deformation literature
- Computer graphics mesh manipulation techniques

---

## Summary and Key Takeaways

### Implementation Checklist for Spherical Harmonics Deformation:

- [ ] **Mathematics**: Implement associated Legendre polynomials and normalization
- [ ] **Coordinate Conversion**: Robust (x,y,z) → (θ,φ) conversion with quadrant handling
- [ ] **Coefficient Generation**: Create randomized coefficients within appropriate ranges for asteroid shapes
- [ ] **Harmonic Evaluation**: Sum coefficients weighted by real spherical harmonic basis functions
- [ ] **Radial Application**: Apply displacement along the vertex normal direction
- [ ] **Noise Layering**: Add Perlin/Simplex noise for fine surface detail
- [ ] **Normal Recalculation**: Update mesh normals after vertex displacement
- [ ] **Performance**: Consider GPU shaders or LOD for real-time applications

### Recommended Development Workflow:
1. Start with simple ℓ=0,1,2 (basic shapes)
2. Test coefficient ranges to understand their visual impact
3. Add noise layer incrementally
4. Optimize performance (precompute, batch operations)
5. Implement LOD based on distance/LOD requirements

---

## References and Sources

### Mathematical References:
- **Wikipedia**: "Spherical Harmonics" - Comprehensive theory and definitions
- **Wikipedia**: "Table of Spherical Harmonics" - Explicit formulas for ℓ=0 through ℓ=10
- **Wolfram MathWorld**: "Spherical Harmonic" - Mathematical properties and relationships
- **Wikipedia**: "Associated Legendre Polynomials" - Polynomial definitions and recurrence formulas
- **Wikipedia**: "Spherical Coordinate System" - Coordinate conversion formulas

### Implementation References:
- **Google Scholar**: Papers on "procedural generation spherical harmonics mesh"
- **Wolfram Functions**: SphericalHarmonicY function implementation details
- **Godot 4 Documentation**: C# API for MeshInstance3D and shader programming

### Practical Sources:
- **Procedural Generation Literature**: Articles on asteroid and planetary body generation
- **Computer Graphics Research**: Real-time mesh deformation techniques
- **Open Source Repositories**: GitHub projects on procedural terrain/planet generation (though specific SH implementations were limited in search results)

---

## Additional Notes for Godot 4 C# Implementation

### Associated Legendre Polynomial Simplified Values (ℓ ≤ 3):

**P₀⁰(x) = 1**
**P₁⁰(x) = x**
**P₁¹(x) = -√(1-x²)**
**P₂⁰(x) = (3x² - 1)/2**
**P₂¹(x) = -3x√(1-x²)**
**P₂²(x) = 3(1-2x²)**
**P₃⁰(x) = (5x³ - 3x)/2**

### Real Spherical Harmonic Explicit Forms (ℓ ≤ 3):

**ℓ=0:**
- Y₀⁰ = 1/2√π

**ℓ=1:**
- Y₁⁰ = √(3/4π) × cosθ
- Y₁¹ = √(3/4π) × sinθ × cosφ
- Y₁⁻¹ = √(3/4π) × sinθ × sinφ

**ℓ=2:**
- Y₂⁰ = √(5/16π) × (3cos²θ - 1)
- Y₂¹ = √(15/16π) × sinθ × cosθ × cosφ
- Y₂² = √(15/4π) × sin²θ × cos2φ

**ℓ=3:**
- Y₃⁰ = √(7/16π) × (5cos³θ - 3cosθ)
- Y₃¹ = √(21/32π) × sinθ × (5cos²θ - 1) × cosφ
- Y₃² = √(105/32π) × sin²θ × (5cos²θ - 1) × cos2φ
- Y₃³ = √(35/32π) × sin³θ × cos3φ

These explicit forms can be used directly in code for low-order harmonics, avoiding the need for associated Legendre polynomial computation.

---

**Document Version**: 1.0
**Last Updated**: March 2026
**Generated For**: Planet Generation Project (Godot 4, C#, .NET 8.0)
