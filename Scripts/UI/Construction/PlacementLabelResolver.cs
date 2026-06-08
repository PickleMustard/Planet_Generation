using System.Collections.Generic;
using Constructables.Buildings.Behaviors;
using Godot;
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
    /// Builds the label rows for placing <paramref name="def"/> over <paramref name="cell"/>.
    /// Returns an empty list when no behavior contributes content.
    /// </summary>
    public static List<PlacementLabelRow> Resolve(BuildingDefinition def, VoronoiCell cell)
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
            }
        }

        return rows;
    }

    /// <summary>
    /// Lists the resources the extraction building can pull from the cell (recipe-tag + tier
    /// gated, abundance-descending) as icon + "Name · NN%" rows.
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
            string text = $"{ResourceNameFormatter.Prettify(kvp.Key)}  ·  {kvp.Value:P0}";
            rows.Add(new PlacementLabelRow(icon, tint, text));
        }
    }
}
