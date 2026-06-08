using Godot;

namespace PlayerInteraction.Camera;

/// <summary>
/// Framing for orbital body inspection: anchor-centered, drag-rotatable, with a
/// compass-tracking north pole. Ports the math from the former
/// OrbitalBodyOrbitCamera. Distance defaults to body.Radius * 4 and zoom is
/// clamped relative to the body radius.
/// </summary>
public sealed class OrbitalBodyFramingStrategy : IFocusFramingStrategy
{
    private const float ORBIT_RADIUS_MULTIPLIER = 4.0f;

    private readonly IOrbitalBody _body;

    public OrbitalBodyFramingStrategy(IOrbitalBody body)
    {
        _body = body;
    }

    public bool AllowsDrag => true;

    public (float Min, float Max) ZoomClamps =>
        (Mathf.Max(_body.Radius * 1.5f, 0.5f), _body.Radius * 12f);

    public float ComputeOrbitDistance(Node3D anchor) => _body.Radius * ORBIT_RADIUS_MULTIPLIER;

    public Transform3D ComputeWorldPose(
        Node3D anchor,
        float yaw,
        float pitch,
        float distance,
        Vector2 screenOffset,
        Camera3D cam)
    {
        Vector3 center = anchor.GlobalPosition;

        float x = Mathf.Cos(pitch) * Mathf.Sin(yaw);
        float y = Mathf.Sin(pitch);
        float z = Mathf.Cos(pitch) * Mathf.Cos(yaw);

        var dir = new Vector3(x, y, z).Normalized();
        var camPos = center - dir * distance;

        var up = Mathf.Abs(dir.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
        var basis = Basis.LookingAt(center - camPos, up);

        (camPos, basis) = FramingMath.ApplyScreenOffset(
            camPos, basis, center, distance, screenOffset, up, cam);

        return new Transform3D(basis, camPos);
    }

    public float GetNorthScreenAngle(Node3D anchor, Camera3D cam)
    {
        Vector3 center = anchor.GlobalPosition;
        Vector3 north = center + Vector3.Up * Mathf.Max(_body.Radius, 1f);

        Vector2 centerScreen = cam.UnprojectPosition(center);
        Vector2 northScreen = cam.UnprojectPosition(north);
        Vector2 delta = northScreen - centerScreen;
        if (delta.LengthSquared() < 1e-6f)
            return 0f;
        // Atan2(x, -y): 0 = pointing up on screen, positive = clockwise.
        return Mathf.Atan2(delta.X, -delta.Y);
    }
}
