using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;

namespace PlayerInteraction.Camera;

/// <summary>
/// Framing for logistics-unit (ship) inspection: looks at the ship from the
/// direction away from its parent body, so the body sits behind it. Falls back
/// to a fixed direction during heliocentric transfers. No drag; zoom only.
/// Because the camera reparents under the unit's anchor, ship motion is tracked
/// for free. Ports the math from the former LogisticsUnitOrbitCamera.
/// </summary>
public sealed class LogisticsUnitFramingStrategy : IFocusFramingStrategy
{
    private const float DEFAULT_FRAMING_DISTANCE = 10.0f;

    private readonly LogisticsUnit _unit;

    public LogisticsUnitFramingStrategy(LogisticsUnit unit)
    {
        _unit = unit;
    }

    public bool AllowsDrag => false;

    public (float Min, float Max) ZoomClamps => (3f, 60f);

    public float ComputeOrbitDistance(Node3D anchor) => DEFAULT_FRAMING_DISTANCE;

    public Transform3D ComputeWorldPose(
        Node3D anchor,
        float yaw,
        float pitch,
        float distance,
        Vector2 screenOffset,
        Camera3D cam)
    {
        Vector3 shipPos = anchor.GlobalPosition;

        Node3D? parentBody = FindParentBody(_unit);
        Vector3 away = parentBody != null && Node.IsInstanceValid(parentBody)
            ? shipPos - parentBody.GlobalPosition
            : Vector3.Back;
        if (away.LengthSquared() < 1e-4f)
            away = Vector3.Back;
        away = away.Normalized();

        Vector3 camPos = shipPos + away * distance;
        Vector3 up = Mathf.Abs(away.Y) > 0.999f ? Vector3.Forward : Vector3.Up;
        Basis basis = Basis.LookingAt(shipPos - camPos, up);

        (camPos, basis) = FramingMath.ApplyScreenOffset(
            camPos, basis, shipPos, distance, screenOffset, up, cam);

        return new Transform3D(basis, camPos);
    }

    public float GetNorthScreenAngle(Node3D anchor, Camera3D cam) => 0f;

    private static Node3D? FindParentBody(Node node)
    {
        Node? current = node.GetParent();
        while (current != null)
        {
            if (current is CelestialBody body)
                return body;
            current = current.GetParent();
        }
        return null;
    }
}
