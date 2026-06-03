using System;
using System.Collections.Generic;
using Constructables;
using Godot;
using ProceduralGeneration;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;
using UI.PlanetBoard;
using UI.PlanetBoard.Modes;
using UI.Wireframe;

namespace UI.OrbitalScheduling;

/// <summary>
/// Five-step wizard for building or editing one orbital-transfer leg, mirroring the
/// transfer-route wizard's engineering-diagram styling. Steps: Port of Call (board
/// station picker) → Cargo Manifest (load/unload) → Fuel (refuel policy) → Timing
/// (departure constraints + max wait) → Confirm. Commits into the draft schedule via
/// <see cref="OrbitalScheduleEditor"/>.
/// </summary>
public sealed partial class LegWizardView : Control
{
    [Signal] public delegate void CommittedEventHandler();
    [Signal] public delegate void CancelledEventHandler();

    private LogisticsUnit? _unit;
    private OrbitalTransferSchedule? _schedule;
    private LegEndpoint? _unitLocation;
    private int _editIndex = -1;

    // Working values.
    private StationSatellite? _destStation;
    private IOrbitalBody? _destBody;
    private int _destBand = -1;
    private readonly Dictionary<string, int> _pickup = new();
    private readonly Dictionary<string, int> _dropoff = new();
    private RefuelPolicy _refuelPolicy = RefuelPolicy.None;
    private string _fuelResourceId = "Fuel";
    private float _refuelAmount = float.MaxValue;
    private ExpenditureBudgetMode _budgetMode = ExpenditureBudgetMode.TimeOfFlight;
    private float? _minBudget;
    private float? _maxBudget;
    private float? _maxWait;

    private int _step;
    private const int StepCount = 5;

    private StepIndicator? _steps;
    private Control? _content;
    private Label? _originLabel;
    private Button? _backBtn;
    private Button? _nextBtn;

    private UI.SystemBoard.SystemBoardView? _board;
    private Label? _destLabel;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    /// <summary>Binds the wizard. legIndex &lt; 0 starts a new (appended) leg.</summary>
    public void Begin(LogisticsUnit unit, OrbitalTransferSchedule schedule, LegEndpoint unitLocation, int legIndex)
    {
        _unit = unit;
        _schedule = schedule;
        _unitLocation = unitLocation;
        _editIndex = legIndex;

        ResetWorking();
        if (legIndex >= 0 && legIndex < schedule.Legs.Count)
            LoadFromLeg(schedule.Legs[legIndex]);

        _step = 0;
        RefreshStep();
    }

    private void ResetWorking()
    {
        _destStation = null;
        _destBody = null;
        _destBand = -1;
        _pickup.Clear();
        _dropoff.Clear();
        _refuelPolicy = RefuelPolicy.None;
        _fuelResourceId = "Fuel";
        _refuelAmount = float.MaxValue;
        _budgetMode = ExpenditureBudgetMode.TimeOfFlight;
        _minBudget = null;
        _maxBudget = null;
        _maxWait = null;
    }

    private void LoadFromLeg(Leg leg)
    {
        _destStation = leg.Destination?.Station;
        _destBody = leg.Destination?.Body;
        _destBand = leg.Destination?.BandIndex ?? -1;
        if (leg.PickupOrder != null)
            foreach (var kv in leg.PickupOrder.Resources) _pickup[kv.Key] = kv.Value;
        if (leg.DropoffOrder != null)
            foreach (var kv in leg.DropoffOrder.Resources) _dropoff[kv.Key] = kv.Value;
        _refuelPolicy = leg.RefuelInstructions.Policy;
        _fuelResourceId = leg.RefuelInstructions.FuelResourceId;
        _refuelAmount = leg.RefuelInstructions.Amount;
        _budgetMode = leg.DepartureConstraints.BudgetMode;
        _minBudget = leg.DepartureConstraints.MinBudget;
        _maxBudget = leg.DepartureConstraints.MaxBudget;
        _maxWait = leg.MaxWaitSeconds;
    }

    // ───────── Layout ─────────

    private void BuildLayout()
    {
        var col = new VBoxContainer();
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.AddThemeConstantOverride("separation", 0);
        AddChild(col);

        var stepBar = new MarginContainer();
        stepBar.AddThemeConstantOverride("margin_left", 18);
        stepBar.AddThemeConstantOverride("margin_right", 18);
        stepBar.AddThemeConstantOverride("margin_top", 8);
        stepBar.AddThemeConstantOverride("margin_bottom", 8);
        col.AddChild(stepBar);
        var stepRow = new HBoxContainer();
        stepRow.AddThemeConstantOverride("separation", 14);
        stepBar.AddChild(stepRow);
        _steps = new StepIndicator { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stepRow.AddChild(_steps);
        _originLabel = new Label { Text = "ORIGIN · —", ThemeTypeVariation = "LabelMono" };
        _originLabel.AddThemeFontSizeOverride("font_size", 10);
        _originLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
        stepRow.AddChild(_originLabel);

        _content = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        ((MarginContainer)_content).AddThemeConstantOverride("margin_left", 18);
        ((MarginContainer)_content).AddThemeConstantOverride("margin_right", 18);
        ((MarginContainer)_content).AddThemeConstantOverride("margin_top", 6);
        ((MarginContainer)_content).AddThemeConstantOverride("margin_bottom", 6);
        col.AddChild(_content);

        var actionBar = new TransferActionBar();
        col.AddChild(actionBar);

        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += () => EmitSignal(SignalName.Cancelled);
        actionBar.LeftSlot.AddChild(cancel);

        _backBtn = new Button { Text = "← Back" };
        _backBtn.Pressed += OnBack;
        actionBar.RightSlot.AddChild(_backBtn);

        _nextBtn = new Button { Text = "Next →", ThemeTypeVariation = "ButtonPrimary" };
        _nextBtn.Pressed += OnNext;
        actionBar.RightSlot.AddChild(_nextBtn);
    }

    private void RefreshStep()
    {
        if (_content == null || _steps == null)
            return;

        var stepDefs = new List<StepIndicator.Step>
        {
            new() { Label = "Port of call" },
            new() { Label = "Manifest" },
            new() { Label = "Fuel" },
            new() { Label = "Timing" },
            new() { Label = "Confirm" },
        };
        for (int i = 0; i < stepDefs.Count; i++)
            stepDefs[i].State = i < _step ? StepIndicator.StepState.Done
                : i == _step ? StepIndicator.StepState.Active
                : StepIndicator.StepState.Pending;
        _steps.SetSteps(stepDefs);

        if (_originLabel != null)
            _originLabel.Text = $"ORIGIN · {OriginName()}";

        foreach (var c in _content.GetChildren())
            c.QueueFree();

        Control panel = _step switch
        {
            0 => BuildPortStep(),
            1 => BuildManifestStep(),
            2 => BuildFuelStep(),
            3 => BuildTimingStep(),
            _ => BuildConfirmStep(),
        };
        _content.AddChild(panel);

        if (_backBtn != null) _backBtn.Disabled = _step == 0;
        if (_nextBtn != null) _nextBtn.Text = _step == StepCount - 1 ? "Confirm ✓" : "Next →";
    }

    private string OriginName()
    {
        if (_schedule == null || _unitLocation == null)
            return "—";
        if (_editIndex > 0 && _editIndex <= _schedule.Legs.Count)
        {
            // Origin = previous leg's destination.
            int prev = _editIndex - 1;
            if (prev >= 0 && prev < _schedule.Legs.Count)
                return OrbitalScheduleUiHelpers.EndpointName(_schedule.Legs[prev].Destination);
        }
        if (_editIndex < 0 && _schedule.Legs.Count > 0)
            return OrbitalScheduleUiHelpers.EndpointName(_schedule.Legs[_schedule.Legs.Count - 1].Destination);
        return OrbitalScheduleUiHelpers.EndpointName(_unitLocation);
    }

    // ───────── Step 1: Port of Call (board) ─────────

    private Control BuildPortStep()
    {
        var split = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 14);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 6);
        split.AddChild(left);

        var title = new Label { Text = "Pick Next Port of Call", ThemeTypeVariation = "LabelHand" };
        title.AddThemeFontSizeOverride("font_size", 22);
        left.AddChild(title);

        var hint = new Label
        {
            Text = "CLICK AN ORBITAL STATION ON THE SYSTEM MAP",
            ThemeTypeVariation = "LabelMono",
        };
        hint.AddThemeFontSizeOverride("font_size", 9);
        hint.AddThemeColorOverride("font_color", WireColors.InkFaint);
        left.AddChild(hint);

        var boardPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        boardPanel.AddThemeStyleboxOverride("panel", BoardFrame());
        left.AddChild(boardPanel);

        var packed = GD.Load<PackedScene>("res://UI/SystemBoard/SystemBoardView.tscn");
        _board = packed?.Instantiate<UI.SystemBoard.SystemBoardView>();
        if (_board != null)
        {
            _board.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _board.SizeFlagsVertical = SizeFlags.ExpandFill;
            boardPanel.AddChild(_board);

            _board.SetPickMode(true);
            _board.OriginStationId = OriginStationId();
            _board.EndpointPicked += OnEndpointPicked;
            if (_unit != null)
                _board.SetFromUnit(_unit);
        }

        // Right: selected-destination card.
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        right.AddThemeConstantOverride("separation", 8);
        split.AddChild(right);

        var dk = new Label { Text = "SELECTED DESTINATION", ThemeTypeVariation = "LabelMono" };
        dk.AddThemeFontSizeOverride("font_size", 9);
        dk.AddThemeColorOverride("font_color", WireColors.InkFaint);
        right.AddChild(dk);

        _destLabel = new Label
        {
            Text = _destStation != null ? OrbitalScheduleUiHelpers.EndpointName(LegEndpoint.ForStation(_destStation, _destBody!, _destBand)) : "Pick a station",
            ThemeTypeVariation = "LabelHand",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _destLabel.AddThemeFontSizeOverride("font_size", 22);
        right.AddChild(_destLabel);

        return split;
    }

    private void OnEndpointPicked(StationSatellite station, IOrbitalBody body)
    {
        _destStation = station;
        _destBody = body;
        _destBand = station.BandIndex;
        if (_destLabel != null)
            _destLabel.Text = string.IsNullOrEmpty(station.Name) ? "Station" : station.Name;
    }

    private string OriginStationId()
    {
        // The origin station id (so the board won't let you pick it as a destination).
        if (_schedule == null) return "";
        LegEndpoint? origin = null;
        if (_editIndex > 0 && _editIndex - 1 < _schedule.Legs.Count)
            origin = _schedule.Legs[_editIndex - 1].Destination;
        else if (_editIndex < 0 && _schedule.Legs.Count > 0)
            origin = _schedule.Legs[_schedule.Legs.Count - 1].Destination;
        else
            origin = _unitLocation;
        return origin?.Station?.Id ?? "";
    }

    // ───────── Step 2: Manifest ─────────

    private Control BuildManifestStep()
    {
        var col = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 8);

        var title = new Label { Text = "Cargo Manifest", ThemeTypeVariation = "LabelHand" };
        title.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(title);

        // Resource adder row.
        var addRow = new HBoxContainer();
        addRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(addRow);

        var picker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var ids = new List<string>();
        var db = ResourceDatabase.Instance;
        if (db != null)
            foreach (var kv in db.GetAllResources())
                ids.Add(kv.Key);
        ids.Sort();
        for (int i = 0; i < ids.Count; i++)
            picker.AddItem(ids[i], i);
        addRow.AddChild(picker);

        var qty = new SpinBox { MinValue = 1, MaxValue = 100000, Value = 1, Step = 1, CustomMinimumSize = new Vector2(90, 0) };
        addRow.AddChild(qty);

        var addLoad = new Button { Text = "+ Load" };
        addLoad.Pressed += () => { AddTo(_pickup, ids, picker, (int)qty.Value); RefreshStep(); };
        addRow.AddChild(addLoad);

        var addUnload = new Button { Text = "+ Unload" };
        addUnload.Pressed += () => { AddTo(_dropoff, ids, picker, (int)qty.Value); RefreshStep(); };
        addRow.AddChild(addUnload);

        var lists = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        lists.AddThemeConstantOverride("separation", 12);
        col.AddChild(lists);
        lists.AddChild(BuildManifestColumn("LOAD AT ORIGIN", _pickup));
        lists.AddChild(BuildManifestColumn("UNLOAD AT DESTINATION", _dropoff));

        return col;
    }

    private Control BuildManifestColumn(string title, Dictionary<string, int> map)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", BoardFrame());
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);

        var head = new Label { Text = title, ThemeTypeVariation = "LabelMono" };
        head.AddThemeFontSizeOverride("font_size", 10);
        head.AddThemeColorOverride("font_color", WireColors.InkFaint);
        box.AddChild(head);

        if (map.Count == 0)
        {
            var empty = new Label { Text = "— none —", ThemeTypeVariation = "LabelMono" };
            empty.AddThemeFontSizeOverride("font_size", 11);
            empty.AddThemeColorOverride("font_color", WireColors.InkFaint);
            box.AddChild(empty);
        }
        else
        {
            foreach (var kv in map)
            {
                var key = kv.Key;
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                var name = new Label { Text = $"{kv.Key}  ×{kv.Value}", ThemeTypeVariation = "LabelHand", SizeFlagsHorizontal = SizeFlags.ExpandFill };
                name.AddThemeFontSizeOverride("font_size", 15);
                row.AddChild(name);
                var rm = new Button { Text = "✕", ThemeTypeVariation = "ButtonDanger" };
                rm.Pressed += () => { map.Remove(key); RefreshStep(); };
                row.AddChild(rm);
                box.AddChild(row);
            }
        }
        return panel;
    }

    private static void AddTo(Dictionary<string, int> map, List<string> ids, OptionButton picker, int qty)
    {
        int idx = picker.Selected;
        if (idx < 0 || idx >= ids.Count || qty <= 0) return;
        string id = ids[idx];
        map[id] = map.TryGetValue(id, out var cur) ? cur + qty : qty;
    }

    // ───────── Step 3: Fuel ─────────

    private Control BuildFuelStep()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);

        var title = new Label { Text = "Refuelling", ThemeTypeVariation = "LabelHand" };
        title.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(title);

        var policyRow = new HBoxContainer();
        policyRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(policyRow);
        policyRow.AddChild(MonoKicker("POLICY"));
        var policy = new OptionButton();
        foreach (var p in Enum.GetNames<RefuelPolicy>())
            policy.AddItem(p);
        policy.Selected = (int)_refuelPolicy;
        policy.ItemSelected += idx => _refuelPolicy = (RefuelPolicy)(int)idx;
        policyRow.AddChild(policy);

        var fuelRow = new HBoxContainer();
        fuelRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(fuelRow);
        fuelRow.AddChild(MonoKicker("FUEL RESOURCE"));
        var fuelEdit = new LineEdit { Text = _fuelResourceId, CustomMinimumSize = new Vector2(160, 0) };
        fuelEdit.TextChanged += t => _fuelResourceId = t;
        fuelRow.AddChild(fuelEdit);

        var amtRow = new HBoxContainer();
        amtRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(amtRow);
        amtRow.AddChild(MonoKicker("AMOUNT"));
        var fill = new CheckBox { Text = "Fill to capacity", ButtonPressed = _refuelAmount >= float.MaxValue };
        amtRow.AddChild(fill);
        var amount = new SpinBox { MinValue = 0, MaxValue = 1000000, Step = 10, CustomMinimumSize = new Vector2(120, 0) };
        amount.Value = _refuelAmount >= float.MaxValue ? 0 : _refuelAmount;
        amount.Editable = _refuelAmount < float.MaxValue;
        amount.ValueChanged += v => { if (_refuelAmount < float.MaxValue) _refuelAmount = (float)v; };
        amtRow.AddChild(amount);
        fill.Toggled += on =>
        {
            _refuelAmount = on ? float.MaxValue : (float)amount.Value;
            amount.Editable = !on;
        };

        return col;
    }

    // ───────── Step 4: Timing ─────────

    private Control BuildTimingStep()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);

        var title = new Label { Text = "Departure & Transfer Window", ThemeTypeVariation = "LabelHand" };
        title.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(title);

        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(modeRow);
        modeRow.AddChild(MonoKicker("BUDGET MODE"));
        var mode = new OptionButton();
        foreach (var m in Enum.GetNames<ExpenditureBudgetMode>())
            mode.AddItem(m);
        mode.Selected = (int)_budgetMode;
        mode.ItemSelected += idx => { _budgetMode = (ExpenditureBudgetMode)(int)idx; RefreshStep(); };
        modeRow.AddChild(mode);

        string unitLabel = _budgetMode == ExpenditureBudgetMode.TimeOfFlight ? "seconds" : "m/s";

        col.AddChild(RangeRow($"MIN ({unitLabel})", _minBudget, v => _minBudget = v));
        col.AddChild(RangeRow($"MAX ({unitLabel})", _maxBudget, v => _maxBudget = v));

        var waitRow = new HBoxContainer();
        waitRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(waitRow);
        waitRow.AddChild(MonoKicker("MAX WAIT (s)"));
        var waitUnset = new CheckBox { Text = "No limit", ButtonPressed = !_maxWait.HasValue };
        waitRow.AddChild(waitUnset);
        var wait = new SpinBox { MinValue = 0, MaxValue = 1000000, Step = 10, CustomMinimumSize = new Vector2(120, 0) };
        wait.Value = _maxWait ?? 0;
        wait.Editable = _maxWait.HasValue;
        wait.ValueChanged += v => { if (_maxWait.HasValue) _maxWait = (float)v; };
        waitRow.AddChild(wait);
        waitUnset.Toggled += on => { _maxWait = on ? (float?)null : (float)wait.Value; wait.Editable = !on; };

        return col;
    }

    private Control RangeRow(string label, float? value, Action<float?> setter)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(MonoKicker(label));
        var unset = new CheckBox { Text = "Unbounded", ButtonPressed = !value.HasValue };
        row.AddChild(unset);
        var spin = new SpinBox { MinValue = 0, MaxValue = 10000000, Step = 10, CustomMinimumSize = new Vector2(140, 0) };
        spin.Value = value ?? 0;
        spin.Editable = value.HasValue;
        spin.ValueChanged += v => setter((float)v);
        row.AddChild(spin);
        unset.Toggled += on =>
        {
            setter(on ? (float?)null : (float)spin.Value);
            spin.Editable = !on;
        };
        return row;
    }

    // ───────── Step 5: Confirm ─────────

    private Control BuildConfirmStep()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);

        var title = new Label { Text = "Confirm Leg", ThemeTypeVariation = "LabelHand" };
        title.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(title);

        col.AddChild(ConfirmLine($"Route:  {OriginName()}  →  {(_destStation != null ? _destStation.Name : "(no destination)")}"));
        col.AddChild(ConfirmLine($"Load:   {Summarize(_pickup)}"));
        col.AddChild(ConfirmLine($"Unload: {Summarize(_dropoff)}"));
        col.AddChild(ConfirmLine($"Fuel:   {_refuelPolicy} ({(_refuelAmount >= float.MaxValue ? "fill" : _refuelAmount.ToString("0") + " kg")})"));
        string ulabel = _budgetMode == ExpenditureBudgetMode.TimeOfFlight ? "s" : "m/s";
        col.AddChild(ConfirmLine($"Window: {(_minBudget?.ToString("0") ?? "—")}–{(_maxBudget?.ToString("0") ?? "—")} {ulabel} ({_budgetMode})"));
        col.AddChild(ConfirmLine($"Max wait: {(_maxWait?.ToString("0") + " s" ?? "no limit")}"));

        if (_destStation == null)
        {
            var warn = new Label { Text = "⚠ Pick a destination station before confirming.", ThemeTypeVariation = "LabelMono" };
            warn.AddThemeColorOverride("font_color", WireColors.Red);
            col.AddChild(warn);
        }
        return col;
    }

    private static string Summarize(Dictionary<string, int> map)
    {
        if (map.Count == 0) return "none";
        var parts = new List<string>();
        foreach (var kv in map) parts.Add($"{kv.Key}×{kv.Value}");
        return string.Join(", ", parts);
    }

    private static Label ConfirmLine(string text)
    {
        var lbl = new Label { Text = text, ThemeTypeVariation = "LabelMono" };
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeColorOverride("font_color", WireColors.InkSoft);
        return lbl;
    }

    private static Label MonoKicker(string text)
    {
        var lbl = new Label { Text = text, ThemeTypeVariation = "LabelMono", CustomMinimumSize = new Vector2(150, 0) };
        lbl.AddThemeFontSizeOverride("font_size", 10);
        lbl.AddThemeColorOverride("font_color", WireColors.InkFaint);
        return lbl;
    }

    // ───────── Navigation ─────────

    private void OnBack()
    {
        if (_step > 0)
        {
            _step--;
            RefreshStep();
        }
    }

    private void OnNext()
    {
        if (_step == 0 && _destStation == null)
        {
            ToastSystem.Instance?.ShowWarning("Pick a destination station first.");
            return;
        }

        if (_step < StepCount - 1)
        {
            _step++;
            RefreshStep();
            return;
        }

        Commit();
    }

    private void Commit()
    {
        if (_unit == null || _schedule == null || _unitLocation == null || _destStation == null || _destBody == null)
        {
            ToastSystem.Instance?.ShowWarning("Leg is incomplete.");
            return;
        }

        var dest = LegEndpoint.ForStation(_destStation, _destBody, _destBand);

        Leg leg;
        if (_editIndex >= 0 && _editIndex < _schedule.Legs.Count)
        {
            leg = _schedule.Legs[_editIndex];
            leg.Destination = dest;
        }
        else
        {
            leg = OrbitalScheduleEditor.AppendLeg(_schedule, dest, _unitLocation);
        }

        leg.PickupOrder = ToManifest(_pickup);
        leg.DropoffOrder = ToManifest(_dropoff);
        leg.RefuelInstructions = new RefuelInstructions
        {
            Policy = _refuelPolicy,
            FuelResourceId = string.IsNullOrWhiteSpace(_fuelResourceId) ? "Fuel" : _fuelResourceId,
            Amount = _refuelAmount,
        };
        leg.DepartureConstraints = new DepartureConstraints
        {
            BudgetMode = _budgetMode,
            MinBudget = _minBudget,
            MaxBudget = _maxBudget,
            NumOptions = leg.DepartureConstraints?.NumOptions ?? 10,
            RankingCriteria = leg.DepartureConstraints?.RankingCriteria ?? TrajectorySolution.RankingCriteria.MostEfficient,
        };
        leg.MaxWaitSeconds = _maxWait;

        // Re-chain (edit case mutated a destination) and refresh closing leg.
        OrbitalScheduleEditor.Normalize(_schedule, _unitLocation);

        EmitSignal(SignalName.Committed);
    }

    private static CargoManifest? ToManifest(Dictionary<string, int> map)
    {
        if (map.Count == 0) return null;
        var m = new CargoManifest();
        foreach (var kv in map)
            if (kv.Value > 0) m.LoadResource(kv.Key, kv.Value);
        return m.ResourceCount > 0 ? m : null;
    }

    private static StyleBoxFlat BoardFrame() => new()
    {
        BgColor = new Color(1f, 1f, 1f, 0.30f),
        BorderColor = WireColors.Ink,
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        ContentMarginLeft = 8,
        ContentMarginRight = 8,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
