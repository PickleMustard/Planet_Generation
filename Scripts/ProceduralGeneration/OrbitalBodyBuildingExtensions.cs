using System.Collections.Generic;
using Constructables;

namespace ProceduralGeneration;

public static class OrbitalBodyBuildingExtensions
{
    /// <summary>
    /// Every distinct <see cref="Building"/> present on the body — under
    /// construction (from <see cref="BuildingConstructionManager.GetActiveBuildings"/>)
    /// or completed (referenced from any continent cell). Zero-work buildings
    /// like the HQ are unregistered from the active list immediately after
    /// creation, so the cell scan is required to surface them.
    /// </summary>
    public static List<Building> GetAllBuildings(this IOrbitalBody body)
    {
        var seen = new HashSet<Building>();
        var result = new List<Building>();

        var mgr = body.BuildingConstructionMgr;
        if (mgr != null)
        {
            foreach (var b in mgr.GetActiveBuildings())
            {
                if (b != null && seen.Add(b))
                    result.Add(b);
            }
        }

        var continents = body.Mesh?.Continents;
        if (continents != null)
        {
            foreach (var kvp in continents)
            {
                var cells = kvp.Value?.cells;
                if (cells == null)
                    continue;
                foreach (var cell in cells)
                {
                    var b = cell?.Building;
                    if (b != null && seen.Add(b))
                        result.Add(b);
                }
            }
        }

        return result;
    }
}
