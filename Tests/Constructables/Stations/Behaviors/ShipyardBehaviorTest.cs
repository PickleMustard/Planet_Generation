using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Stations;
using Constructables.Stations.Behaviors;
using Constructables.Tick;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.PlanetGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using Structures.Transfers;

namespace Tests.Constructables.Stations.Behaviors;

/// <summary>
/// Verifies <see cref="ShipyardBehavior"/> creates a ShipBuildQueue, owns each build's construction
/// state, secures resources all-or-nothing from the station's BulkStorage, advances work per tick,
/// and integrates with <see cref="ManufactureTickEngine"/> for wake-sleep registration.
/// </summary>
[TestSuite]
public partial class ShipyardBehaviorTest
{
    // ========================================================================
    // Registration / lifecycle
    // ========================================================================

    [TestCase]
    public void OnRegister_CreatesShipBuildQueueWithCorrectMaxParallelBuilds()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 3 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        AssertThat(shipyard.MaxParallelBuilds).IsEqual(3);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void Configure_ReadsMaxParallelAndWorkPerTick()
    {
        var shipyard = new ShipyardBehavior();
        shipyard.Configure(new Dictionary<string, object>
        {
            { "max_parallel_ship_builds", "3" },
            { "work_per_tick", "2.5" },
        });

        AssertThat(shipyard.MaxParallelShipBuilds).IsEqual(3);
        AssertThat(shipyard.WorkPerTick).IsEqual(2.5f);
    }

    [TestCase]
    public void WantsTick_ReturnsFalseWhenQueueEmpty()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        AssertThat(shipyard.WantsTick).IsFalse();

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void WantsTick_ReturnsTrueWhenQueuedShipCanPromote()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = MakeShip("WantsTick");
        shipyard.EnqueueShipConstruction(ship);

        // Queued ship + free slot -> wants a tick to promote it.
        AssertThat(shipyard.WantsTick).IsTrue();

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void WantsTick_ReturnsFalseWhenAllActiveBlockedOnResources()
    {
        var station = new StationSatellite();
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        // Ship needs a resource the station does not have.
        var ship = MakeShip("Blocked", work: 100f, res: new() { { "iron_ore", 50 } });
        shipyard.EnqueueShipConstruction(ship);

        // Promote it into the only slot; it cannot secure resources.
        shipyard.OnManufactureTick(0.016f, station);

        // Slot full, build blocked on missing resources, nothing promotable -> sleep.
        AssertThat(shipyard.WantsTick).IsFalse();

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void OnManufactureTick_NoBuildsDoesNotThrow()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        shipyard.OnManufactureTick(0.016f, station);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void Priority_Is200()
    {
        var shipyard = new ShipyardBehavior();
        AssertThat(shipyard.Priority).IsEqual(200);
    }

    [TestCase]
    public void OnUnregister_ClearsQueue()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        shipyard.OnUnregister();

        AssertThat(shipyard.ActiveShipBuildCount).IsEqual(0);
        AssertThat(shipyard.QueuedShipBuildCount).IsEqual(0);

        shipyard.OnDetach();
    }

    [TestCase]
    public void GetShipBuildQueue_ReturnsEmptyListBeforeRegister()
    {
        var shipyard = new ShipyardBehavior();
        var queue = shipyard.GetShipBuildQueue();
        AssertThat(queue.Count).IsEqual(0);
    }

    [TestCase]
    public void GetActiveBuilds_ReturnsEmptyListBeforeRegister()
    {
        var shipyard = new ShipyardBehavior();
        var active = shipyard.GetActiveBuilds();
        AssertThat(active.Count).IsEqual(0);
    }

    // ========================================================================
    // CreateAndEnqueueShip argument validation
    // ========================================================================

    [TestCase]
    public void CreateAndEnqueueShip_ThrowsWhenNoOwner()
    {
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        // OnAttach never called — _owner is null

        AssertThrown(() =>
            shipyard.CreateAndEnqueueShip(
                new MockOrbitalBody(),
                0,
                new ShipDefinition { Name = "TestShip", WorkRequired = 5f }
            )
        ).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public void CreateAndEnqueueShip_ThrowsWhenNullDefinition()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        AssertThrown(() =>
            shipyard.CreateAndEnqueueShip(new MockOrbitalBody(), 0, null!)
        ).IsInstanceOf<ArgumentNullException>();

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void CreateAndEnqueueShip_ThrowsWhenNullTargetBody()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var definition = new ShipDefinition { Name = "TestShip", WorkRequired = 5f };
        AssertThrown(() =>
            shipyard.CreateAndEnqueueShip(null!, 0, definition)
        ).IsInstanceOf<ArgumentNullException>();

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    // ========================================================================
    // EnqueueShipConstruction — wake-sleep + bookkeeping
    // ========================================================================

    [TestCase]
    public void EnqueueShipConstruction_RegistersStationWithTickEngine()
    {
        ManufactureTickEngine.Instance?.Stop();

        var engine = ManufactureTickEngine.CreateForTesting();
        try
        {
            var station = new StationSatellite();
            var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
            shipyard.OnAttach(station);
            shipyard.OnRegister();

            DrainEngineOps(engine);
            int countBefore = engine.TickableCount;

            var ship = MakeShip("RegisterShip");
            shipyard.EnqueueShipConstruction(ship);

            DrainEngineOps(engine);

            AssertThat(engine.TickableCount).IsEqual(countBefore + 1);

            shipyard.OnUnregister();
            shipyard.OnDetach();
        }
        finally
        {
            engine.Stop();
        }
    }

    [TestCase]
    public void EnqueueShipConstruction_SetsConstructingStationOnShip()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = MakeShip("ConstructingStationShip");
        shipyard.EnqueueShipConstruction(ship);

        AssertThat(ship.ConstructingStation).IsSame(station);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void EnqueueShipConstruction_RefusesShipWithoutDefinition()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = new LogisticsUnit { Name = "NoDef" };
        shipyard.EnqueueShipConstruction(ship);

        // No ShipDefinition -> not enqueued.
        AssertThat(shipyard.ActiveShipBuildCount + shipyard.QueuedShipBuildCount).IsEqual(0);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void EnqueueAndTick_PromotesUpToMaxParallelBuilds()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 2 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        // No required resources -> they begin immediately once promoted. High work so none complete.
        shipyard.EnqueueShipConstruction(MakeShip("Ship1"));
        shipyard.EnqueueShipConstruction(MakeShip("Ship2"));
        shipyard.EnqueueShipConstruction(MakeShip("Ship3"));

        shipyard.OnManufactureTick(0.016f, station);

        // With MaxParallelBuilds=2, two go active, one stays queued.
        AssertThat(shipyard.ActiveShipBuildCount).IsEqual(2);
        AssertThat(shipyard.QueuedShipBuildCount).IsEqual(1);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    // ========================================================================
    // Resource securing (the core fix)
    // ========================================================================

    [TestCase]
    public void OnManufactureTick_SecuresResourcesFromStorageAndProgresses()
    {
        var station = new StationSatellite();
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        station.BulkStorage.Deposit("iron_ore", 50);

        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1, WorkPerTick = 5f };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = MakeShip("SecureShip", work: 100f, res: new() { { "iron_ore", 50 } });
        shipyard.EnqueueShipConstruction(ship);

        // Before tick: nothing secured, no progress.
        AssertThat(shipyard.GetSecuredResources(ship).Count).IsEqual(0);

        shipyard.OnManufactureTick(0.016f, station);

        // Resources withdrawn from storage into the build; work advanced by WorkPerTick.
        AssertThat(station.BulkStorage.GetQuantity("iron_ore")).IsEqual(0);
        AssertThat(shipyard.GetSecuredResources(ship)["iron_ore"]).IsEqual(50);
        AssertThat(shipyard.GetBuildStatus(ship)).IsEqual("InProgress");
        AssertThat(shipyard.GetBuildProgress(ship)).IsGreater(0f);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void OnManufactureTick_DoesNotSecureWhenResourcesMissing()
    {
        var station = new StationSatellite();
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1, WorkPerTick = 5f };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = MakeShip("MissingResShip", work: 100f, res: new() { { "iron_ore", 50 } });
        shipyard.EnqueueShipConstruction(ship);

        shipyard.OnManufactureTick(0.016f, station);

        // Nothing in storage -> nothing secured, no progress, build is blocked.
        AssertThat(shipyard.GetSecuredResources(ship).Count).IsEqual(0);
        AssertThat(shipyard.GetBuildProgress(ship)).IsEqual(0f);
        AssertThat(shipyard.GetBuildStatus(ship)).IsEqual("Blocked");

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void OnManufactureTick_CompletesBuildWhenWorkReached()
    {
        var station = new StationSatellite();
        // WorkPerTick equals WorkRequired -> completes in a single tick.
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1, WorkPerTick = 10f };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        // No required resources -> begins immediately on promotion.
        var ship = MakeShip("InstantShip", work: 10f);
        shipyard.EnqueueShipConstruction(ship);

        shipyard.OnManufactureTick(0.016f, station);

        // Build completed and removed from active builds.
        AssertThat(shipyard.ActiveShipBuildCount).IsEqual(0);
        AssertThat(shipyard.QueuedShipBuildCount).IsEqual(0);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    // ========================================================================
    // CancelShipConstruction — refund of secured resources
    // ========================================================================

    [TestCase]
    public void CancelShipConstruction_RemovesFromQueue()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = MakeShip("CancelMe");
        shipyard.EnqueueShipConstruction(ship);

        AssertThat(shipyard.ActiveShipBuildCount + shipyard.QueuedShipBuildCount).IsEqual(1);

        shipyard.CancelShipConstruction(ship);

        AssertThat(shipyard.ActiveShipBuildCount + shipyard.QueuedShipBuildCount).IsEqual(0);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void CancelShipConstruction_RefundsSecuredResources()
    {
        var station = new StationSatellite();
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = MakeShip("RefundShip", work: 10f, res: new() { { "iron_ore", 100 } });
        shipyard.EnqueueShipConstruction(ship);

        // Simulate the station having secured 50 iron_ore for this build.
        SetSecured(shipyard, ship, "iron_ore", 50);

        int ironBefore = station.BulkStorage.GetQuantity("iron_ore");
        shipyard.CancelShipConstruction(ship);

        // Secured resources refunded to the station's BulkStorage; build state gone.
        AssertThat(station.BulkStorage.GetQuantity("iron_ore")).IsEqual(ironBefore + 50);
        AssertThat(shipyard.GetSecuredResources(ship).Count).IsEqual(0);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void CancelShipConstruction_RefundsNothingWhenNotYetSecured()
    {
        var station = new StationSatellite();
        station.BulkStorage.AddSlot(new StorageSlot(SlotFilter.Any()));
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = MakeShip("EarlyCancel", work: 10f, res: new() { { "iron_ore", 100 } });
        shipyard.EnqueueShipConstruction(ship);

        int ironBefore = station.BulkStorage.GetQuantity("iron_ore");
        shipyard.CancelShipConstruction(ship);

        // Nothing was secured -> nothing minted back into storage.
        AssertThat(station.BulkStorage.GetQuantity("iron_ore")).IsEqual(ironBefore);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    // ========================================================================
    // Queue callback wiring
    // ========================================================================

    [TestCase]
    public void OnRegister_CreatesQueue()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var queueField = typeof(ShipyardBehavior).GetField(
            "_shipBuildQueue",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        var queue = queueField?.GetValue(shipyard) as ShipBuildQueue;
        AssertThat(queue).IsNotNull();
        AssertThat(shipyard.ActiveShipBuildCount).IsEqual(0);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void OnUnregister_ClearsQueueReference()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        shipyard.OnUnregister();

        var queueField = typeof(ShipyardBehavior).GetField(
            "_shipBuildQueue",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        var queueAfter = queueField?.GetValue(shipyard) as ShipBuildQueue;
        AssertThat(queueAfter).IsNull();

        shipyard.OnDetach();
    }

    // ========================================================================
    // SetShipPaused and ReorderQueue delegation
    // ========================================================================

    [TestCase]
    public void SetShipPaused_DelegatesToQueue()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship = MakeShip("PauseShip");
        shipyard.EnqueueShipConstruction(ship);

        shipyard.SetShipPaused(ship, true);
        AssertThat(shipyard.IsShipPaused(ship)).IsTrue();

        shipyard.SetShipPaused(ship, false);
        AssertThat(shipyard.IsShipPaused(ship)).IsFalse();

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    [TestCase]
    public void ReorderQueue_DelegatesToQueue()
    {
        var station = new StationSatellite();
        var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
        shipyard.OnAttach(station);
        shipyard.OnRegister();

        var ship1 = MakeShip("Ship1");
        var ship2 = MakeShip("Ship2");
        shipyard.EnqueueShipConstruction(ship1);
        shipyard.EnqueueShipConstruction(ship2);

        int totalBefore = shipyard.ActiveShipBuildCount + shipyard.QueuedShipBuildCount;
        AssertThat(totalBefore).IsEqual(2);

        shipyard.ReorderQueue(ship2, ship1);
        AssertThat(shipyard.ActiveShipBuildCount + shipyard.QueuedShipBuildCount).IsEqual(2);

        shipyard.OnUnregister();
        shipyard.OnDetach();
    }

    // ========================================================================
    // ManufactureTickEngine wake-sleep on new order
    // ========================================================================

    [TestCase]
    public void ManufactureTickEngine_RegisterCalledOnEnqueue()
    {
        ManufactureTickEngine.Instance?.Stop();

        var engine = ManufactureTickEngine.CreateForTesting();
        try
        {
            var station = new StationSatellite();
            var shipyard = new ShipyardBehavior { MaxParallelShipBuilds = 1 };
            shipyard.OnAttach(station);
            shipyard.OnRegister();

            AssertThat(shipyard.WantsTick).IsFalse();

            var ship = MakeShip("EngineShip");
            shipyard.EnqueueShipConstruction(ship);

            DrainEngineOps(engine);

            AssertThat(engine.TickableCount).IsEqual(1);

            shipyard.OnUnregister();
            shipyard.OnDetach();
        }
        finally
        {
            engine.Stop();
        }
    }

    // ========================================================================
    // Test helpers
    // ========================================================================

    /// <summary>Creates a LogisticsUnit with a ship definition (so it can be enqueued/built).</summary>
    private static LogisticsUnit MakeShip(string name, float work = 100f, Dictionary<string, int>? res = null)
    {
        var def = new ShipDefinition
        {
            Name = name,
            WorkRequired = work,
            RequiredResources = res ?? new Dictionary<string, int>(),
        };
        var ship = new LogisticsUnit { Name = name };
        ship.SetShipDefinition(def);
        return ship;
    }

    /// <summary>Populates the station-owned build state's secured (held) resources via reflection.</summary>
    private static void SetSecured(ShipyardBehavior shipyard, LogisticsUnit ship, string resourceId, int amount)
    {
        var field = typeof(ShipyardBehavior).GetField(
            "_buildStates",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        var states = (Dictionary<LogisticsUnit, ConstructionState>)field!.GetValue(shipyard)!;
        states[ship].AvailableResources[resourceId] = amount;
    }

    // ========================================================================
    // Mock IOrbitalBody for CreateAndEnqueueShip tests
    // ========================================================================

    private partial class MockOrbitalBody : Node3D, IOrbitalBody
    {
        private readonly Node3D _satellitesContainer = new();

        public MockOrbitalBody()
        {
            AddChild(_satellitesContainer);
        }

        // IOrbitalBody
        public BodyClassification Classification =>
            BodyClassification.FromLegacy(CelestialBodyType.RockyPlanet, null);
        public BodyBillboardTextures BillboardTextures => null!;
        public float Radius { get; set; } = 100f;
        public float Mass { get; set; } = 1e12f;
        public Vector3 Velocity { get; set; }
        public Vector3 BodyPosition { get; set; }
        public string BodyName => "MockOrbitalBody";
        public UnifiedCelestialMesh Mesh => null!;
        public Godot.Collections.Array<OrbitBand> OrbitBands => new();
        public OrbitConfiguration? OrbitConfig => null;
        public Node3D SatellitesContainer => _satellitesContainer;
        public BuildingConstructionManager BuildingConstructionMgr => null!;
        public BodyEconomyManager EconomyMgr => null!;
        public bool UsesBandPlacement => true;
        public float Atmosphere => 1.0f;
        public IOrbitalBody? OrbitalParent { get; set; }

        public void InitializeOrbitSystem() { }
        public int GetBandCount() => 4;
        public bool CanAddToBand(int bandIndex) => true;
        public int GetBandSatelliteCount(int bandIndex) => 0;
        public void IncrementBandCount(int bandIndex) { }
        public void DecrementBandCount(int bandIndex) { }

        public OrbitalParameters GetOrbitalParametersForBand(int bandIndex, float startingAngle)
            => new(1f, 0f, 0f, Vector3.Zero, Vector3.Zero, 0f, bandIndex);

        public OrbitalParameters GetOrbitalParametersAtRadius(float radius, float startingAngle)
            => new(radius, 0f, 0f, Vector3.Zero, Vector3.Zero, 0f, 0);

        public int GetClosestBandForApproach(float approachSpeed) => 0;
        public float GetOrbitBandRadius(int bandIndex) => Radius * (1.1f + bandIndex * 0.2f);
        public float GetOrbitalSpeedForBand(int bandIndex) => 1f;

        public void RegisterTransferEndpoint(string id, TransferStationDefinition def, GodotObject owner) { }
        public void UnregisterTransferEndpoint(string id) { }
        public bool HasTransferEndpoint(string id) => false;
        public TransferStationDefinition? GetTransferEndpointDef(string id) => null;
        public Building? GetTransferEndpointBuilding(string id) => null;
        public GodotObject? GetTransferEndpointOwner(string id) => null;
        public IReadOnlyList<string> GetAllTransferEndpoints() => new List<string>();
        public IReadOnlyList<string> GetTransferEndpointsOnContinent(int continentIndex) => new List<string>();
        public float GetTotalTransferCapacityOnContinent(int continentIndex) => 0f;
    }

    // ========================================================================
    // Static helper for ManufactureTickEngine test drain
    // ========================================================================

    /// <summary>
    /// Invokes the private DrainOps method on a ManufactureTickEngine via reflection.
    /// This processes any pending Register/Unregister operations without running ticks.
    /// </summary>
    private static void DrainEngineOps(ManufactureTickEngine engine)
    {
        var drainMethod = typeof(ManufactureTickEngine).GetMethod(
            "DrainOps",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        drainMethod?.Invoke(engine, null);
    }
}
