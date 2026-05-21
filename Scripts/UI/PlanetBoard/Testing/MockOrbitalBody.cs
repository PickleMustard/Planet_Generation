using System;
using System.Collections.Generic;
using Constructables;
using Godot;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Transfers;

namespace UI.PlanetBoard.Testing;

public sealed partial class MockOrbitalBody : Node, IOrbitalBody
{
    private readonly Dictionary<string, TransferStationDefinition> _epDefs = new();
    private readonly Dictionary<string, GodotObject> _epOwners = new();
    private MockBuildingConstructionManager _mgr = null!;

    public override void _Ready()
    {
        _mgr = new MockBuildingConstructionManager { Name = "MockBuildingConstructionMgr" };
        AddChild(_mgr);
    }

    public BodyClassification Classification => BodyClassification.FromLegacy(CelestialBodyType.RockyPlanet, null);
    public BodyBillboardTextures BillboardTextures => null!;
    public float Radius { get; set; } = 1000f;
    public float Mass { get; set; } = 1f;
    public Vector3 Velocity { get; set; }
    public Vector3 BodyPosition { get; set; }
    public string BodyName => "Mock Planet";
    public UnifiedCelestialMesh Mesh => null!;
    public Godot.Collections.Array<OrbitBand> OrbitBands => new();
    public OrbitConfiguration? OrbitConfig => null;
    public Node3D SatellitesContainer => null!;
    public BuildingConstructionManager BuildingConstructionMgr => _mgr;
    public BodyEconomyManager EconomyMgr => null!;
    public bool UsesBandPlacement => false;
    public float Atmosphere => 1.0f;
    public IOrbitalBody? OrbitalParent { get; set; }

    public void InitializeOrbitSystem() { }
    public int GetBandCount() => 0;
    public bool CanAddToBand(int _) => false;
    public int GetBandSatelliteCount(int _) => 0;
    public void IncrementBandCount(int _) { }
    public void DecrementBandCount(int _) { }
    public OrbitalParameters GetOrbitalParametersForBand(int _, float __) => new(0f, 0f, 0f, Vector3.Zero, Vector3.Zero, 0f, 0);
    public OrbitalParameters GetOrbitalParametersAtRadius(float _, float __) => new(0f, 0f, 0f, Vector3.Zero, Vector3.Zero, 0f, 0);
    public int GetClosestBandForApproach(float _) => 0;
    public float GetOrbitBandRadius(int _) => 0f;
    public float GetOrbitalSpeedForBand(int _) => 0f;

    public void RegisterTransferEndpoint(string id, TransferStationDefinition def, GodotObject owner)
    {
        _epDefs[id] = def;
        _epOwners[id] = owner;
    }

    public void UnregisterTransferEndpoint(string id)
    {
        _epDefs.Remove(id);
        _epOwners.Remove(id);
    }

    public bool HasTransferEndpoint(string id) => _epDefs.ContainsKey(id);
    public TransferStationDefinition? GetTransferEndpointDef(string id) => _epDefs.TryGetValue(id, out var d) ? d : null;
    [Obsolete("Use GetTransferEndpointOwner instead")]
    public Building? GetTransferEndpointBuilding(string id) => _epOwners.TryGetValue(id, out var o) ? o as Building : null;
    public GodotObject? GetTransferEndpointOwner(string id) => _epOwners.TryGetValue(id, out var o) ? o : null;
    public IReadOnlyList<string> GetAllTransferEndpoints() => new List<string>(_epDefs.Keys);
    public IReadOnlyList<string> GetTransferEndpointsOnContinent(int _) => Array.Empty<string>();
    public float GetTotalTransferCapacityOnContinent(int _) => 0f;

    public void AddBuilding(Building b) => _mgr.AddTestBuilding(b);

    public void ClearBuildings()
    {
        _mgr.ClearTestBuildings();
        _epDefs.Clear();
        _epOwners.Clear();
    }
}
