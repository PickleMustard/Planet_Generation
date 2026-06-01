#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Constructables.Stations.Behaviors;
using Logistics.Resources;
using ProceduralGeneration.PlanetGeneration;
using Structures.Logistics;
using Debug;
using Debug.Console;
using Debug.DatabaseViewer;
using UtilityLibrary;
using UtilityLibrary.NameGeneration;

namespace Constructables;

public partial class ConstructionManager : IDebugDataProvider
{
    /// <summary>
    /// Debug command to spawn a station satellite in an orbit band.
    /// When --template is provided, spawns in construction mode.
    /// </summary>
    [DebugCommand(
        "spawn_station",
        "Spawn a station satellite in an orbit band",
        "(namespace) spawn_station <band_index> [name] [--template <template_name>]",
        Category = "Modification",
        RequiresTarget = true
    )]
    public int SpawnStationCommand(CommandContext ctx, string[] args)
    {
        if (ctx.TargetInstance is not CelestialBody body)
        {
            ctx.WriteError("Target must be a CelestialBody.");
            return 1;
        }
        if (args.Length < 1)
        {
            ctx.WriteError("Usage: (CelestialBody.Name) spawn_station <band_index> [name] [--template <template_name>]");
            return 1;
        }

        // Parse --template flag
        string? templateName = null;
        var cleanArgs = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--template" && i + 1 < args.Length)
            {
                templateName = args[++i];
            }
            else
            {
                cleanArgs.Add(args[i]);
            }
        }

        if (!int.TryParse(cleanArgs[0], out var bandIndex))
        {
            ctx.WriteError($"Invalid band index: '{cleanArgs[0]}'. Must be an integer.");
            return 1;
        }

        var bandCount = body.GetBandCount();
        if (bandCount == 0)
        {
            ctx.WriteError("This body has no orbit bands configured.");
            return 1;
        }

        if (bandIndex < 0)
        {
            ctx.WriteError($"Invalid band index: {bandIndex}. Valid range: 0-{bandCount - 1}");
            return 1;
        }

        if (bandIndex >= bandCount)
        {
            ctx.WriteError(
                $"Band index {bandIndex} out of range. Available bands: {bandCount} (0-{bandCount - 1})"
            );
            return 1;
        }

        if (!body.CanAddToBand(bandIndex))
        {
            var capacity = body.OrbitBands[bandIndex].Capacity;
            var current = body.GetBandSatelliteCount(bandIndex);
            ctx.WriteError($"Band {bandIndex} is at capacity ({current}/{capacity})");
            return 1;
        }

        var name = cleanArgs.Count > 1 ? cleanArgs[1] : null;

        // Look up station definition if --template was provided
        StationDefinition? stationDef = null;
        if (templateName != null)
        {
            StationDatabase.Instance.TryGetStation(templateName, out stationDef);
            if (stationDef == null)
            {
                ctx.WriteError($"Station template '{templateName}' not found. Available templates:");
                var allStations = StationDatabase.Instance.GetAllStations().Values;
                foreach (var s in allStations)
                    ctx.WriteLine($"  {s.Name} ({s.StationType}, {s.ConstructionTime}s)");
                return 1;
            }
        }

        StationSatellite station;
        try
        {
            station = CreateStation(body, bandIndex, name, stationDef);
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to create station: {ex.Message}");
            return 1;
        }

        if (stationDef != null)
        {
            ctx.WriteLine(
                $"[color=green]Station '{station.Name}' construction started in band {bandIndex} on '{Name}'[/color]"
            );
            ctx.WriteLine($"Template: {stationDef.Name} | Type: {stationDef.StationType} | Time: {stationDef.ConstructionTime}s");
            ctx.WriteLine($"Can build ships: {stationDef.HasBehavior("ShipyardBehavior")}");
        }
        else
        {
            ctx.WriteLine(
                $"[color=green]Station '{station.Name}' created in band {bandIndex} on '{Name}'[/color]"
            );
        }
        return 0;
    }

    /// <summary>
    /// Debug command to spawn a logistics ship in an orbit band.
    /// When --template is provided, spawns in construction mode.
    /// </summary>
    [DebugCommand(
        "spawn_ship",
        "Spawn a logistics ship in an orbit band",
        "(namespace) spawn_ship [band_index] [name] [dry_mass] [--template <template_name>]",
        Category = "Modification",
        RequiresTarget = true
    )]
    public int SpawnShipCommand(CommandContext ctx, string[] args)
    {
        if (ctx.TargetInstance is not CelestialBody body)
        {
            ctx.WriteError("Target must be a CelestialBody.");
            return 1;
        }
        var bandCount = body.GetBandCount();
        if (bandCount == 0)
        {
            ctx.WriteError("This body has no orbit bands configured.");
            return 1;
        }

        // Parse --template flag
        string? templateName = null;
        var cleanArgs = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--template" && i + 1 < args.Length)
            {
                templateName = args[++i];
            }
            else
            {
                cleanArgs.Add(args[i]);
            }
        }

        int? bandIndex = null;
        string? name = null;
        float? dryMass = null;

        // Parse optional band index
        if (cleanArgs.Count > 0)
        {
            if (!int.TryParse(cleanArgs[0], out var parsed))
            {
                ctx.WriteError($"Invalid band index: '{cleanArgs[0]}'. Must be an integer.");
                return 1;
            }
            bandIndex = parsed;
        }

        // Parse optional name
        if (cleanArgs.Count > 1)
        {
            name = cleanArgs[1];
        }

        // Parse optional dry mass
        if (cleanArgs.Count > 2)
        {
            if (!float.TryParse(cleanArgs[2], out var parsedMass))
            {
                ctx.WriteError($"Invalid dry mass: '{cleanArgs[2]}'. Must be a number.");
                return 1;
            }
            dryMass = parsedMass;
        }

        // Auto-select first available band if not specified
        if (!bandIndex.HasValue)
        {
            for (int i = 0; i < bandCount; i++)
            {
                if (body.CanAddToBand(i))
                {
                    bandIndex = i;
                    break;
                }
            }

            if (!bandIndex.HasValue)
            {
                ctx.WriteError("No available orbit bands. All bands are at capacity.");
                return 1;
            }
        }

        // Validate band index range
        if (bandIndex < 0 || bandIndex >= bandCount)
        {
            ctx.WriteError(
                $"Band index {bandIndex.Value} out of range. Available bands: {bandCount} (0-{bandCount - 1})"
            );
            return 1;
        }

        if (!body.CanAddToBand(bandIndex.Value))
        {
            var capacity = body.OrbitBands[bandIndex.Value].Capacity;
            var current = body.GetBandSatelliteCount(bandIndex.Value);
            ctx.WriteError($"Band {bandIndex.Value} is at capacity ({current}/{capacity})");
            return 1;
        }

        // Look up ship definition if --template was provided
        ShipDefinition? shipDef = null;
        if (templateName != null)
        {
            ShipDatabase.Instance.TryGetShip(templateName, out shipDef);
            if (shipDef == null)
            {
                ctx.WriteError($"Ship template '{templateName}' not found. Available templates:");
                var allShips = ShipDatabase.Instance.GetAllShips().Values;
                foreach (var s in allShips)
                    ctx.WriteLine($"  {s.Name} ({s.DryMass}kg, {s.WorkRequired:F0} work)");
                return 1;
            }
        }

        // Generate name if not provided
        name ??= NameGenerator.GenerateShipName();

        // Find a station with ShipyardBehavior on this body, or create a bare ship
        Godot.Collections.Array<Godot.Node> satellites;
        if (body.SatellitesContainer != null)
            satellites = body.SatellitesContainer.GetChildren();
        else
            satellites = new Godot.Collections.Array<Godot.Node>();
        StationSatellite? shipyardStation = null;
        ShipyardBehavior? shipyard = null;
        foreach (var child in satellites)
        {
            if (child is StationSatellite station)
            {
                var yard = station.GetBehavior<ShipyardBehavior>();
                if (yard != null)
                {
                    shipyardStation = station;
                    shipyard = yard;
                    break;
                }
            }
        }

        LogisticsUnit ship;
        try
        {
            if (shipDef != null && shipyard != null && shipyardStation != null)
            {
                // Use ShipyardBehavior for ships with definitions
                ship = shipyard.CreateAndEnqueueShip(body, bandIndex.Value, shipDef, name);
            }
            else
            {
                // Bare instant spawn (no construction mode)
                ship = new LogisticsUnit { Name = name };
                body.SatellitesContainer.AddChild(ship);
                ship.Initialize(body, bandIndex.Value);
                ship.InitializeCargo();

                if (shipDef != null)
                {
                    ship.SetShipDefinition(shipDef);
                    ship.SetFuelCapacity(shipDef.FuelCapacity);
                    ship.SetDryMass(shipDef.DryMass);
                }
                else
                {
                    ship.SetFuelCapacity(1000f);
                }

                body.IncrementBandCount(bandIndex.Value);
            }
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to create ship: {ex.Message}");
            return 1;
        }

        // Apply optional dry mass override (only for instant spawn)
        if (dryMass.HasValue && shipDef == null)
        {
            ship.SetDryMass(dryMass.Value);
        }

        if (shipDef != null)
        {
            ctx.WriteLine(
                $"[color=green]Ship '{ship.Name}' construction started in band {bandIndex.Value} on '{Name}'[/color]"
            );
            ctx.WriteLine($"Template: {shipDef.Name} | Mass: {shipDef.DryMass}kg | Work: {shipDef.WorkRequired:F0}");
        }
        else
        {
            ctx.WriteLine(
                $"[color=green]Ship '{ship.Name}' created in band {bandIndex.Value} on '{Name}'[/color]"
            );
            ctx.WriteLine($"Fuel capacity: {ship.MaxFuel}");
        }
        return 0;
    }

    /// <summary>
    /// Debug command to show all items currently under construction.
    /// </summary>
    [DebugCommand(
        "construction_status",
        "Show all items under construction",
        "construction_status",
        Category = "Info"
    )]
    public int ConstructionStatusCommand(CommandContext ctx, string[] args)
    {
        var stations = _stationsUnderConstruction;
        var buildings = _buildingsUnderConstruction;

        if (stations.Count == 0 && buildings.Count == 0)
        {
            ctx.WriteLine("No items under construction.");
            return 0;
        }

        if (stations.Count > 0)
        {
            ctx.WriteLine($"[color=yellow]Stations ({stations.Count}):[/color]");
            foreach (var station in stations)
            {
                float progress = station.GetProgress() * 100f;
                string typeInfo = station switch
                {
                    StationSatellite s when s.GetBehavior<ShipyardBehavior>() is { } yard =>
                        $" [Yard: {yard.ActiveShipBuildCount} active, {yard.QueuedShipBuildCount} queued]",
                    StationSatellite s when s.GetBehavior<OrbitalConstructorBehavior>() != null =>
                        " [Architect]",
                    _ => ""
                };
                ctx.WriteLine($"  {station.Name} - {station.GetStatus()} ({progress:F1}%) Band {station.BandIndex}{typeInfo}");
            }
        }

        if (buildings.Count > 0)
        {
            ctx.WriteLine($"[color=yellow]Buildings ({buildings.Count}):[/color]");
            foreach (var building in buildings)
            {
                float progress = building.GetProgress() * 100f;
                string architect = building.ConstructingStation != null ? $" via {building.ConstructingStation.Name}" : "";
                ctx.WriteLine($"  {building.Name} - {building.GetStatus()} ({progress:F1}%){architect}");
            }
        }

        return 0;
    }

    string IDataProvider.Name => Name;
    string IDataProvider.Category => "Celestial";
    bool IDataProvider.NeedsRefresh => true;

    object IDebugDataProvider.SourceObject => this;

    string IDebugDataProvider.InstanceNamespace
    {
        get
        {
            // Sanitize name: remove all non-alphanumeric characters
            string nameStr = Name.ToString();
            var sanitized = new string(nameStr.Where(c => char.IsLetterOrDigit(c)).ToArray());
            return string.IsNullOrEmpty(sanitized)
                ? $"CelestialBody._{nameStr.GetHashCode()}"
                : $"CelestialBody.{sanitized}";
        }
    }

    bool IDebugDataProvider.IsSourceValid => IsInstanceValid(this);

    DebugDataNode IDataProvider.GetData()
    {
        var node = new DebugDataNode(Name.ToString())
            .AddProperty("# Stations in Construction", _stationsUnderConstruction.Count)
            .AddProperty("# Buildings in Construction", _buildingsUnderConstruction.Count);

        return node;
    }

    void IDataProvider.Refresh() { }

    IEnumerable<string> IDataProvider.Search(string pattern)
    {
        var results = new List<string>();

        // Search by name
        if (Name.ToString().Contains(pattern, StringComparison.OrdinalIgnoreCase))
        {
            results.Add(Name.ToString());
        }

        return results;
    }
}
#endif
