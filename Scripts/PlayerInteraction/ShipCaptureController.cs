using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures;

/// <summary>
/// Captures the player ship into a nearby <see cref="CelestialBody"/>'s reference frame and
/// enforces a "no-entry" shell around the body's surface. Owned and driven by
/// <see cref="PlayerController"/>; not a scene-tree node.
///
/// Each captured frame the ship rides the body's translation (frame sync) and has the radial
/// component of its velocity stripped so tangential travel curves around the body (orbital
/// motion) while the player keeps their chosen speed. Moving radially outward (high angle of
/// attack) releases the ship. The inner no-entry shell hard-stops the ship and nudges it back
/// to the shell edge so it can never penetrate the mesh.
///
/// The geometry is split into <c>static</c> pure methods so it can be unit-tested without a tree.
/// </summary>
public class ShipCaptureController
{
    // --- feel knobs (tune in playtest) ---
    /// <summary>Half-angle (deg) of the outward cone that triggers release while captured.</summary>
    public float ExitConeDeg { get; set; } = 35f;

    /// <summary>How aggressively captured velocity is bent toward tangential (per second).</summary>
    public float OrbitBendStrength { get; set; } = 4f;

    /// <summary>Velocity multiplier-per-second applied inside the no-entry decel band (0..1).</summary>
    public float NoEntryDamping { get; set; } = 0.01f;

    /// <summary>Soft deceleration band outside the no-entry shell, as a multiple of body Radius.</summary>
    public float DecelBandFactor { get; set; } = 0.3f;

    /// <summary>Frames between full re-scans of the collidable-body group.</summary>
    private const int RefreshInterval = 120;

    private readonly SceneTree _tree;
    private readonly List<CelestialBody> _cachedBodies = new();
    private int _refreshCounter;

    private CelestialBody? _capturedBody;
    private Vector3 _lastBodyPos;

    /// <summary>The body the ship is currently captured by, or null.</summary>
    public CelestialBody? CapturedBody => _capturedBody;

    /// <summary>
    /// True when the most recent <see cref="Resolve"/> hard-clamped the ship against a body's
    /// no-entry shell (i.e. it hit the surface this frame). Reset at the start of every call.
    /// </summary>
    public bool CollidedThisFrame { get; private set; }

    public ShipCaptureController(SceneTree tree)
    {
        _tree = tree;
    }

    /// <summary>Drop capture state (e.g. when the camera FSM takes over from free-fly).</summary>
    public void ClearCapture() => _capturedBody = null;

    /// <summary>
    /// Resolves one physics frame of movement. Mutates <paramref name="velocity"/> (orbital bend
    /// + collision deceleration) and returns the corrected world-space delta to apply to BOTH the
    /// ship and its camera. With no governing body nearby, returns plain <c>velocity * deltaTime</c>.
    /// </summary>
    public Vector3 Resolve(Vector3 shipPos, ref Vector3 velocity, float deltaTime)
    {
        CollidedThisFrame = false;
        MaybeRefreshCache();

        CelestialBody? body = FindGoverningBody(shipPos);
        if (body == null)
        {
            _capturedBody = null;
            return velocity * deltaTime;
        }

        Vector3 bodyPos = body.GlobalPosition;
        Vector3 toShip = shipPos - bodyPos;
        float dist = toShip.Length();
        Vector3 nOut = dist > 1e-6f ? toShip / dist : Vector3.Up;

        // --- capture state transitions ---
        if (_capturedBody == body)
        {
            if (dist > body.CaptureRadius || WantsToLeave(velocity, nOut, ExitConeDeg))
                _capturedBody = null;
        }
        else if (dist <= body.CaptureRadius && !WantsToLeave(velocity, nOut, ExitConeDeg))
        {
            _capturedBody = body;
            _lastBodyPos = bodyPos; // zero the first sync delta — no teleport on capture
        }

        bool captured = _capturedBody == body;

        // --- no-entry deceleration (applies near the shell whether captured or not) ---
        velocity = ApplyNoEntryDecel(
            velocity, nOut, dist, body.NoEntryRadius,
            body.Radius * DecelBandFactor, NoEntryDamping, deltaTime
        );

        // --- orbital bend + body-frame sync (captured only) ---
        Vector3 bodyDelta = Vector3.Zero;
        if (captured)
        {
            float bend = Mathf.Clamp(OrbitBendStrength * deltaTime, 0f, 1f);
            velocity = BendTangential(velocity, nOut, bend);
            bodyDelta = bodyPos - _lastBodyPos;
        }
        // Keep _lastBodyPos current even while uncaptured so the next capture starts at zero.
        _lastBodyPos = bodyPos;

        // --- assemble + hard clamp against the no-entry shell ---
        Vector3 finalDelta = velocity * deltaTime + bodyDelta;
        Vector3 preClampDelta = finalDelta;
        (finalDelta, velocity) = ClampNoEntry(shipPos, finalDelta, bodyPos, body.NoEntryRadius, velocity);
        CollidedThisFrame = finalDelta != preClampDelta;
        return finalDelta;
    }

    // ----------------------------------------------------------------------------------
    // Body lookup (stateful, needs the scene tree)
    // ----------------------------------------------------------------------------------

    private void MaybeRefreshCache()
    {
        if (_cachedBodies.Count > 0 && _refreshCounter-- > 0)
            return;

        _refreshCounter = RefreshInterval;
        _cachedBodies.Clear();
        foreach (var n in _tree.GetNodesInGroup("celestial_collidable"))
        {
            // Belts are thousands of nodes and not worth individual collision; skip them.
            if (n is CelestialBody cb && cb.Classification is not BodyClassification.Belt)
                _cachedBodies.Add(cb);
        }
    }

    /// <summary>
    /// Returns the body whose capture sphere the ship is inside, preferring the currently-captured
    /// body (sticky) and otherwise the one with the smallest distance/CaptureRadius ratio.
    /// </summary>
    private CelestialBody? FindGoverningBody(Vector3 shipPos)
    {
        if (_capturedBody != null && GodotObject.IsInstanceValid(_capturedBody))
        {
            float d = shipPos.DistanceTo(_capturedBody.GlobalPosition);
            if (d <= _capturedBody.CaptureRadius)
                return _capturedBody;
        }

        CelestialBody? best = null;
        float bestRatio = 1f; // inside a capture sphere => ratio <= 1
        foreach (var b in _cachedBodies)
        {
            if (!GodotObject.IsInstanceValid(b))
                continue;
            float cr = b.CaptureRadius;
            if (cr <= 0f)
                continue;
            float ratio = shipPos.DistanceTo(b.GlobalPosition) / cr;
            if (ratio <= 1f && ratio < bestRatio)
            {
                bestRatio = ratio;
                best = b;
            }
        }
        return best;
    }

    // ----------------------------------------------------------------------------------
    // Pure geometry (unit-testable, no scene tree)
    // ----------------------------------------------------------------------------------

    /// <summary>Cosine-style alignment of velocity with the outward radial normal: +1 out, 0 tangential, -1 in.</summary>
    public static float RadialDot(Vector3 velocity, Vector3 nOut)
    {
        if (velocity.LengthSquared() <= 1e-12f)
            return 0f;
        return velocity.Normalized().Dot(nOut);
    }

    /// <summary>True when velocity points outward within <paramref name="exitConeDeg"/> of the radial normal.</summary>
    public static bool WantsToLeave(Vector3 velocity, Vector3 nOut, float exitConeDeg)
        => RadialDot(velocity, nOut) >= Mathf.Cos(Mathf.DegToRad(exitConeDeg));

    /// <summary>
    /// Blends velocity toward its tangential component (radial part removed), preserving tangential
    /// speed. Repeated application as <paramref name="nOut"/> rotates produces orbital motion.
    /// </summary>
    public static Vector3 BendTangential(Vector3 velocity, Vector3 nOut, float bend)
    {
        Vector3 vTan = velocity - nOut * velocity.Dot(nOut);
        return velocity.Lerp(vTan, bend);
    }

    /// <summary>
    /// Inside the soft decel band around the no-entry shell, strips inward velocity and damps the
    /// rest. Returns the modified velocity.
    /// </summary>
    public static Vector3 ApplyNoEntryDecel(
        Vector3 velocity, Vector3 nOut, float dist, float noEntry,
        float decelBand, float damping, float deltaTime)
    {
        if (dist >= noEntry + decelBand)
            return velocity;

        float inward = velocity.Dot(nOut);
        if (inward < 0f)
            velocity -= nOut * inward; // remove the inward (toward-surface) component
        velocity *= Mathf.Pow(Mathf.Clamp(damping, 0f, 1f), deltaTime);
        return velocity;
    }

    /// <summary>
    /// If the candidate position (shipPos + finalDelta) would land inside the no-entry shell, nudges
    /// it back to the shell edge and zeroes residual inward velocity. Returns the corrected delta and velocity.
    /// </summary>
    public static (Vector3 finalDelta, Vector3 velocity) ClampNoEntry(
        Vector3 shipPos, Vector3 finalDelta, Vector3 bodyPos, float noEntry, Vector3 velocity)
    {
        Vector3 rel = (shipPos + finalDelta) - bodyPos;
        float d = rel.Length();
        if (d < noEntry && d > 1e-6f)
        {
            Vector3 dir = rel / d;
            finalDelta = (bodyPos + dir * noEntry) - shipPos;
            float inward = velocity.Dot(dir);
            if (inward < 0f)
                velocity -= dir * inward;
        }
        return (finalDelta, velocity);
    }
}
