using System.Collections.Generic;
using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Singleton autoload that ticks all active ContinentEconomy instances each physics frame.
/// Follows the same pattern as ConstructionManager.
/// </summary>
public partial class EconomyManager : Node
{
    private static EconomyManager? _instance;
    public static EconomyManager? Instance => _instance;

    private readonly List<ContinentEconomy> _activeEconomies = new();
    private readonly List<StationEconomy> _activeStationEconomies = new();
    private double _totalTime;

    public int ActiveEconomyCount => _activeEconomies.Count;
    public int ActiveStationEconomyCount => _activeStationEconomies.Count;

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            QueueFree();
            return;
        }

        _instance = this;
        GameLogger.Info("[EconomyManager] Initialized");
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
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
            GameLogger.Debug(
                $"[EconomyManager] Registered economy for continent {economy.Continent.StartingIndex} " +
                $"(total: {_activeEconomies.Count})"
            );
        }
    }

    public void UnregisterEconomy(ContinentEconomy economy)
    {
        if (_activeEconomies.Remove(economy))
        {
            GameLogger.Debug(
                $"[EconomyManager] Unregistered economy for continent {economy.Continent.StartingIndex} " +
                $"(total: {_activeEconomies.Count})"
            );
        }
    }

    public void RegisterStationEconomy(StationEconomy economy)
    {
        if (!_activeStationEconomies.Contains(economy))
        {
            _activeStationEconomies.Add(economy);
            GameLogger.Debug(
                $"[EconomyManager] Registered station economy for {economy.StationId} " +
                $"(total stations: {_activeStationEconomies.Count})"
            );
        }
    }

    public void UnregisterStationEconomy(StationEconomy economy)
    {
        if (_activeStationEconomies.Remove(economy))
        {
            GameLogger.Debug(
                $"[EconomyManager] Unregistered station economy for {economy.StationId} " +
                $"(total stations: {_activeStationEconomies.Count})"
            );
        }
    }

    public void UnregisterAll()
    {
        _activeEconomies.Clear();
        _activeStationEconomies.Clear();
        GameLogger.Info("[EconomyManager] Unregistered all economies");
    }
}
