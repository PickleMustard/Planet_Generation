using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures;
using Structures.Enums;

namespace ProceduralGeneration;

/// <summary>
/// Helpers for walking the <see cref="IOrbitalBody.OrbitalParent"/> chain.
/// </summary>
public static class OrbitalBodyExtensions
{
    /// <summary>
    /// Walks up the orbital-parent chain until a star, neutron star, or black hole
    /// is found. Returns null if none exists (e.g. orphaned body, starless system).
    /// Black holes count — orbiting one still defines a system center, even though
    /// solar buildings will see no useful illumination from it.
    /// </summary>
    public static CelestialBody? FindParentStar(this IOrbitalBody body)
    {
        var current = body.OrbitalParent;
        int safety = 16;
        while (current != null && safety-- > 0)
        {
            if (current is CelestialBody cb && cb.Classification != null)
            {
                if (cb.Classification is BodyClassification.Star
                    or BodyClassification.NeutronStar
                    or BodyClassification.BlackHole)
                {
                    return cb;
                }
            }
            current = current.OrbitalParent;
        }
        return null;
    }

    /// <summary>
    /// World-space distance between this body and its parent star.
    /// Returns 0 if no parent star exists.
    /// </summary>
    public static float DistanceToParentStar(this IOrbitalBody body)
    {
        var star = body.FindParentStar();
        if (star == null)
            return 0f;
        if (body is Node3D self)
            return self.GlobalPosition.DistanceTo(star.GlobalPosition);
        return 0f;
    }
}
