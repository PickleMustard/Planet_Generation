using Godot;
using Structures.GameState;

public interface IOrbitalBody
{
    // Physical properties
    public float Radius { get; set; }
    public float Mass { get; set; }
    public Vector3 Velocity { get; set; }
    public Vector3 BodyPosition { get; set; }
    public string BodyName { get; }

    // Orbital Band System - Properties
    public Godot.Collections.Array<OrbitBand> OrbitBands { get; }
    public OrbitConfiguration? OrbitConfig { get; }
    public Node3D SatellitesContainer { get; }

    // Orbital Band System - Methods
    public void InitializeOrbitSystem();
    public int GetBandCount();
    public bool CanAddToBand(int bandIndex);
    public int GetBandSatelliteCount(int bandIndex);

    /// <summary>
    /// Increments the satellite count for the specified band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when band index is invalid</exception>
    void IncrementBandCount(int bandIndex);

    /// <summary>
    /// Decrements the satellite count for the specified band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when band index is invalid</exception>
    void DecrementBandCount(int bandIndex);

    #region OrbitalParameters

    /// <summary>
    /// Indicates whether this body uses discrete band-based placement (planets, moons)
    /// or continuous placement (stars, black holes, neutron stars).
    /// </summary>
    bool UsesBandPlacement { get; }

    /// <summary>
    /// Gets orbital parameters for a satellite placed in the specified band.
    /// </summary>
    /// <param name="bandIndex">Index of the orbit band.</param>
    /// <param name="startingAngle">Starting orbital angle in radians.</param>
    /// <returns>Complete orbital parameters including position and velocity.</returns>
    OrbitalParameters GetOrbitalParametersForBand(int bandIndex, float startingAngle);

    /// <summary>
    /// Gets orbital parameters for a satellite placed at an arbitrary radius (continuous placement).
    /// Used for bodies that don't use discrete orbit bands.
    /// </summary>
    /// <param name="radius">Desired orbital radius in meters.</param>
    /// <param name="startingAngle">Starting orbital angle in radians.</param>
    /// <returns>Complete orbital parameters including position and velocity.</returns>
    OrbitalParameters GetOrbitalParametersAtRadius(float radius, float startingAngle);
    public int GetClosestBandForApproach(float approachSpeed);
    public float GetOrbitBandRadius(int bandIndex);
    public float GetOrbitalSpeedForBand(int bandIndex);

    #endregion
}
