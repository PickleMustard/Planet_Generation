using Godot;

namespace PlayerInteraction.Camera;

/// <summary>
/// Pure easing helpers for camera transitions. Kept free of scene-tree
/// dependencies (Mathf operates on managed floats) so it is unit-testable.
/// </summary>
public static class CameraEasing
{
    /// <summary>
    /// Normalized S-curve mapping <paramref name="t"/> in [0,1] to [0,1] with
    /// independently tunable acceleration and deceleration. The first half is an
    /// ease-in raised to <paramref name="accelExp"/> (higher = gentler start /
    /// longer acceleration ramp); the second half is an ease-out raised to
    /// <paramref name="decelExp"/> (higher = gentler stop / longer deceleration).
    /// Both exponents of 1 reduce to a straight line. Endpoints are exact
    /// (0→0, 1→1) and the curve is continuous and monotonic at the 0.5 midpoint.
    /// </summary>
    public static float SCurve(float t, float accelExp, float decelExp)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        accelExp = Mathf.Max(accelExp, 0.01f);
        decelExp = Mathf.Max(decelExp, 0.01f);

        if (t < 0.5f)
            return 0.5f * Mathf.Pow(t * 2f, accelExp);
        return 1f - 0.5f * Mathf.Pow((1f - t) * 2f, decelExp);
    }
}
