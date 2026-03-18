using Godot;
using CelestialBody = ProceduralGeneration.PlanetGeneration.CelestialBody;

namespace Structures.Logistics;

/// <summary>
/// Represents a gravity assist opportunity where a spacecraft can use a celestial body's
/// gravity to change direction and gain/lose velocity.
/// </summary>
public struct GravityAssistOpportunity
{
    /// <summary>
    /// The celestial body used for the gravity assist.
    /// </summary>
    public CelestialBody AssistBody;

    /// <summary>
    /// Time in seconds when the spacecraft approaches the assist body.
    /// </summary>
    public float ApproachTime;

    /// <summary>
    /// The delta-v savings achieved by using this gravity assist in m/s.
    /// Positive values indicate a savings (less fuel needed).
    /// </summary>
    public float DeltaVSavings;

    /// <summary>
    /// The angle by which the trajectory is deflected in radians.
    /// </summary>
    public float DeflectionAngle;

    /// <summary>
    /// The velocity vector after the gravity assist in m/s.
    /// </summary>
    public Vector3 ExitVelocity;

    /// <summary>
    /// Creates a new GravityAssistOpportunity with the specified values.
    /// </summary>
    /// <param name="assistBody">The celestial body used for the gravity assist.</param>
    /// <param name="approachTime">Time of approach in seconds.</param>
    /// <param name="deltaVSavings">Delta-v savings achieved in m/s.</param>
    /// <param name="deflectionAngle">Deflection angle in radians.</param>
    /// <param name="exitVelocity">Exit velocity vector in m/s.</param>
    public GravityAssistOpportunity(
        CelestialBody assistBody,
        float approachTime,
        float deltaVSavings,
        float deflectionAngle,
        Vector3 exitVelocity
    )
    {
        AssistBody = assistBody;
        ApproachTime = approachTime;
        DeltaVSavings = deltaVSavings;
        DeflectionAngle = deflectionAngle;
        ExitVelocity = exitVelocity;
    }

    /// <summary>
    /// Gets a human-readable description of this gravity assist opportunity.
    /// </summary>
    /// <returns>A string describing the key parameters of this opportunity.</returns>
    public string GetDescription()
    {
        string bodyName = AssistBody != null ? AssistBody.Name : "Unknown";
        return $"Gravity Assist: {bodyName}, Time: {ApproachTime:F1}s, " +
               $"ΔV Savings: {DeltaVSavings:F2} m/s, Deflection: {Mathf.RadToDeg(DeflectionAngle):F1}°";
    }
}
