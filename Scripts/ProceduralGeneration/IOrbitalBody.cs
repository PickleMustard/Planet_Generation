using Constructables;
using Godot;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.GameState;
using Structures.Transfers;
using System;
using System.Collections.Generic;
using System.Collections.Generic;

public interface IOrbitalBody
{
    // Type classification
    BodyClassification Classification { get; }

    public BodyBillboardTextures BillboardTextures { get; }

    // Physical properties
    public float Radius { get; set; }
    public float Mass { get; set; }
    public Vector3 Velocity { get; set; }
    public Vector3 BodyPosition { get; set; }
    public string BodyName { get; }

    public UnifiedCelestialMesh Mesh { get; }

    /// <summary>
    /// Atmospheric pressure for this body, in atmospheres (1.0 = Earth standard).
    /// Sampled at generation from the type's YAML range. Small bodies
    /// (asteroids, comets, moons) and dwarf planets are 0 — no atmosphere.
    /// Gas/ice giants have very high values. Used by wind power for placement
    /// gating and output scaling, and reserved for future atmosphere processing.
    /// </summary>
    public float Atmosphere { get; }

    /// <summary>
    /// The body this one orbits, or null for system-root bodies (stars / black holes
    /// orbiting only the barycenter). Wired by <see cref="SystemGenerator"/> after
    /// body construction. Walk the chain via
    /// <c>OrbitalBodyExtensions.FindParentStar</c> to find the dominant light source
    /// for solar power calculations.
    /// </summary>
    public IOrbitalBody? OrbitalParent { get; set; }

    // Orbital Band System - Properties
    public Godot.Collections.Array<OrbitBand> OrbitBands { get; }
    public OrbitConfiguration? OrbitConfig { get; }
    public Node3D SatellitesContainer { get; }
    public BuildingConstructionManager BuildingConstructionMgr { get; }
    public BodyEconomyManager EconomyMgr { get; }

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

    #region TransferEndpointRegistry

    /// <summary>
    /// Registers a transfer-station endpoint on this body. Called from
    /// <see cref="Constructables.Buildings.Behaviors.TransferStationBehavior.OnRegister"/>.
    /// Owner may be a Building (Resource) or a StationSatellite (Node3D).
    /// </summary>
    void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, GodotObject owner);

    /// <summary>
    /// Unregisters a transfer-station endpoint from this body.
    /// </summary>
    void UnregisterTransferEndpoint(string endpointId);

    /// <summary>
    /// Whether the given building id has a registered transfer-station endpoint on this body.
    /// </summary>
    bool HasTransferEndpoint(string endpointId);

    /// <summary>
    /// Returns the transfer-station definition for the given endpoint id, or null.
    /// </summary>
    TransferStationDefinition? GetTransferEndpointDef(string endpointId);

    /// <summary>
    /// Returns the transfer-station building associated with an endpoint id, or null.
    /// </summary>
    [Obsolete("Use GetTransferEndpointOwner instead")]
    Building? GetTransferEndpointBuilding(string endpointId);

    /// <summary>
    /// Returns the GodotObject owner (Building or StationSatellite) for an endpoint id, or null.
    /// </summary>
    GodotObject? GetTransferEndpointOwner(string endpointId);

    /// <summary>
    /// All registered transfer endpoint ids on this body.
    /// </summary>
    IReadOnlyList<string> GetAllTransferEndpoints();

    /// <summary>
    /// All registered endpoint ids whose backing building lives on the given continent.
    /// </summary>
    IReadOnlyList<string> GetTransferEndpointsOnContinent(int continentIndex);

    /// <summary>
    /// Sums the cargo capacity across every endpoint on a continent.
    /// </summary>
    float GetTotalTransferCapacityOnContinent(int continentIndex);

    #endregion
}
