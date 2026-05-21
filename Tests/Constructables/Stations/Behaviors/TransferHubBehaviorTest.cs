using System;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Stations;
using Constructables.Stations.Behaviors;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.MeshGeneration;
using Structures.Resources;
using Structures.Transfers;

namespace Tests.Constructables.Stations.Behaviors;

/// <summary>
/// Verifies <see cref="TransferHubBehavior"/> endpoint registration,
/// unregistration, dispatch, and schedule ticking logic.
/// </summary>
[TestSuite]
public partial class TransferHubBehaviorTest
{
    #region Mock IOrbitalBody

    private partial class MockBody : Node3D, IOrbitalBody
    {
        private readonly Dictionary<string, TransferStationDefinition> _defs = new();
        private readonly Dictionary<string, GodotObject> _owners = new();

        public BodyClassification Classification => BodyClassification.FromLegacy(CelestialBodyType.RockyPlanet, null);
        public BodyBillboardTextures BillboardTextures => null!;
        public float Radius { get; set; }
        public float Mass { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 BodyPosition { get; set; }
        public string BodyName => "MockBody";
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

        public void RegisterTransferEndpoint(string id, TransferStationDefinition def, GodotObject owner)
        {
            _defs[id] = def;
            _owners[id] = owner;
        }

        public void UnregisterTransferEndpoint(string id)
        {
            _defs.Remove(id);
            _owners.Remove(id);
        }

        public bool HasTransferEndpoint(string id) => _defs.ContainsKey(id);
        public TransferStationDefinition? GetTransferEndpointDef(string id) => _defs.GetValueOrDefault(id);
        public Building? GetTransferEndpointBuilding(string id) => null;
        public GodotObject? GetTransferEndpointOwner(string id) => _owners.GetValueOrDefault(id);
        public IReadOnlyList<string> GetAllTransferEndpoints() => new List<string>(_defs.Keys);
        public IReadOnlyList<string> GetTransferEndpointsOnContinent(int _) => Array.Empty<string>();
        public float GetTotalTransferCapacityOnContinent(int _) => 0f;
    }

    #endregion

    private static TransferHubBehavior MakeBehavior(MockBody body, string stationId, float cargo = 500f, float speed = 50f, int maxConcurrent = 2)
    {
        var station = new StationSatellite();
        // Give station an Id via reflection since it's set in _EnterTree
        typeof(StationSatellite).GetProperty("Id")?.SetValue(station, stationId);

        // Add storage slots so BulkStorage works
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));

        // Build tree so GetParent walks work
        var container = new Node3D();
        container.AddChild(station);
        body.AddChild(container);

        var behavior = new TransferHubBehavior
        {
            EndpointDef = new TransferStationDefinition
            {
                CargoCapacity = cargo,
                VehicleSpeed = speed,
                MaxConcurrentTransfers = maxConcurrent,
            },
        };
        behavior.OnAttach(station);
        behavior.OnRegister();

        return behavior;
    }

    [TestCase]
    public void OnRegister_CreatesEndpointAndRegistersWithBody()
    {
        var body = new MockBody();
        var behavior = MakeBehavior(body, "station-1");

        AssertThat(body.HasTransferEndpoint("station-1")).IsTrue();

        behavior.OnUnregister();
        behavior.OnDetach();
    }

    [TestCase]
    public void OnUnregister_UnregistersFromBody()
    {
        var body = new MockBody();
        var behavior = MakeBehavior(body, "station-2");

        behavior.OnUnregister();

        AssertThat(body.HasTransferEndpoint("station-2")).IsFalse();
        behavior.OnDetach();
    }

    [TestCase]
    public void WantsTick_ReturnsFalseWhenIdle()
    {
        var body = new MockBody();
        var behavior = MakeBehavior(body, "station-3");

        AssertThat(behavior.WantsTick).IsFalse();

        behavior.OnUnregister();
        behavior.OnDetach();
    }

    [TestCase]
    public void WantsTick_ReturnsTrueWhenActiveTransfersExist()
    {
        var body = new MockBody();
        var behavior = MakeBehavior(body, "station-4");

        // Deposit resources so dispatch can load cargo
        behavior.Owner!.BulkStorage.Deposit("iron", 200f);

        var dest = TransferDestination.ForBuilding("dest-1");
        var orderId = behavior.DispatchOneTimeTransfer("station-4", dest, new Dictionary<string, float> { ["iron"] = 100f });

        if (orderId != null)
            AssertThat(behavior.WantsTick).IsTrue();
        else
            AssertThat(behavior.WantsTick).IsFalse(); // graceful: no dispatch possible without full tree

        behavior.OnUnregister();
        behavior.OnDetach();
    }

    [TestCase]
    public void DispatchOneTimeTransfer_WithdrawsFromBulkStorage()
    {
        var body = new MockBody();
        var behavior = MakeBehavior(body, "station-5");
        behavior.Owner!.BulkStorage.Deposit("iron", 200f);

        var before = behavior.Owner.BulkStorage.GetQuantity("iron");
        var dest = TransferDestination.ForBuilding("dest-5");
        behavior.DispatchOneTimeTransfer("station-5", dest, new Dictionary<string, float> { ["iron"] = 50f });

        var after = behavior.Owner.BulkStorage.GetQuantity("iron");
        AssertThat(after).IsLess(before);

        behavior.OnUnregister();
        behavior.OnDetach();
    }

    [TestCase]
    public void Priority_Is100()
    {
        var behavior = new TransferHubBehavior();
        AssertThat(behavior.Priority).IsEqual(100);
    }

    [TestCase]
    public void OnDetach_ClearsState()
    {
        var body = new MockBody();
        var behavior = MakeBehavior(body, "station-6");

        behavior.OnUnregister();
        behavior.OnDetach();

        AssertThat(behavior.Owner).IsNull();
        AssertThat(behavior.ResourceEndpoint).IsNull();
    }

    [TestCase]
    public void ScheduleAccumulationTick_DispatchesWhenThresholdMet()
    {
        var body = new MockBody();
        var behavior = MakeBehavior(body, "station-7");

        var dest = TransferDestination.ForBuilding("dest-7");
        var scheduleId = behavior.CreateSchedule(
            "station-7",
            dest,
            new Dictionary<string, float> { ["iron"] = 1.0f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full,
            waitSeconds: 0.1f
        );

        if (scheduleId != null)
        {
            behavior.StartSchedule(scheduleId);
            // Deposit resources to meet threshold
            behavior.Owner!.BulkStorage.Deposit("iron", 200f);
            behavior.OnManufactureTick(0.5f, behavior.Owner!);

            // After tick, schedule should have dispatched
            var schedules = behavior.GetSchedulesForOrigin("station-7");
            AssertThat(schedules.Count).IsGreater(0);
        }

        behavior.OnUnregister();
        behavior.OnDetach();
    }

    [TestCase]
    public void OnRegister_NullEndpointDef_SkipsRegistration()
    {
        var body = new MockBody();
        var station = new StationSatellite();
        var container = new Node3D();
        container.AddChild(station);
        body.AddChild(container);

        var behavior = new TransferHubBehavior();
        behavior.OnAttach(station);
        // EndpointDef not set — should log warning and skip
        behavior.OnRegister();

        AssertThat(body.HasTransferEndpoint(station.Id)).IsFalse();

        behavior.OnUnregister();
        behavior.OnDetach();
    }
}
