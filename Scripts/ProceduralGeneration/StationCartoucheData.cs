using System.Collections.Generic;
using System.Text;
using Constructables;
using Constructables.Stations;
using Constructables.Stations.Behaviors;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;

namespace ProceduralGeneration;

/// <summary>
/// Pulls display data for the station-overview cartouche from a
/// <see cref="StationSatellite"/>. Mirrors <see cref="PlanetCartoucheData"/> for stations,
/// using attached behaviors to derive classification and capacity figures.
/// </summary>
public static class StationCartoucheData
{
    private const string Placeholder = "—";

    /// <summary>"Shipyard · Refinery" or "Storage Depot" — top-priority behavior name plus any
    /// secondary behavior badges. Returns a stable string even when the station has no behaviors.</summary>
    public static string GetClassification(StationSatellite station)
    {
        if (station.Behaviors.Count == 0)
            return station.StationType ?? "Station";

        var ordered = SortedByPriorityDesc(station);
        if (ordered.Count == 0)
            return station.StationType ?? "Station";

        var sb = new StringBuilder();
        sb.Append(BehaviorDisplayName(ordered[0]));
        for (int i = 1; i < ordered.Count; i++)
        {
            sb.Append(" · ");
            sb.Append(BehaviorDisplayName(ordered[i]));
        }
        return sb.ToString();
    }

    /// <summary>"BY THE SURVEYOR GENERAL · KP-04" pulled from the parent body's barycenter,
    /// suffixed with the station id stub.</summary>
    public static string GetDesignation(StationSatellite station)
    {
        string sector = ResolveSector(station);
        string idStub = string.IsNullOrEmpty(station.Id)
            ? Placeholder
            : station.Id.Length >= 6 ? station.Id[..6].ToUpper() : station.Id.ToUpper();
        return string.IsNullOrEmpty(sector)
            ? $"BY THE SURVEYOR GENERAL · {idStub}"
            : $"BY THE SURVEYOR GENERAL · {sector} · {idStub}";
    }

    /// <summary>
    /// Returns the cartouche stat-grid rows in display order. Missing data renders as an em-dash.
    /// </summary>
    public static IReadOnlyList<CartoucheStat> GetStatRows(StationSatellite station)
    {
        var rows = new List<CartoucheStat>(8);

        rows.Add(new CartoucheStat("CLASS", station.StationType ?? Placeholder));
        rows.Add(new CartoucheStat("PARENT", ResolveParentName(station)));
        rows.Add(new CartoucheStat("BAND", station.BandIndex >= 0 ? station.BandIndex.ToString() : Placeholder));
        rows.Add(new CartoucheStat("STORAGE", ResolveStorageFill(station)));
        rows.Add(new CartoucheStat("BEHAVIORS", station.Behaviors.Count.ToString()));
        rows.Add(new CartoucheStat("STATUS", ResolveStatus(station)));
        rows.Add(new CartoucheStat("RADIUS", $"{station.OrbitalRadius:F1}"));
        rows.Add(new CartoucheStat("SPEED", $"{station.OrbitalSpeed:F4} r/s"));

        return rows;
    }

    private static string BehaviorDisplayName(IStationBehavior behavior) => behavior switch
    {
        ShipyardBehavior => "Shipyard",
        OrbitalConstructorBehavior => "Orbital Architect",
        TransferHubBehavior => "Refinery",
        StorageHubBehavior => "Storage Depot",
        _ => behavior.GetType().Name,
    };

    private static List<IStationBehavior> SortedByPriorityDesc(StationSatellite station)
    {
        var list = new List<IStationBehavior>(station.Behaviors);
        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return list;
    }

    private static string ResolveParentName(StationSatellite station)
    {
        if (station.ParentBody is Node3D node)
            return node.Name;
        return Placeholder;
    }

    private static string ResolveSector(StationSatellite station)
    {
        Node? cursor = station.GetParentOrNull<Node>();
        while (cursor != null)
        {
            if (cursor is Barycenter b)
                return b.SectorId ?? "";
            cursor = cursor.GetParentOrNull<Node>();
        }
        return "";
    }

    private static string ResolveStatus(StationSatellite station)
    {
        if (station.IsUnderConstruction)
            return $"Building {station.GetProgress() * 100:F0}%";
        return station.IsActive ? "Active" : "Inactive";
    }

    private static string ResolveStorageFill(StationSatellite station)
    {
        var slots = station.BulkStorage.Slots;
        if (slots.Count == 0)
            return Placeholder;

        float used = 0f, capacity = 0f;
        foreach (var slot in slots)
        {
            used += slot.Quantity;
            capacity += slot.Capacity;
        }
        if (capacity <= 0f)
            return Placeholder;
        return $"{Mathf.RoundToInt(used)} / {Mathf.RoundToInt(capacity)}";
    }
}
