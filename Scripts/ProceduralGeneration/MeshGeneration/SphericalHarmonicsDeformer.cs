using Godot;

namespace ProceduralGeneration.MeshGeneration;

/// <summary>
/// Evaluates real spherical harmonics for degrees l=0 through l=3 (16 total basis functions)
/// and generates random coefficients for procedural mesh deformation. All basis functions are
/// hardcoded explicit formulas — no runtime Legendre polynomial computation is performed.
///
/// <para>
/// The 16 basis functions decompose as:
/// <list type="bullet">
///   <item><description>l=0 (1 function):  constant term — base radius modifier</description></item>
///   <item><description>l=1 (3 functions): dipole terms  — elongation / bulges</description></item>
///   <item><description>l=2 (5 functions): quadrupole    — oblateness / pear shapes</description></item>
///   <item><description>l=3 (7 functions): octupole      — 3-lobe asymmetry</description></item>
/// </list>
/// </para>
///
/// <para>
/// Thread-safety: after <see cref="GenerateCoefficients"/> completes, the instance is
/// effectively immutable and safe to read from multiple threads. Do not call
/// <see cref="GenerateCoefficients"/> concurrently with <see cref="Evaluate"/>.
/// </para>
/// </summary>
public class SphericalHarmonicsDeformer
{
    // ────────────────────────────────────────────────────────────────────────
    //  Constants — normalization factors for the real spherical harmonics.
    //  Computed as exact closed-form values (no runtime factorial / sqrt).
    //  Convention: positive "CG" form (sign absorbed into coefficient),
    //  using orthonormal real SH on the unit sphere.
    // ────────────────────────────────────────────────────────────────────────

    // l=0
    private const float Y00_COEFF = 0.2820947917738781f;   // 1/2 * sqrt(1/pi)

    // l=1
    private const float Y1_COEFF = 0.4886025119029199f;   // 1/2 * sqrt(3/pi)

    // l=2
    private const float Y2M2_COEFF = 0.5462742152960396f;  // 1/4 * sqrt(15/pi)
    private const float Y2M1_COEFF = 1.0925484305920792f;  // 1/2 * sqrt(15/pi)
    private const float Y20_COEFF = 0.3153915652525200f;   // 1/4 * sqrt(5/pi)

    // l=3
    private const float Y3M3_COEFF = 0.5900435899266435f;  // 1/4 * sqrt(35/(2*pi))
    private const float Y3M2_COEFF = 1.4453057213202769f;  // 1/4 * sqrt(105/pi)
    private const float Y3M1_COEFF = 0.4570457994644658f;  // 1/4 * sqrt(21/(2*pi))
    private const float Y30_COEFF = 0.3731763325901154f;   // 1/4 * sqrt(7/pi)

    /// <summary>Total number of basis functions for degrees l=0..3.</summary>
    public const int CoefficientCount = 16;

    // ────────────────────────────────────────────────────────────────────────
    //  Amplitude scaling per degree — used by GenerateCoefficients.
    //  Index = degree l.  Higher degrees get smaller amplitudes to avoid
    //  overwhelming the low-frequency shape.
    // ────────────────────────────────────────────────────────────────────────
    private static readonly float[] DegreeScaling = { 1.0f, 0.8f, 0.5f, 0.3f };

    // ────────────────────────────────────────────────────────────────────────
    //  Instance state
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 16 coefficients stored as a flat array indexed by flattened (l, m).
    /// Layout: [Y00, Y1-1, Y10, Y11, Y2-2, Y2-1, Y20, Y21, Y22, Y3-3, …, Y33].
    /// Index for (l, m) = l*l + l + m.
    /// </summary>
    private float[] _coefficients = new float[CoefficientCount];

    // ────────────────────────────────────────────────────────────────────────
    //  Public API
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluate the spherical-harmonics displacement for a normalised direction vector.
    /// The direction is converted to spherical coordinates (theta, phi) internally.
    /// </summary>
    /// <param name="direction">
    /// Unit-length direction from the mesh centre.  If the vector is not normalised the
    /// result is still valid but will correspond to the normalised direction.
    /// </param>
    /// <returns>The weighted sum of all 16 basis functions — a radial displacement value.</returns>
    public float Evaluate(Vector3 direction)
    {
        // ── Spherical coordinate conversion ────────────────────────────────
        // theta = polar angle from +Z axis  [0, pi]
        // phi   = azimuthal angle in XY     [−pi, pi]
        float r = direction.Length();
        if (r < 1e-8f) return 0f;

        float cosTheta = direction.Z / r;
        float theta = Mathf.Acos(Mathf.Clamp(cosTheta, -1f, 1f));
        float phi = Mathf.Atan2(direction.Y, direction.X);

        // ── Precompute trig values ─────────────────────────────────────────
        float sinT = Mathf.Sin(theta);
        float cosT = cosTheta;
        float sin2T = sinT * sinT;
        float sin3T = sin2T * sinT;
        float cos2T = cosT * cosT;
        float cos3T = cos2T * cosT;

        float sinP = Mathf.Sin(phi);
        float cosP = Mathf.Cos(phi);
        float sin2P = Mathf.Sin(2f * phi);
        float cos2P = Mathf.Cos(2f * phi);
        float sin3P = Mathf.Sin(3f * phi);
        float cos3P = Mathf.Cos(3f * phi);

        // ── Evaluate each basis function and accumulate ────────────────────
        float sum = 0f;

        // l=0  (index 0)
        sum += _coefficients[0] * Y00_COEFF;

        // l=1  (indices 1–3)
        sum += _coefficients[1] * (Y1_COEFF * sinT * sinP);     // Y1,-1
        sum += _coefficients[2] * (Y1_COEFF * cosT);            // Y1, 0
        sum += _coefficients[3] * (Y1_COEFF * sinT * cosP);     // Y1,+1

        // l=2  (indices 4–8)
        sum += _coefficients[4] * (Y2M2_COEFF * sin2T * sin2P); // Y2,-2
        sum += _coefficients[5] * (Y2M1_COEFF * sinT * cosT * sinP);  // Y2,-1
        sum += _coefficients[6] * (Y20_COEFF * (3f * cos2T - 1f));     // Y2, 0
        sum += _coefficients[7] * (Y2M1_COEFF * sinT * cosT * cosP);  // Y2,+1
        sum += _coefficients[8] * (Y2M2_COEFF * sin2T * cos2P); // Y2,+2

        // l=3  (indices 9–15)
        sum += _coefficients[9] * (Y3M3_COEFF * sin3T * sin3P);                  // Y3,-3
        sum += _coefficients[10] * (Y3M2_COEFF * sin2T * cosT * sin2P);           // Y3,-2
        sum += _coefficients[11] * (Y3M1_COEFF * sinT * (5f * cos2T - 1f) * sinP); // Y3,-1
        sum += _coefficients[12] * (Y30_COEFF * (5f * cos3T - 3f * cosT));         // Y3, 0
        sum += _coefficients[13] * (Y3M1_COEFF * sinT * (5f * cos2T - 1f) * cosP); // Y3,+1
        sum += _coefficients[14] * (Y3M2_COEFF * sin2T * cosT * cos2P);           // Y3,+2
        sum += _coefficients[15] * (Y3M3_COEFF * sin3T * cos3P);                  // Y3,+3

        return sum;
    }

    /// <summary>
    /// Generate random coefficients with amplitude scaling by degree.
    /// <para>
    /// Each coefficient is drawn uniformly from [−amplitude × scale, +amplitude × scale]
    /// where <c>scale</c> defaults to a per-degree tuple that biases toward low-frequency
    /// detail: l=0 → ×1.0, l=1 → ×0.8, l=2 → ×0.5, l=3 → ×0.3. Callers can override the
    /// per-band scales via <paramref name="bandScales"/> — useful for subtype-specific
    /// shape character (e.g. stormy gas giants amplifying l≥2 to add asymmetric blobs).
    /// </para>
    /// <para>
    /// Deterministic: calling with the same <paramref name="rng"/> seed state produces
    /// identical coefficients.
    /// </para>
    /// </summary>
    /// <param name="rng">Godot <see cref="RandomNumberGenerator"/> (caller controls seed).</param>
    /// <param name="amplitude">Base amplitude — the maximum absolute value at degree 0.</param>
    /// <param name="bandScales">
    /// Optional per-degree scale overrides (index = degree l). Missing or negative entries fall
    /// back to the built-in <see cref="DegreeScaling"/> tuple, so a short or partial array is
    /// safe. Pass <c>null</c> to use the built-in scaling unchanged.
    /// </param>
    public void GenerateCoefficients(RandomNumberGenerator rng, float amplitude, float[]? bandScales = null)
    {
        int index = 0;
        for (int l = 0; l <= 3; l++)
        {
            float scale = (bandScales != null && l < bandScales.Length && bandScales[l] >= 0f)
                ? bandScales[l]
                : DegreeScaling[l];
            float scaledAmplitude = amplitude * scale;
            for (int m = -l; m <= l; m++)
            {
                _coefficients[index] = rng.RandfRange(-scaledAmplitude, scaledAmplitude);
                index++;
            }
        }
    }

    /// <summary>
    /// Returns a copy of the current coefficient array (length <see cref="CoefficientCount"/>).
    /// Useful for serialisation, debugging, or unit-test assertions.
    /// </summary>
    public float[] GetCoefficients()
    {
        float[] copy = new float[CoefficientCount];
        _coefficients.CopyTo(copy, 0);
        return copy;
    }

    /// <summary>
    /// Sets the coefficients directly. The supplied array must have exactly
    /// <see cref="CoefficientCount"/> elements.
    /// </summary>
    /// <param name="coefficients">Flat coefficient array ordered by flattened (l, m) = l² + l + m.</param>
    public void SetCoefficients(float[] coefficients)
    {
        if (coefficients.Length != CoefficientCount)
        {
            UtilityLibrary.GameLogger.Error(
                $"SphericalHarmonicsDeformer.SetCoefficients: expected {CoefficientCount} " +
                $"coefficients but received {coefficients.Length}.");
            return;
        }

        coefficients.CopyTo(_coefficients, 0);
    }
}
