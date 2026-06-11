using System.Collections.Generic;
using Constructables.Buildings.Behaviors;
using Godot;
using ProceduralGeneration;
using Structures.GameState;
using Structures.Resources;

namespace UI.Construction;

/// <summary>
/// Maps a building's behaviors to the rows shown in the placement <see cref="PlacementCursorLabel"/>.
/// This is the flexibility seam: each behavior that wants to surface per-cell info during
/// placement adds a case here. Behaviors that contribute nothing leave the label empty (hidden).
/// </summary>
public static class PlacementLabelResolver
{
    /// <summary>
    /// Builds the label rows for placing <paramref name="def"/> over <paramref name="cell"/> on
    /// <paramref name="body"/>. The body supplies environment context (atmosphere, star distance)
    /// for power-output previews. Returns an empty list when no behavior contributes content.
    /// </summary>
    public static List<PlacementLabelRow> Resolve(
        BuildingDefinition def, VoronoiCell cell, IOrbitalBody? body)
    {
        var rows = new List<PlacementLabelRow>();
        if (def == null || cell == null)
            return rows;

        foreach (var entry in def.BehaviorEntries)
        {
            switch (entry.BehaviorId)
            {
                case "ExtractionBehavior":
                    AppendExtractionRows(def, cell, rows);
                    break;
                case "PowerProducerBehavior":
                    AppendPowerRows(def, cell, body, rows);
                    break;
            }
        }

        return rows;
    }

    /// <summary>
    /// Shows the producer's environment-adjusted power output as "Power · X.XX/cyc", plus a
    /// "why-line" naming the environmental driver (atmosphere / star distance / vent) and the
    /// resulting multiplier so the player sees how the placement site shapes output.
    /// </summary>
    private static void AppendPowerRows(
        BuildingDefinition def, VoronoiCell cell, IOrbitalBody? body, List<PlacementLabelRow> rows)
    {
        var preview = PowerProducerBehavior.GetPlacementPreview(def, body, new[] { cell });
        if (!preview.HasOutput)
            return;

        var db = ResourceDatabase.Instance;
        Texture2D? icon = db?.GetResourceIcon("power");
        Color tint = db?.GetResourceIconTint("power") ?? Colors.White;
        rows.Add(new PlacementLabelRow(icon, tint, $"Power  ·  {preview.Effective:0.##}/cyc"));

        string? why = preview.ModifierType switch
        {
            EnvironmentalModifier.ModifierType.AtmosphereLinear
                => $"Atmosphere {preview.Atmosphere:0.##} atm  ·  ×{preview.Scale:0.##}",
            EnvironmentalModifier.ModifierType.StarDistanceInverseSquare
                => $"{preview.DistanceAU:0.##} AU  ·  ×{preview.Scale:0.##}",
            EnvironmentalModifier.ModifierType.VentPresenceBinary
                => $"Geothermal vent  ·  ×{preview.Scale:0.##}",
            _ => null,
        };
        if (why != null)
            rows.Add(new PlacementLabelRow(null, Colors.White, why));
    }

    /// <summary>
    /// Lists the resources the extraction building can pull from the cell (recipe-tag + tier
    /// gated, richest-first) as icon + "Name · X.XX/cyc" rows, where the number is the
    /// richness-tier fraction-of-a-stack produced per work cycle.
    /// </summary>
    private static void AppendExtractionRows(
        BuildingDefinition def, VoronoiCell cell, List<PlacementLabelRow> rows)
    {
        var deposits = ExtractionBehavior.GetExtractableDeposits(def, cell);
        if (deposits.Count == 0)
            return;

        var db = ResourceDatabase.Instance;
        foreach (var kvp in deposits)
        {
            Texture2D? icon = db?.GetResourceIcon(kvp.Key);
            Color tint = db?.GetResourceIconTint(kvp.Key) ?? Colors.White;
            string text = $"{ResourceNameFormatter.Prettify(kvp.Key)}  ·  {kvp.Value:0.##}/cyc";
            rows.Add(new PlacementLabelRow(icon, tint, text));
        }
    }
}
