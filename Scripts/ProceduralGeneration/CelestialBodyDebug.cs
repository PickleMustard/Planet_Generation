#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Debug;
using Debug.Console;
using Debug.DatabaseViewer;

namespace ProceduralGeneration.PlanetGeneration;

public partial class CelestialBody : IDebugDataProvider
{

    /// <summary>
    /// Debug command to show orbit band information.
    /// </summary>
    /// <param name="ctx">Command context for console output</param>
    /// <param name="args">Arguments (unused)</param>
    /// <returns>0 on success</returns>
    [DebugCommand(
        "orbit_bands",
        "Show orbit band information",
        "(namespace) orbit_bands",
        Category = "Query",
        RequiresTarget = true
    )]
    public int GetOrbitBands(CommandContext ctx, string[] args)
    {
        var bandCount = GetBandCount();

        if (bandCount == 0)
        {
            ctx.WriteLine("No orbit bands configured.");
            return 0;
        }

        ctx.WriteLine($"[color=cyan]Orbit Bands for {Name}:[/color]");

        for (int i = 0; i < bandCount; i++)
        {
            var band = OrbitBands[i];
            var current = GetBandSatelliteCount(i);
            var canAdd = CanAddToBand(i);
            var status = canAdd ? "[color=green]available[/color]" : "[color=red]full[/color]";

            ctx.WriteLine(
                $"  Band {i}: radius={band.Radius:F1}, {current}/{band.Capacity} {status}"
            );
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
            .AddProperty("Type", Type.ToString())
            .AddProperty("Subtype", Classification?.SubtypeAsObject?.ToString() ?? "(none)")
            .AddProperty("Mass", Mass)
            .AddProperty("Velocity", Velocity)
            .AddProperty("Position", GlobalPosition);

        if (Mesh != null)
        {
            node.AddProperty("Mesh Size", Mesh.size);
        }

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

        // Search by type
        if (Type.ToString().Contains(pattern, StringComparison.OrdinalIgnoreCase))
        {
            results.Add($"Type:{Type}");
        }

        return results;
    }
}
#endif
