#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Constructables.ArtificialSatellites;
using ProceduralGeneration.PlanetGeneration;
using UI.Debug;
using UI.Debug.Console;
using UI.Debug.DatabaseViewer;

namespace Constructables;

public partial class ConstructionManager : IDebugDataProvider
{
    /// <summary>
    /// Debug command to spawn a station satellite in an orbit band.
    /// </summary>
    /// <param name="ctx">Command context for console output</param>
    /// <param name="args">Arguments: [0] = band index, [1] = optional station name</param>
    /// <returns>0 on success, 1 on failure</returns>
    [DebugCommand(
        "spawn_station",
        "Spawn a station satellite in an orbit band",
        "(namespace) spawn_station <band_index> [name]",
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
            ctx.WriteError("Usage: (CelestialBody.Name) spawn_station <band_index> [name]");
            return 1;
        }

        if (!int.TryParse(args[0], out var bandIndex))
        {
            ctx.WriteError($"Invalid band index: '{args[0]}'. Must be an integer.");
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

        var name = args.Length > 1 ? args[1] : null;
        StationSatellite station;
        try
        {
            station = CreateStation(body, bandIndex, name);
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to create station: {ex.Message}");
            return 1;
        }

        ctx.WriteLine(
            $"[color=green]Station '{station.Name}' created in band {bandIndex} on '{Name}'[/color]"
        );
        return 0;
    }

    /// <summary>
    /// Debug command to spawn a logistics ship in an orbit band.
    /// </summary>
    /// <param name="ctx">Command context for console output</param>
    /// <param name="args">Arguments: [0] = optional band index, [1] = optional name, [2] = optional dry mass</param>
    /// <returns>0 on success, 1 on failure</returns>
    [DebugCommand(
        "spawn_ship",
        "Spawn a logistics ship in an orbit band",
        "(namespace) spawn_ship [band_index] [name] [dry_mass]",
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

        int? bandIndex = null;
        string? name = null;
        float? dryMass = null;

        // Parse optional band index
        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out var parsed))
            {
                ctx.WriteError($"Invalid band index: '{args[0]}'. Must be an integer.");
                return 1;
            }
            bandIndex = parsed;
        }

        // Parse optional name
        if (args.Length > 1)
        {
            name = args[1];
        }

        // Parse optional dry mass
        if (args.Length > 2)
        {
            if (!float.TryParse(args[2], out var parsedMass))
            {
                ctx.WriteError($"Invalid dry mass: '{args[2]}'. Must be a number.");
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

        // Generate name if not provided
        name ??= LogisticsCommands.GenerateRandomShipName();

        LogisticsUnit ship;
        try
        {
            ship = CreateLogisticsUnit(body, bandIndex.Value, name);
        }
        catch (Exception ex)
        {
            ctx.WriteError($"Failed to create ship: {ex.Message}");
            return 1;
        }

        // Apply optional dry mass override
        if (dryMass.HasValue)
        {
            ship.SetDryMass(dryMass.Value);
        }

        ctx.WriteLine(
            $"[color=green]Ship '{ship.Name}' created in band {bandIndex.Value} on '{Name}'[/color]"
        );
        ctx.WriteLine($"Fuel capacity: {ship.MaxFuel}");
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
            .AddProperty("# Ships in Construction", _shipsUnderConstruction.Count)
            .AddProperty("# Stations in Construction", _stationsUnderConstruction.Count);

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
