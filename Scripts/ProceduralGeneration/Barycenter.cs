using Constructables;
using Godot;
using Godot.Collections;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.GameState;
using Structures.Transfers;
using System;
using System.Collections.Generic;
using System.Collections.Generic;

public partial class Barycenter : Node3D, IOrbitalBody
{
    private partial class OrbitalBody : Resource
    {
        public Vector3 position { get; set; }
        public Vector3 velocity { get; set; }
        public float weight { get; set; }
    }

    private OrbitalBody transform = new OrbitalBody();
    private Array<OrbitalBody> entries;
    private int totalBodies;

    [Export]
    public string SystemName { get; set; } = "";

    [Export]
    public string SectorId { get; set; } = "";

    public Vector3 Velocity
    {
        get => transform.velocity;
        set => transform.velocity = value;
    }
    public float Radius
    {
        get => 1f;
        set => throw new System.NotImplementedException();
    }
    public float Mass
    {
        get => transform.weight;
        set => transform.weight = value;
    }
    public Vector3 BodyPosition
    {
        get => transform.position;
        set => transform.position = value;
    }
    public string BodyName
    {
        get => "Barycenter";
    }

    public Array<OrbitBand> OrbitBands => throw new System.NotImplementedException();

    public OrbitConfiguration? OrbitConfig => throw new System.NotImplementedException();

    public Node3D SatellitesContainer => throw new System.NotImplementedException();

    public BodyClassification Classification => throw new System.NotImplementedException();
    public BodyBillboardTextures BillboardTextures => throw new System.NotImplementedException();
    public UnifiedCelestialMesh Mesh => throw new System.NotImplementedException();

    /// <summary>
    /// Barycenters have no atmosphere — they are an abstract gravitational point.
    /// </summary>
    public float Atmosphere => 0f;

    /// <summary>
    /// Barycenters are system-root; never have an orbital parent.
    /// </summary>
    public IOrbitalBody? OrbitalParent { get => null; set { /* no-op */ } }

    public bool UsesBandPlacement => throw new System.NotImplementedException();

    public BuildingConstructionManager BuildingConstructionMgr =>
        throw new System.NotImplementedException();

    public BodyEconomyManager EconomyMgr => throw new System.NotImplementedException();

    public Barycenter(Vector3 position, Vector3 velocity, float weight)
    {
        transform.position = position;
        transform.velocity = velocity;
        transform.weight = weight;
        this.entries = new Array<OrbitalBody>();
        this.totalBodies = 0;
    }

    public override void _Ready()
    {
        Name = "Barycenter";
        AddToGroup("Barycenter");
    }

    public void RegisterBody()
    {
        totalBodies++;
    }

    public void AddEntry(Vector3 position, Vector3 velocity, float weight)
    {
        OrbitalBody entry = new OrbitalBody();
        entry.position = position;
        entry.velocity = velocity;
        entry.weight = weight;
        entries.Add(entry);
        if (entries.Count == totalBodies)
        {
            Vector3 totalPosition = Vector3.Zero;
            Vector3 totalVelocity = Vector3.Zero;
            float totalWeight = 0f;
            foreach (var e in entries)
            {
                totalPosition += e.position * e.weight;
                totalVelocity += e.velocity * e.weight;
                totalWeight += e.weight;
            }
            position = totalPosition / totalWeight;
            velocity = totalVelocity / totalWeight;
            weight = totalWeight;
            entries.Clear();
        }
    }

    public void InitializeOrbitSystem()
    {
        throw new System.NotImplementedException();
    }

    public int GetBandCount()
    {
        throw new System.NotImplementedException();
    }

    public bool CanAddToBand(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public int GetBandSatelliteCount(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public void IncrementBandCount(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public void DecrementBandCount(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public OrbitalParameters GetOrbitalParametersForBand(int bandIndex, float startingAngle)
    {
        throw new System.NotImplementedException();
    }

    public OrbitalParameters GetOrbitalParametersAtRadius(float radius, float startingAngle)
    {
        throw new System.NotImplementedException();
    }

    public int GetClosestBandForApproach(float approachSpeed)
    {
        throw new System.NotImplementedException();
    }

    public float GetOrbitBandRadius(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    public float GetOrbitalSpeedForBand(int bandIndex)
    {
        throw new System.NotImplementedException();
    }

    #region TransferEndpointRegistry (stubs — barycenters never have surface buildings)

    public void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, GodotObject owner) { }
    public void UnregisterTransferEndpoint(string endpointId) { }
    public bool HasTransferEndpoint(string endpointId) => false;
    public TransferStationDefinition? GetTransferEndpointDef(string endpointId) => null;
    [Obsolete("Use GetTransferEndpointOwner instead")]
    public Building? GetTransferEndpointBuilding(string endpointId) => null;
    public GodotObject? GetTransferEndpointOwner(string endpointId) => null;
    public IReadOnlyList<string> GetAllTransferEndpoints() => System.Array.Empty<string>();
    public IReadOnlyList<string> GetTransferEndpointsOnContinent(int continentIndex) => System.Array.Empty<string>();
    public float GetTotalTransferCapacityOnContinent(int continentIndex) => 0f;

    #endregion
}
