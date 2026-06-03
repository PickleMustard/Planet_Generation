using Godot;
using UtilityLibrary.GameMath.Orbital;

namespace ProceduralGeneration;

public static class OrbitalDistanceCalculator
{
    public static float CalculateDistanceFromStarAU(Godot.Collections.Dictionary body)
    {
        float apogee = GetApogeeFromBody(body);
        float perigee = GetPerigeeFromBody(body);
        float semiMajorAxis = (apogee + perigee) / 2f;
        return OrbitalMath.ConvertUnitsToAU(semiMajorAxis);
    }

    public static float CalculateDistanceFromParentAU(Vector3 parentPos, Vector3 childPos)
    {
        return OrbitalMath.CalculateDistanceFromParentAU(parentPos, childPos);
    }

    /// <summary>
    /// Cumulative distance from the system center in AU, summed along the orbital chain. Walks
    /// <see cref="IOrbitalBody.OrbitalParent"/> adding each link's own distance-from-its-parent. Used
    /// as the load/restore fallback when <c>CelestialBody.EffectiveAU</c> was not pre-set at
    /// generation time.
    /// </summary>
    public static float ComputeEffectiveAU(IOrbitalBody body)
    {
        float sum = 0f;
        IOrbitalBody? current = body;
        while (current != null)
        {
            if (current is PlanetGeneration.CelestialBody cb)
                sum += cb.GetDistanceFromCenterAU();
            current = current.OrbitalParent;
        }
        return sum;
    }

    public static float CalculateBeltDistanceAU(Godot.Collections.Dictionary belt)
    {
        float ringApogee = 1000f;
        float ringPerigee = 500f;

        if (belt.ContainsKey("template"))
        {
            var template = belt["template"].As<Godot.Collections.Dictionary>();
            if (template.ContainsKey("ring_apogee"))
            {
                ringApogee = (float)template["ring_apogee"];
            }
            if (template.ContainsKey("ring_perigee"))
            {
                ringPerigee = (float)template["ring_perigee"];
            }
        }

        float avgDistance = (ringApogee + ringPerigee) / 2f;
        return OrbitalMath.ConvertUnitsToAU(avgDistance);
    }

    private static float GetApogeeFromBody(Godot.Collections.Dictionary body)
    {
        if (body.ContainsKey("orbital_parameters"))
        {
            var orbitalParams = body["orbital_parameters"].As<Godot.Collections.Dictionary>();
            if (orbitalParams.ContainsKey("apogee"))
            {
                return (float)orbitalParams["apogee"];
            }
        }

        if (body.ContainsKey("template"))
        {
            var template = body["template"].As<Godot.Collections.Dictionary>();
            if (template.ContainsKey("apogee"))
            {
                return (float)template["apogee"];
            }
        }

        return 1000f;
    }

    private static float GetPerigeeFromBody(Godot.Collections.Dictionary body)
    {
        if (body.ContainsKey("orbital_parameters"))
        {
            var orbitalParams = body["orbital_parameters"].As<Godot.Collections.Dictionary>();
            if (orbitalParams.ContainsKey("perigee"))
            {
                return (float)orbitalParams["perigee"];
            }
        }

        if (body.ContainsKey("template"))
        {
            var template = body["template"].As<Godot.Collections.Dictionary>();
            if (template.ContainsKey("perigee"))
            {
                return (float)template["perigee"];
            }
        }

        return 500f;
    }
}
