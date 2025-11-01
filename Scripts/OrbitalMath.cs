using Godot;

namespace PlanetGeneration;
public static class OrbitalMath
{
    public const float GRAVITATIONAL_CONSTANT = .0067394967f;

    public static Vector3 CalculateOrbitalPosition(float apogee, float perigee, float angle, float eccentricity = 0f)
    {
        float semiMajorAxis = (apogee + perigee) / 2f;
        float semiMinorAxis = semiMajorAxis * Mathf.Sqrt(1f - eccentricity * eccentricity);
        float focalDistance = semiMajorAxis * eccentricity;

        // Center the ellipse at the focal point (parent body position)
        float x = semiMajorAxis * Mathf.Cos(angle) - focalDistance;
        float z = semiMinorAxis * Mathf.Sin(angle);

        return new Vector3(x, 0, z);
    }

    public static Vector3 CalculateOrbitalVelocity(float centralMass, Vector3 position, bool clockwise = false)
    {
        float distance = position.Length();
        if (distance <= 0f) return Vector3.Zero;

        float orbitalSpeed = Mathf.Sqrt(GRAVITATIONAL_CONSTANT * centralMass / distance);
        Vector3 tangentDirection = new Vector3(-position.Z, 0, position.X).Normalized();

        if (!clockwise)
            tangentDirection = -tangentDirection;

        return tangentDirection * orbitalSpeed;
    }

    public static float CalculateEccentricity(float apogee, float perigee)
    {
        return (apogee - perigee) / (apogee + perigee);
    }
}
