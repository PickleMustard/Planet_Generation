using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Special headquarters building for game start. Provides all essential services.
/// </summary>
public partial class HeadquartersBuilding : BuildingConstruction
{
    public override void _Ready()
    {
        base._Ready();

        BuildingDatabase.Instance?.MarkGloballyPlaced("company_headquarters");
    }

    /// <summary>
    /// Initializes headquarters using data from BuildingDefinition YAML.
    /// Called from ConstructionManager.FinalizeHeadquarters once the player
    /// confirms the game-start naming dialogue.
    /// </summary>
    public void InitializeHeadquarters(CelestialBody parentBody, int continentIndex)
    {
        if (parentBody?.Mesh?.Continents == null)
            return;

        if (!parentBody.Mesh.Continents.TryGetValue(continentIndex, out var continent))
            return;

        continent.InitializeEconomy();

        if (continent.Economy == null)
            return;

        foreach (var kvp in Definition?.StartingStorageCapacity ?? new Dictionary<string, float>())
        {
            continent.Economy.AddStorageCapacity(kvp.Key, kvp.Value, this);
        }

        foreach (var kvp in Definition?.StartingStockpiles ?? new Dictionary<string, int>())
        {
            continent.Economy.DepositResource(kvp.Key, kvp.Value);
        }

        GameLogger.Info($"[HeadquartersBuilding] Initialized on {parentBody.Name}");
    }

    public override bool CanDemolish() => false;
}
