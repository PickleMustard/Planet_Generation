using Constructables;
using Godot;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Top-left panel displaying unit name, ship type, state, destination, current leg, and fuel.
/// </summary>
public partial class LogisticsUnitHeaderPanel : PanelContainer
{
    [Export] private Label? _unitNameLabel;
    [Export] private Label? _unitTypeLabel;
    [Export] private Label? _stateLabel;
    [Export] private Label? _destinationLabel;
    [Export] private Label? _currentLegLabel;
    [Export] private ProgressBar? _fuelBar;
    [Export] private Label? _fuelLabel;

    public void Populate(LogisticsUnit unit)
    {
        if (_unitNameLabel != null)
            _unitNameLabel.Text = unit.Name;

        if (_unitTypeLabel != null)
            _unitTypeLabel.Text = unit.ShipDef?.Name ?? "Logistics Unit";

        if (_stateLabel != null)
            _stateLabel.Text = unit.State.ToString();

        PopulateDestination(unit);
        PopulateCurrentLeg(unit);
        PopulateFuel(unit);
    }

    public void Clear()
    {
        if (_unitNameLabel != null) _unitNameLabel.Text = "";
        if (_unitTypeLabel != null) _unitTypeLabel.Text = "";
        if (_stateLabel != null) _stateLabel.Text = "";
        if (_destinationLabel != null) _destinationLabel.Text = "";
        if (_currentLegLabel != null) _currentLegLabel.Text = "";
        if (_fuelBar != null)
        {
            _fuelBar.Value = 0;
        }
        if (_fuelLabel != null) _fuelLabel.Text = "";
    }

    private void PopulateDestination(LogisticsUnit unit)
    {
        if (_destinationLabel == null)
            return;

        var schedule = unit.ScheduleExecutor?.ActiveSchedule;
        var currentLeg = schedule?.CurrentLeg;

        if (currentLeg?.Destination != null)
        {
            string destName = currentLeg.Destination.Station != null
                ? currentLeg.Destination.Station.Name
                : currentLeg.Destination.Body?.BodyName ?? "Unknown";
            _destinationLabel.Text = destName;
        }
        else
        {
            _destinationLabel.Text = "None";
        }
    }

    private void PopulateCurrentLeg(LogisticsUnit unit)
    {
        if (_currentLegLabel == null)
            return;

        var schedule = unit.ScheduleExecutor?.ActiveSchedule;
        if (schedule?.Legs != null && schedule.Legs.Count > 0)
        {
            _currentLegLabel.Text = $"Leg {schedule.CurrentLegIndex + 1} of {schedule.Legs.Count}";
        }
        else
        {
            _currentLegLabel.Text = "No active route";
        }
    }

    private void PopulateFuel(LogisticsUnit unit)
    {
        float pct = unit.MaxFuel > 0 ? (unit.Fuel / unit.MaxFuel) * 100f : 0f;

        if (_fuelBar != null)
            _fuelBar.Value = pct;

        if (_fuelLabel != null)
            _fuelLabel.Text = $"{unit.Fuel:F1} / {unit.MaxFuel:F1} kg";
    }
}
