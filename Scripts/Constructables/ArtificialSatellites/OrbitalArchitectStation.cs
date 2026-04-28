using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// A station capable of constructing surface buildings via an orbital work beam system.
/// Registers its work budget with the parent body's BuildingConstructionManager
/// so that multiple architects on the same body contribute to a single aggregated pool.
/// </summary>
public partial class OrbitalArchitectStation : StationSatellite
{
    public override void SetStationDefinition(StationDefinition definition)
    {
        base.SetStationDefinition(definition);
        GameLogger.Info(
            $"OrbitalArchitectStation {Name}: Initialized with budget "
                + $"{definition.BuildingWorkBudgetPerTick}/tick, penalty {definition.BuildingScalingPenalty}"
        );
    }

    protected override void OnConstructionComplete()
    {
        RegisterWithBodyManager();
        base.OnConstructionComplete();
    }

    public override void _ExitTree()
    {
        _parentBody.BuildingConstructionMgr.UnregisterArchitect(this);
    }

    protected virtual void RegisterWithBodyManager()
    {
        // Station tree: CelestialBody -> SatellitesContainer -> this
        if (_parentBody?.BuildingConstructionMgr == null)
        {
            GameLogger.Warning(
                $"OrbitalArchitectStation {Name}: No BuildingConstructionManager found on parent body"
            );
            return;
        }

        _parentBody.BuildingConstructionMgr.RegisterArchitect(
            this,
            _stationDefinition?.BuildingWorkBudgetPerTick ?? 1.0f,
            _stationDefinition?.BuildingScalingPenalty ?? 0.05f,
            wasteFactor: 1.0f
        );
    }
}
