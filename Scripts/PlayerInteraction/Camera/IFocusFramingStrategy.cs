using Godot;

namespace PlayerInteraction.Camera;

/// <summary>
/// Per-window framing rule for the camera while it is focused on (reparented
/// under) an object's CameraAnchor. Collapses the three former orbit-camera
/// classes (orbital body / station / logistics unit) into interchangeable
/// strategies that the <see cref="PlayerCameraController"/> drives.
///
/// All poses are returned in WORLD space; the controller assigns them to the
/// reparented camera's <see cref="Node3D.GlobalTransform"/> (Godot derives the
/// local transform). Reparenting also makes the camera follow the focused
/// object's orbital motion between recomputes.
/// </summary>
public interface IFocusFramingStrategy
{
    /// <summary>Whether left-drag rotates the view (orbital bodies only).</summary>
    bool AllowsDrag { get; }

    /// <summary>Min/max orbit distance allowed by scroll-wheel zoom.</summary>
    (float Min, float Max) ZoomClamps { get; }

    /// <summary>Initial orbit distance when focus begins.</summary>
    float ComputeOrbitDistance(Node3D anchor);

    /// <summary>
    /// Computes the camera's desired world transform for the current orbit
    /// parameters. <paramref name="yaw"/>/<paramref name="pitch"/> are ignored
    /// by strategies that lock the view direction (station / logistics).
    /// </summary>
    Transform3D ComputeWorldPose(
        Node3D anchor,
        float yaw,
        float pitch,
        float distance,
        Vector2 screenOffset,
        Camera3D cam);

    /// <summary>
    /// On-screen angle (radians, clockwise from screen-up) of the focused
    /// object's north pole, for the compass rose. Returns 0 when not meaningful.
    /// </summary>
    float GetNorthScreenAngle(Node3D anchor, Camera3D cam);
}
