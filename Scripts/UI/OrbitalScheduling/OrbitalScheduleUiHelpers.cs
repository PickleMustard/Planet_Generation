using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using ProceduralGeneration;
using Structures.Logistics;

namespace UI.OrbitalScheduling;

/// <summary>
/// Shared helpers for the orbital-scheduling GUI: locating the unit's current
/// position, enumerating reachable orbital bodies that host stations, and
/// formatting endpoints / manifests for display.
/// </summary>
public static class OrbitalScheduleUiHelpers
{
    /// <summary>The nearest orbital body the unit is currently parented under, or null.</summary>
    public static IOrbitalBody? GetCurrentBody(LogisticsUnit unit)
    {
        Node? parent = unit.GetParent();
        while (parent != null)
        {
            if (parent is IOrbitalBody body)
                return body;
            parent = parent.GetParent();
        }
        return null;
    }

    /// <summary>
    /// The leg-1 origin: the unit's current location. If a station the unit was
    /// built at sits at that body, the endpoint includes it (enabling pickup/refuel);
    /// otherwise it is a bare-body endpoint.
    /// </summary>
    public static LegEndpoint? GetUnitLocationEndpoint(LogisticsUnit unit)
    {
        var body = GetCurrentBody(unit);
        if (body == null)
            return null;

        var home = unit.ConstructingStation;
        if (home != null && ReferenceEquals(home.ParentBody, body) && !home.IsUnderConstruction)
            return LegEndpoint.ForStation(home, body, home.BandIndex);

        return LegEndpoint.ForBody(body, unit.BandIndex);
    }

    /// <summary>
    /// Reachable orbital bodies that host at least one completed station, suitable
    /// as leg destinations. Falls back to all reachable bodies when none host a
    /// station, so the board is still browsable.
    /// </summary>
    public static List<IOrbitalBody> GetSelectableBodies(LogisticsUnit unit)
    {
        var all = GetAllBodies(unit);
        var withStations = all.Where(HasCompletedStation).ToList();
        return withStations.Count > 0 ? withStations : all;
    }

    public static List<IOrbitalBody> GetAllBodies(LogisticsUnit unit)
        => GetAllBodies(FindSystemContainer(unit));

    /// <summary>
    /// Enumerates every orbital body (and its satellites, recursively) under the
    /// given system container. Returns an empty list when <paramref name="container"/>
    /// is null.
    /// </summary>
    public static List<IOrbitalBody> GetAllBodies(Node? container)
    {
        var result = new List<IOrbitalBody>();
        if (container == null)
            return result;

        // The barycenter is a child of the system container and implements
        // IOrbitalBody, but it is only a gravitational placeholder — its
        // IOrbitalBody members (SatellitesContainer, etc.) are throwing stubs.
        // Skip it so it is never enumerated as a real body.
        foreach (var child in container.GetChildren())
            if (child is IOrbitalBody body and not Barycenter)
                CollectBodyAndSatellites(body, result);

        return result;
    }

    public static bool HasCompletedStation(IOrbitalBody body)
    {
        var container = body.SatellitesContainer;
        if (container == null)
            return false;
        foreach (var child in container.GetChildren())
            if (child is StationSatellite st && !st.IsUnderConstruction)
                return true;
        return false;
    }

    public static string EndpointName(LegEndpoint? endpoint)
    {
        if (endpoint == null)
            return "—";
        if (endpoint.Station != null)
            return string.IsNullOrEmpty(endpoint.Station.Name) ? "Station" : endpoint.Station.Name;
        return endpoint.Body?.BodyName ?? "Orbit";
    }

    public static string BodyName(IOrbitalBody? body) => body?.BodyName ?? "—";

    public static string ManifestSummary(Leg leg)
    {
        int pickup = leg.PickupOrder?.TotalUnits ?? 0;
        int dropoff = leg.DropoffOrder?.TotalUnits ?? 0;
        if (pickup == 0 && dropoff == 0)
            return "no cargo";
        var parts = new List<string>();
        if (pickup > 0) parts.Add($"load {pickup}u");
        if (dropoff > 0) parts.Add($"unload {dropoff}u");
        return string.Join(", ", parts);
    }

    public static string FuelSummary(Leg leg)
    {
        if (leg.RefuelInstructions.Policy == Structures.Enums.RefuelPolicy.None)
            return "no refuel";
        string amount = leg.RefuelInstructions.Amount >= float.MaxValue
            ? "fill"
            : $"{leg.RefuelInstructions.Amount:0} kg";
        return $"{leg.RefuelInstructions.Policy} ({amount})";
    }

    public static string TimingSummary(Leg leg)
    {
        var c = leg.DepartureConstraints;
        string unit = c.BudgetMode == Structures.Enums.ExpenditureBudgetMode.TimeOfFlight ? "s" : "m/s";
        string min = c.MinBudget.HasValue ? $"{c.MinBudget.Value:0}" : "—";
        string max = c.MaxBudget.HasValue ? $"{c.MaxBudget.Value:0}" : "—";
        string wait = leg.MaxWaitSeconds.HasValue ? $", wait≤{leg.MaxWaitSeconds.Value:0}s" : "";
        return $"{min}–{max} {unit}{wait}";
    }

    private static void CollectBodyAndSatellites(IOrbitalBody body, List<IOrbitalBody> into)
    {
        if (!into.Contains(body))
            into.Add(body);

        var container = body.SatellitesContainer;
        if (container == null)
            return;
        foreach (var child in container.GetChildren())
            if (child is IOrbitalBody sat)
                CollectBodyAndSatellites(sat, into);
    }

    public static Node? FindSystemContainer(Node node)
    {
        Node? n = node;
        while (n != null)
        {
            if (n.Name == "system_container")
                return n;
            n = n.GetParent();
        }
        return null;
    }
}
