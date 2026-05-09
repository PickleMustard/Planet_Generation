using System.Collections.Generic;
using Godot;
using Structures.Enums;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Centralized per-body manager that distributes construction work across all buildings
/// on a celestial body. Orbital architect stations register with this manager to contribute
/// their work budgets. Buildings register here and are ticked from an aggregated pool.
///
/// If no architects are registered, buildings queue in a pending state and begin ticking
/// once the first architect registers.
/// </summary>
public partial class BuildingConstructionManager : Node
{
    private struct ArchitectRegistration
    {
        public StationSatellite Station;
        public float BaseBudgetPerTick;
        public float ScalingPenalty;
        public float WasteFactor;
    }

    private readonly List<ArchitectRegistration> _architects = new();
    private readonly List<Building> _activeBuildings = new();
    private readonly List<Building> _pendingBuildings = new();

    private float _progressTimer;
    private const float PROGRESS_SIGNAL_INTERVAL = 0.5f;

    public int ArchitectCount => _architects.Count;
    public int ActiveBuildingCount => _activeBuildings.Count;
    public int PendingBuildingCount => _pendingBuildings.Count;

    public IReadOnlyList<Building> GetActiveBuildings() => _activeBuildings;

    /// <summary>
    /// Registers an orbital architect station, contributing its work budget to the aggregated pool.
    /// If buildings are pending, they are activated.
    /// </summary>
    public void RegisterArchitect(
        StationSatellite station,
        float baseBudgetPerTick,
        float scalingPenalty,
        float wasteFactor = 1.0f)
    {
        // Prevent duplicate registration
        for (int i = 0; i < _architects.Count; i++)
        {
            if (_architects[i].Station == station)
                return;
        }

        _architects.Add(new ArchitectRegistration
        {
            Station = station,
            BaseBudgetPerTick = baseBudgetPerTick,
            ScalingPenalty = scalingPenalty,
            WasteFactor = wasteFactor,
        });

        GameLogger.Info(
            $"BuildingConstructionManager: Registered architect '{station.Name}' " +
            $"(budget={baseBudgetPerTick}/tick, penalty={scalingPenalty}, waste={wasteFactor}). " +
            $"Total architects: {_architects.Count}");

        // Activate any pending buildings now that we have an architect
        if (_pendingBuildings.Count > 0)
            ActivatePendingBuildings();
    }

    /// <summary>
    /// Unregisters an orbital architect station. If no architects remain,
    /// active buildings move to pending (retaining progress).
    /// </summary>
    public void UnregisterArchitect(StationSatellite station)
    {
        for (int i = _architects.Count - 1; i >= 0; i--)
        {
            if (_architects[i].Station == station)
            {
                _architects.RemoveAt(i);
                GameLogger.Info(
                    $"BuildingConstructionManager: Unregistered architect '{station.Name}'. " +
                    $"Remaining: {_architects.Count}");
                break;
            }
        }

        if (_architects.Count == 0 && _activeBuildings.Count > 0)
        {
            GameLogger.Info(
                "BuildingConstructionManager: No architects remaining, " +
                $"moving {_activeBuildings.Count} buildings to pending");
            _pendingBuildings.AddRange(_activeBuildings);
            _activeBuildings.Clear();
        }
    }

    /// <summary>
    /// Registers a building for construction. If architects are available it becomes
    /// active immediately; otherwise it queues as pending.
    /// </summary>
    public void RegisterBuilding(Building building)
    {
        if (_activeBuildings.Contains(building) || _pendingBuildings.Contains(building))
            return;

        if (_architects.Count > 0)
        {
            ApplyWasteFactor(building);
            _activeBuildings.Add(building);
            GameLogger.Info(
                $"BuildingConstructionManager: Building '{building.Name}' registered as active " +
                $"(active: {_activeBuildings.Count})");
        }
        else
        {
            _pendingBuildings.Add(building);
            GameLogger.Info(
                $"BuildingConstructionManager: Building '{building.Name}' registered as pending " +
                $"(no architects available, pending: {_pendingBuildings.Count})");
        }
    }

    /// <summary>
    /// Removes a building from tracking (on completion or cancellation).
    /// </summary>
    public void UnregisterBuilding(Building building)
    {
        if (!_activeBuildings.Remove(building))
            _pendingBuildings.Remove(building);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_architects.Count == 0 || _activeBuildings.Count == 0)
            return;

        float dt = (float)delta;

        // Compute aggregated budget from all architects
        float totalBaseBudget = 0f;
        float weightedPenaltySum = 0f;
        foreach (var arch in _architects)
        {
            totalBaseBudget += arch.BaseBudgetPerTick;
            weightedPenaltySum += arch.BaseBudgetPerTick * arch.ScalingPenalty;
        }

        float avgPenalty = totalBaseBudget > 0f ? weightedPenaltySum / totalBaseBudget : 0f;

        // Count eligible (non-blocked) buildings
        int eligibleCount = 0;
        foreach (var building in _activeBuildings)
        {
            if (building.CheckRequiredResourcesAvailable())
                eligibleCount++;
        }

        if (eligibleCount == 0)
            return;

        // Diminishing returns formula (same as former BuildingWorkBudget)
        float scalingFactor = 1.0f / (1.0f + avgPenalty * (eligibleCount - 1));
        float effectiveBudget = totalBaseBudget * dt * scalingFactor;
        float workPerBuilding = effectiveBudget / eligibleCount;

        _progressTimer += dt;
        bool emitProgress = _progressTimer >= PROGRESS_SIGNAL_INTERVAL;
        if (emitProgress)
            _progressTimer = 0f;

        for (int i = _activeBuildings.Count - 1; i >= 0; i--)
        {
            var building = _activeBuildings[i];

            if (building.CheckRequiredResourcesAvailable())
            {
                building.UpdateProgress(workPerBuilding);
            }

            if (emitProgress)
            {
                ConstructionManager.Instance?.NotifyProgressUpdate(
                    building.Name.ToString(), building.GetProgress(), building.GetStatus());
            }

            if (building.GetStatus() == ConstructionStatus.Complete.ToString())
            {
                _activeBuildings.RemoveAt(i);
                ConstructionManager.Instance?.NotifyConstructionComplete(building);
                GameLogger.Info(
                    $"BuildingConstructionManager: Building '{building.Name}' construction complete");
            }
        }
    }

    private void ActivatePendingBuildings()
    {
        GameLogger.Info(
            $"BuildingConstructionManager: Activating {_pendingBuildings.Count} pending buildings");

        foreach (var building in _pendingBuildings)
        {
            ApplyWasteFactor(building);
            _activeBuildings.Add(building);
        }
        _pendingBuildings.Clear();
    }

    /// <summary>
    /// Applies the worst waste factor among registered architects to a building's work required.
    /// </summary>
    private void ApplyWasteFactor(Building building)
    {
        float worstWaste = 1.0f;
        foreach (var arch in _architects)
        {
            if (arch.WasteFactor > worstWaste)
                worstWaste = arch.WasteFactor;
        }

        if (worstWaste > 1.0f)
        {
            building.workRequired *= worstWaste;
            GameLogger.Debug(
                $"BuildingConstructionManager: Applied waste factor {worstWaste}x to '{building.Name}'");
        }
    }
}
