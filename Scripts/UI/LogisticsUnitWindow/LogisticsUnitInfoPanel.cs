using System.Collections.Generic;
using System.Linq;
using Constructables;
using Godot;
using Structures.Logistics;
using UI.Components;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Bottom info panel for the logistics-unit details window. Renders the section
/// selected in <see cref="LogisticsUnitTabbedPanel"/> into a fixed set of
/// columns (an <c>HBoxContainer</c> of <c>VBoxContainer</c>s) so the content
/// fits without scrolling.
/// </summary>
public partial class LogisticsUnitInfoPanel : PanelContainer
{
    private Label? _titleLabel;
    private HBoxContainer? _columns;
    private LogisticsUnit? _unit;

    public override void _Ready()
    {
        _titleLabel = GetNodeOrNull<Label>("VBoxContainer/TitleLabel");
        _columns = GetNodeOrNull<HBoxContainer>("VBoxContainer/Columns");
    }

    public void Initialize(LogisticsUnit unit) => _unit = unit;

    public void Clear()
    {
        _unit = null;
        ClearColumns();
        if (_titleLabel != null)
            _titleLabel.Text = "";
    }

    /// <summary>Renders the given section key (e.g. "general", "engine", "leg:2", "hold:0").</summary>
    public void ShowSection(string sectionKey)
    {
        ClearColumns();
        if (_unit == null || _columns == null)
            return;

        if (sectionKey.StartsWith("leg:"))
        {
            ShowLeg(int.TryParse(sectionKey.Substring(4), out int i) ? i : -1);
            return;
        }
        if (sectionKey.StartsWith("hold:"))
        {
            ShowManifest();
            return;
        }

        switch (sectionKey)
        {
            case "general": ShowGeneral(); break;
            case "statistics": ShowStatistics(); break;
            case "engine": ShowEngine(); break;
            case "upgrades": ShowUpgrades(); break;
            case "route:none": ShowRouteEmpty(); break;
            default: SetTitle("Details"); break;
        }
    }

    // ───────── Overview sections ─────────

    private void ShowGeneral()
    {
        SetTitle("General Information");
        var c0 = AddColumn();
        var c1 = AddColumn();

        DetailRowBuilder.AddRow(c0, "Name", _unit!.Name);
        DetailRowBuilder.AddRow(c0, "Type", _unit.ShipDef?.Name ?? "Unknown");
        DetailRowBuilder.AddRow(c0, "State", _unit.State.ToString());
        DetailRowBuilder.AddRow(c0, "Parent", LogisticsUnitDataHelpers.GetParentBodyName(_unit));

        DetailRowBuilder.AddRow(c1, "Destination", LogisticsUnitDataHelpers.GetDestinationName(_unit));
        DetailRowBuilder.AddRow(c1, "Transit", LogisticsUnitDataHelpers.GetTransitStatus(_unit));
        DetailRowBuilder.AddRow(c1, "Engine Class", _unit.ShipDef?.EngineCategory ?? "—");
        if (LogisticsUnitDataHelpers.IsUnderConstruction(_unit))
            DetailRowBuilder.AddAlert(c1, "Under construction");
    }

    private void ShowStatistics()
    {
        SetTitle("Statistics");
        var c0 = AddColumn();
        var c1 = AddColumn();

        DetailRowBuilder.AddRow(c0, "Dry Mass", $"{_unit!.DryMass:F1} kg");
        DetailRowBuilder.AddRow(c0, "Cargo Mass", $"{_unit.GetCargoMass():F1} kg");
        DetailRowBuilder.AddRow(c0, "Total Mass", $"{_unit.GetTotalMass():F1} kg");
        float cap = _unit.ShipDef?.CargoCapacity ?? 0f;
        DetailRowBuilder.AddPercentRow(c0, "Cargo Used", _unit.GetCargoMass(), cap, "kg");

        DetailRowBuilder.AddPercentRow(c1, "Fuel", _unit.Fuel, _unit.MaxFuel, "kg");
        DetailRowBuilder.AddRow(c1, "Available ΔV", $"{_unit.GetAvailableDeltaV():F1} m/s");
        DetailRowBuilder.AddRow(c1, "Orbital Radius", $"{_unit.OrbitalRadius:F2}");
        DetailRowBuilder.AddRow(c1, "Band",
            _unit.BandIndex >= 0 ? _unit.BandIndex.ToString() : "Continuous");
    }

    private void ShowEngine()
    {
        SetTitle("Engine");
        var engine = _unit!.CurrentEngine;
        if (engine == null)
        {
            DetailRowBuilder.AddRow(AddColumn(), "Engine", "None installed");
            return;
        }

        var c0 = AddColumn();
        var c1 = AddColumn();
        DetailRowBuilder.AddRow(c0, "Base Isp", $"{engine.BaseSpecificImpulse:F1} s");
        DetailRowBuilder.AddRow(c0, "Base Thrust", $"{engine.BaseThrust:F1} N");
        DetailRowBuilder.AddRow(c0, "Class", _unit.ShipDef?.EngineCategory ?? "—");

        DetailRowBuilder.AddRow(c1, "Effective Isp", $"{engine.EffectiveSpecificImpulse:F1} s");
        DetailRowBuilder.AddRow(c1, "Effective Thrust", $"{engine.EffectiveThrust:F1} N");
        DetailRowBuilder.AddRow(c1, "Exhaust Velocity", $"{engine.ExhaustVelocity:F1} m/s");
    }

    private void ShowUpgrades()
    {
        SetTitle("Upgrades & Modifiers");
        var engine = _unit!.CurrentEngine;
        var modifiers = engine?.GetActiveModifiers();

        if (modifiers == null || modifiers.Count == 0)
        {
            DetailRowBuilder.AddRow(AddColumn(), "Modifiers", "None installed");
            return;
        }

        // Spread modifiers across up to three columns.
        var cols = new[] { AddColumn(), AddColumn(), AddColumn() };
        int i = 0;
        foreach (var mod in modifiers)
        {
            var col = cols[i % cols.Length];
            DetailRowBuilder.AddRow(col, mod.Source,
                $"{mod.Type} Isp:{mod.IspValue:F2} Thr:{mod.ThrustValue:F2}");
            i++;
        }
    }

    // ───────── Route section ─────────

    private void ShowRouteEmpty()
    {
        SetTitle("Route Planning");
        DetailRowBuilder.AddRow(AddColumn(), "Route", "No active schedule");
    }

    private void ShowLeg(int legIndex)
    {
        var schedule = LogisticsUnitDataHelpers.GetSchedule(_unit!);
        if (schedule == null || legIndex < 0 || legIndex >= schedule.Legs.Count)
        {
            ShowRouteEmpty();
            return;
        }

        Leg leg = schedule.Legs[legIndex];
        string origin = LogisticsUnitDataHelpers.EndpointName(leg.Origin);
        string dest = LogisticsUnitDataHelpers.EndpointName(leg.Destination);
        SetTitle($"Leg {legIndex + 1}: {origin} → {dest}");

        var c0 = AddColumn();
        var c1 = AddColumn();

        DetailRowBuilder.AddRow(c0, "State", leg.State.ToString());
        DetailRowBuilder.AddRow(c0, "Origin", origin);
        DetailRowBuilder.AddRow(c0, "Destination", dest);
        DetailRowBuilder.AddRow(c0, "Attempts", leg.TrajectoryAttempts.ToString());

        var traj = leg.SelectedTrajectory;
        if (traj != null)
        {
            DetailRowBuilder.AddRow(c1, "Time of Flight", $"{traj.TimeOfFlight:F1} s");
            DetailRowBuilder.AddRow(c1, "Fuel Burn", $"{traj.FuelRequired:F1} kg");
            DetailRowBuilder.AddRow(c1, "Delta-V", $"{traj.DeltaVRequired:F1} m/s");
            DetailRowBuilder.AddRow(c1, "Transfer", traj.TransferType.ToString());

            if (legIndex == schedule.CurrentLegIndex
                && _unit!.MovementController?.IsTransferring == true)
            {
                DetailRowBuilder.AddProgressRow(c1, "Progress",
                    _unit.MovementController.TransferProgress);
            }
        }
        else
        {
            DetailRowBuilder.AddRow(c1, "Trajectory", "Not yet planned");
        }
    }

    // ───────── Manifest section ─────────

    private void ShowManifest()
    {
        SetTitle("Cargo Manifest");
        var cargo = _unit!.Cargo;
        if (cargo == null || cargo.Resources.Count == 0)
        {
            DetailRowBuilder.AddRow(AddColumn(), "Cargo", "Empty");
            return;
        }

        var entries = cargo.Resources
            .Where(kvp => kvp.Value > 0)
            .OrderByDescending(kvp => kvp.Value)
            .ToList();

        // Three balanced columns so the manifest fits without scrolling.
        const int colCount = 3;
        var cols = new List<VBoxContainer>();
        for (int i = 0; i < colCount; i++)
            cols.Add(AddColumn());

        int perColumn = Mathf.CeilToInt(entries.Count / (float)colCount);
        for (int i = 0; i < entries.Count; i++)
        {
            var col = cols[Mathf.Min(i / Mathf.Max(perColumn, 1), colCount - 1)];
            DetailRowBuilder.AddRow(col, entries[i].Key, entries[i].Value.ToString());
        }

        float cap = _unit.ShipDef?.CargoCapacity ?? 0f;
        DetailRowBuilder.AddSeparator(cols[colCount - 1]);
        DetailRowBuilder.AddPercentRow(cols[colCount - 1], "Capacity", cargo.TotalCargoMass, cap, "kg");
    }

    // ───────── Helpers ─────────

    private void SetTitle(string text)
    {
        if (_titleLabel != null)
            _titleLabel.Text = text;
    }

    private VBoxContainer AddColumn()
    {
        var col = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddThemeConstantOverride("separation", 2);
        _columns!.AddChild(col);
        return col;
    }

    private void ClearColumns()
    {
        if (_columns == null)
            return;
        foreach (var child in _columns.GetChildren())
            child.QueueFree();
    }
}
