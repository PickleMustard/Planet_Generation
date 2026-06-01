#if DEBUG
using System;
using System.Linq;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Constructables;
using UtilityLibrary.NameGeneration;

namespace Debug.Console;

/// <summary>
/// Global debug commands for logistics operations.
/// These commands don't require a target instance.
/// </summary>
public static class LogisticsCommands
{
    /// <summary>
    /// Generates a random ship name from the satellites.yml name list.
    /// </summary>
    internal static string GenerateRandomShipName()
    {
        return NameGenerator.GenerateShipName();
    }

    // NOTE: spawn_station was removed from LogisticsCommands to resolve a duplicate
    // command name conflict with ConstructionManagerDebug.SpawnStationCommand.
    // The instance version on ConstructionManagerDebug is the canonical one.
    // Use: (CelestialBody.Earth) spawn_station 0 MyStation

    [DebugCommand(
        "list_constructables",
        "List all ships and stations, optionally filtered by type",
        "list_constructables [type]",
        Category = "Logistics"
    )]
    public static int ListConstructables(CommandContext ctx, string[] args)
    {
        string? filter = args.Length > 0 ? args[0].ToLowerInvariant() : null;

        if (filter != null && filter != "ships" && filter != "stations" && filter != "all")
        {
            ctx.WriteError("Invalid filter. Use: ships, stations, or all");
            return 1;
        }

        bool showShips = filter == null || filter == "all" || filter == "ships";
        bool showStations = filter == null || filter == "all" || filter == "stations";

        ctx.WriteLine("[color=yellow]=== Constructables ===[/color]");

        int total = 0;

        if (showShips)
        {
            var shipNamespaces = InstanceRegistry.GetAllShipNamespaces().ToList();
            if (shipNamespaces.Count > 0)
            {
                ctx.WriteLine($"\n[color=cyan]Ships ({shipNamespaces.Count}):[/color]");
                foreach (var ns in shipNamespaces)
                {
                    if (InstanceRegistry.TryGetInstance(ns, out var instance) && instance != null)
                    {
                        var ship = instance as LogisticsUnit;
                        string parent = ship?.GetParent()?.Name ?? "Unknown";
                        string state = ship?.State.ToString() ?? "N/A";
                        ctx.WriteLine(
                            $"  {ns}: {instance.GetType().Name} (Parent: {parent}, State: {state})"
                        );
                    }
                }
                total += shipNamespaces.Count;
            }
            else
            {
                ctx.WriteLine("\n[color=cyan]Ships:[/color] None");
            }
        }

        if (showStations)
        {
            var stationNamespaces = InstanceRegistry.GetAllStationNamespaces().ToList();
            if (stationNamespaces.Count > 0)
            {
                ctx.WriteLine($"\n[color=cyan]Stations ({stationNamespaces.Count}):[/color]");
                foreach (var ns in stationNamespaces)
                {
                    if (InstanceRegistry.TryGetInstance(ns, out var instance) && instance != null)
                    {
                        string parent = instance is Node3D node
                            ? node.GetParent()?.Name ?? "Unknown"
                            : "Unknown";
                        ctx.WriteLine($"  {ns}: {instance.GetType().Name} (Parent: {parent})");
                    }
                }
                total += stationNamespaces.Count;
            }
            else
            {
                ctx.WriteLine("\n[color=cyan]Stations:[/color] None");
            }
        }

        ctx.WriteLine($"\n[color=yellow]Total: {total}[/color]");

        return 0;
    }

    [DebugCommand(
        "list_bands",
        "List orbit bands for a celestial body",
        "list_bands [body_namespace]",
        Category = "Logistics"
    )]
    public static int ListOrbitBands(CommandContext ctx, string[] args)
    {
        string? ns = args.Length > 0 ? args[0] : null;

        if (string.IsNullOrEmpty(ns))
        {
            // Try to find first celestial body
            var bodies = InstanceRegistry.GetNamespaces<CelestialBody>().ToList();
            if (bodies.Count == 0)
            {
                ctx.WriteError("No celestial bodies found.");
                return 1;
            }
            ns = bodies.First();
        }

        if (!InstanceRegistry.TryGetInstance(ns, out var instance))
        {
            ctx.WriteError($"Instance not found: {ns}");
            return 1;
        }

        if (instance is not CelestialBody body)
        {
            ctx.WriteError($"'{ns}' is not a CelestialBody.");
            return 1;
        }

        try
        {
            var orbitBands = body.OrbitBands;
            if (orbitBands == null || orbitBands.Count == 0)
            {
                ctx.WriteLine($"[color=yellow]{body.Name} has no orbit bands configured.[/color]");
                return 0;
            }

            ctx.WriteLine($"[color=yellow]=== Orbit Bands for {body.Name} ===[/color]");

            for (int i = 0; i < orbitBands.Count; i++)
            {
                var band = orbitBands[i];
                int current = body.GetBandSatelliteCount(i);
                bool canAdd = body.CanAddToBand(i);
                string status = canAdd
                    ? "[color=green]Available[/color]"
                    : "[color=red]Full[/color]";

                ctx.WriteLine(
                    $"  Band {i}: Radius={band.Radius:F1}, Capacity={band.Capacity}, "
                        + $"Current={current}, {status}"
                );
            }

            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to get orbit bands: {ex.Message}");
            return 1;
        }
    }
}
#endif
