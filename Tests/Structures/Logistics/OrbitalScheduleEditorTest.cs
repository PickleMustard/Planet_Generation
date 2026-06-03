using System;
using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.TextureGeneration;
using Structures;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using Structures.Transfers;

namespace Tests.Structures.Logistics;

[TestSuite]
public class OrbitalScheduleEditorTest
{
    /// <summary>
    /// Minimal non-Node stand-in for an orbital body. Only object identity matters to
    /// the editor (bare-body endpoints compare by reference); every other member is
    /// unused by the operations under test.
    /// </summary>
    private sealed class StubBody : IOrbitalBody
    {
        private readonly string _name;
        public StubBody(string name) => _name = name;

        public BodyClassification Classification => default;
        public BodyBillboardTextures BillboardTextures => null!;
        public float Radius { get; set; }
        public float Mass { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 BodyPosition { get; set; }
        public string BodyName => _name;
        public UnifiedCelestialMesh Mesh => null!;
        public float Atmosphere => 0f;
        public IOrbitalBody? OrbitalParent { get; set; }
        public Godot.Collections.Array<OrbitBand> OrbitBands => null!;
        public OrbitConfiguration? OrbitConfig => null;
        public Node3D SatellitesContainer => null!;
        public BuildingConstructionManager BuildingConstructionMgr => null!;
        public BodyEconomyManager EconomyMgr => null!;
        public bool UsesBandPlacement => true;

        public void InitializeOrbitSystem() => throw new NotImplementedException();
        public int GetBandCount() => 0;
        public bool CanAddToBand(int bandIndex) => false;
        public int GetBandSatelliteCount(int bandIndex) => 0;
        public void IncrementBandCount(int bandIndex) { }
        public void DecrementBandCount(int bandIndex) { }
        public OrbitalParameters GetOrbitalParametersForBand(int bandIndex, float startingAngle) => default;
        public OrbitalParameters GetOrbitalParametersAtRadius(float radius, float startingAngle) => default;
        public int GetClosestBandForApproach(float approachSpeed) => 0;
        public float GetOrbitBandRadius(int bandIndex) => 0f;
        public float GetOrbitalSpeedForBand(int bandIndex) => 0f;

        public void RegisterTransferEndpoint(string endpointId, TransferStationDefinition def, GodotObject owner) { }
        public void UnregisterTransferEndpoint(string endpointId) { }
        public bool HasTransferEndpoint(string endpointId) => false;
        public TransferStationDefinition? GetTransferEndpointDef(string endpointId) => null;
        public Building? GetTransferEndpointBuilding(string endpointId) => null;
        public GodotObject? GetTransferEndpointOwner(string endpointId) => null;
        public IReadOnlyList<string> GetAllTransferEndpoints() => Array.Empty<string>();
        public IReadOnlyList<string> GetTransferEndpointsOnContinent(int continentIndex) => Array.Empty<string>();
        public float GetTotalTransferCapacityOnContinent(int continentIndex) => 0f;
    }

    private static LegEndpoint Body(IOrbitalBody b) => LegEndpoint.ForBody(b);

    [TestCase]
    public void Append_BuildsChainFromUnitLocation()
    {
        var home = new StubBody("Home");
        var a = new StubBody("A");
        var b = new StubBody("B");
        var loc = Body(home);
        var s = new OrbitalTransferSchedule();

        OrbitalScheduleEditor.AppendLeg(s, Body(a), loc);
        OrbitalScheduleEditor.AppendLeg(s, Body(b), loc);

        AssertThat(s.Legs.Count).IsEqual(2);
        AssertThat(s.Legs[0].Origin.Body).IsEqual(home);
        AssertThat(s.Legs[0].Destination.Body).IsEqual(a);
        AssertThat(s.Legs[1].Origin.Body).IsEqual(a);
        AssertThat(s.Legs[1].Destination.Body).IsEqual(b);
    }

    [TestCase]
    public void Swap_ExchangesDestinationsAndKeepsManifests()
    {
        var home = new StubBody("Home");
        var a = new StubBody("A");
        var b = new StubBody("B");
        var c = new StubBody("C");
        var loc = Body(home);
        var s = new OrbitalTransferSchedule();
        OrbitalScheduleEditor.AppendLeg(s, Body(a), loc); // Home→A
        OrbitalScheduleEditor.AppendLeg(s, Body(b), loc); // A→B
        OrbitalScheduleEditor.AppendLeg(s, Body(c), loc); // B→C

        var m0 = new CargoManifest(); m0.LoadResource("R0", 1); s.Legs[0].PickupOrder = m0;
        var m1 = new CargoManifest(); m1.LoadResource("R1", 1); s.Legs[1].PickupOrder = m1;

        OrbitalScheduleEditor.SwapLegs(s, 0, 1, loc);

        // Destinations swapped, origins re-chained, manifests stay in their slots.
        AssertThat(s.Legs[0].Destination.Body).IsEqual(b);
        AssertThat(s.Legs[1].Destination.Body).IsEqual(a);
        AssertThat(s.Legs[0].Origin.Body).IsEqual(home);
        AssertThat(s.Legs[1].Origin.Body).IsEqual(b);
        AssertThat(s.Legs[0].PickupOrder).IsEqual(m0);
        AssertThat(s.Legs[1].PickupOrder).IsEqual(m1);
    }

    [TestCase]
    public void Delete_MergesPreviousLegToDeletedDestination()
    {
        var home = new StubBody("Home");
        var a = new StubBody("A");
        var b = new StubBody("B");
        var c = new StubBody("C");
        var loc = Body(home);
        var s = new OrbitalTransferSchedule();
        OrbitalScheduleEditor.AppendLeg(s, Body(a), loc); // Home→A
        OrbitalScheduleEditor.AppendLeg(s, Body(b), loc); // A→B
        OrbitalScheduleEditor.AppendLeg(s, Body(c), loc); // B→C

        // Delete the middle leg (A→B): the previous leg should now end at B.
        OrbitalScheduleEditor.DeleteLeg(s, 1, loc);

        AssertThat(s.Legs.Count).IsEqual(2);
        AssertThat(s.Legs[0].Origin.Body).IsEqual(home);
        AssertThat(s.Legs[0].Destination.Body).IsEqual(b);
        AssertThat(s.Legs[1].Origin.Body).IsEqual(b);
        AssertThat(s.Legs[1].Destination.Body).IsEqual(c);
    }

    [TestCase]
    public void ClosingLeg_AddedWhenRepeatingAndEndsElsewhere()
    {
        var home = new StubBody("Home");
        var a = new StubBody("A");
        var loc = Body(home);
        var s = new OrbitalTransferSchedule { IsRepeating = true };
        OrbitalScheduleEditor.AppendLeg(s, Body(a), loc); // Home→A, then closing A→Home

        AssertThat(s.Legs.Count).IsEqual(2);
        AssertThat(s.Legs[1].IsClosingLeg).IsTrue();
        AssertThat(s.Legs[1].Origin.Body).IsEqual(a);
        AssertThat(s.Legs[1].Destination.Body).IsEqual(home);
    }

    [TestCase]
    public void ClosingLeg_NotAddedWhenScheduleAlreadyReturnsToStart()
    {
        var home = new StubBody("Home");
        var a = new StubBody("A");
        var loc = Body(home);
        var s = new OrbitalTransferSchedule { IsRepeating = true };
        OrbitalScheduleEditor.AppendLeg(s, Body(a), loc);    // Home→A
        OrbitalScheduleEditor.AppendLeg(s, Body(home), loc); // A→Home (closes naturally)

        foreach (var leg in s.Legs)
            AssertThat(leg.IsClosingLeg).IsFalse();
        AssertThat(s.Legs.Count).IsEqual(2);
    }
}
