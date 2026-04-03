using System.Collections.Generic;
using Godot;
using Structures.Resources;

namespace ProceduralGeneration.MeshGeneration.ResourceGeneration;

/// <summary>
/// Generates resources for satellite bodies (asteroids, moons, etc.)
/// using group-based eligibility against satellite subtype configurations.
/// </summary>
public static class SatelliteResourceGenerator
{
    /// <summary>
    /// Generates resource deposits for a satellite body using the group-based system.
    /// </summary>
    /// <param name="planetaryResourceConfig">Planetary resource configuration with resolved resource sets.</param>
    /// <param name="satelliteSubtype">The satellite subtype enum value.</param>
    /// <param name="rng">Random number generator for deterministic generation.</param>
    /// <param name="maxResources">Maximum number of resources to generate.</param>
    /// <returns>Dictionary mapping resource IDs to ResourceDeposits.</returns>
    public static Dictionary<string, ResourceDeposit> GenerateResources(
        PlanetaryResourceConfig planetaryResourceConfig,
        object? satelliteSubtype,
        RandomNumberGenerator rng,
        int maxResources = 3)
    {
        return CellResourceGenerator.GenerateSatelliteResources(
            planetaryResourceConfig, satelliteSubtype, rng, maxResources);
    }
}
