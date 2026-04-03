using System.Collections.Generic;
using Godot;
using UtilityLibrary;

namespace Constructables.ArtificialSatellites;

/// <summary>
/// A station capable of constructing surface buildings via an orbital work beam system.
/// Can construct unlimited buildings simultaneously, but with a shared work budget
/// that diminishes as more buildings are constructed concurrently.
/// </summary>
public partial class OrbitalArchitectStation : StationSatellite
{
    private BuildingWorkBudget? _buildingWorkBudget;

    public override void SetStationDefinition(StationDefinition definition)
    {
        base.SetStationDefinition(definition);
        _buildingWorkBudget = new BuildingWorkBudget(
            this,
            definition.BuildingWorkBudgetPerTick,
            definition.BuildingScalingPenalty
        );
        GameLogger.Info($"OrbitalArchitectStation {Name}: Initialized with budget {definition.BuildingWorkBudgetPerTick}/tick, penalty {definition.BuildingScalingPenalty}");
    }

    protected override void TickOperational(float delta)
    {
        base.TickOperational(delta);
        _buildingWorkBudget?.Tick(delta);
    }

    /// <summary>
    /// Registers a building to be constructed under this station's work budget.
    /// </summary>
    public void RegisterBuildingConstruction(BuildingConstruction building)
    {
        if (_buildingWorkBudget == null)
        {
            GameLogger.Warning($"OrbitalArchitectStation {Name}: Cannot register building - no work budget initialized");
            return;
        }

        building.ConstructingStation = this;
        _buildingWorkBudget.Register(building);
    }

    /// <summary>
    /// Unregisters a building from this station's work budget (on completion or cancellation).
    /// </summary>
    public void UnregisterBuildingConstruction(BuildingConstruction building)
    {
        _buildingWorkBudget?.Unregister(building);
        if (building.ConstructingStation == this)
            building.ConstructingStation = null;
    }

    /// <summary>
    /// Returns the list of buildings currently being constructed by this station.
    /// </summary>
    public IReadOnlyList<BuildingConstruction> GetActiveBuildings()
    {
        return _buildingWorkBudget?.GetActiveBuildings()
            ?? (IReadOnlyList<BuildingConstruction>)new List<BuildingConstruction>();
    }
}
