using Godot;

namespace PlayerInteraction.Camera;

/// <summary>
/// Shared framing helpers ported from the former orbit cameras.
/// </summary>
internal static class FramingMath
{
    /// <summary>
    /// Slides the camera laterally so the focused point projects to the
    /// negative-space center (unobstructed window region) instead of the
    /// geometric viewport center, then re-aims at the point. Converts the pixel
    /// <paramref name="screenOffset"/> to world units via the camera's vertical
    /// frustum extent at <paramref name="distance"/>. Returns the adjusted
    /// (camPos, basis); a no-op when the offset is zero or the viewport height
    /// is unknown.
    /// </summary>
    public static (Vector3 CamPos, Basis Basis) ApplyScreenOffset(
        Vector3 camPos,
        Basis basis,
        Vector3 lookAtPoint,
        float distance,
        Vector2 screenOffset,
        Vector3 up,
        Camera3D cam)
    {
        if (screenOffset.X == 0f && screenOffset.Y == 0f)
            return (camPos, basis);

        float vpHeight = cam.GetViewport().GetVisibleRect().Size.Y;
        if (vpHeight <= 0f)
            return (camPos, basis);

        float fovRad = Mathf.DegToRad(cam.Fov);
        float worldPerPixel = (2f * distance * Mathf.Tan(fovRad * 0.5f)) / vpHeight;

        // Negative X: shifting the camera right moves the body left on screen.
        // Matching Y sign because screen Y is inverted relative to world Y.
        camPos += -basis.X * (screenOffset.X * worldPerPixel)
                + basis.Y * (screenOffset.Y * worldPerPixel);
        basis = Basis.LookingAt(lookAtPoint - camPos, up);
        return (camPos, basis);
    }
}
