using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;

namespace UI.Components;

/// <summary>
/// Shared read-only accessors for presenting <see cref="LogisticsUnit"/> data in
/// the fleet GUI. Used by the overview panel, the details cartouche, and the
/// details info panel so the formatting stays consistent across windows.
/// </summary>
public static class LogisticsUnitDataHelpers
{
    /// <summary>True while the unit is still being built by a shipyard.</summary>
    public static bool IsUnderConstruction(LogisticsUnit unit) =>
        unit.ConstructingStation != null && unit.ConstructingStation.IsUnderConstruction;

    /// <summary>Walks the scene-tree parent chain to the owning celestial body.</summary>
    public static CelestialBody? GetParentBody(LogisticsUnit unit)
    {
        Node? current = unit.GetParent();
        while (current != null)
        {
            if (current is CelestialBody body)
                return body;
            current = current.GetParent();
        }
        return null;
    }

    /// <summary>Parent body name, or "In-Transit" when the unit has no celestial parent.</summary>
    public static string GetParentBodyName(LogisticsUnit unit)
    {
        var body = GetParentBody(unit);
        return body != null ? body.BodyName : "In-Transit";
    }

    /// <summary>The schedule the unit is currently executing, or null.</summary>
    public static OrbitalTransferSchedule? GetSchedule(LogisticsUnit unit) =>
        unit.ScheduleExecutor?.ActiveSchedule;

    /// <summary>The next / in-progress leg, or null when no active schedule.</summary>
    public static Leg? GetCurrentLeg(LogisticsUnit unit) => GetSchedule(unit)?.CurrentLeg;

    /// <summary>Display name for a leg endpoint (station name preferred, else body).</summary>
    public static string EndpointName(LegEndpoint? endpoint)
    {
        if (endpoint == null)
            return "—";
        if (endpoint.Station != null)
            return endpoint.Station.Name;
        return endpoint.Body?.BodyName ?? "—";
    }

    /// <summary>Destination of the current leg, or "—" when none.</summary>
    public static string GetDestinationName(LogisticsUnit unit)
    {
        var leg = GetCurrentLeg(unit);
        return leg != null ? EndpointName(leg.Destination) : "—";
    }

    /// <summary>Short transit-status string suitable for a list/summary cell.</summary>
    public static string GetTransitStatus(LogisticsUnit unit)
    {
        if (IsUnderConstruction(unit))
            return "Building";

        var mc = unit.MovementController;
        if (mc != null && mc.IsTransferring)
            return $"In-Transit {mc.TransferProgress * 100f:F0}%";

        return unit.State.ToString();
    }

    /// <summary>Top-N cargo entries by quantity (descending), excluding empties.</summary>
    public static List<KeyValuePair<string, int>> GetTopCargo(LogisticsUnit unit, int count)
    {
        var cargo = unit.Cargo;
        if (cargo == null || cargo.Resources.Count == 0)
            return new List<KeyValuePair<string, int>>();

        return cargo.Resources
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .Take(count)
            .ToList();
    }
}
