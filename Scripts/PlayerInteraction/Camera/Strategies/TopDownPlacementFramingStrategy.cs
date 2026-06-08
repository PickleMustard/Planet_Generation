using Godot;

namespace PlayerInteraction.Camera;

/// <summary>
/// Framing for the top-down station placement board: the camera sits directly
/// above the target body's anchor looking straight down (-Y), so the body's
/// orbit bands (concentric XZ circles) read as true circles. Drag-rotate is
/// disabled; the controller instead maps the scroll-wheel to camera height and a
/// middle-drag to a planar XZ pan via <see cref="ComputeTopDownPose"/>.
/// </summary>
public sealed class TopDownPlacementFramingStrategy : IFocusFramingStrategy
{
    private readonly IOrbitalBody _body;

    /// <summary>Radius of the outermost feature (outer band or body) in world units.</summary>
    public float OuterExtent { get; }

    public TopDownPlacementFramingStrategy(IOrbitalBody body)
    {
        _body = body;
        OuterExtent = ComputeOuterExtent(body);
    }

    public bool AllowsDrag => false;

    /// <summary>Interpreted as the camera-height (zoom) range in placement mode.</summary>
    public (float Min, float Max) ZoomClamps =>
        (Mathf.Max(_body.Radius * 2.5f, 5f), Mathf.Max(OuterExtent * 3f, _body.Radius * 25f));

    /// <summary>Initial camera height framing the outer ring with some margin.</summary>
    public float ComputeOrbitDistance(Node3D anchor)
    {
        var (min, max) = ZoomClamps;
        return Mathf.Clamp(OuterExtent * 1.8f, min, max);
    }

    /// <summary>
    /// Interface entry point. Treats <paramref name="distance"/> as height and
    /// applies no pan; the controller calls <see cref="ComputeTopDownPose"/>
    /// directly while in placement mode to thread pan/height through.
    /// </summary>
    public Transform3D ComputeWorldPose(
        Node3D anchor,
        float yaw,
        float pitch,
        float distance,
        Vector2 screenOffset,
        Camera3D cam)
        => ComputeTopDownPose(anchor, Vector3.Zero, distance, cam);

    /// <summary>
    /// Overhead pose: camera at <c>center + panOffset + Up * height</c> looking
    /// straight down. Up vector is Vector3.Forward because the look direction is
    /// straight down -Y (Vector3.Up would be degenerate).
    /// </summary>
    public Transform3D ComputeTopDownPose(Node3D anchor, Vector3 panOffset, float height, Camera3D cam)
    {
        Vector3 center = anchor.GlobalPosition + panOffset;
        Vector3 camPos = center + Vector3.Up * height;
        var basis = Basis.LookingAt(center - camPos, Vector3.Forward);
        return new Transform3D(basis, camPos);
    }

    public float GetNorthScreenAngle(Node3D anchor, Camera3D cam) => 0f;

    private static float ComputeOuterExtent(IOrbitalBody body)
    {
        float extent = Mathf.Max(body.Radius * 2f, 1f);
        if (body.UsesBandPlacement)
        {
            int count = body.GetBandCount();
            if (count > 0)
                extent = Mathf.Max(extent, body.GetOrbitBandRadius(count - 1));
        }
        return extent;
    }
}
