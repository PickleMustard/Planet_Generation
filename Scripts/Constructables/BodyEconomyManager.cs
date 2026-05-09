using System.Collections.Generic;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace Constructables;

/// <summary>
/// Per-body registry of active ContinentEconomy and StationEconomy instances. Used by UI
/// (HUD totals, body summaries) and resource transfers. Ticking is driven by the
/// ManufactureTickEngine on a dedicated 60Hz thread — this class no longer drives ticks
/// from _PhysicsProcess.
/// Added as a child Node of IOrbitalBody implementations.
///
/// Power totals delegate to the parent body's <see cref="Power.BodyPowerGridManager"/>;
/// power is no longer body-wide but per-grid, so these accessors aggregate over all grids
/// on the parent.
/// </summary>
public partial class BodyEconomyManager : Node
{
    private CelestialBody? GetParentBody() => GetParent() as CelestialBody;

    /// <summary>
    /// Sums <see cref="Power.PowerGrid.LastGeneration"/> across every grid on the parent body.
    /// </summary>
    public float GetTotalPowerGeneration()
    {
        var pg = GetParentBody()?.PowerGridMgr;
        if (pg == null)
            return 0f;
        float total = 0f;
        foreach (var grid in pg.Grids)
            total += grid.LastGeneration;
        return total;
    }

    /// <summary>
    /// Sums <see cref="Power.PowerGrid.LastDraw"/> across every grid on the parent body.
    /// </summary>
    public float GetTotalPowerConsumption()
    {
        var pg = GetParentBody()?.PowerGridMgr;
        if (pg == null)
            return 0f;
        float total = 0f;
        foreach (var grid in pg.Grids)
            total += grid.LastDraw;
        return total;
    }

    public int GetTotalBuildingCount()
    {
        var body = GetParentBody();
        var continents = body?.Mesh?.Continents;
        if (continents == null)
            return 0;

        int count = 0;
        foreach (var kvp in continents)
        {
            foreach (var cell in kvp.Value.cells)
            {
                if (cell.Building != null)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Sums a single resource across every building's input + output storage on this body.
    /// </summary>
    public float GetTotalQuantity(string resourceId)
    {
        var body = GetParentBody();
        var continents = body?.Mesh?.Continents;
        if (continents == null || string.IsNullOrEmpty(resourceId))
            return 0f;

        float total = 0f;
        var seen = new HashSet<Building>();
        foreach (var kvp in continents)
        {
            foreach (var cell in kvp.Value.cells)
            {
                var b = cell.Building;
                if (b == null || !seen.Add(b))
                    continue;
                total += b.InputStorage.GetQuantity(resourceId);
                total += b.OutputStorage.GetQuantity(resourceId);
            }
        }
        return total;
    }

    /// <summary>
    /// Counts grids that are currently in brownout.
    /// </summary>
    public int GetPowerDeficitCount()
    {
        var pg = GetParentBody()?.PowerGridMgr;
        if (pg == null)
            return 0;
        int count = 0;
        foreach (var grid in pg.Grids)
            if (grid.IsBrownedOut)
                count++;
        return count;
    }

    public Dictionary<string, int> GetActiveShortageSummary()
    {
        //TODO: Implement shortage summary queries; should query the list of buildings that are stuck in resource input shortages
        return new Dictionary<string, int>();
    }
}
