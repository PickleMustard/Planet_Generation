using System.Collections.Generic;
using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Per-body manager that ticks all active ContinentEconomy and StationEconomy instances
/// belonging to this orbital body each physics frame.
/// Added as a child Node of IOrbitalBody implementations.
/// </summary>
public partial class BodyEconomyManager : Node
{
    private readonly List<ContinentEconomy> _activeEconomies = new();
    private readonly List<StationEconomy> _activeStationEconomies = new();
    private double _totalTime;

    public int ActiveEconomyCount => _activeEconomies.Count;
    public int ActiveStationEconomyCount => _activeStationEconomies.Count;

    /// <summary>
    /// Gets all active continent economies. Use for read-only access (e.g., UI display).
    /// </summary>
    public IReadOnlyList<ContinentEconomy> GetActiveEconomies() => _activeEconomies;

    /// <summary>
    /// Gets all active station economies. Use for read-only access (e.g., UI display).
    /// </summary>
    public IReadOnlyList<StationEconomy> GetActiveStationEconomies() => _activeStationEconomies;

    /// <summary>
    /// Gets the total power generation across all continent economies.
    /// </summary>
    public float GetTotalPowerGeneration()
    {
        float total = 0f;
        foreach (var eco in _activeEconomies)
            total += eco.PowerGeneration;
        return total;
    }

    /// <summary>
    /// Gets the total power consumption across all continent economies.
    /// </summary>
    public float GetTotalPowerConsumption()
    {
        float total = 0f;
        foreach (var eco in _activeEconomies)
            total += eco.PowerConsumption;
        return total;
    }

    /// <summary>
    /// Gets the total number of buildings across all continent economies.
    /// </summary>
    public int GetTotalBuildingCount()
    {
        int total = 0;
        foreach (var eco in _activeEconomies)
            total += eco.ActiveBuildingCount;
        return total;
    }

    /// <summary>
    /// Gets the count of continents currently in power deficit.
    /// </summary>
    public int GetPowerDeficitCount()
    {
        int count = 0;
        foreach (var eco in _activeEconomies)
            if (eco.IsPowerDeficit)
                count++;
        return count;
    }

    /// <summary>
    /// Gets a summary of all active resource shortages across all economies.
    /// Returns a dictionary of resourceId -> count of economies with that shortage.
    /// </summary>
    public Dictionary<string, int> GetActiveShortageSummary()
    {
        var shortages = new Dictionary<string, int>();
        foreach (var eco in _activeEconomies)
        {
            // Note: This relies on the economy exposing shortage info
            // For now, we'll check stockpiles with negative net rates
            var netRates = eco.GetAllNetRates();
            var stockpiles = eco.GetAllStockpiles();
            foreach (var kvp in netRates)
            {
                float stockpile = stockpiles.TryGetValue(kvp.Key, out float s) ? s : 0f;
                if (stockpile <= 0f && kvp.Value < 0f)
                {
                    if (shortages.ContainsKey(kvp.Key))
                        shortages[kvp.Key]++;
                    else
                        shortages[kvp.Key] = 1;
                }
            }
        }
        return shortages;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _totalTime += delta;

        for (int i = 0; i < _activeEconomies.Count; i++)
        {
            _activeEconomies[i].Tick(dt, _totalTime);
        }

        for (int i = 0; i < _activeStationEconomies.Count; i++)
        {
            _activeStationEconomies[i].Tick(dt, _totalTime);
        }
    }

    public void RegisterEconomy(ContinentEconomy economy)
    {
        if (!_activeEconomies.Contains(economy))
        {
            _activeEconomies.Add(economy);
            GameLogger.Info(
                $"[BodyEconomyManager] Registered economy for continent {economy.Continent.StartingIndex} "
                    + $"(total: {_activeEconomies.Count})"
            );
        }
    }

    public void UnregisterEconomy(ContinentEconomy economy)
    {
        if (_activeEconomies.Remove(economy))
        {
            GameLogger.Debug(
                $"[BodyEconomyManager] Unregistered economy for continent {economy.Continent.StartingIndex} "
                    + $"(total: {_activeEconomies.Count})"
            );
        }
    }

    public void RegisterStationEconomy(StationEconomy economy)
    {
        if (!_activeStationEconomies.Contains(economy))
        {
            _activeStationEconomies.Add(economy);
            GameLogger.Debug(
                $"[BodyEconomyManager] Registered station economy for {economy.StationId} "
                    + $"(total stations: {_activeStationEconomies.Count})"
            );
        }
    }

    public void UnregisterStationEconomy(StationEconomy economy)
    {
        if (_activeStationEconomies.Remove(economy))
        {
            GameLogger.Debug(
                $"[BodyEconomyManager] Unregistered station economy for {economy.StationId} "
                    + $"(total stations: {_activeStationEconomies.Count})"
            );
        }
    }

    public void UnregisterAll()
    {
        _activeEconomies.Clear();
        _activeStationEconomies.Clear();
        GameLogger.Info("[BodyEconomyManager] Unregistered all economies");
    }
}
