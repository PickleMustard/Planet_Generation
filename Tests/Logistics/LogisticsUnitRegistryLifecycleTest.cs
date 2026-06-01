using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Godot;
using Structures.GameState;

namespace Tests.Logistics;

/// <summary>
/// Tests the LogisticsUnit ↔ SystemData registry hook in _EnterTree/_ExitTree.
/// Builds a minimal scene tree fragment: an isolated owner -> "system_container"
/// node with a SystemData child -> ship parent body container, so the unit can be
/// added and trigger Godot's tree callbacks. Uses ShipCount + TryGetShip on the
/// SystemData fixture rather than the absolute /root path used by FindIn.
/// </summary>
[TestSuite]
public class LogisticsUnitRegistryLifecycleTest
{
    /// <summary>
    /// Container hierarchy:
    ///   owner (Node)
    ///     ├─ "system_container" (Node)
    ///     │   ├─ SystemData ("SystemData")
    ///     │   └─ "body" (Node3D)        — stand-in for a CelestialBody's SatellitesContainer
    ///     └─ "body2" (Node3D)            — second host, used by reparent test
    /// </summary>
    private sealed class Fixture
    {
        public Node Owner = null!;
        public Node SystemContainer = null!;
        public SystemData SystemData = null!;
        public Node3D BodyA = null!;
        public Node3D BodyB = null!;
    }

    private static Fixture Build()
    {
        var owner = new Node { Name = "owner" };
        var container = new Node { Name = "system_container" };
        var sys = new SystemData { Name = "SystemData" };
        var bodyA = new Node3D { Name = "bodyA" };
        var bodyB = new Node3D { Name = "bodyB" };

        owner.AddChild(container);
        container.AddChild(sys);
        container.AddChild(bodyA);
        container.AddChild(bodyB);

        return new Fixture
        {
            Owner = owner,
            SystemContainer = container,
            SystemData = sys,
            BodyA = bodyA,
            BodyB = bodyB,
        };
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EnterTree_RegistersWithSystemData()
    {
        var f = Build();
        var unit = new LogisticsUnit { Name = "Ship1" };

        f.BodyA.AddChild(unit); // fires _EnterTree

        AssertThat(f.SystemData.ShipCount).IsEqual(1);
        f.SystemData.TryGetShip(unit.Id, out var resolved);
        AssertThat(resolved).IsEqual(unit);

        f.Owner.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EnterTree_PreservesPersistedId()
    {
        var f = Build();
        var unit = new LogisticsUnit { Name = "Ship2" };
        unit.SetPersistedId("saved-id-42");

        f.BodyA.AddChild(unit);

        AssertThat(unit.Id).IsEqual("saved-id-42");
        AssertThat(f.SystemData.TryGetShip("saved-id-42", out _)).IsTrue();

        f.Owner.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EnterTree_NoPersistedId_GeneratesGuid()
    {
        var f = Build();
        var unit = new LogisticsUnit { Name = "Ship3" };

        f.BodyA.AddChild(unit);

        AssertThat(unit.Id).IsNotEqual("");
        // Guid.ToString() default format is 36 chars: 8-4-4-4-12.
        AssertThat(unit.Id.Length).IsEqual(36);

        f.Owner.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ExitTree_UnregistersFromSystemData()
    {
        var f = Build();
        var unit = new LogisticsUnit { Name = "Ship4" };
        f.BodyA.AddChild(unit);
        AssertThat(f.SystemData.ShipCount).IsEqual(1);

        f.BodyA.RemoveChild(unit); // fires _ExitTree

        AssertThat(f.SystemData.ShipCount).IsEqual(0);

        unit.Free();
        f.Owner.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Reparent_PreservesRegistryEntry()
    {
        var f = Build();
        var unit = new LogisticsUnit { Name = "Ship5" };
        unit.SetPersistedId("persistent");
        f.BodyA.AddChild(unit);

        // Manual reparent: RemoveChild + AddChild fires _ExitTree then _EnterTree.
        f.BodyA.RemoveChild(unit);
        f.BodyB.AddChild(unit);

        AssertThat(unit.Id).IsEqual("persistent");
        AssertThat(f.SystemData.ShipCount).IsEqual(1);
        AssertThat(f.SystemData.TryGetShip("persistent", out _)).IsTrue();

        f.Owner.Free();
    }
}
