using System.Collections.Generic;
using Constructables.Stations.Behaviors;
using Godot;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Per-body router for building construction. Holds the set of <see cref="OrbitalConstructorBehavior"/>
/// architects on the body and assigns newly-placed buildings to the least-loaded architect.
/// Buildings without an available architect queue body-wide; they drain into architects as soon
/// as one registers. The architects themselves own the work loop (driven by
/// <see cref="Constructables.Tick.ManufactureTickEngine"/>); this manager is pure routing.
/// </summary>
public partial class BuildingConstructionManager : Node
{
    private readonly List<OrbitalConstructorBehavior> _architects = new();
    private readonly List<Building> _pendingBuildings = new();
    private readonly Dictionary<Building, OrbitalConstructorBehavior> _ownership = new();

    public int ArchitectCount => _architects.Count;
    public int PendingBuildingCount => _pendingBuildings.Count;

    public IReadOnlyList<OrbitalConstructorBehavior> GetArchitects() => _architects;
    public IReadOnlyList<Building> GetPendingBuildings() => _pendingBuildings;

    /// <summary>
    /// Aggregated active-building view across every registered architect, kept for legacy
    /// UI/tests that previously read from a centralized list.
    /// </summary>
    public virtual IReadOnlyList<Building> GetActiveBuildings()
    {
        var result = new List<Building>();
        foreach (var arch in _architects)
        {
            foreach (var slot in arch.RegularSlots)
                if (slot.Building != null) result.Add(slot.Building);
            foreach (var slot in arch.OvertimeSlots)
                if (slot.Building != null) result.Add(slot.Building);
        }
        return result;
    }

    public int ActiveBuildingCount
    {
        get
        {
            int n = 0;
            foreach (var arch in _architects)
                n += arch.OccupiedSlotCount;
            return n;
        }
    }

    /// <summary>
    /// Registers an architect behavior. If buildings are pending, they drain into the new
    /// architect (and any siblings) via least-loaded routing.
    /// </summary>
    public void RegisterArchitect(OrbitalConstructorBehavior architect)
    {
        if (architect == null) return;
        if (_architects.Contains(architect)) return;

        _architects.Add(architect);
        GameLogger.Info(
            $"BuildingConstructionManager: Registered architect "
            + $"'{architect.Owner?.Name ?? "?"}'. Total: {_architects.Count}"
        );

        if (_pendingBuildings.Count > 0)
            DrainPending();
    }

    /// <summary>
    /// Unregisters an architect. Any buildings it owns are moved back to the pending pool
    /// (or rerouted to another architect via least-loaded selection).
    /// </summary>
    public void UnregisterArchitect(OrbitalConstructorBehavior architect)
    {
        if (architect == null) return;
        if (!_architects.Remove(architect)) return;

        var displaced = new List<Building>();
        foreach (var slot in architect.RegularSlots)
            if (slot.Building != null) displaced.Add(slot.Building);
        foreach (var slot in architect.OvertimeSlots)
            if (slot.Building != null) displaced.Add(slot.Building);
        foreach (var b in architect.Queue)
            displaced.Add(b);

        foreach (var b in displaced)
        {
            architect.RemoveBuilding(b);
            _ownership.Remove(b);
        }

        GameLogger.Info(
            $"BuildingConstructionManager: Unregistered architect "
            + $"'{architect.Owner?.Name ?? "?"}'. Displaced {displaced.Count} buildings. "
            + $"Remaining architects: {_architects.Count}"
        );

        foreach (var b in displaced)
            RouteOrPend(b);
    }

    /// <summary>
    /// Registers a building for construction. Routes to the least-loaded architect if any
    /// exist; otherwise queues body-wide pending.
    /// </summary>
    public void RegisterBuilding(Building building)
    {
        if (building == null) return;
        if (_ownership.ContainsKey(building) || _pendingBuildings.Contains(building))
            return;

        RouteOrPend(building);
    }

    /// <summary>
    /// Removes a building from tracking (on completion or cancellation).
    /// </summary>
    public void UnregisterBuilding(Building building)
    {
        if (building == null) return;
        if (_ownership.TryGetValue(building, out var owner))
        {
            owner.RemoveBuilding(building);
            _ownership.Remove(building);
            return;
        }
        _pendingBuildings.Remove(building);
    }

    /// <summary>Returns the architect that owns the building, or null if pending/unknown.</summary>
    public OrbitalConstructorBehavior? GetOwner(Building building)
    {
        if (building == null) return null;
        _ownership.TryGetValue(building, out var owner);
        return owner;
    }

    /// <summary>Picks the architect with the lowest <see cref="OrbitalConstructorBehavior.Load"/>.</summary>
    public OrbitalConstructorBehavior? FindLeastLoadedArchitect()
    {
        OrbitalConstructorBehavior? best = null;
        int bestLoad = int.MaxValue;
        foreach (var arch in _architects)
        {
            int load = arch.Load;
            if (load < bestLoad)
            {
                bestLoad = load;
                best = arch;
            }
        }
        return best;
    }

    private void RouteOrPend(Building building)
    {
        var target = FindLeastLoadedArchitect();
        if (target == null)
        {
            _pendingBuildings.Add(building);
            GameLogger.Info(
                $"BuildingConstructionManager: '{building.Name}' pending "
                + $"(no architects, pending: {_pendingBuildings.Count})"
            );
            return;
        }

        target.AssignBuilding(building);
        _ownership[building] = target;
        GameLogger.Info(
            $"BuildingConstructionManager: Routed '{building.Name}' to architect "
            + $"'{target.Owner?.Name ?? "?"}' (load {target.Load})"
        );
    }

    private void DrainPending()
    {
        if (_architects.Count == 0) return;

        var snapshot = new List<Building>(_pendingBuildings);
        _pendingBuildings.Clear();
        foreach (var b in snapshot)
            RouteOrPend(b);
    }
}
