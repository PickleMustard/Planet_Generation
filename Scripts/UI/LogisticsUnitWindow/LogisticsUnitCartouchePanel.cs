using System.Collections.Generic;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using UI.Components;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Logistics-unit equivalent of <see cref="UI.StationWindow.StationCartouchePanel"/>.
/// Renders the unit's name, ship class, designation, and a stats grid summarising
/// capacity, fuel, status, parent body, destination, and delta-v.
/// </summary>
public partial class LogisticsUnitCartouchePanel : Control
{
    [Export]
    private ChamferedPanel? _namePlate;

    [Export]
    private Label? _atlasBannerLabel;

    [Export]
    private Label? _unitNameLabel;

    [Export]
    private Label? _classLabel;

    [Export]
    private Label? _designationLabel;

    [Export]
    private ChamferedPanel? _statsBlock;

    [Export]
    private GridContainer? _statsGrid;

    public void Populate(LogisticsUnit unit)
    {
        if (_unitNameLabel != null)
            _unitNameLabel.Text = unit.Name;

        if (_classLabel != null)
            _classLabel.Text = unit.ShipDef?.Name ?? "Logistics Unit";

        if (_designationLabel != null)
            _designationLabel.Text = GetDesignation(unit);

        if (_statsGrid != null)
        {
            ClearStatsGrid();
            foreach (var row in GetStatRows(unit))
            {
                _statsGrid.AddChild(BuildStatLabel(row.Key, isKey: true));
                _statsGrid.AddChild(BuildStatLabel(row.Value, isKey: false));
            }
        }
    }

    public void Clear()
    {
        if (_unitNameLabel != null) _unitNameLabel.Text = "";
        if (_classLabel != null) _classLabel.Text = "";
        if (_designationLabel != null) _designationLabel.Text = "";
        ClearStatsGrid();
    }

    private void ClearStatsGrid()
    {
        if (_statsGrid == null)
            return;
        foreach (var child in _statsGrid.GetChildren())
            child.QueueFree();
    }

    private static string GetDesignation(LogisticsUnit unit)
    {
        bool building = unit.ConstructingStation != null
            && unit.ConstructingStation.IsUnderConstruction;
        return building ? "UNDER CONSTRUCTION" : "FLEET ASSET · IN SERVICE";
    }

    private static List<KeyValuePair<string, string>> GetStatRows(LogisticsUnit unit)
    {
        var rows = new List<KeyValuePair<string, string>>();

        rows.Add(new("Status", unit.State.ToString()));
        rows.Add(new("Parent", LogisticsUnitDataHelpers.GetParentBodyName(unit)));

        float capacity = unit.ShipDef?.CargoCapacity ?? 0f;
        rows.Add(new("Capacity", $"{capacity:F0} kg"));
        rows.Add(new("Cargo", $"{unit.GetCargoMass():F0} kg"));

        rows.Add(new("Fuel", $"{unit.GetFuelPercentage():F0}%"));
        rows.Add(new("ΔV", $"{unit.GetAvailableDeltaV():F0} m/s"));

        string dest = LogisticsUnitDataHelpers.GetDestinationName(unit);
        rows.Add(new("Dest", dest));
        rows.Add(new("Engine", unit.ShipDef?.EngineCategory ?? "—"));

        return rows;
    }

    private static Label BuildStatLabel(string text, bool isKey)
    {
        return new Label
        {
            Text = text,
            ThemeTypeVariation = isKey ? "LabelFaint" : "LabelMono",
        };
    }
}
