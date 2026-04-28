using System.Linq;
using Constructables;
using Godot;
using Structures.Logistics;
using UI.Components;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Bottom details panel that updates its content based on selection in the tabbed panel.
/// Shows detailed info for resources, legs, and engine selected from tab content.
/// </summary>
public partial class LogisticsUnitDetailsPanel : PanelContainer
{
    private Label? _titleLabel;
    private VBoxContainer? _contentContainer;
    private LogisticsUnit? _unit;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBoxContainer/DetailTitleLabel");
        _contentContainer = GetNode<VBoxContainer>("VBoxContainer/DetailContent");
    }

    public void Initialize(LogisticsUnit unit, LogisticsUnitTabbedPanel tabbedPanel)
    {
        _unit = unit;
        tabbedPanel.ItemSelected += OnItemSelected;
    }

    public void Disconnect(LogisticsUnitTabbedPanel tabbedPanel)
    {
        tabbedPanel.ItemSelected -= OnItemSelected;
    }

    public void Clear()
    {
        _unit = null;
        ClearContent();
        if (_titleLabel != null)
            _titleLabel.Text = "Details";
    }

    private void OnItemSelected(string itemType, int itemIndex)
    {
        ClearContent();

        switch (itemType)
        {
            case "resource":
                ShowResourceDetails(itemIndex);
                break;
            case "leg":
                ShowLegDetails(itemIndex);
                break;
            case "engine":
                ShowEngineDetails();
                break;
        }
    }

    // ───────── Resource Details ─────────

    private void ShowResourceDetails(int resourceIndex)
    {
        if (_unit?.Cargo == null || _titleLabel == null || _contentContainer == null)
            return;

        int idx = 0;
        foreach (var kvp in _unit.Cargo.Resources)
        {
            if (kvp.Value <= 0)
                continue;

            if (idx == resourceIndex)
            {
                _titleLabel.Text = kvp.Key;
                AddDetailRow("Quantity", $"{kvp.Value:F1}");
                return;
            }
            idx++;
        }
    }

    // ───────── Leg Details ─────────

    private void ShowLegDetails(int legIndex)
    {
        if (_unit == null || _titleLabel == null || _contentContainer == null)
            return;

        var schedule = _unit.ScheduleExecutor?.ActiveSchedule;
        if (schedule == null || legIndex < 0 || legIndex >= schedule.Legs.Count)
            return;

        var leg = schedule.Legs[legIndex];

        string originName = leg.Origin?.Station != null
            ? leg.Origin.Station.Name
            : leg.Origin?.Body?.BodyName ?? "Unknown";
        string destName = leg.Destination?.Station != null
            ? leg.Destination.Station.Name
            : leg.Destination?.Body?.BodyName ?? "Unknown";

        _titleLabel.Text = $"Leg {legIndex + 1}: {originName} -> {destName}";

        AddDetailRow("State", leg.State.ToString());
        AddDetailRow("Origin", originName);
        AddDetailRow("Destination", destName);

        if (leg.Origin?.IsStation == true)
            AddDetailRow("Origin Station", leg.Origin.Station!.Name);
        if (leg.Destination?.IsStation == true)
            AddDetailRow("Dest Station", leg.Destination.Station!.Name);

        // Pickup/Dropoff orders
        if (leg.PickupOrder != null && leg.PickupOrder.Resources.Count > 0)
        {
            AddDetailHeader("Pickup Order");
            foreach (var kvp in leg.PickupOrder.Resources)
                AddDetailRow($"  {kvp.Key}", $"{kvp.Value:F1}");
        }

        if (leg.DropoffOrder != null && leg.DropoffOrder.Resources.Count > 0)
        {
            AddDetailHeader("Dropoff Order");
            foreach (var kvp in leg.DropoffOrder.Resources)
                AddDetailRow($"  {kvp.Key}", $"{kvp.Value:F1}");
        }

        // Departure constraints
        var constraints = leg.DepartureConstraints;
        if (constraints != null)
        {
            AddDetailHeader("Constraints");
            AddDetailRow("Budget Mode", constraints.BudgetMode.ToString());
            if (constraints.MinBudget.HasValue)
                AddDetailRow("Min Budget", $"{constraints.MinBudget.Value:F1}");
            if (constraints.MaxBudget.HasValue)
                AddDetailRow("Max Budget", $"{constraints.MaxBudget.Value:F1}");
        }

        // Refuel instructions
        var refuel = leg.RefuelInstructions;
        if (refuel != null && refuel.Policy != Structures.Enums.RefuelPolicy.None)
        {
            AddDetailHeader("Refueling");
            AddDetailRow("Policy", refuel.Policy.ToString());
        }

        // Trajectory if planned
        if (leg.SelectedTrajectory != null)
        {
            var traj = leg.SelectedTrajectory;
            AddDetailHeader("Trajectory");
            AddDetailRow("Delta-V", $"{traj.DeltaVRequired:F1} m/s");
            AddDetailRow("Time of Flight", $"{traj.TimeOfFlight:F1} s");
            AddDetailRow("Transfer Type", traj.TransferType.ToString());
            AddDetailRow("Fuel Required", $"{traj.FuelRequired:F1} kg");
        }

        AddDetailRow("Planning Attempts", leg.TrajectoryAttempts.ToString());
    }

    // ───────── Engine Details ─────────

    private void ShowEngineDetails()
    {
        if (_unit?.CurrentEngine == null || _titleLabel == null || _contentContainer == null)
            return;

        var engine = _unit.CurrentEngine;
        _titleLabel.Text = "Engine Details";

        AddDetailRow("Base Isp", $"{engine.BaseSpecificImpulse:F1} s");
        AddDetailRow("Base Thrust", $"{engine.BaseThrust:F1} N");
        AddDetailRow("Effective Isp", $"{engine.EffectiveSpecificImpulse:F1} s");
        AddDetailRow("Effective Thrust", $"{engine.EffectiveThrust:F1} N");
        AddDetailRow("Exhaust Velocity", $"{engine.ExhaustVelocity:F1} m/s");
    }

    // ───────── Helpers ─────────

    private void ClearContent() => DetailRowBuilder.Clear(_contentContainer);

    private void AddDetailRow(string key, string value)
        => DetailRowBuilder.AddRow(_contentContainer, key, value);

    private void AddDetailHeader(string text)
        => DetailRowBuilder.AddHeader(_contentContainer, text);
}
