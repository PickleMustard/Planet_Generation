using Godot;
using UtilityLibrary.GameMath.Orbital;

namespace Structures.GameState;

/// <summary>
/// Immutable bundle of orbital state returned by an IOrbitalBody to a satellite requesting placement.
/// </summary>
public readonly struct OrbitalParameters
{
    /// <summary>Orbital radius from host center.</summary>
    public float Radius { get; init; }

    /// <summary>Angular speed in rad/s, physics-derived: sqrt(G*M/r^3).</summary>
    public float AngularSpeed { get; init; }

    /// <summary>Linear speed = Radius * AngularSpeed.</summary>
    public float LinearSpeed { get; init; }

    /// <summary>Starting position relative to host body.</summary>
    public Vector3 InitialPosition { get; init; }

    /// <summary>Starting velocity vector (tangent to orbit).</summary>
    public Vector3 InitialVelocity { get; init; }

    /// <summary>Host body mass (for re-derivation if needed).</summary>
    public float HostMass { get; init; }

    /// <summary>Band index, or -1 for continuous placement.</summary>
    public int BandIndex { get; init; }

    /// <summary>
    /// Creates a new OrbitalParameters struct with all required fields.
    /// </summary>
    public OrbitalParameters(
        float radius,
        float angularSpeed,
        float linearSpeed,
        Vector3 initialPosition,
        Vector3 initialVelocity,
        float hostMass,
        int bandIndex
    )
    {
        Radius = radius;
        AngularSpeed = angularSpeed;
        LinearSpeed = linearSpeed;
        InitialPosition = initialPosition;
        InitialVelocity = initialVelocity;
        HostMass = hostMass;
        BandIndex = bandIndex;
    }

    /// <summary>
    /// Creates orbital parameters for a circular orbit in the XZ plane.
    /// </summary>
    /// <param name="radius">Orbital radius in meters.</param>
    /// <param name="angularSpeed">Angular speed in rad/s.</param>
    /// <param name="startingAngle">Starting angle in radians.</param>
    /// <param name="hostMass">Mass of the host body.</param>
    /// <param name="bandIndex">Band index (-1 for continuous).</param>
    /// <returns>Configured OrbitalParameters struct.</returns>
    public static OrbitalParameters CreateCircular(
        float radius,
        float angularSpeed,
        float startingAngle,
        float hostMass,
        int bandIndex
    )
    {
        // Position: r * (cos(θ), 0, sin(θ))
        Vector3 position = new Vector3(
            Mathf.Cos(startingAngle) * radius,
            0f,
            Mathf.Sin(startingAngle) * radius
        );

        // Linear speed: v = r * ω
        float linearSpeed = radius * angularSpeed;

        // Velocity: v * (-sin(θ), 0, cos(θ)) - tangent to orbit
        Vector3 velocity = new Vector3(
            -Mathf.Sin(startingAngle) * linearSpeed,
            0f,
            Mathf.Cos(startingAngle) * linearSpeed
        );

        return new OrbitalParameters(
            radius: radius,
            angularSpeed: angularSpeed,
            linearSpeed: linearSpeed,
            initialPosition: position,
            initialVelocity: velocity,
            hostMass: hostMass,
            bandIndex: bandIndex
        );
    }

    /// <summary>
    /// Calculates physics-based angular speed for a circular orbit.
    /// Formula: ω = sqrt(G*M/r^3)
    /// </summary>
    /// <param name="hostMass">Mass of the central body in kg.</param>
    /// <param name="radius">Orbital radius in meters.</param>
    /// <returns>Angular speed in rad/s.</returns>
    public static float CalculateAngularSpeed(float hostMass, float radius)
    {
        if (radius <= 0f || hostMass <= 0f)
            return 0f;

        return Mathf.Sqrt(
            OrbitalMath.GRAVITATIONAL_CONSTANT * hostMass / (radius * radius)
        );
    }

    public override string ToString()
    {
        return $"OrbitalParameters: Radius={Radius:F2}, AngularSpeed={AngularSpeed:F6}, LinearSpeed={LinearSpeed:F2}, BandIndex={BandIndex}";
    }
}
