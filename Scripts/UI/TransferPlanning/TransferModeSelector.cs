using System;
using Godot;
using Structures.Enums;

namespace UI.TransferPlanning;

/// <summary>
/// Toggle between "One-Time" and "Schedule" mode.
/// In schedule mode, shows departure condition and threshold controls.
/// </summary>
public partial class TransferModeSelector : Control
{
    public event Action<bool>? ModeChanged;

    private Button? _oneTimeButton;
    private Button? _scheduleButton;
    private VBoxContainer? _scheduleOptions;
    private OptionButton? _departureModeOption;
    private OptionButton? _thresholdOption;

    public bool IsScheduleMode { get; private set; }

    public override void _Ready()
    {
        _oneTimeButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/ModeButtons/OneTimeButton");
        _scheduleButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/ModeButtons/ScheduleButton");
        _scheduleOptions = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ScheduleOptions");
        _departureModeOption = GetNodeOrNull<OptionButton>(
            "MarginContainer/VBoxContainer/ScheduleOptions/DepartureModeContainer/DepartureModeOption");
        _thresholdOption = GetNodeOrNull<OptionButton>(
            "MarginContainer/VBoxContainer/ScheduleOptions/ThresholdContainer/ThresholdOption");

        if (_oneTimeButton != null)
        {
            _oneTimeButton.ToggleMode = true;
            _oneTimeButton.Pressed += () => SetMode(false);
        }

        if (_scheduleButton != null)
        {
            _scheduleButton.ToggleMode = true;
            _scheduleButton.Pressed += () => SetMode(true);
        }

        // Populate departure mode options
        if (_departureModeOption != null)
        {
            _departureModeOption.Clear();
            _departureModeOption.AddItem("Any Resource Ready", (int)DepartureConditionMode.AnyResource);
            _departureModeOption.AddItem("All Resources Ready", (int)DepartureConditionMode.AllResources);
        }

        // Populate threshold options
        if (_thresholdOption != null)
        {
            _thresholdOption.Clear();
            _thresholdOption.AddItem("Quarter (25%)", (int)DepartureThreshold.Quarter);
            _thresholdOption.AddItem("Half (50%)", (int)DepartureThreshold.Half);
            _thresholdOption.AddItem("Full (100%)", (int)DepartureThreshold.Full);
            _thresholdOption.Selected = 1; // Default to Half
        }

        Reset();
    }

    public void Reset()
    {
        SetMode(false);
        if (_departureModeOption != null) _departureModeOption.Selected = 0;
        if (_thresholdOption != null) _thresholdOption.Selected = 1;
    }

    public DepartureConditionMode GetDepartureMode()
    {
        if (_departureModeOption == null) return DepartureConditionMode.AnyResource;
        int id = _departureModeOption.GetSelectedId();
        return (DepartureConditionMode)id;
    }

    public DepartureThreshold GetThreshold()
    {
        if (_thresholdOption == null) return DepartureThreshold.Half;
        int id = _thresholdOption.GetSelectedId();
        return (DepartureThreshold)id;
    }

    private void SetMode(bool scheduleMode)
    {
        IsScheduleMode = scheduleMode;

        if (_oneTimeButton != null) _oneTimeButton.ButtonPressed = !scheduleMode;
        if (_scheduleButton != null) _scheduleButton.ButtonPressed = scheduleMode;
        if (_scheduleOptions != null) _scheduleOptions.Visible = scheduleMode;

        ModeChanged?.Invoke(scheduleMode);
    }
}
