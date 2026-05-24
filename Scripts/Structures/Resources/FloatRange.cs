using Godot;

namespace Structures.Resources;

/// <summary>
/// Inclusive [Min, Max] range of a single float-valued generation knob (e.g. tectonic stress scale,
/// subdivisions). Serializes as a 2-element YAML list: <c>[min, max]</c>.
/// </summary>
public readonly record struct FloatRange(float Min, float Max)
{
    public float Midpoint => 0.5f * (Min + Max);

    public bool IsZero => Min == 0f && Max == 0f;

    /// <summary>Uniformly samples a value inside [Min, Max] using the supplied Godot RNG.</summary>
    public float Roll(RandomNumberGenerator rng)
    {
        if (Min >= Max) return Min;
        return rng.RandfRange(Min, Max);
    }

    /// <summary>Linearly interpolates between Min and Max by <paramref name="t"/> in [0,1].</summary>
    public float Lerp(float t) => Mathf.Lerp(Min, Max, t);
}
