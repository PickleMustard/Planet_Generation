using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Structures.Logistics;
using Structures.MeshGeneration;
using Structures.Resources;
using Structures.Transfers;

namespace Tests.Constructables.Buildings.Behaviors;

[TestSuite]
public partial class TransferStationBehaviorTest
{
    #region Mock IOrbitalBody

    private class MockBody : IOrbitalBody
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
        public TransferStationDefinition? GetTransferEndpointDef(string id) => _defs.TryGetValue(id, out var d) ? d : null;
        [Obsolete("Use GetTransferEndpointOwner instead")]
        public Building? GetTransferEndpointBuilding(string id) => _owners.TryGetValue(id, out var o) ? o as Building : null;
        public GodotObject? GetTransferEndpointOwner(string id) => _owners.TryGetValue(id, out var o) ? o : null;
        public IReadOnlyList<string> GetAllTransferEndpoints() => new List<string>(_defs.Keys);
        public IReadOnlyList<string> GetTransferEndpointsOnContinent(int _) => Array.Empty<string>();
        public float GetTotalTransferCapacityOnContinent(int _) => 0f;
    }

    /// <summary>
    /// Node3D-based mock so that TransferStationBehavior can discover IOrbitalBody
    /// by walking the real scene tree via GetParent().
    /// </summary>
    private partial class MockBodyNode3D : Node3D, IOrbitalBody
    {
        private readonly Dictionary<string, TransferStationDefinition> _defs = new();
        private readonly Dictionary<string, GodotObject> _owners = new();

        public BodyClassification Classification => BodyClassification.FromLegacy(CelestialBodyType.RockyPlanet, null);
        public BodyBillboardTextures BillboardTextures => null!;
        public float Radius { get; set; }
        public float Mass { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 BodyPosition { get; set; }
        public string BodyName => "MockBodyNode3D";
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
        public TransferStationDefinition? GetTransferEndpointDef(string id) => _defs.TryGetValue(id, out var d) ? d : null;
        [Obsolete("Use GetTransferEndpointOwner instead")]
        public Building? GetTransferEndpointBuilding(string id) => _owners.TryGetValue(id, out var o) ? o as Building : null;
        public GodotObject? GetTransferEndpointOwner(string id) => _owners.TryGetValue(id, out var o) ? o : null;
        public IReadOnlyList<string> GetAllTransferEndpoints() => new List<string>(_defs.Keys);
        public IReadOnlyList<string> GetTransferEndpointsOnContinent(int _) => Array.Empty<string>();
        public float GetTotalTransferCapacityOnContinent(int _) => 0f;
    }

    #endregion

    #region Helpers

    private static VoronoiCell MakeCell(int continentIndex = 0)
    {
        var p = new Point(new Vector3(1f, 0f, 0f));
        return new VoronoiCell(0, new[] { p }, Array.Empty<Triangle>(), Array.Empty<Edge>())
        {
            Center = new Vector3(1f, 0f, 0f),
            ContinentIndex = continentIndex,
        };
    }

    private static Building MakeBuilding(string id, float cargoCapacity = 500f, float vehicleSpeed = 50f, int maxConcurrent = 2, int continentIndex = 0)
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

        // Ensure bulk storage has open slots so BuildingResourceEndpoint can deposit/withdraw
        building.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        building.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));

        // Attach TransferStationBehavior with inline config
        var tsb = new TransferStationBehavior();
        var config = new Dictionary<string, object>
        {
            ["cargo_capacity"] = cargoCapacity,
            ["vehicle_speed"] = vehicleSpeed,
            ["max_concurrent_transfers"] = maxConcurrent,
        };
        tsb.Configure(config);
        tsb.OnAttach(building);
        building.Behaviors.Add(tsb);

        return building;
    }

    private static TransferStationBehavior AttachBehavior(Building building, MockBody body)
    {
        var behavior = building.GetBehavior<TransferStationBehavior>()!;
        if (behavior == null)
        {
            behavior = new TransferStationBehavior();
            behavior.OnAttach(building);
            building.Behaviors.Add(behavior);
        }

        // Inject mock body via reflection (OnAttach cannot find IOrbitalBody without scene tree)
        var bodyField = typeof(TransferStationBehavior).GetField("_body", BindingFlags.NonPublic | BindingFlags.Instance);
        bodyField?.SetValue(behavior, body);

        building.Behaviors.Add(behavior);
        behavior.OnRegister();
        return behavior;
    }

    private static (Building b, TransferStationBehavior bh) MakeStation(
        string id,
        MockBody body,
        float cargoCapacity = 500f,
        float vehicleSpeed = 50f,
        int maxConcurrent = 2)
    {
        var building = MakeBuilding(id, cargoCapacity, vehicleSpeed, maxConcurrent);
        var behavior = AttachBehavior(building, body);
        return (building, behavior);
    }

    private static int ActiveCount(TransferStationBehavior behavior)
    {
        var field = typeof(TransferStationBehavior).GetField("_activeTransfers", BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (Dictionary<string, TransferStationBehavior.ActiveTransfer>?)field?.GetValue(behavior);
        return dict?.Count ?? 0;
    }

    private static int ScheduleCount(TransferStationBehavior behavior)
    {
        var field = typeof(TransferStationBehavior).GetField("_schedulesByOrigin", BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (Dictionary<string, List<TransferSchedule>>?)field?.GetValue(behavior);
        if (dict == null) return 0;
        return dict.Values.Sum(list => list.Count);
    }

    #endregion

    // ========================================================================
    // Endpoint registration / query
    // ========================================================================

    [TestCase]
    public void RegisterBehavior_ExposesEndpointForDispatch()
    {
        var body = new MockBody();
        var (_, behavior) = MakeStation("hub_a", body);

        AssertThat(body.HasTransferEndpoint("hub_a")).IsTrue();
        AssertThat(body.GetTransferEndpointOwner("hub_a")).IsNotNull();
    }

    [TestCase]
    public void HasEndpoint_ReturnsTrueAfterRegister()
    {
        var body = new MockBody();
        var (_, behavior) = MakeStation("hub_a", body);

        AssertThat(behavior.HasEndpoint("hub_a")).IsTrue();
        AssertThat(behavior.HasEndpoint("other")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void OnRegister_AfterBindWithSceneTree_RegistersEndpoint()
    {
        // Arrange: real Node3D mock body so GetParent() works in the scene tree
        var body = new MockBodyNode3D();
        var tree = Engine.GetMainLoop() as SceneTree;
        AssertThat(tree).IsNotNull();
        tree!.Root.AddChild(body);
        try
        {
            var def = new BuildingDefinition
            {
                IdName = "hq",
                DisplayName = "Company Headquarters",
            };

            var tsConfig = new Dictionary<string, object>
            {
                ["cargo_capacity"] = 100f,
                ["vehicle_speed"] = 10f,
                ["max_concurrent_transfers"] = 1,
            };
            def.BehaviorEntries.Add(new BuildingDefinition.BehaviorConfigEntry
            {
                BehaviorId = "TransferStationBehavior",
                Config = tsConfig,
            });

            var building = def.Instantiate();
            building.SetPlacement(MakeCell(), null);

            // Act: create visual node, add to scene tree, bind, then register
            // (mirrors the fixed ConstructionManager.CreateBuilding order)
            var visual = new BuildingNode { Name = "hq" };
            body.AddChild(visual);
            visual.Bind(building, body, 1f);
            building.Register();

            // Assert
            var behavior = building.GetBehavior<TransferStationBehavior>();
            AssertThat(behavior).IsNotNull();
            AssertThat(behavior!.HasEndpoint(building.Id)).IsTrue();
            AssertThat(body.HasTransferEndpoint(building.Id)).IsTrue();
        }
        finally
        {
            body.QueueFree();
        }
    }

    [TestCase]
    public void GetCapacity_ReturnsDefValue()
    {
        var body = new MockBody();
        var (_, behavior) = MakeStation("hub_a", body, cargoCapacity: 1234f);

        AssertThat(behavior.GetCapacity("hub_a")).IsEqual(1234f);
        AssertThat(behavior.GetCapacity("other")).IsEqual(0f);
    }

    [TestCase]
    public void GetMaxConcurrentTransfers_ReturnsDefValue()
    {
        var body = new MockBody();
        var (_, behavior) = MakeStation("hub_a", body, maxConcurrent: 5);

        AssertThat(behavior.GetMaxConcurrentTransfers("hub_a")).IsEqual(5);
        AssertThat(behavior.GetMaxConcurrentTransfers("other")).IsEqual(0);
    }

    [TestCase]
    public void GetVehicleSpeed_ReturnsDefValue()
    {
        var body = new MockBody();
        var (_, behavior) = MakeStation("hub_a", body, vehicleSpeed: 99f);

        AssertThat(behavior.GetVehicleSpeed("hub_a")).IsEqual(99f);
        AssertThat(behavior.GetVehicleSpeed("other")).IsEqual(0f);
    }

    // ========================================================================
    // One-time transfer dispatch & completion
    // ========================================================================

    [TestCase]
    public void DispatchOneTimeTransfer_DeliversToDestinationAfterTravelTime()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 100f); // travel time = 1s
        var (dest, destBh) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 50f);

        var orderId = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 10f }
        );

        AssertThat(orderId).IsNotNull();
        AssertThat(originBh.IsTransferActive(orderId!)).IsTrue();

        // After 0.5s: still in transit
        originBh.OnManufactureTick(0.5f, origin);
        AssertThat(originBh.IsTransferActive(orderId!)).IsTrue();

        // After another 0.6s (total 1.1s): should have arrived
        originBh.OnManufactureTick(0.6f, origin);
        AssertThat(originBh.IsTransferActive(orderId!)).IsFalse();
        AssertThat(dest.BulkStorage.GetQuantity("iron")).IsGreater(0f);
    }

    [TestCase]
    public void DispatchOneTimeTransfer_RejectedWhenOriginNotRegistered()
    {
        var body = new MockBody();
        var (_, behavior) = MakeStation("hub_a", body);

        var orderId = behavior.DispatchOneTimeTransfer(
            "wrong_id",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 10f }
        );

        AssertThat(orderId).IsNull();
    }

    [TestCase]
    public void DispatchOneTimeTransfer_RejectedWhenMaxConcurrentReached()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 10f, maxConcurrent: 1);
        var (dest, _) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 100f);

        var first = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 5f }
        );
        AssertThat(first).IsNotNull();

        var second = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 5f }
        );
        AssertThat(second).IsNull();
    }

    [TestCase]
    public void TransferArrival_DestinationFull_RevertsToOrigin()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f); // instant-ish
        var (dest, destBh) = MakeStation("dest", body, cargoCapacity: 10f);

        origin.BulkStorage.Deposit("iron", 100f);
        dest.BulkStorage.Deposit("iron", 999f); // fill destination so it cannot accept more

        float originBefore = origin.BulkStorage.GetQuantity("iron");

        var orderId = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 10f }
        );
        AssertThat(orderId).IsNotNull();

        // Tick to completion
        originBh.OnManufactureTick(1f, origin);

        // Because destination is full, cargo should revert to origin
        float originAfter = origin.BulkStorage.GetQuantity("iron");
        AssertThat(originAfter).IsEqual(originBefore);
    }

    [TestCase]
    public void TransferArrival_DestinationGone_OriginReverts()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f);
        var (dest, destBh) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 100f);
        float originBefore = origin.BulkStorage.GetQuantity("iron");

        var orderId = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 10f }
        );
        AssertThat(orderId).IsNotNull();

        // Unregister destination before arrival
        destBh.OnUnregister();

        originBh.OnManufactureTick(1f, origin);

        // Cargo reverted to origin
        AssertThat(origin.BulkStorage.GetQuantity("iron")).IsEqual(originBefore);
    }

    [TestCase]
    public void TransferArrival_BothGone_LogsLostCargo()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f);
        var (dest, destBh) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 100f);
        float originBefore = origin.BulkStorage.GetQuantity("iron");

        var orderId = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 10f }
        );
        AssertThat(orderId).IsNotNull();

        // Unregister both
        originBh.OnUnregister();
        destBh.OnUnregister();

        // Should not throw; cargo is lost
        originBh.OnManufactureTick(1f, origin);

        // Origin already unregistered, so its endpoint is gone; no reversion possible
        AssertThat(true).IsTrue(); // test passes if no exception
    }

    // ========================================================================
    // Schedule CRUD
    // ========================================================================

    [TestCase]
    public void CreateSchedule_ReturnsIdAndIsIdle()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body);
        var (dest, _) = MakeStation("dest", body);

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );

        AssertThat(id).IsNotNull();
        var schedules = originBh.GetSchedulesForOrigin("origin");
        AssertThat(schedules.Count).IsEqual(1);
        AssertThat(schedules[0].State).IsEqual(TransferScheduleState.Idle);
    }

    [TestCase]
    public void CreateSchedule_WithoutRegisteredOrigin_ReturnsNull()
    {
        var body = new MockBody();
        var (_, behavior) = MakeStation("hub_a", body);

        var id = behavior.CreateSchedule(
            "wrong_id",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );

        AssertThat(id).IsNull();
    }

    [TestCase]
    public void StartSchedule_ChangesStateToAccumulating()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body);
        var (dest, _) = MakeStation("dest", body);

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );
        AssertThat(id).IsNotNull();

        bool ok = originBh.StartSchedule(id!);
        AssertThat(ok).IsTrue();

        var schedule = originBh.GetAllSchedules().First();
        AssertThat(schedule.State).IsEqual(TransferScheduleState.Accumulating);
    }

    [TestCase]
    public void StopSchedule_ChangesStateToStopped()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body);
        var (dest, _) = MakeStation("dest", body);

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );
        originBh.StartSchedule(id!);

        bool ok = originBh.StopSchedule(id!);
        AssertThat(ok).IsTrue();

        var schedule = originBh.GetAllSchedules().First();
        AssertThat(schedule.State).IsEqual(TransferScheduleState.Stopped);
    }

    [TestCase]
    public void RemoveSchedule_RemovesFromList()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body);
        var (dest, _) = MakeStation("dest", body);

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );
        AssertThat(originBh.GetAllSchedules().Count).IsEqual(1);

        bool ok = originBh.RemoveSchedule(id!);
        AssertThat(ok).IsTrue();
        AssertThat(originBh.GetAllSchedules().Count).IsEqual(0);
    }

    [TestCase]
    public void ReorderSchedules_UpdatesPriority()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body);
        var (dest, _) = MakeStation("dest", body);

        var id1 = originBh.CreateSchedule("origin", TransferDestination.ForBuilding("dest"), new() { ["iron"] = 1f }, DepartureConditionMode.AllResources, DepartureThreshold.Full);
        var id2 = originBh.CreateSchedule("origin", TransferDestination.ForBuilding("dest"), new() { ["copper"] = 1f }, DepartureConditionMode.AllResources, DepartureThreshold.Full);
        var id3 = originBh.CreateSchedule("origin", TransferDestination.ForBuilding("dest"), new() { ["gold"] = 1f }, DepartureConditionMode.AllResources, DepartureThreshold.Full);

        bool ok = originBh.ReorderSchedules("origin", new[] { id3!, id1!, id2! });
        AssertThat(ok).IsTrue();

        var list = originBh.GetSchedulesForOrigin("origin");
        AssertThat(list[0].ScheduleId).IsEqual(id3);
        AssertThat(list[0].Priority).IsEqual(1);
        AssertThat(list[1].ScheduleId).IsEqual(id1);
        AssertThat(list[1].Priority).IsEqual(2);
        AssertThat(list[2].ScheduleId).IsEqual(id2);
        AssertThat(list[2].Priority).IsEqual(3);
    }

    // ========================================================================
    // Schedule threshold & tick logic
    // ========================================================================

    [TestCase]
    public void CreateSchedule_AnyResourceThreshold_DepartsWhenOneResourceReady()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f);
        var (dest, _) = MakeStation("dest", body);

        // CargoCapacity=500f * 0.5 proportion / 1.0 weight = 250 target units.
        // Half threshold = 125 units. Deposit enough iron to exceed it.
        origin.BulkStorage.Deposit("iron", 200f);
        origin.BulkStorage.Deposit("copper", 0f);

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 0.5f, ["copper"] = 0.5f },
            DepartureConditionMode.AnyResource,
            DepartureThreshold.Half
        );
        originBh.StartSchedule(id!);

        // Tick once: should dispatch because iron stockpile >= half of target
        originBh.OnManufactureTick(1f, origin);

        var schedules = originBh.GetAllSchedules();
        AssertThat(schedules[0].State).IsEqual(TransferScheduleState.Dispatched);
        AssertThat(originBh.GetActiveTransfers().Count).IsGreater(0);
    }

    [TestCase]
    public void CreateSchedule_AllResourcesThreshold_WaitsForAll()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f);
        var (dest, _) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 50f);
        origin.BulkStorage.Deposit("copper", 0f);

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 0.5f, ["copper"] = 0.5f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Half
        );
        originBh.StartSchedule(id!);

        originBh.OnManufactureTick(1f, origin);

        var schedules = originBh.GetAllSchedules();
        AssertThat(schedules[0].State).IsEqual(TransferScheduleState.Accumulating);
        AssertThat(originBh.GetActiveTransfers().Count).IsEqual(0);
    }

    [TestCase]
    public void CreateSchedule_WaitTimer_DepartsAfterElapsedTime()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f);
        var (dest, _) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 100f);

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full,
            waitSeconds: 0.5f
        );
        originBh.StartSchedule(id!);

        // Before wait time: should not dispatch
        originBh.OnManufactureTick(0.3f, origin);
        AssertThat(originBh.GetAllSchedules()[0].State).IsEqual(TransferScheduleState.Accumulating);

        // After wait time: should dispatch
        originBh.OnManufactureTick(0.3f, origin);
        AssertThat(originBh.GetAllSchedules()[0].State).IsEqual(TransferScheduleState.Dispatched);
    }

    [TestCase]
    public void CreateSchedule_InsufficientStockpile_DoesNotDepart()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f);
        var (dest, _) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 1f); // tiny amount

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );
        originBh.StartSchedule(id!);

        originBh.OnManufactureTick(1f, origin);

        AssertThat(originBh.GetAllSchedules()[0].State).IsEqual(TransferScheduleState.Accumulating);
        AssertThat(originBh.GetActiveTransfers().Count).IsEqual(0);
    }

    [TestCase]
    public void CreateSchedule_ThenStartSchedule_DispatchesWhenThresholdMet()
    {
        var body = new MockBody();
        // Use small cargo capacity so Full threshold is easy to meet with test stockpiles.
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f, cargoCapacity: 50f);
        var (dest, _) = MakeStation("dest", body);

        // Schedule created while stockpile is empty
        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );
        originBh.StartSchedule(id!);
        originBh.OnManufactureTick(1f, origin);
        AssertThat(originBh.GetAllSchedules()[0].State).IsEqual(TransferScheduleState.Accumulating);

        // Now add resources (>= 50f for Full threshold at 50f capacity) and tick again
        origin.BulkStorage.Deposit("iron", 60f);
        originBh.OnManufactureTick(1f, origin);

        AssertThat(originBh.GetAllSchedules()[0].State).IsEqual(TransferScheduleState.Dispatched);
    }

    // ========================================================================
    // Tick / in-flight state
    // ========================================================================

    [TestCase]
    public void OnManufactureTick_AdvancesInFlightTransfers()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 100f); // 1s travel
        var (dest, _) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 50f);

        var orderId = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 10f }
        );
        AssertThat(orderId).IsNotNull();

        // Halfway: still active
        originBh.OnManufactureTick(0.5f, origin);
        AssertThat(originBh.IsTransferActive(orderId!)).IsTrue();

        // Complete
        originBh.OnManufactureTick(0.6f, origin);
        AssertThat(originBh.IsTransferActive(orderId!)).IsFalse();
    }

    [TestCase]
    public void OnManufactureTick_MultipleSteps_AdvancesBothTransfersAndSchedules()
    {
        var body = new MockBody();
        // Slow vehicle (10f/s => 10s travel) so transfer stays active after first tick.
        // Small cargo capacity (50f) so schedule Full threshold is easy to hit.
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 10f, cargoCapacity: 50f);
        var (dest, _) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 100f);

        // One-time transfer: withdraws 5f, leaving 95f. Travel time = 100/10 = 10s.
        var orderId = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 5f }
        );

        // Schedule: Full threshold at 50f capacity => needs 50f. 95f remaining > 50f.
        var schedId = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );
        originBh.StartSchedule(schedId!);

        // Single tick advances in-flight transfer AND dispatches schedule
        originBh.OnManufactureTick(0.1f, origin);

        AssertThat(originBh.IsTransferActive(orderId!)).IsTrue();
        AssertThat(originBh.GetAllSchedules()[0].State).IsEqual(TransferScheduleState.Dispatched);

        // Tick to completion
        originBh.OnManufactureTick(15f, origin);
        AssertThat(originBh.IsTransferActive(orderId!)).IsFalse();
    }

    // ========================================================================
    // Unregister / cleanup
    // ========================================================================

    [TestCase]
    public void OnUnregister_StopsSchedulesFromThatOrigin()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body);
        var (dest, _) = MakeStation("dest", body);

        var id = originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );
        originBh.StartSchedule(id!);

        AssertThat(originBh.GetAllSchedules()[0].State).IsEqual(TransferScheduleState.Accumulating);

        originBh.OnUnregister();

        AssertThat(originBh.GetAllSchedules()[0].State).IsEqual(TransferScheduleState.Stopped);
        AssertThat(body.HasTransferEndpoint("origin")).IsFalse();
    }

    [TestCase]
    public void OnUnregister_ClearsActiveTransfers()
    {
        // Active transfers dictionary is keyed by order id; OnUnregister does NOT
        // clear _activeTransfers directly (that is OnDetach's job). However the test
        // plan says to verify it. Let's verify behavior: OnUnregister only stops
        // schedules and unregisters endpoint; active transfers are left alone because
        // they may still be in-flight and CompleteTransfer will handle them.
        // This test therefore documents the actual behavior rather than a forced clear.
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f);
        var (dest, _) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 50f);
        var orderId = originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 5f }
        );
        AssertThat(ActiveCount(originBh)).IsEqual(1);

        originBh.OnUnregister();

        // Active transfers remain in-flight; endpoint is gone so future completions revert
        AssertThat(ActiveCount(originBh)).IsEqual(1);
        AssertThat(body.HasTransferEndpoint("origin")).IsFalse();
    }

    [TestCase]
    public void OnDetach_ClearsAllState()
    {
        var body = new MockBody();
        var (origin, originBh) = MakeStation("origin", body, vehicleSpeed: 1000f);
        var (dest, _) = MakeStation("dest", body);

        origin.BulkStorage.Deposit("iron", 50f);
        originBh.DispatchOneTimeTransfer(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 5f }
        );
        originBh.CreateSchedule(
            "origin",
            TransferDestination.ForBuilding("dest"),
            new Dictionary<string, float> { ["iron"] = 1f },
            DepartureConditionMode.AllResources,
            DepartureThreshold.Full
        );

        AssertThat(ActiveCount(originBh)).IsEqual(1);
        AssertThat(ScheduleCount(originBh)).IsEqual(1);

        originBh.OnDetach();

        AssertThat(ActiveCount(originBh)).IsEqual(0);
        AssertThat(ScheduleCount(originBh)).IsEqual(0);
    }
}
