using System.Collections.Generic;
using Godot;

namespace UI.Construction;

/// <summary>
/// Pure geometry helpers for top-down station placement (cursor→plane projection
/// and band snapping). No scene-tree dependencies so they are unit-testable.
/// </summary>
public static class PlacementMath
{
    /// <summary>
    /// Intersects the ray (<paramref name="rayOrigin"/>, <paramref name="rayDir"/>)
    /// with the horizontal plane y = <paramref name="planeY"/>. Returns false when
    /// the ray is parallel to the plane or the hit is behind the origin.
    /// </summary>
    public static bool ProjectToPlaneY(Vector3 rayOrigin, Vector3 rayDir, float planeY, out Vector3 hit)
    {
        hit = Vector3.Zero;
        if (Mathf.Abs(rayDir.Y) < 1e-6f)
            return false;

        float t = (planeY - rayOrigin.Y) / rayDir.Y;
        if (t < 0f)
            return false;

        hit = rayOrigin + rayDir * t;
        return true;
    }

    /// <summary>
    /// Returns the index of the band whose radius is closest to
    /// <paramref name="radius"/>, mirroring the argmin in the old
    /// StationPlacementMode.UpdateBandSnapping. Returns -1 (and echoes the input
    /// radius) when the list is empty.
    /// </summary>
    public static int NearestBand(float radius, IReadOnlyList<float> bandRadii, out float snappedRadius)
    {
        if (bandRadii == null || bandRadii.Count == 0)
        {
            snappedRadius = radius;
            return -1;
        }

        float bestDelta = float.MaxValue;
        int bestBand = 0;
        for (int i = 0; i < bandRadii.Count; i++)
        {
            float delta = Mathf.Abs(radius - bandRadii[i]);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestBand = i;
            }
        }

        snappedRadius = bandRadii[bestBand];
        return bestBand;
    }
}
