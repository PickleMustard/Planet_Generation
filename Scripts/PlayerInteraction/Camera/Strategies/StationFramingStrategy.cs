using Constructables;
using Godot;

namespace PlayerInteraction.Camera;

/// <summary>
/// Framing for station inspection: the camera locks along the parent-body →
/// station ray so the parent body sits behind the station. No drag; zoom only.
/// Ports the math from the former StationOrbitCamera.
/// </summary>
public sealed class StationFramingStrategy : IFocusFramingStrategy
{
    private const float FRAMING_DISTANCE_MULTIPLIER = 1.5f;
    private const float MIN_FRAMING_DISTANCE = 6.0f;

    private readonly StationSatellite _station;

    public StationFramingStrategy(StationSatellite station)
    {
        _station = station;
    }

    public bool AllowsDrag => false;

    public (float Min, float Max) ZoomClamps => (MIN_FRAMING_DISTANCE * 0.5f, MIN_FRAMING_DISTANCE * 8f);

    public float ComputeOrbitDistance(Node3D anchor) =>
        Mathf.Max(anchor.Position.Length() * FRAMING_DISTANCE_MULTIPLIER, MIN_FRAMING_DISTANCE);

    public Transform3D ComputeWorldPose(
        Node3D anchor,
        float yaw,
        float pitch,
        float distance,
        Vector2 screenOffset,
        Camera3D cam)
    {
        Vector3 stationPos = anchor.GlobalPosition;
        Vector3 parentPos = _station.ParentBody is Node3D parentNode
            ? parentNode.GlobalPosition
            : stationPos + Vector3.Back;

        Vector3 away = stationPos - parentPos;
        if (away.LengthSquared() < 1e-4f)
            away = Vector3.Back;
        away = away.Normalized();

        Vector3 camPos = stationPos + away * distance;
        Vector3 up = Mathf.Abs(away.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
        Basis basis = Basis.LookingAt(stationPos - camPos, up);

        (camPos, basis) = FramingMath.ApplyScreenOffset(
            camPos, basis, stationPos, distance, screenOffset, up, cam);

        return new Transform3D(basis, camPos);
    }

    public float GetNorthScreenAngle(Node3D anchor, Camera3D cam) => 0f;
}
