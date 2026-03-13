using Godot;

namespace UtilityLibrary;

public static class OrbitalMath
{
    public const float GRAVITATIONAL_CONSTANT = 6.7394967f;

    public static Vector3 CalculateOrbitalPosition(
        Vector3 pHat,
        Vector3 qHat,
        float apogee,
        float perigee,
        float angle,
        float eccentricity = 0f
    )
    {
        float semiMajorAxis = (apogee + perigee) / 2f;
        float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(1f - eccentricity * eccentricity);
        float focalDistance = semiMajorAxis * eccentricity;

        float radius =
            semiMajorAxis
            * (1 - eccentricity * eccentricity)
            / (1 + eccentricity * Mathf.Cos(angle));
        Vector3 resultVector = radius * Mathf.Cos(angle) * pHat + radius * Mathf.Sin(angle) * qHat;
        return resultVector;
    }

    public static Vector3 CalculateOrbitalVelocity(
        float centralMass,
        Vector3 position,
        bool clockwise = false
    )
    {
        float distance = position.Length();
        if (distance <= 0f)
            return Vector3.Zero;

        float orbitalSpeed = Mathf.Sqrt(GRAVITATIONAL_CONSTANT * centralMass / distance);
        Vector3 tangentDirection = new Vector3(-position.Z, 0, position.X).Normalized();

        if (!clockwise)
            tangentDirection = -tangentDirection;

        return tangentDirection * orbitalSpeed;
    }

    public static float CalculateEccentricity(float apogee, float perigee)
    {
        float denominator = apogee + perigee;
        if (denominator <= 0f)
            return 1f; // Default to circular orbit to avoid division by zero

        return (apogee - perigee) / denominator;
    }

    /// <summary>
    /// Calculates orbital velocity at a specific position on an elliptical orbit.
    /// Uses the vis-viva equation: v² = GM(2/r - 1/a)
    /// </summary>
    /// <param name="centralMass">Mass of the parent body</param>
    /// <param name="apogee">Farthest point from parent</param>
    /// <param name="perigee">Closest point to parent</param>
    /// <param name="position">Current position of the satellite</param>
    /// <param name="clockwise">Whether orbit is clockwise (default: false = counter-clockwise)</param>
    /// <returns>Orbital velocity vector at the given position</returns>
    public static Vector3 CalculateEllipticalOrbitalVelocity(
        Vector3 pHat,
        Vector3 qHat,
        float centralMass,
        float apogee,
        float perigee,
        float angle,
        bool clockwise = false
    )
    {
        float eccentricity = CalculateEccentricity(apogee, perigee);
        float semiMajorAxis = apogee / (1.0f + eccentricity);

        if (semiMajorAxis <= 0f)
        {
            GD.PrintErr(
                $"OrbitalMath: Invalid semi-major axis {semiMajorAxis}, returning zero velocity"
            );
            return Vector3.Zero;
        }

        float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(1f - eccentricity * eccentricity);
        float focalDistance = semiMajorAxis * eccentricity;

        float radius =
            semiMajorAxis
            * (1 - eccentricity * eccentricity)
            / (1 + eccentricity * Mathf.Cos(angle));
        Vector3 resultVector = radius * Mathf.Cos(angle) * pHat + radius * Mathf.Sin(angle) * qHat;

        float constants =
            (GRAVITATIONAL_CONSTANT * centralMass)
            / (
                Mathf.Sqrt(
                    GRAVITATIONAL_CONSTANT
                        * centralMass
                        * semiMajorAxis
                        * (1 - eccentricity * eccentricity)
                )
            );
        Vector3 resultantVector =
            constants * (-Mathf.Sin(angle) * pHat + (eccentricity + Mathf.Cos(angle)) * qHat);
        return clockwise ? resultantVector : -resultantVector;
    }
}
