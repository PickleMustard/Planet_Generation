using System.Collections.Generic;
using Structures.Enums;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Distributes a limited work budget across all buildings being constructed by an orbital-architect station.
/// Uses a diminishing returns formula: as more buildings are constructed concurrently,
/// each receives proportionally less work plus an extra scaling penalty.
///
/// Formula: workPerBuilding = (baseBudget * delta) / (N * (1 + penalty * (N - 1)))
/// </summary>
public class BuildingWorkBudget
{
    private readonly StationSatellite _owner;
    private readonly List<BuildingConstruction> _activeBuildings = new();
    private readonly float _baseBudgetPerTick;
    private readonly float _scalingPenalty;

    private float _progressTimer;
    private const float PROGRESS_SIGNAL_INTERVAL = 0.5f;

    public BuildingWorkBudget(StationSatellite owner, float baseBudgetPerTick, float scalingPenalty)
    {
        _owner = owner;
        _baseBudgetPerTick = baseBudgetPerTick;
        _scalingPenalty = scalingPenalty;
    }

    public int ActiveCount => _activeBuildings.Count;
    public IReadOnlyList<BuildingConstruction> GetActiveBuildings() => _activeBuildings;

    /// <summary>
    /// Registers a building for construction under this work budget.
    /// </summary>
    public void Register(BuildingConstruction building)
    {
        if (_activeBuildings.Contains(building))
            return;

        _activeBuildings.Add(building);
        GameLogger.Info($"BuildingWorkBudget [{_owner.Name}]: Registered building '{building.Name}' (active: {_activeBuildings.Count})");
    }

    /// <summary>
    /// Removes a building from the work budget (on completion or cancellation).
    /// </summary>
    public void Unregister(BuildingConstruction building)
    {
        if (_activeBuildings.Remove(building))
        {
            GameLogger.Info($"BuildingWorkBudget [{_owner.Name}]: Unregistered building '{building.Name}' (active: {_activeBuildings.Count})");
        }
    }

    /// <summary>
    /// Distributes work across all active buildings. Called each physics tick by the owning station.
    /// </summary>
    public void Tick(float delta)
    {
        if (_activeBuildings.Count == 0)
            return;

        // Count non-blocked buildings
        int eligibleCount = 0;
        foreach (var building in _activeBuildings)
        {
            if (building.CheckRequiredResourcesAvailable())
                eligibleCount++;
        }

        if (eligibleCount == 0)
            return;

        // Diminishing returns: workPerBuilding = (baseBudget * delta) / (N * (1 + penalty * (N - 1)))
        float scalingFactor = 1.0f / (1.0f + _scalingPenalty * (eligibleCount - 1));
        float effectiveBudget = _baseBudgetPerTick * delta * scalingFactor;
        float workPerBuilding = effectiveBudget / eligibleCount;

        _progressTimer += delta;
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
                GameLogger.Info($"BuildingWorkBudget [{_owner.Name}]: Building '{building.Name}' construction complete");
            }
        }
    }
}
