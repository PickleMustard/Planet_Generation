using System.Collections.Generic;
using Constructables.Stations;
using Constructables.Tick;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Enums;
using UtilityLibrary;

namespace Constructables.Stations.Behaviors;

/// <summary>
/// Slot-based building constructor. Each architect station owns a fixed pool of regular
/// slots (immutable from YAML) plus a mutable pool of overtime slots. Buildings register
/// here via the body's <see cref="BuildingConstructionManager"/> router, fill open slots,
/// or queue if all slots are full. The architect ticks via <see cref="ManufactureTickEngine"/>
/// while any slot is occupied; each occupied slot advances its building by
/// <see cref="WorkBudgetPerTick"/> per tick.
///
/// Overtime slots multiply the building's required resources by <c>(1 + N * OvertimeCostPercent)</c>
/// where N is the count of currently-occupied overtime slots. The multiplier is recomputed
/// every tick so it tracks live occupancy. Regular slots are never affected.
/// </summary>
public partial class OrbitalConstructorBehavior : RefCounted, IStationBehavior
{
    public sealed class SlotRecord
    {
        public Building? Building;
        public Dictionary<string, int>? BaseRequiredResources;
        public float LastAppliedMultiplier = 1.0f;
        public bool IsRetiring;
    }

    private StationSatellite? _owner;
    private IOrbitalBody? _body;

    /// <summary>Per-slot work budget contributed per tick to each occupied slot.</summary>
    public float WorkBudgetPerTick { get; set; } = 1.0f;

    /// <summary>Number of regular slots. Set from YAML; immutable at runtime.</summary>
    public int RegularSlotCount { get; set; } = 1;

    /// <summary>Target number of overtime slots. Mutable via <see cref="SetOvertimeTarget"/>.</summary>
    public int OvertimeSlotTarget { get; private set; }

    /// <summary>Per-occupied-overtime-slot resource cost multiplier increment.</summary>
    public float OvertimeCostPercent { get; set; } = 0.10f;

    private readonly List<SlotRecord> _regularSlots = new();
    private readonly List<SlotRecord> _overtimeSlots = new();
    private readonly List<Building> _queue = new();
    private bool _isTickRegistered;
    private bool _firstTickLogged;
    private float _progressEmitTimer;
    private const float PROGRESS_EMIT_INTERVAL = 0.5f;

    public StationSatellite? Owner => _owner;
    public IOrbitalBody? Body => _body;

    public int RegularSlotsConfigured => _regularSlots.Count;
    public int OvertimeSlotsConfigured => _overtimeSlots.Count;
    public int QueueCount => _queue.Count;

    public int OccupiedRegularCount => CountOccupied(_regularSlots);
    public int OccupiedOvertimeCount => CountOccupied(_overtimeSlots);
    public int OccupiedSlotCount => OccupiedRegularCount + OccupiedOvertimeCount;

    /// <summary>Load metric used by <see cref="BuildingConstructionManager"/> for routing.</summary>
    public int Load => OccupiedSlotCount + QueueCount;

    public IReadOnlyList<SlotRecord> RegularSlots => _regularSlots;
    public IReadOnlyList<SlotRecord> OvertimeSlots => _overtimeSlots;
    public IReadOnlyList<Building> Queue => _queue;

    public void OnAttach(StationSatellite owner) => _owner = owner;

    public void OnRegister()
    {
        if (_owner == null) return;

        Node? cursor = _owner.GetParent();
        while (cursor != null)
        {
            if (cursor is IOrbitalBody body)
            {
                _body = body;
                break;
            }
            cursor = cursor.GetParent();
        }

        if (_body?.BuildingConstructionMgr == null)
        {
            GameLogger.Warning(
                $"OrbitalConstructorBehavior {_owner.Name}: "
                + "No BuildingConstructionManager found on parent body"
            );
            return;
        }

        EnsureSlotsInitialized();
        _body.BuildingConstructionMgr.RegisterArchitect(this);

        GameLogger.Info(
            $"OrbitalConstructorBehavior {_owner.Name}: Registered with "
            + $"regular={RegularSlotCount}, overtime={OvertimeSlotTarget}, "
            + $"work/slot={WorkBudgetPerTick}, ot_pct={OvertimeCostPercent}"
        );
    }

    public void OnUnregister()
    {
        if (_owner == null || _body?.BuildingConstructionMgr == null) return;
        _body.BuildingConstructionMgr.UnregisterArchitect(this);
    }

    public void OnDetach()
    {
        _owner = null;
        _body = null;
        _regularSlots.Clear();
        _overtimeSlots.Clear();
        _queue.Clear();
    }

    public bool WantsTick => OccupiedSlotCount > 0;
    public int Priority => 50;

    /// <summary>
    /// Assigns a building to a slot or the queue. Regular slots fill first, then overtime
    /// up to the configured target. Returns true if assigned to a slot (resource multiplier
    /// applied), false if queued.
    /// </summary>
    public bool AssignBuilding(Building building)
    {
        if (building == null) return false;
        if (ContainsBuilding(building)) return true;

        EnsureSlotsInitialized();

        var slot = FindEmptyRegularSlot() ?? FindEmptyOvertimeSlot();
        if (slot != null)
        {
            FillSlot(slot, building);
            EnsureTickRegistration();
            return true;
        }

        _queue.Add(building);
        GameLogger.Debug(
            $"OrbitalConstructorBehavior {_owner?.Name}: Building '{building.Name}' queued "
            + $"(queue depth {_queue.Count})"
        );
        return false;
    }

    /// <summary>
    /// Removes a building from this architect's slots or queue. On removal, the building's
    /// requiredResources are restored to base. Promotes a queued building into any freed slot.
    /// Marked-retiring overtime slots are removed instead of refilled.
    /// </summary>
    public bool RemoveBuilding(Building building)
    {
        for (int i = 0; i < _regularSlots.Count; i++)
        {
            if (_regularSlots[i].Building == building)
            {
                ClearSlot(_regularSlots[i]);
                PromoteFromQueue();
                EvaluateTickRegistration();
                return true;
            }
        }

        for (int i = _overtimeSlots.Count - 1; i >= 0; i--)
        {
            if (_overtimeSlots[i].Building == building)
            {
                bool retiring = _overtimeSlots[i].IsRetiring;
                ClearSlot(_overtimeSlots[i]);
                if (retiring)
                    _overtimeSlots.RemoveAt(i);
                PromoteFromQueue();
                EvaluateTickRegistration();
                return true;
            }
        }

        if (_queue.Remove(building))
            return true;

        return false;
    }

    /// <summary>
    /// Transfers a building from this architect to <paramref name="target"/>. Restores the
    /// building's base required-resources before reassignment so <paramref name="target"/>'s
    /// own overtime occupancy drives the new multiplier.
    /// </summary>
    public bool TransferBuildingTo(OrbitalConstructorBehavior target, Building building)
    {
        if (target == null || target == this || building == null) return false;
        if (!ContainsBuilding(building)) return false;

        if (!RemoveBuilding(building))
            return false;

        target.AssignBuilding(building);
        GameLogger.Info(
            $"OrbitalConstructorBehavior {_owner?.Name}: Transferred '{building.Name}' "
            + $"to {target.Owner?.Name ?? "(unknown)"}"
        );
        return true;
    }

    /// <summary>
    /// Adjusts the overtime slot target. Growing adds empty slots; shrinking marks trailing
    /// occupied slots as retiring (they finish in place, slot removed on completion). Empty
    /// slots beyond the new target are removed immediately.
    /// </summary>
    public void SetOvertimeTarget(int target)
    {
        if (target < 0) target = 0;
        EnsureSlotsInitialized();

        if (target > _overtimeSlots.Count)
        {
            while (_overtimeSlots.Count < target)
                _overtimeSlots.Add(new SlotRecord());
            OvertimeSlotTarget = target;
            for (int i = 0; i < _overtimeSlots.Count; i++)
                _overtimeSlots[i].IsRetiring = false;
            PromoteFromQueue();
            EnsureTickRegistration();
            return;
        }

        OvertimeSlotTarget = target;
        for (int i = _overtimeSlots.Count - 1; i >= target; i--)
        {
            if (_overtimeSlots[i].Building == null)
                _overtimeSlots.RemoveAt(i);
            else
                _overtimeSlots[i].IsRetiring = true;
        }
        EvaluateTickRegistration();
    }

    public void OnManufactureTick(float delta, StationSatellite owner)
    {
        if (!_firstTickLogged && OccupiedSlotCount > 0)
        {
            _firstTickLogged = true;
            GameLogger.Info(
                $"OrbitalConstructorBehavior {_owner?.Name}: first tick, "
                + $"regular_occ={OccupiedRegularCount}, ot_occ={OccupiedOvertimeCount}, "
                + $"queue={_queue.Count}, work/tick={WorkBudgetPerTick}, delta={delta}"
            );
        }

        int otOccupied = OccupiedOvertimeCount;
        float otMultiplier = 1.0f + otOccupied * OvertimeCostPercent;

        TickSlots(_regularSlots, delta, multiplier: 1.0f, isOvertime: false);
        TickSlots(_overtimeSlots, delta, multiplier: otMultiplier, isOvertime: true);

        if (HasFreeSlot() && _queue.Count > 0)
            PromoteFromQueue();

        _progressEmitTimer += delta;
        if (_progressEmitTimer >= PROGRESS_EMIT_INTERVAL)
        {
            _progressEmitTimer = 0f;
            EmitProgressUpdates();
        }

        EvaluateTickRegistration();
    }

    private void EmitProgressUpdates()
    {
        var cm = ConstructionManager.Instance;
        if (cm == null) return;
        foreach (var s in _regularSlots)
        {
            if (s.Building != null)
                cm.NotifyProgressUpdate(s.Building.Name, s.Building.GetProgress(), s.Building.GetStatus());
        }
        foreach (var s in _overtimeSlots)
        {
            if (s.Building != null)
                cm.NotifyProgressUpdate(s.Building.Name, s.Building.GetProgress(), s.Building.GetStatus());
        }
    }

    private void TickSlots(List<SlotRecord> slots, float delta, float multiplier, bool isOvertime)
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            var slot = slots[i];
            var building = slot.Building;
            if (building == null) continue;

            // Re-apply required-resource multiplier only when it changes. Avoids the per-tick
            // Godot.Collections.Dictionary round-trip on the hot path. Regular slots set
            // multiplier=1.0 at fill time and never re-write thereafter.
            if (!Mathf.IsEqualApprox(slot.LastAppliedMultiplier, multiplier))
            {
                if (slot.BaseRequiredResources != null)
                    ApplyMultiplier(building, slot.BaseRequiredResources, multiplier);
                slot.LastAppliedMultiplier = multiplier;
            }

            TryDeliverFromStation(building);

            if (building.CheckRequiredResourcesAvailable())
                building.UpdateProgress(WorkBudgetPerTick * delta);

            if (building.GetStatus() == ConstructionStatus.Complete.ToString())
            {
                bool retiring = isOvertime && slot.IsRetiring;
                ClearSlot(slot);
                if (retiring)
                    slots.RemoveAt(i);
                ConstructionManager.Instance?.NotifyConstructionComplete(building);
                GameLogger.Info(
                    $"OrbitalConstructorBehavior {_owner?.Name}: '{building.Name}' construction complete"
                );
            }
        }
    }

    private void EnsureSlotsInitialized()
    {
        while (_regularSlots.Count < RegularSlotCount)
            _regularSlots.Add(new SlotRecord());
        while (_regularSlots.Count > RegularSlotCount)
            _regularSlots.RemoveAt(_regularSlots.Count - 1);

        if (OvertimeSlotTarget == 0 && _overtimeSlots.Count == 0)
        {
            // First-time init: pull initial value from the definition path that set
            // OvertimeSlotTarget; the property defaults to 0, so callers must invoke
            // SetOvertimeTarget if they want a non-zero starting allotment.
        }

        while (_overtimeSlots.Count < OvertimeSlotTarget)
            _overtimeSlots.Add(new SlotRecord());
    }

    private SlotRecord? FindEmptyRegularSlot()
    {
        for (int i = 0; i < _regularSlots.Count; i++)
            if (_regularSlots[i].Building == null)
                return _regularSlots[i];
        return null;
    }

    private SlotRecord? FindEmptyOvertimeSlot()
    {
        for (int i = 0; i < _overtimeSlots.Count && i < OvertimeSlotTarget; i++)
        {
            var s = _overtimeSlots[i];
            if (s.Building == null && !s.IsRetiring)
                return s;
        }
        return null;
    }

    private bool HasFreeSlot()
    {
        if (FindEmptyRegularSlot() != null) return true;
        return FindEmptyOvertimeSlot() != null;
    }

    private void FillSlot(SlotRecord slot, Building building)
    {
        slot.Building = building;
        building.ConstructingStation = _owner;
        slot.BaseRequiredResources = building.SnapshotRequiredResources();
        slot.LastAppliedMultiplier = 1.0f;
        GameLogger.Info(
            $"OrbitalConstructorBehavior {_owner?.Name}: '{building.Name}' assigned to slot "
            + $"(regular {OccupiedRegularCount}/{_regularSlots.Count}, "
            + $"overtime {OccupiedOvertimeCount}/{OvertimeSlotTarget}, "
            + $"work/tick {WorkBudgetPerTick}, base required={building.SnapshotRequiredResources().Count} entries)"
        );
    }

    private void ClearSlot(SlotRecord slot)
    {
        var building = slot.Building;
        if (building != null && slot.BaseRequiredResources != null
            && !Mathf.IsEqualApprox(slot.LastAppliedMultiplier, 1.0f))
        {
            building.ReplaceRequiredResources(slot.BaseRequiredResources);
        }
        if (building != null)
            building.ConstructingStation = null;
        slot.Building = null;
        slot.BaseRequiredResources = null;
        slot.LastAppliedMultiplier = 1.0f;
    }

    private void TryDeliverFromStation(Building building)
    {
        if (_owner == null) return;
        var required = building.SnapshotRequiredResources();
        if (required.Count == 0) return;
        var available = building.SnapshotAvailableResources();
        foreach (var kvp in required)
        {
            int have = available.TryGetValue(kvp.Key, out var a) ? a : 0;
            int shortfall = kvp.Value - have;
            if (shortfall <= 0) continue;
            float withdrawn = _owner.BulkStorage.Withdraw(kvp.Key, shortfall);
            int delivered = Mathf.FloorToInt(withdrawn);
            if (delivered > 0)
                building.DeliverResources(kvp.Key, delivered);
        }
    }

    private void PromoteFromQueue()
    {
        while (_queue.Count > 0 && HasFreeSlot())
        {
            var building = _queue[0];
            _queue.RemoveAt(0);
            var slot = FindEmptyRegularSlot() ?? FindEmptyOvertimeSlot();
            if (slot == null) break;
            FillSlot(slot, building);
        }
    }

    private bool ContainsBuilding(Building building)
    {
        foreach (var s in _regularSlots) if (s.Building == building) return true;
        foreach (var s in _overtimeSlots) if (s.Building == building) return true;
        return _queue.Contains(building);
    }

    private void EnsureTickRegistration()
    {
        if (_owner == null) return;
        if (_isTickRegistered) return;
        ManufactureTickEngine.Instance?.Register(_owner);
        _isTickRegistered = true;
    }

    private void EvaluateTickRegistration()
    {
        if (_owner == null) return;

        if (OccupiedSlotCount > 0)
        {
            EnsureTickRegistration();
            return;
        }

        // Station has other behaviors that may still want ticks; StationSatellite's own
        // end-of-tick EvaluateTickRegistration handles the actual unregister. We only
        // need to clear our flag so a future Assign re-registers cleanly.
        _isTickRegistered = false;
    }

    private static int CountOccupied(List<SlotRecord> slots)
    {
        int n = 0;
        foreach (var s in slots) if (s.Building != null) n++;
        return n;
    }

    private static void ApplyMultiplier(
        Building building,
        Dictionary<string, int> baseRequired,
        float multiplier)
    {
        var target = new Dictionary<string, int>();
        foreach (var kvp in baseRequired)
            target[kvp.Key] = Mathf.CeilToInt(kvp.Value * multiplier);
        building.ReplaceRequiredResources(target);
    }
}
