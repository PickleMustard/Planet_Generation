using System;
using Constructables;
using Godot;
using Structures.Enums;
using Structures.Logistics;
using UI.Wireframe;

namespace UI.OrbitalScheduling;

/// <summary>
/// Hierarchical list of an orbital schedule's legs as train-ticket cards, with
/// playback controls (start/resume, pause, step) and a loop indicator that marks
/// where the schedule cycles back to its first leg. Reorder / delete / add edit the
/// draft schedule through <see cref="OrbitalScheduleEditor"/> and revalidate.
/// </summary>
public sealed partial class LegListView : Control
{
    [Signal] public delegate void AddLegRequestedEventHandler();
    [Signal] public delegate void EditLegRequestedEventHandler(int index);

    private LogisticsUnit? _unit;
    private OrbitalTransferSchedule? _schedule;
    private LegEndpoint? _unitLocation;

    private VBoxContainer? _cardList;
    private Label? _statusLabel;
    private Button? _startBtn;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    public void Bind(LogisticsUnit unit, OrbitalTransferSchedule schedule, LegEndpoint? unitLocation)
    {
        _unit = unit;
        _schedule = schedule;
        _unitLocation = unitLocation;
        Refresh();
    }

    private void BuildLayout()
    {
        var col = new VBoxContainer();
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.AddThemeConstantOverride("separation", 8);
        AddChild(col);

        // Playback bar.
        var bar = new MarginContainer();
        bar.AddThemeConstantOverride("margin_left", 18);
        bar.AddThemeConstantOverride("margin_right", 18);
        bar.AddThemeConstantOverride("margin_top", 10);
        col.AddChild(bar);
        var barRow = new HBoxContainer();
        barRow.AddThemeConstantOverride("separation", 8);
        bar.AddChild(barRow);

        _startBtn = new Button { Text = "▶ Start / Resume", ThemeTypeVariation = "ButtonPrimary" };
        _startBtn.Pressed += OnStart;
        barRow.AddChild(_startBtn);

        var pause = new Button { Text = "❚❚ Pause" };
        pause.Pressed += OnPause;
        barRow.AddChild(pause);

        var step = new Button { Text = "⏭ Next Leg Only" };
        step.Pressed += OnStep;
        barRow.AddChild(step);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        barRow.AddChild(spacer);

        var repeat = new CheckBox { Text = "Loop" };
        repeat.ButtonPressed = _schedule?.IsRepeating ?? true;
        repeat.Toggled += OnRepeatToggled;
        barRow.AddChild(repeat);

        _statusLabel = new Label { ThemeTypeVariation = "LabelMono" };
        _statusLabel.AddThemeFontSizeOverride("font_size", 11);
        _statusLabel.AddThemeColorOverride("font_color", WireColors.InkSoft);
        barRow.AddChild(_statusLabel);

        // Scrollable card list.
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var scrollMargin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        scrollMargin.AddThemeConstantOverride("margin_left", 18);
        scrollMargin.AddThemeConstantOverride("margin_right", 18);
        scrollMargin.AddChild(scroll);
        col.AddChild(scrollMargin);

        _cardList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _cardList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_cardList);

        // Footer: add leg.
        var footer = new MarginContainer();
        footer.AddThemeConstantOverride("margin_left", 18);
        footer.AddThemeConstantOverride("margin_right", 18);
        footer.AddThemeConstantOverride("margin_bottom", 12);
        col.AddChild(footer);
        var add = new Button { Text = "+ Add Leg" };
        add.Pressed += () => EmitSignal(SignalName.AddLegRequested);
        footer.AddChild(add);
    }

    public void Refresh()
    {
        if (_cardList == null || _schedule == null)
            return;

        foreach (var c in _cardList.GetChildren())
            c.QueueFree();

        var validation = _unit != null
            ? OrbitalScheduleValidator.Validate(_unit, _schedule)
            : null;

        int current = _schedule.CurrentLegIndex;
        bool running = _schedule.State == OrbitalScheduleState.Running;

        if (_schedule.Legs.Count == 0)
        {
            var empty = new Label
            {
                Text = "No legs yet — add one to build a schedule.",
                ThemeTypeVariation = "LabelHand",
            };
            empty.AddThemeFontSizeOverride("font_size", 18);
            empty.AddThemeColorOverride("font_color", WireColors.InkFaint);
            _cardList.AddChild(empty);
        }

        for (int i = 0; i < _schedule.Legs.Count; i++)
        {
            var leg = _schedule.Legs[i];

            // Loop divider before the closing leg (the return-to-start hop).
            if (leg.IsClosingLeg)
                _cardList.AddChild(BuildLoopDivider());

            var data = new LegCardData
            {
                Index = i,
                OriginName = OrbitalScheduleUiHelpers.EndpointName(leg.Origin),
                DestName = OrbitalScheduleUiHelpers.EndpointName(leg.Destination),
                StateText = leg.State.ToString(),
                ManifestSummary = OrbitalScheduleUiHelpers.ManifestSummary(leg),
                FuelSummary = OrbitalScheduleUiHelpers.FuelSummary(leg),
                TimingSummary = OrbitalScheduleUiHelpers.TimingSummary(leg),
                IsCurrent = running && i == current,
                IsClosingLeg = leg.IsClosingLeg,
            };
            var v = validation?.ForLeg(i);
            if (v != null)
            {
                data.IsValid = v.IsValid;
                data.InvalidReason = v.Reason;
            }

            var card = new LegCard();
            _cardList.AddChild(card);
            card.Bind(data);
            card.EditRequested += OnEditLeg;
            card.MoveUpRequested += OnMoveUp;
            card.MoveDownRequested += OnMoveDown;
            card.DeleteRequested += OnDeleteLeg;
        }

        // Loop divider at the end when repeating with no explicit closing leg.
        if (_schedule.IsRepeating && _schedule.Legs.Count > 0
            && !_schedule.Legs[_schedule.Legs.Count - 1].IsClosingLeg)
            _cardList.AddChild(BuildLoopDivider());

        UpdateStatus(validation);
    }

    private void UpdateStatus(ScheduleValidationResult? validation)
    {
        if (_statusLabel == null || _schedule == null)
            return;
        bool valid = validation?.IsValid ?? false;
        _statusLabel.Text = $"STATE · {_schedule.State}   {(valid ? "✓ valid" : "✗ invalid")}";
        _statusLabel.AddThemeColorOverride("font_color", valid ? WireColors.Green : WireColors.Red);
        if (_startBtn != null)
            _startBtn.Disabled = !valid;
    }

    private Control BuildLoopDivider()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.10f),
            BorderColor = WireColors.Orange,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
            ContentMarginLeft = 10,
        });
        var lbl = new Label { Text = "↻ loops back to the first leg", ThemeTypeVariation = "LabelMono" };
        lbl.AddThemeFontSizeOverride("font_size", 10);
        lbl.AddThemeColorOverride("font_color", WireColors.Orange);
        panel.AddChild(lbl);
        return panel;
    }

    // ───────── Edit ops ─────────

    private void OnEditLeg(int index) => EmitSignal(SignalName.EditLegRequested, index);

    private void OnMoveUp(int index)
    {
        if (_schedule == null || _unitLocation == null || index <= 0) return;
        OrbitalScheduleEditor.SwapLegs(_schedule, index, index - 1, _unitLocation);
        Refresh();
    }

    private void OnMoveDown(int index)
    {
        if (_schedule == null || _unitLocation == null) return;
        // Don't swap a real leg with the auto closing leg.
        int last = LastEditableIndex();
        if (index < 0 || index >= last) return;
        OrbitalScheduleEditor.SwapLegs(_schedule, index, index + 1, _unitLocation);
        Refresh();
    }

    private void OnDeleteLeg(int index)
    {
        if (_schedule == null || _unitLocation == null) return;
        OrbitalScheduleEditor.DeleteLeg(_schedule, index, _unitLocation);
        Refresh();
    }

    private int LastEditableIndex()
    {
        if (_schedule == null) return -1;
        int last = _schedule.Legs.Count - 1;
        while (last >= 0 && _schedule.Legs[last].IsClosingLeg)
            last--;
        return last;
    }

    // ───────── Playback ─────────

    private void OnRepeatToggled(bool on)
    {
        if (_schedule == null || _unitLocation == null) return;
        _schedule.IsRepeating = on;
        OrbitalScheduleEditor.Normalize(_schedule, _unitLocation);
        Refresh();
    }

    private void OnStart()
    {
        var exec = _unit?.ScheduleExecutor;
        if (exec == null || _unit == null || _schedule == null) return;

        var validation = OrbitalScheduleValidator.Validate(_unit, _schedule);
        if (!validation.IsValid)
        {
            ToastSystem.Instance?.ShowError("Schedule has errors — fix the flagged legs first.");
            return;
        }
        exec.StartSchedule();
        Refresh();
    }

    private void OnPause()
    {
        _unit?.ScheduleExecutor?.StopSchedule();
        Refresh();
    }

    private void OnStep()
    {
        var exec = _unit?.ScheduleExecutor;
        if (exec == null || _unit == null || _schedule == null) return;
        var validation = OrbitalScheduleValidator.Validate(_unit, _schedule);
        if (!validation.IsValid)
        {
            ToastSystem.Instance?.ShowError("Schedule has errors — fix the flagged legs first.");
            return;
        }
        exec.StepOnceFromCurrent();
        Refresh();
    }
}
