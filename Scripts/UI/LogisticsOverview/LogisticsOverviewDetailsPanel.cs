using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Logistics;
using UI.Components;

namespace UI.LogisticsOverview;

/// <summary>
/// Right-hand detail view for <see cref="LogisticsOverviewWindow"/>. Summarises
/// the selected unit (identity, capacity, engine, fuel, transit status, parent)
/// and, when the unit is working a route, the current leg with a progress bar
/// and the top cargo entries.
/// </summary>
public partial class LogisticsOverviewDetailsPanel : PanelContainer
{
    private const int TopCargoCount = 8;

    private Label? _titleLabel;
    private VBoxContainer? _body;

    public override void _Ready()
    {
        _titleLabel = GetNodeOrNull<Label>("VBoxContainer/TitleLabel");
        _body = GetNodeOrNull<VBoxContainer>("VBoxContainer/Body");
    }

    public void Clear()
    {
        if (_titleLabel != null)
            _titleLabel.Text = "No unit selected";
        ClearBody();
    }

    public void Populate(LogisticsUnit unit)
    {
        if (_titleLabel != null)
            _titleLabel.Text = unit.Name;

        ClearBody();
        if (_body == null)
            return;

        var columns = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 32);
        _body.AddChild(columns);

        var idCol = NewColumn(columns);
        var statusCol = NewColumn(columns);

        DetailRowBuilder.AddHeader(idCol, "Identity");
        DetailRowBuilder.AddRow(idCol, "Name", unit.Name);
        DetailRowBuilder.AddRow(idCol, "Type", unit.ShipDef?.Name ?? "Unknown");
        DetailRowBuilder.AddRow(idCol, "Capacity", $"{unit.ShipDef?.CargoCapacity ?? 0f:F0} kg");
        DetailRowBuilder.AddRow(idCol, "Engine", unit.ShipDef?.EngineCategory ?? "—");
        DetailRowBuilder.AddRow(idCol, "Parent", LogisticsUnitDataHelpers.GetParentBodyName(unit));

        DetailRowBuilder.AddHeader(statusCol, "Status");
        DetailRowBuilder.AddRow(statusCol, "Transit", LogisticsUnitDataHelpers.GetTransitStatus(unit));
        DetailRowBuilder.AddPercentRow(statusCol, "Fuel", unit.Fuel, unit.MaxFuel, "kg");
        DetailRowBuilder.AddRow(statusCol, "Cargo Mass", $"{unit.GetCargoMass():F0} kg");
        DetailRowBuilder.AddRow(statusCol, "Available ΔV", $"{unit.GetAvailableDeltaV():F0} m/s");

        if (LogisticsUnitDataHelpers.IsUnderConstruction(unit))
        {
            DetailRowBuilder.AddSeparator(_body);
            DetailRowBuilder.AddAlert(_body, "Unit is still under construction");
            return;
        }

        AppendCurrentLeg(unit);
    }

    private void AppendCurrentLeg(LogisticsUnit unit)
    {
        if (_body == null)
            return;

        var leg = LogisticsUnitDataHelpers.GetCurrentLeg(unit);
        if (leg == null)
        {
            DetailRowBuilder.AddSeparator(_body);
            DetailRowBuilder.AddRow(_body, "Route", "No active route");
            return;
        }

        DetailRowBuilder.AddSeparator(_body);

        string origin = LogisticsUnitDataHelpers.EndpointName(leg.Origin);
        string dest = LogisticsUnitDataHelpers.EndpointName(leg.Destination);
        DetailRowBuilder.AddHeader(_body, $"Current Leg: {origin} → {dest}");

        var legColumns = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        legColumns.AddThemeConstantOverride("separation", 32);
        _body.AddChild(legColumns);

        var legCol = NewColumn(legColumns);
        var cargoCol = NewColumn(legColumns);

        var traj = leg.SelectedTrajectory;
        if (traj != null)
        {
            DetailRowBuilder.AddRow(legCol, "Time of Flight", $"{traj.TimeOfFlight:F0} s");
            DetailRowBuilder.AddRow(legCol, "Fuel Burn", $"{traj.FuelRequired:F1} kg");
            DetailRowBuilder.AddRow(legCol, "Delta-V", $"{traj.DeltaVRequired:F1} m/s");
        }
        else
        {
            DetailRowBuilder.AddRow(legCol, "Trajectory", "Not yet planned");
        }

        // Explicit progress bar for the in-transit leg.
        var mc = unit.MovementController;
        float progress = mc != null && mc.IsTransferring ? mc.TransferProgress : 0f;
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = progress * 100f,
            CustomMinimumSize = new Vector2(0, 18),
            ShowPercentage = true,
        };
        legCol.AddChild(bar);

        // Top cargo entries.
        DetailRowBuilder.AddHeader(cargoCol, "Cargo (Top)");
        List<KeyValuePair<string, int>> top = LogisticsUnitDataHelpers.GetTopCargo(unit, TopCargoCount);
        if (top.Count == 0)
        {
            DetailRowBuilder.AddRow(cargoCol, "Cargo", "Empty");
        }
        else
        {
            foreach (var kvp in top)
                DetailRowBuilder.AddRow(cargoCol, kvp.Key, kvp.Value.ToString());
        }
    }

    private static VBoxContainer NewColumn(HBoxContainer parent)
    {
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 2);
        parent.AddChild(col);
        return col;
    }

    private void ClearBody()
    {
        if (_body == null)
            return;
        foreach (var child in _body.GetChildren())
            child.QueueFree();
    }
}
