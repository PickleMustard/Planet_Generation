#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UI.Debug;
using ProceduralGeneration;
using ProceduralGeneration.PlanetGeneration;
using Constructables.ArtificialSatellites;
using Structures.GameState;
using UtilityLibrary;

namespace UI.Debug.Console;

/// <summary>
/// Global debug commands for logistics operations.
/// These commands don't require a target instance.
/// </summary>
public static class LogisticsCommands
{
    // Cached ship names loaded from satellites.yml
    private static string[]? _shipNames;
    private static string[]? _shipAdjectives;
    private static bool _namesLoaded = false;

    /// <summary>
    /// Generates a random ship name from the satellites.yml name list.
    /// </summary>
    private static string GenerateRandomShipName()
    {
        LoadShipNamesIfNeeded();

        if (_shipNames == null || _shipNames.Length == 0)
        {
            return $"Ship_{DateTime.Now.Ticks % 10000}";
        }

        var rand = Randomizer.GetRandomNumberGenerator();

        // 30% chance: adjective + name (e.g., "Iron-Veined Voyager")
        // 70% chance: just a name
        if (_shipAdjectives != null && _shipAdjectives.Length > 0 && rand.Randf() < 0.3f)
        {
            string adj = _shipAdjectives[rand.Randi() % _shipAdjectives.Length];
            string name = _shipNames[rand.Randi() % _shipNames.Length];
            // Convert adjective to title case and replace spaces with hyphens
            adj = ToTitleCase(adj).Replace(" ", "-");
            return $"{adj} {name}";
        }

        return _shipNames[rand.Randi() % _shipNames.Length];
    }

    private static string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }

    private static void LoadShipNamesIfNeeded()
    {
        if (_namesLoaded) return;

        try
        {
            var namesData = TemplateLoader.LoadNamesFile("satellites");

            var allNames = new List<string>();
            var allAdjectives = new List<string>();

            // Helper to extract string array from variant
            void ExtractStrings(Godot.Collections.Dictionary data, string key, List<string> target)
            {
                if (data.TryGetValue(key, out var variant))
                {
                    var arr = variant.As<Godot.Collections.Array>();
                    if (arr != null)
                    {
                        foreach (var item in arr)
                        {
                            var str = item.AsString();
                            if (!string.IsNullOrEmpty(str))
                            {
                                target.Add(str);
                            }
                        }
                    }
                }
            }

            // Load mythology names
            ExtractStrings(namesData, "mythology", allNames);

            // Load scientists
            ExtractStrings(namesData, "scientists", allNames);

            // Load explorers
            ExtractStrings(namesData, "explorers", allNames);

            // Load adjectives
            ExtractStrings(namesData, "adjectives", allAdjectives);

            _shipNames = allNames.ToArray();
            _shipAdjectives = allAdjectives.ToArray();
            _namesLoaded = true;

            GD.Print($"[LogisticsCommands] Loaded {_shipNames.Length} ship names and {_shipAdjectives.Length} adjectives");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[LogisticsCommands] Failed to load ship names: {e.Message}");
            _shipNames = Array.Empty<string>();
            _shipAdjectives = Array.Empty<string>();
            _namesLoaded = true;
        }
    }
    [DebugCommand(
        "spawn_ship",
        "Spawn a new logistics ship in an orbit band",
        "spawn_ship [name] [band_index] [dry_mass]",
        Category = "Logistics"
    )]
    public static int SpawnShip(CommandContext ctx, string[] args)
    {
        // Generate random name if not provided
        string name = args.Length > 0 ? args[0] : GenerateRandomShipName();
        int? bandIndex = null;
        float? dryMass = null;

        if (args.Length > 1)
        {
            if (int.TryParse(args[1], out var parsed))
            {
                bandIndex = parsed;
            }
            else
            {
                ctx.WriteError($"Invalid band index: '{args[1]}'. Must be an integer.");
                return 1;
            }
        }

        // Parse optional dry mass parameter
        if (args.Length > 2)
        {
            if (float.TryParse(args[2], out var parsedDryMass))
            {
                dryMass = parsedDryMass;
            }
            else
            {
                ctx.WriteError($"Invalid dry mass: '{args[2]}'. Must be a number.");
                return 1;
            }
        }

        // Find a celestial body to spawn the ship on
        var body = FindFirstCelestialBody();
        if (body == null)
        {
            ctx.WriteError("No celestial body found. Please spawn a planet first.");
            return 1;
        }

        try
        {
            // Create a LogisticsUnit directly (not a StationSatellite)
            var ship = new LogisticsUnit { Name = name };

            int targetBand = bandIndex ?? FindAvailableBand(body);
            if (targetBand < 0)
            {
                ctx.WriteError($"Failed to create ship '{name}'. No available orbit bands.");
                return 1;
            }

            // Set dry mass if provided
            if (dryMass.HasValue)
            {
                ship.SetDryMass(dryMass.Value);
            }

            body.SatellitesContainer.AddChild(ship);
            ship.Initialize(body, targetBand);
            ship.InitializeCargo();
            ship.SetFuelCapacity(1000f);

            // Note: ship auto-registers via RegisterWithDebug() called in Initialize()
            // Get the namespace after registration
            string? ns = InstanceRegistry.GetNamespace(ship);

            ctx.WriteLine($"[color=green]Ship '{ship.Name}' created in orbit around {body.Name}[/color]");
            if (!string.IsNullOrEmpty(ns))
            {
                ctx.WriteLine($"Namespace: {ns}");
            }
            ctx.WriteLine($"Band: {targetBand}, Fuel capacity: {ship.MaxFuel}");

            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to create ship: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Finds an available orbit band on a celestial body.
    /// </summary>
    private static int FindAvailableBand(CelestialBody body)
    {
        for (int i = 0; i < body.OrbitBands.Count; i++)
        {
            if (body.CanAddToBand(i))
                return i;
        }
        return -1;
    }

    [DebugCommand(
        "spawn_station",
        "Spawn a station in a specific orbit band",
        "spawn_station <band_index> [name]",
        Category = "Logistics",
        RequiresTarget = true
    )]
    public static int SpawnStationOnTarget(CommandContext ctx, string[] args)
    {
        if (ctx.TargetInstance == null)
        {
            ctx.WriteError("No target selected. Use 'CelestialBody.0 spawn_station 0 MyStation'");
            return 1;
        }

        if (args.Length < 1)
        {
            ctx.WriteError("Usage: spawn_station <band_index> [name]");
            return 1;
        }

        if (!int.TryParse(args[0], out var bandIndex))
        {
            ctx.WriteError($"Invalid band index: '{args[0]}'. Must be an integer.");
            return 1;
        }

        string name = args.Length > 1 ? args[1] : $"Station_{DateTime.Now.Ticks % 10000}";

        if (ctx.TargetInstance is not CelestialBody body)
        {
            ctx.WriteError($"Target '{ctx.TargetInstance.GetType().Name}' is not a CelestialBody.");
            return 1;
        }

        try
        {
            var station = body.CreateStation(bandIndex, name);
            if (station == null)
            {
                ctx.WriteError($"Failed to create station. Band {bandIndex} may be full or invalid.");
                return 1;
            }

            // Register under Constructables.Stations namespace
            string ns = InstanceRegistry.RegisterStation(station);

            ctx.WriteLine($"[color=green]Station '{name}' created in band {bandIndex} around {body.Name}[/color]");
            ctx.WriteLine($"Namespace: {ns}");

            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to create station: {ex.Message}");
            return 1;
        }
    }

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
                        ctx.WriteLine($"  {ns}: {instance.GetType().Name} (Parent: {parent}, State: {state})");
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
                        string parent = instance is Node3D node ? node.GetParent()?.Name ?? "Unknown" : "Unknown";
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
        "list_bands <body_namespace>",
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
                string status = canAdd ? "[color=green]Available[/color]" : "[color=red]Full[/color]";

                ctx.WriteLine($"  Band {i}: Radius={band.Radius:F1}, Capacity={band.Capacity}, " +
                            $"Current={current}, {status}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to get orbit bands: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Finds the first celestial body in the instance registry.
    /// </summary>
    private static CelestialBody? FindFirstCelestialBody()
    {
        var bodies = InstanceRegistry.GetNamespaces<CelestialBody>().ToList();

        // Also search all instances for any CelestialBody
        foreach (var instance in InstanceRegistry.GetAllInstances())
        {
            if (instance is CelestialBody body)
            {
                return body;
            }
        }

        return null;
    }
}
#endif
