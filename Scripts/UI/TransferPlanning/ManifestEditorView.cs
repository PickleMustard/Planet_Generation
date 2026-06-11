using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Godot;
using Structures.GameState;
using Structures.Resources;
using Structures.Transfers;
using UI.Wireframe;
using UtilityLibrary;

namespace UI.TransferPlanning;

public enum TransferMode
{
    Recurring,
    OneTime,
}

public enum ManifestUnitMode
{
    Tonnage,
    Percent,
}

/// <summary>
/// View 4 — Manifest Editor. Drag a resource from the right-hand stockpile palette
/// onto an empty manifest row; tweak units; pick a condition; press Finish to
/// create a new <see cref="TransferSchedule"/> or dispatch a one-time order.
/// </summary>
public partial class ManifestEditorView : Control
{
    [Signal] public delegate void RouteFiledEventHandler();
    [Signal] public delegate void BackRequestedEventHandler();
    [Signal] public delegate void CancelledEventHandler();

    private const float DefaultProportion = 0.20f;

    private TransferStationBehavior? _behavior;
    private string _originBuildingId = "";
    private TransferDestination? _draftDestination;
    private TransferSchedule? _editTarget;

    private readonly Dictionary<string, float> _proportions = new();
    private ConditionOption _condition = ConditionOption.Default;
    private TransferMode _mode = TransferMode.Recurring;
    private ManifestUnitMode _unitMode = ManifestUnitMode.Tonnage;

    [Export] private VBoxContainer? _manifestRows;
    [Export] private VBoxContainer? _stockpileList;
    [Export] private OptionButton? _conditionDropdown;
    [Export] private Label? _conditionDescription;
    [Export] private VBoxContainer? _watchedExtras;
    [Export] private ScaleStrip? _scale;
    [Export] private Label? _itemSummary;
    private Button? _finishBtn;
    [Export] private Label? _destLabel;
    [Export] private Control? _conditionPanel;
    [Export] private Button? _modeRecurringBtn;
    [Export] private Button? _modeOneTimeBtn;
    [Export] private Button? _unitTonnageBtn;
    [Export] private Button? _unitPercentBtn;
    [Export] private LedgerColumnHead? _manifestHead;
    [Export] private StepIndicator? _steps;
    [Export] private PanelContainer? _manifestPanel;
    [Export] private TransferActionBar? _actionBar;

    private static PackedScene? _scene;

    public static ManifestEditorView Create()
    {
        _scene ??= GD.Load<PackedScene>("res://UI/TransferPlanning/ManifestEditorView.tscn");
        return _scene.Instantiate<ManifestEditorView>();
    }

    public override void _Ready()
    {
        if (_modeRecurringBtn != null) _modeRecurringBtn.Pressed += () => SetMode(TransferMode.Recurring);
        if (_modeOneTimeBtn != null) _modeOneTimeBtn.Pressed += () => SetMode(TransferMode.OneTime);
        if (_unitTonnageBtn != null) _unitTonnageBtn.Pressed += () => SetUnitMode(ManifestUnitMode.Tonnage);
        if (_unitPercentBtn != null) _unitPercentBtn.Pressed += () => SetUnitMode(ManifestUnitMode.Percent);

        if (_conditionDropdown != null)
        {
            for (int i = 0; i < ConditionOption.All.Length; i++)
                _conditionDropdown.AddItem(ConditionOption.All[i].Label, i);
            _conditionDropdown.ItemSelected += OnConditionSelected;
        }

        // Drop target on the manifest table itself so empty drops still register.
        _manifestPanel?.SetDragForwarding(
            new Callable(this, MethodName.OnGetDragData),
            new Callable(this, MethodName.OnCanDropData),
            new Callable(this, MethodName.OnDropData));

        if (_scale != null) _scale.Limit = 1200f;

        UpdateSteps();
        RefreshManifestHead();

        if (_actionBar != null)
        {
            var backBtn = new Button { Text = "← Back to destination" };
            backBtn.Pressed += () => EmitSignal(SignalName.BackRequested);
            _actionBar.LeftSlot.AddChild(backBtn);

            var cancelBtn = new Button { Text = "Cancel" };
            cancelBtn.Pressed += () => EmitSignal(SignalName.Cancelled);
            _actionBar.RightSlot.AddChild(cancelBtn);

            _finishBtn = new Button { Text = "Finish & File Route ✓", ThemeTypeVariation = "ButtonPrimary" };
            _finishBtn.Pressed += OnFinish;
            _actionBar.RightSlot.AddChild(_finishBtn);
        }
    }

    public void Bind(TransferStationBehavior? behavior, string originBuildingId, Theme? _)
    {
        _behavior = behavior;
        _originBuildingId = originBuildingId ?? "";
        Reset();
    }

    public void SetDraftDestination(TransferDestination destination)
    {
        _draftDestination = destination;
        _editTarget = null;
        UpdateDestinationLabel();
        Reset();
        SetModeToggleEnabled(true);
    }

    public void SetEditTarget(TransferSchedule schedule)
    {
        _editTarget = schedule;
        _draftDestination = schedule.Destination;
        _proportions.Clear();
        foreach (var kvp in schedule.ResourceProportions)
            _proportions[kvp.Key] = kvp.Value;
        _condition = ResolveOption(schedule);
        _mode = TransferMode.Recurring;
        ApplyModeUi();
        SetModeToggleEnabled(false);
        UpdateDestinationLabel();
        RebuildStockpile();
        RebuildManifestRows();
        SelectConditionInDropdown();
        UpdateScale();
    }

    private void Reset()
    {
        _proportions.Clear();
        _condition = ConditionOption.Default;
        _mode = TransferMode.Recurring;
        ApplyModeUi();
        UpdateDestinationLabel();
        RebuildStockpile();
        RebuildManifestRows();
        SelectConditionInDropdown();
        UpdateScale();
    }

    private void SetModeToggleEnabled(bool enabled)
    {
        if (_modeRecurringBtn != null) _modeRecurringBtn.Disabled = !enabled;
        if (_modeOneTimeBtn != null) _modeOneTimeBtn.Disabled = !enabled;
    }

    private void SetMode(TransferMode mode)
    {
        if (_mode == mode)
        {
            // Keep the toggle in a consistent state if user clicks the active one.
            if (_modeRecurringBtn != null) _modeRecurringBtn.ButtonPressed = _mode == TransferMode.Recurring;
            if (_modeOneTimeBtn != null) _modeOneTimeBtn.ButtonPressed = _mode == TransferMode.OneTime;
            return;
        }
        _mode = mode;
        ApplyModeUi();
    }

    private void ApplyModeUi()
    {
        bool oneTime = _mode == TransferMode.OneTime;
        if (_modeRecurringBtn != null) _modeRecurringBtn.ButtonPressed = !oneTime;
        if (_modeOneTimeBtn != null) _modeOneTimeBtn.ButtonPressed = oneTime;
        if (_conditionPanel != null) _conditionPanel.Visible = !oneTime;
        if (_finishBtn != null) _finishBtn.Text = oneTime ? "Dispatch Now ✓" : "Finish & File Route ✓";
        UpdateSteps();
    }

    private void SetUnitMode(ManifestUnitMode mode)
    {
        _unitMode = mode;
        if (_unitTonnageBtn != null) _unitTonnageBtn.ButtonPressed = mode == ManifestUnitMode.Tonnage;
        if (_unitPercentBtn != null) _unitPercentBtn.ButtonPressed = mode == ManifestUnitMode.Percent;
        RefreshManifestHead();
        RebuildManifestRows();
    }

    private void RefreshManifestHead()
    {
        if (_manifestHead == null) return;
        bool tonnage = _unitMode == ManifestUnitMode.Tonnage;
        _manifestHead.SetColumns(
        [
            new LedgerColumnHead.Col { Title = "Resource", StretchRatio = 1f },
            new LedgerColumnHead.Col { Title = "Wt/u", WidthPx = 60, Align = HorizontalAlignment.Center },
            new LedgerColumnHead.Col { Title = tonnage ? "Tons" : "Share %", WidthPx = 110, Align = HorizontalAlignment.Center },
            new LedgerColumnHead.Col { Title = tonnage ? "Share %" : "Total Wt", WidthPx = 80, Align = HorizontalAlignment.Center },
            new LedgerColumnHead.Col { Title = "", WidthPx = 60, Align = HorizontalAlignment.Center },
        ]);
    }

    private void UpdateSteps()
    {
        if (_steps == null) return;
        bool oneTime = _mode == TransferMode.OneTime;
        _steps.SetSteps(new List<StepIndicator.Step>
        {
            new() { Label = "Pick destination", State = StepIndicator.StepState.Done },
            new() { Label = "Build manifest", State = StepIndicator.StepState.Active },
            new() { Label = oneTime ? "Review" : "Set condition", State = StepIndicator.StepState.Active },
            new() { Label = oneTime ? "Dispatch" : "Confirm", State = StepIndicator.StepState.Pending },
        });
    }

    private void RebuildStockpile()
    {
        if (_stockpileList == null) return;
        foreach (var c in _stockpileList.GetChildren()) c.QueueFree();
        var endpoint = _behavior?.ResourceEndpoint;
        if (endpoint == null) return;
        var db = ResourceDatabase.Instance;
        foreach (var kvp in endpoint.GetAllStockpiles())
        {
            if (kvp.Value <= 0f) continue;
            var id = kvp.Key;
            var row = new StockpileRow
            {
                ResourceId = id,
                Label = SlipDataBuilder.LookupResourceLabel(db, id),
                Weight = SlipDataBuilder.LookupTransportWeight(db, id),
            };
            _stockpileList.AddChild(row);
        }
    }

    private void RebuildManifestRows()
    {
        if (_manifestRows == null) return;
        foreach (var c in _manifestRows.GetChildren()) c.QueueFree();

        foreach (var kvp in _proportions)
            _manifestRows.AddChild(BuildManifestRow(kvp.Key, kvp.Value));

        // Always add a ghost drop row at the bottom.
        _manifestRows.AddChild(BuildGhostRow());

        UpdateScale();
    }

    private Control BuildManifestRow(string resourceId, float proportion)
    {
        var row = new ManifestRow
        {
            ResourceId = resourceId,
            Owner = this,
        };
        row.SetData(resourceId, proportion);
        return row;
    }

    private Control BuildGhostRow()
    {
        var ghost = new ManifestRow { Owner = this };
        ghost.SetGhost();
        return ghost;
    }

    private void OnConditionSelected(long index)
    {
        if (index < 0 || index >= ConditionOption.All.Length) return;
        _condition = ConditionOption.All[index];
        UpdateConditionDescription();
    }

    private void SelectConditionInDropdown()
    {
        if (_conditionDropdown == null) return;
        for (int i = 0; i < ConditionOption.All.Length; i++)
        {
            if (ConditionOption.All[i].Id == _condition.Id)
            {
                _conditionDropdown.Selected = i;
                break;
            }
        }
        UpdateConditionDescription();
    }

    private void UpdateConditionDescription()
    {
        if (_conditionDescription == null) return;
        _conditionDescription.Text = _condition.ScopeKind switch
        {
            ConditionOption.ConditionScope.Some => "Dispatches when any tracked resource crosses the threshold.",
            ConditionOption.ConditionScope.Time => "Dispatches on a fixed timer; manifest contents do not gate departure.",
            _ => "Dispatches when ALL resources reach the share threshold.",
        };
        if (_watchedExtras != null)
            _watchedExtras.Visible = _condition.ScopeKind == ConditionOption.ConditionScope.Some;
    }

    private void UpdateScale()
    {
        if (_scale == null || _behavior == null) return;
        float capacity = _behavior.GetCapacity(_originBuildingId);
        if (capacity <= 0f) capacity = 1200f;
        _scale.Limit = capacity;

        float load = 0f;
        foreach (var kvp in _proportions)
            load += capacity * kvp.Value;
        _scale.Load = load;

        if (_itemSummary != null)
            _itemSummary.Text = $"{_proportions.Count} items · {load:0.#} t";
    }

    private void UpdateDestinationLabel()
    {
        if (_destLabel == null) return;
        if (_draftDestination == null)
        {
            _destLabel.Text = "DEST · —";
            return;
        }
        _destLabel.Text = $"DEST · {SlipDataBuilder.ShortDestinationCode(_draftDestination)}";
    }

    private static ConditionOption ResolveOption(TransferSchedule s)
    {
        if (s.WaitSeconds.HasValue)
        {
            int target = (int)s.WaitSeconds.Value;
            foreach (var c in ConditionOption.All)
                if (c.ScopeKind == ConditionOption.ConditionScope.Time && (int)(c.WaitSeconds ?? -1f) == target)
                    return c;
        }
        foreach (var c in ConditionOption.All)
        {
            if (c.ScopeKind == ConditionOption.ConditionScope.Time) continue;
            if (c.Mode == s.DepartureMode && c.Threshold == s.Threshold) return c;
        }
        return ConditionOption.Default;
    }

    private void OnFinish()
    {
        if (_behavior == null || _draftDestination == null)
        {
            ToastSystem.Instance?.ShowError("Destination is not set.");
            return;
        }
        if (_proportions.Count == 0)
        {
            ToastSystem.Instance?.ShowError("Add at least one resource to the manifest.");
            return;
        }

        if (_mode == TransferMode.OneTime)
        {
            DispatchOneTime();
            return;
        }

        if (_editTarget != null)
        {
            _behavior.RemoveSchedule(_editTarget.ScheduleId);
            _editTarget = null;
        }

        string? id = _behavior.CreateSchedule(
            _originBuildingId,
            _draftDestination,
            _proportions,
            _condition.Mode,
            _condition.Threshold,
            _condition.WaitSeconds);
        if (id == null)
        {
            ToastSystem.Instance?.ShowError("Failed to file route.");
            return;
        }
        _behavior.StartSchedule(id);
        EmitSignal(SignalName.RouteFiled);
    }

    private void DispatchOneTime()
    {
        if (_behavior == null || _draftDestination == null) return;
        float capacity = _behavior.GetCapacity(_originBuildingId);
        if (capacity <= 0f)
        {
            ToastSystem.Instance?.ShowError("Origin has no transfer capacity.");
            return;
        }
        var db = ResourceDatabase.Instance;
        var requested = new Dictionary<string, float>();
        foreach (var kvp in _proportions)
        {
            float perUnitWeight = SlipDataBuilder.LookupTransportWeight(db, kvp.Key);
            if (perUnitWeight <= 0f) continue;
            int units = Mathf.FloorToInt(capacity * kvp.Value / perUnitWeight);
            if (units <= 0) continue;
            requested[kvp.Key] = units;
        }
        if (requested.Count == 0)
        {
            ToastSystem.Instance?.ShowError("Manifest is empty after rounding to whole units.");
            return;
        }
        string? orderId = _behavior.DispatchOneTimeTransfer(_originBuildingId, _draftDestination, requested);
        if (orderId == null)
        {
            ToastSystem.Instance?.ShowError("Failed to dispatch one-time transfer.");
            return;
        }
        EmitSignal(SignalName.RouteFiled);
    }

    // --- Drop forwarding from manifest panel container ---
    private Variant OnGetDragData(Vector2 _) => default;

    private bool OnCanDropData(Vector2 _, Variant data) =>
        data.VariantType == Variant.Type.String;

    private void OnDropData(Vector2 _, Variant data)
    {
        if (data.VariantType != Variant.Type.String) return;
        AddResourceToManifest(data.AsString());
    }

    internal void AddResourceToManifest(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return;
        if (!_proportions.ContainsKey(resourceId))
            _proportions[resourceId] = DefaultProportion;
        RebuildManifestRows();
    }

    internal void RemoveFromManifest(string resourceId)
    {
        _proportions.Remove(resourceId);
        RebuildManifestRows();
    }

    private static int CurrentStepMagnitude()
    {
        bool ctrl = Input.IsKeyPressed(Key.Ctrl);
        bool shift = Input.IsKeyPressed(Key.Shift);
        if (ctrl && shift) return 100;
        if (shift) return 20;
        if (ctrl) return 5;
        return 1;
    }

    internal float GetCapacityOrDefault()
    {
        float capacity = _behavior?.GetCapacity(_originBuildingId) ?? 1200f;
        return capacity <= 0f ? 1200f : capacity;
    }

    private float SumOtherProportions(string excludeId)
    {
        float sum = 0f;
        foreach (var kvp in _proportions)
            if (kvp.Key != excludeId)
                sum += kvp.Value;
        return sum;
    }

    internal void AdjustEntry(string resourceId, int sign)
    {
        if (!_proportions.TryGetValue(resourceId, out var current))
            return;

        float capacity = GetCapacityOrDefault();
        int mag = CurrentStepMagnitude();

        float deltaProp = _unitMode == ManifestUnitMode.Tonnage
            ? sign * mag / capacity
            : sign * mag / 100f;

        // Floor: keep at least 1 ton / 1% in the active unit (use ✕ to remove).
        float minProp = _unitMode == ManifestUnitMode.Tonnage
            ? 1f / capacity
            : 0.01f;

        // Hard cap: total allocation cannot exceed full capacity.
        float headroom = 1f - SumOtherProportions(resourceId);
        if (headroom < minProp) headroom = minProp;

        float updated = Mathf.Clamp(current + deltaProp, minProp, headroom);
        _proportions[resourceId] = updated;
        UpdateScale();

        if (_manifestRows == null) return;
        foreach (var child in _manifestRows.GetChildren())
        {
            if (child is ManifestRow row && row.ResourceId == resourceId)
            {
                row.RefreshValues(updated, capacity);
                break;
            }
        }
    }

    public sealed partial class StockpileRow : PanelContainer
    {
        public string ResourceId = "";
        public string Label = "";
        public float Weight = 1f;

        public override void _Ready()
        {
            MouseDefaultCursorShape = CursorShape.Drag;
            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(1f, 1f, 1f, 0.55f),
                BorderColor = WireColors.Ink,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                ShadowColor = WireColors.Ink,
                ShadowOffset = new Vector2(1.5f, 1.5f),
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 6,
                ContentMarginBottom = 6,
            });

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            AddChild(row);

            var icon = new Label
            {
                Text = SlipDataBuilder.ShortResourceIcon(ResourceId),
                ThemeTypeVariation = "LabelMono",
                CustomMinimumSize = new Vector2(30, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            icon.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(icon);

            var labelCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(labelCol);
            var nameLabel = new Label { Text = Label, ThemeTypeVariation = "LabelHand" };
            nameLabel.AddThemeFontSizeOverride("font_size", 15);
            labelCol.AddChild(nameLabel);
            var subLabel = new Label
            {
                Text = $"{ResourceId} · {Weight:0.#}t/u",
                ThemeTypeVariation = "LabelMono",
            };
            subLabel.AddThemeFontSizeOverride("font_size", 9);
            subLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
            labelCol.AddChild(subLabel);
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            var preview = new Label
            {
                Text = SlipDataBuilder.ShortResourceIcon(ResourceId),
                ThemeTypeVariation = "LabelHand",
            };
            preview.AddThemeFontSizeOverride("font_size", 18);
            SetDragPreview(preview);
            return ResourceId;
        }
    }

    public sealed partial class ManifestRow : PanelContainer
    {
        public string ResourceId = "";
        public ManifestEditorView? Owner;
        private bool _ghost;
        private Label? _counterLabel;
        private Label? _columnLabel;

        public override void _Ready()
        {
            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(1f, 1f, 1f, 0.18f),
                BorderColor = new Color(WireColors.Ink, 0.3f),
                BorderWidthBottom = 1,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 4,
                ContentMarginBottom = 4,
            });
        }

        public void SetGhost()
        {
            _ghost = true;
            ResourceId = "";
            ClearChildren();
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);
            AddChild(row);
            var lbl = new Label
            {
                Text = "drag a resource from the stockpile →",
                ThemeTypeVariation = "LabelHand",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            lbl.AddThemeFontSizeOverride("font_size", 16);
            lbl.AddThemeColorOverride("font_color", WireColors.InkFaint);
            row.AddChild(lbl);
        }

        public void SetData(string resourceId, float proportion)
        {
            _ghost = false;
            ResourceId = resourceId;
            ClearChildren();

            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);
            AddChild(row);

            var icon = new Label
            {
                Text = SlipDataBuilder.ShortResourceIcon(resourceId),
                ThemeTypeVariation = "LabelMono",
                CustomMinimumSize = new Vector2(40, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            icon.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(icon);

            var nameLabel = new Label
            {
                Text = SlipDataBuilder.LookupResourceLabel(ResourceDatabase.Instance, resourceId),
                ThemeTypeVariation = "LabelHand",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 17);
            row.AddChild(nameLabel);

            float weight = SlipDataBuilder.LookupTransportWeight(ResourceDatabase.Instance, resourceId);
            var weightLabel = new Label
            {
                Text = $"{weight:0.#} t/u",
                ThemeTypeVariation = "LabelMono",
                CustomMinimumSize = new Vector2(60, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            weightLabel.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(weightLabel);

            var counter = new HBoxContainer { CustomMinimumSize = new Vector2(110, 0) };
            counter.AddThemeConstantOverride("separation", 6);
            counter.Alignment = BoxContainer.AlignmentMode.Center;
            row.AddChild(counter);

            float capacity = Owner?.GetCapacityOrDefault() ?? 1200f;

            var minus = new Button { Text = "−" };
            minus.Pressed += () => Owner?.AdjustEntry(ResourceId, -1);
            counter.AddChild(minus);
            _counterLabel = new Label
            {
                Text = FormatCounter(proportion, capacity),
                ThemeTypeVariation = "LabelMono",
            };
            _counterLabel.AddThemeFontSizeOverride("font_size", 14);
            counter.AddChild(_counterLabel);
            var plus = new Button { Text = "+" };
            plus.Pressed += () => Owner?.AdjustEntry(ResourceId, +1);
            counter.AddChild(plus);

            _columnLabel = new Label
            {
                Text = FormatColumn(proportion, capacity),
                ThemeTypeVariation = "LabelMono",
                CustomMinimumSize = new Vector2(80, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _columnLabel.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(_columnLabel);

            var remove = new Button { Text = "✕", ThemeTypeVariation = "ButtonDanger" };
            remove.Pressed += () => Owner?.RemoveFromManifest(ResourceId);
            row.AddChild(remove);
        }

        public void RefreshValues(float proportion, float capacity)
        {
            if (_counterLabel != null)
                _counterLabel.Text = FormatCounter(proportion, capacity);
            if (_columnLabel != null)
                _columnLabel.Text = FormatColumn(proportion, capacity);
        }

        // Counter shows the editable value in the active unit; column shows the converse.
        private string FormatCounter(float proportion, float capacity)
        {
            bool tonnage = Owner?._unitMode != ManifestUnitMode.Percent;
            return tonnage
                ? $"{capacity * proportion:0.#} t"
                : $"{(int)Mathf.Round(proportion * 100)}%";
        }

        private string FormatColumn(float proportion, float capacity)
        {
            bool tonnage = Owner?._unitMode != ManifestUnitMode.Percent;
            return tonnage
                ? $"{(int)Mathf.Round(proportion * 100)}%"
                : $"{capacity * proportion:0.#} t";
        }

        private void ClearChildren()
        {
            foreach (var c in GetChildren()) c.QueueFree();
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return data.VariantType == Variant.Type.String;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (data.VariantType != Variant.Type.String) return;
            Owner?.AddResourceToManifest(data.AsString());
        }
    }
}
