using Godot;

namespace UI.SystemBoard;

/// <summary>
/// Pure geometry helpers for the System Overview board: regular-polygon and
/// triangle vertex generation (stations / logistics units), XZ projection of
/// world positions, velocity-to-heading conversion, point-in-polygon hit
/// testing, and a cheap bulged-Bézier route arc. Kept free of scene/node
/// dependencies so it is unit-testable without a Godot runtime.
/// </summary>
public static class SystemBoardShapes
{
    /// <summary>
    /// Vertices of a regular <paramref name="sides"/>-gon centred at
    /// <paramref name="c"/> with circumradius <paramref name="r"/>. The first
    /// vertex points "up" (−Y) before <paramref name="rotation"/> (radians) is
    /// applied. Used for station octagons (<c>sides = 8</c>).
    /// </summary>
    public static Vector2[] RegularPolygon(Vector2 c, float r, int sides, float rotation = 0f)
    {
        if (sides < 3) sides = 3;
        var verts = new Vector2[sides];
        float step = Mathf.Tau / sides;
        for (int i = 0; i < sides; i++)
        {
            float a = rotation - Mathf.Pi / 2f + i * step;
            verts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return verts;
    }

    /// <summary>
    /// Vertices of an equilateral triangle inscribed in a circle of radius
    /// <paramref name="r"/> centred at <paramref name="c"/>, with its apex (first
    /// vertex) pointing along <paramref name="headingRad"/>. Used for logistics
    /// units, whose apex points in the direction of travel.
    /// </summary>
    public static Vector2[] Triangle(Vector2 c, float r, float headingRad)
    {
        const float third = Mathf.Tau / 3f;
        return new[]
        {
            c + new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad)) * r,
            c + new Vector2(Mathf.Cos(headingRad + third), Mathf.Sin(headingRad + third)) * r,
            c + new Vector2(Mathf.Cos(headingRad - third), Mathf.Sin(headingRad - third)) * r,
        };
    }

    /// <summary>Projects a 3D world position onto the board plane (drops Y).</summary>
    public static Vector2 ProjectXZ(Vector3 p) => new(p.X, p.Z);

    /// <summary>
    /// Heading (radians) of a velocity vector projected onto the XZ plane.
    /// Returns 0 for a (near-)zero velocity so callers get a stable fallback.
    /// </summary>
    public static float HeadingFromVelocityXZ(Vector3 vel)
    {
        var v = new Vector2(vel.X, vel.Z);
        if (v.LengthSquared() < 1e-8f)
            return 0f;
        return Mathf.Atan2(v.Y, v.X);
    }

    /// <summary>Even-odd ray-cast point-in-polygon test.</summary>
    public static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        if (poly == null || poly.Length < 3)
            return false;

        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[j];
            if (((a.Y > p.Y) != (b.Y > p.Y)) &&
                (p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Samples a quadratic Bézier from <paramref name="a"/> to <paramref name="b"/>
    /// bowed perpendicular to the chord by <paramref name="bow"/> board units. A
    /// cheap stand-in for a true transfer-orbit arc when drawing an in-transit
    /// logistics leg.
    /// </summary>
    public static Vector2[] BulgedBezier(Vector2 a, Vector2 b, float bow, int samples)
    {
        if (samples < 2) samples = 2;
        Vector2 mid = (a + b) * 0.5f;
        Vector2 perp = (b - a).Orthogonal().Normalized();
        Vector2 ctrl = mid + perp * bow;

        var pts = new Vector2[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            float u = 1f - t;
            pts[i] = u * u * a + 2f * u * t * ctrl + t * t * b;
        }
        return pts;
    }
}
