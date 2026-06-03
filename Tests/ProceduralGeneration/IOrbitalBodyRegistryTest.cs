using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Buildings;
using Constructables.Buildings.Behaviors;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Resources;
using Structures.Transfers;

namespace Tests.ProceduralGeneration;

/// <summary>
/// Verifies the <see cref="IOrbitalBody"/> transfer endpoint registry contract.
/// Both <see cref="CelestialBody"/> and <see cref="SatelliteBody"/> implement the
/// same dictionary-backed registry; this suite exercises the contract semantics
/// via a lightweight mock so it can run as a pure unit test without Godot runtime.
/// </summary>
[TestSuite]
public class IOrbitalBodyRegistryTest
{
    private class MockRegistryBody : IOrbitalBody
    {
        private readonly Dictionary<string, TransferStationDefinition> _defs = new();
        private readonly Dictionary<string, Building> _buildings = new();
        private readonly Dictionary<string, GodotObject> _owners = new();

        public BodyClassification Classification => BodyClassification.FromType(OrbitalBodyType.RockyPlanet, null);
        public BodyBillboardTextures BillboardTextures => null!;
        public float Radius { get; set; }
        public float Mass { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 BodyPosition { get; set; }
        public string BodyName => "MockRegistryBody";
        public UnifiedCelestialMesh Mesh => null!;
        public Godot.Collections.Array<OrbitBand> OrbitBands => new();
        public OrbitConfiguration? OrbitConfig => null;
        public Node3D SatellitesContainer => null!;
        public BuildingConstructionManager BuildingConstructionMgr => null!;
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

        // Exact mirror of CelestialBody / SatelliteBody registry logic
        public void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, GodotObject owner)
        {
            if (string.IsNullOrEmpty(endpointId)) return;
            _defs[endpointId] = def;
            _owners[endpointId] = owner;
            if (owner is Building b)
                _buildings[endpointId] = b;
        }

        public void UnregisterTransferEndpoint(string endpointId)
        {
            if (string.IsNullOrEmpty(endpointId)) return;
            _defs.Remove(endpointId);
            _owners.Remove(endpointId);
            _buildings.Remove(endpointId);
        }

        public bool HasTransferEndpoint(string endpointId) => _defs.ContainsKey(endpointId);
        public TransferStationDefinition? GetTransferEndpointDef(string endpointId) => _defs.TryGetValue(endpointId, out var d) ? d : null;
        [Obsolete("Use GetTransferEndpointOwner instead")]
        public Building? GetTransferEndpointBuilding(string endpointId) => _buildings.TryGetValue(endpointId, out var b) ? b : null;
        public GodotObject? GetTransferEndpointOwner(string endpointId) => _owners.TryGetValue(endpointId, out var o) ? o : null;
        public IReadOnlyList<string> GetAllTransferEndpoints() => new List<string>(_defs.Keys);
        public IReadOnlyList<string> GetTransferEndpointsOnContinent(int continentIndex)
        {
            var list = new List<string>();
            foreach (var kvp in _buildings)
            {
                if (kvp.Value.PrimaryCell?.ContinentIndex == continentIndex)
                    list.Add(kvp.Key);
            }
            return list;
        }
        public float GetTotalTransferCapacityOnContinent(int continentIndex)
        {
            if (continentIndex < 0) return 0f;
            float total = 0f;
            foreach (var id in GetTransferEndpointsOnContinent(continentIndex))
            {
                var def = GetTransferEndpointDef(id);
                if (def != null) total += def.CargoCapacity;
            }
            return total;
        }
    }

    private static VoronoiCell MakeCell(int continentIndex = 0)
    {
        var p = new Point(new Vector3(1f, 0f, 0f));
        return new VoronoiCell(0, new[] { p }, Array.Empty<Triangle>(), Array.Empty<Edge>())
        {
            Center = new Vector3(1f, 0f, 0f),
            ContinentIndex = continentIndex,
        };
    }

    private static Building MakeBuilding(string id, int continentIndex = 0)
    {
        var def = new BuildingDefinition
        {
            IdName = id,
            DisplayName = $"Test {id}",
        };
        var building = new Building();
        building.ApplyDefinition(def);
        building.Id = id;
        building.SetPlacement(MakeCell(continentIndex), null);
        return building;
    }

    [TestCase]
    public void RegisterEndpoint_AddsToAllEndpointsList()
    {
        var body = new MockRegistryBody();
        var building = MakeBuilding("hub_a");
        var tsDef = new TransferStationDefinition { CargoCapacity = 100f };

        body.RegisterTransferEndpoint("hub_a", tsDef, building);

        var all = body.GetAllTransferEndpoints();
        AssertThat(all.Count).IsEqual(1);
        AssertThat(all[0]).IsEqual("hub_a");
    }

    [TestCase]
    public void RegisterEndpoint_SetsDefinitionAndBuildingRefs()
    {
        var body = new MockRegistryBody();
        var building = MakeBuilding("hub_b");
        var tsDef = new TransferStationDefinition { CargoCapacity = 250f };

        body.RegisterTransferEndpoint("hub_b", tsDef, building);

        AssertThat(body.GetTransferEndpointDef("hub_b")).IsNotNull();
        AssertThat(body.GetTransferEndpointDef("hub_b")!.CargoCapacity).IsEqual(250f);
        AssertThat(body.GetTransferEndpointOwner("hub_b")).IsEqual(building);
    }

    [TestCase]
    public void UnregisterEndpoint_RemovesFromAllLists()
    {
        var body = new MockRegistryBody();
        var building = MakeBuilding("hub_c");
        var tsDef = new TransferStationDefinition { CargoCapacity = 100f };

        body.RegisterTransferEndpoint("hub_c", tsDef, building);
        AssertThat(body.HasTransferEndpoint("hub_c")).IsTrue();

        body.UnregisterTransferEndpoint("hub_c");
        AssertThat(body.HasTransferEndpoint("hub_c")).IsFalse();
        AssertThat(body.GetTransferEndpointDef("hub_c")).IsNull();
        AssertThat(body.GetTransferEndpointOwner("hub_c")).IsNull();
        AssertThat(body.GetAllTransferEndpoints().Count).IsEqual(0);
    }

    [TestCase]
    public void HasTransferEndpoint_KnownId_ReturnsTrue()
    {
        var body = new MockRegistryBody();
        body.RegisterTransferEndpoint("x", new TransferStationDefinition(), MakeBuilding("x"));

        AssertThat(body.HasTransferEndpoint("x")).IsTrue();
        AssertThat(body.HasTransferEndpoint("y")).IsFalse();
    }

    [TestCase]
    public void GetTransferEndpointsOnContinent_FiltersByContinentIndex()
    {
        var body = new MockRegistryBody();
        body.RegisterTransferEndpoint("a", new TransferStationDefinition(), MakeBuilding("a", continentIndex: 0));
        body.RegisterTransferEndpoint("b", new TransferStationDefinition(), MakeBuilding("b", continentIndex: 1));
        body.RegisterTransferEndpoint("c", new TransferStationDefinition(), MakeBuilding("c", continentIndex: 0));

        var c0 = body.GetTransferEndpointsOnContinent(0);
        var c1 = body.GetTransferEndpointsOnContinent(1);
        var c2 = body.GetTransferEndpointsOnContinent(2);

        AssertThat(c0.Count).IsEqual(2);
        AssertThat(c0.Contains("a")).IsTrue();
        AssertThat(c0.Contains("c")).IsTrue();
        AssertThat(c1.Count).IsEqual(1);
        AssertThat(c1[0]).IsEqual("b");
        AssertThat(c2.Count).IsEqual(0);
    }

    [TestCase]
    public void GetTotalTransferCapacityOnContinent_SumsDefinitions()
    {
        var body = new MockRegistryBody();
        body.RegisterTransferEndpoint("a", new TransferStationDefinition { CargoCapacity = 100f }, MakeBuilding("a", 0));
        body.RegisterTransferEndpoint("b", new TransferStationDefinition { CargoCapacity = 200f }, MakeBuilding("b", 0));
        body.RegisterTransferEndpoint("c", new TransferStationDefinition { CargoCapacity = 500f }, MakeBuilding("c", 1));

        AssertThat(body.GetTotalTransferCapacityOnContinent(0)).IsEqual(300f);
        AssertThat(body.GetTotalTransferCapacityOnContinent(1)).IsEqual(500f);
        AssertThat(body.GetTotalTransferCapacityOnContinent(2)).IsEqual(0f);
    }

    [TestCase]
    public void UnregisterEndpoint_UpdatesContinentCapacity()
    {
        var body = new MockRegistryBody();
        body.RegisterTransferEndpoint("a", new TransferStationDefinition { CargoCapacity = 100f }, MakeBuilding("a", 0));
        body.RegisterTransferEndpoint("b", new TransferStationDefinition { CargoCapacity = 200f }, MakeBuilding("b", 0));

        AssertThat(body.GetTotalTransferCapacityOnContinent(0)).IsEqual(300f);

        body.UnregisterTransferEndpoint("a");
        AssertThat(body.GetTotalTransferCapacityOnContinent(0)).IsEqual(200f);
    }
}
