using Godot;

namespace Constructables.ArtificialSatellites;

public interface IArtificialSatellite
{
    public bool IsStationary { get; set; }
    public bool IsActive { get; set; }
    public Vector3 Velocity { get; set; }
    public Vector3 UnitPosition { get; set; }

    #region OrbitalParameters

    /// <summary>Current orbital angle in radians.</summary>
    float OrbitalAngle { get; }

    /// <summary>Current orbital radius in meters.</summary>
    float OrbitalRadius { get; }

    /// <summary>Current orbital angular speed in rad/s.</summary>
    float OrbitalSpeed { get; }

    /// <summary>
    /// Initializes the satellite's orbit using the host body's orbital parameters.
    /// Uses band-based or continuous placement based on the host body's configuration.
    /// </summary>
    /// <param name="hostBody">The orbital body to orbit around.</param>
    /// <param name="bandIndex">Index of the orbit band (for band-based bodies).</param>
    void InitializeOrbit(IOrbitalBody hostBody, int bandIndex);

    /// <summary>
    /// Initializes the satellite's orbit at a specific radius (for continuous placement).
    /// Used for bodies like stars that don't use discrete orbit bands.
    /// </summary>
    /// <param name="hostBody">The orbital body to orbit around.</param>
    /// <param name="radius">Desired orbital radius in meters.</param>
    void InitializeOrbitAtRadius(IOrbitalBody hostBody, float radius);

    #endregion
}
