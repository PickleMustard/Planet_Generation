using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Structures.GameState;

namespace Tests.Structures.GameState;

/// <summary>
/// Unit tests for the ship registry methods on SystemData. These tests exercise the
/// dictionary API directly (no scene-tree wiring) — the lifecycle integration with
/// _EnterTree/_ExitTree is covered by LogisticsUnitRegistryLifecycleTest.
/// </summary>
[TestSuite]
public class SystemDataRegistryTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void RegisterShip_StoresByIdAndReturnsInGetAll()
    {
        var sys = new SystemData();
        var unit = new LogisticsUnit { Name = "Test1" };
        unit.SetPersistedId("ship-a");

        sys.RegisterShip(unit);

        AssertThat(sys.ShipCount).IsEqual(1);
        AssertThat(sys.TryGetShip("ship-a", out var found)).IsTrue();
        AssertThat(found).IsEqual(unit);
        AssertThat(sys.GetAllShips()).Contains(unit);

        sys.Free();
        unit.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void UnregisterShip_RemovesEntry()
    {
        var sys = new SystemData();
        var unit = new LogisticsUnit { Name = "Test2" };
        unit.SetPersistedId("ship-b");
        sys.RegisterShip(unit);

        bool removed = sys.UnregisterShip(unit);

        AssertThat(removed).IsTrue();
        AssertThat(sys.ShipCount).IsEqual(0);
        AssertThat(sys.TryGetShip("ship-b", out _)).IsFalse();

        sys.Free();
        unit.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RegisterShip_DuplicateInstance_IsIdempotent()
    {
        var sys = new SystemData();
        var unit = new LogisticsUnit { Name = "Test3" };
        unit.SetPersistedId("ship-c");

        sys.RegisterShip(unit);
        sys.RegisterShip(unit);

        AssertThat(sys.ShipCount).IsEqual(1);

        sys.Free();
        unit.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RegisterShip_DuplicateIdDifferentInstance_Replaces()
    {
        var sys = new SystemData();
        var first = new LogisticsUnit { Name = "First" };
        first.SetPersistedId("dup");
        var second = new LogisticsUnit { Name = "Second" };
        second.SetPersistedId("dup");

        sys.RegisterShip(first);
        sys.RegisterShip(second);

        AssertThat(sys.ShipCount).IsEqual(1);
        sys.TryGetShip("dup", out var resolved);
        AssertThat(resolved).IsEqual(second);

        // The displaced 'first' calling Unregister must NOT eject the replacement.
        bool firstRemoved = sys.UnregisterShip(first);
        AssertThat(firstRemoved).IsFalse();
        AssertThat(sys.ShipCount).IsEqual(1);

        sys.Free();
        first.Free();
        second.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RegisterShip_EmptyId_NoOp()
    {
        var sys = new SystemData();
        var unit = new LogisticsUnit { Name = "NoId" }; // Id stays string.Empty
        sys.RegisterShip(unit);

        AssertThat(sys.ShipCount).IsEqual(0);

        sys.Free();
        unit.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TryGetShip_EmptyOrMissingId_ReturnsFalse()
    {
        var sys = new SystemData();
        AssertThat(sys.TryGetShip("", out _)).IsFalse();
        AssertThat(sys.TryGetShip("nope", out _)).IsFalse();
        sys.Free();
    }
}
