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

/// <summary>
/// View 4 — Manifest Editor. Drag a resource from the right-hand stockpile palette
/// onto an empty manifest row; tweak units; pick a condition; press Finish to
/// create a new <see cref="TransferSchedule"/>.
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

    private VBoxContainer? _manifestRows;
    private VBoxContainer? _stockpileList;
    private OptionButton? _conditionDropdown;
    private Label? _conditionDescription;
    private VBoxContainer? _watchedExtras;
    private ScaleStrip? _scale;
    private Label? _itemSummary;
    private Button? _finishBtn;
    private Label? _destLabel;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
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
    }

    public void SetEditTarget(TransferSchedule schedule)
    {
        _editTarget = schedule;
        _draftDestination = schedule.Destination;
        _proportions.Clear();
        foreach (var kvp in schedule.ResourceProportions)
            _proportions[kvp.Key] = kvp.Value;
        _condition = ResolveOption(schedule);
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
        UpdateDestinationLabel();
        RebuildStockpile();
        RebuildManifestRows();
        SelectConditionInDropdown();
        UpdateScale();
    }

    private void BuildLayout()
    {
        var col = new VBoxContainer();
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.AddThemeConstantOverride("separation", 0);
        AddChild(col);

        var stepBar = new MarginContainer();
        stepBar.AddThemeConstantOverride("margin_left", 18);
        stepBar.AddThemeConstantOverride("margin_right", 18);
        stepBar.AddThemeConstantOverride("margin_top", 6);
        stepBar.AddThemeConstantOverride("margin_bottom", 6);
        col.AddChild(stepBar);
        var stepRow = new HBoxContainer();
        stepRow.AddThemeConstantOverride("separation", 14);
        stepBar.AddChild(stepRow);
        var steps = new StepIndicator { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stepRow.AddChild(steps);
        steps.SetSteps(new List<StepIndicator.Step>
        {
            new() { Label = "Pick destination", State = StepIndicator.StepState.Done },
            new() { Label = "Build manifest", State = StepIndicator.StepState.Active },
            new() { Label = "Set condition", State = StepIndicator.StepState.Active },
            new() { Label = "Confirm", State = StepIndicator.StepState.Pending },
        });
        _destLabel = new Label
        {
            Text = "DEST · —",
            ThemeTypeVariation = "LabelMono",
        };
        _destLabel.AddThemeFontSizeOverride("font_size", 10);
        _destLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
        stepRow.AddChild(_destLabel);

        var split = new HSplitContainer
        {
            SplitOffset = -320,
            Collapsed = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddChild(split);

        split.AddChild(BuildLeftColumn());
        split.AddChild(BuildStockpileColumn());

        var actionBar = new TransferActionBar();
        col.AddChild(actionBar);

        var backBtn = new Button { Text = "← Back to destination" };
        backBtn.Pressed += () => EmitSignal(SignalName.BackRequested);
        actionBar.LeftSlot.AddChild(backBtn);

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.Pressed += () => EmitSignal(SignalName.Cancelled);
        actionBar.RightSlot.AddChild(cancelBtn);

        _finishBtn = new Button { Text = "Finish & File Route ✓", ThemeTypeVariation = "ButtonPrimary" };
        _finishBtn.Pressed += OnFinish;
        actionBar.RightSlot.AddChild(_finishBtn);
    }

    private Control BuildLeftColumn()
    {
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);

        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 8);
        box.AddChild(titleRow);
        var title = new Label
        {
            Text = "Cargo Manifest",
            ThemeTypeVariation = "LabelHand",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        titleRow.AddChild(title);
        _itemSummary = new Label
        {
            ThemeTypeVariation = "LabelMono",
        };
        _itemSummary.AddThemeFontSizeOverride("font_size", 12);
        _itemSummary.AddThemeColorOverride("font_color", WireColors.InkFaint);
        titleRow.AddChild(_itemSummary);

        var manifestPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        manifestPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.3f),
            BorderColor = WireColors.Ink,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
        });
        box.AddChild(manifestPanel);

        var manifestCol = new VBoxContainer();
        manifestPanel.AddChild(manifestCol);

        var head = new LedgerColumnHead();
        manifestCol.AddChild(head);
        head.SetColumns(
        [
            new LedgerColumnHead.Col { Title = "Resource", StretchRatio = 1f },
            new LedgerColumnHead.Col { Title = "Wt/u", WidthPx = 60, Align = HorizontalAlignment.Center },
            new LedgerColumnHead.Col { Title = "Share %", WidthPx = 110, Align = HorizontalAlignment.Center },
            new LedgerColumnHead.Col { Title = "Total Wt", WidthPx = 80, Align = HorizontalAlignment.Center },
            new LedgerColumnHead.Col { Title = "", WidthPx = 60, Align = HorizontalAlignment.Center },
        ]);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 220),
        };
        manifestCol.AddChild(scroll);

        _manifestRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _manifestRows.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(_manifestRows);

        // Drop target on the table itself so empty drops still register.
        manifestPanel.SetDragForwarding(
            new Callable(this, MethodName.OnGetDragData),
            new Callable(this, MethodName.OnCanDropData),
            new Callable(this, MethodName.OnDropData));

        var conditionPanel = BuildConditionPanel();
        box.AddChild(conditionPanel);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(spacer);

        _scale = new ScaleStrip { Limit = 1200f };
        box.AddChild(_scale);

        return margin;
    }

    private Control BuildConditionPanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.3f),
            BorderColor = WireColors.Ink,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 10,
            ContentMarginTop = 10,
            ContentMarginRight = 10,
            ContentMarginBottom = 10,
        });

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        panel.AddChild(stack);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        stack.AddChild(row);

        var labelStack = new VBoxContainer();
        labelStack.AddThemeConstantOverride("separation", 0);
        row.AddChild(labelStack);
        var labelKicker = new Label
        {
            Text = "DISPATCH WHEN",
            ThemeTypeVariation = "LabelMono",
        };
        labelKicker.AddThemeFontSizeOverride("font_size", 9);
        labelKicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        labelStack.AddChild(labelKicker);
        var labelValue = new Label
        {
            Text = "Condition",
            ThemeTypeVariation = "LabelHand",
        };
        labelValue.AddThemeFontSizeOverride("font_size", 18);
        labelStack.AddChild(labelValue);

        _conditionDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        for (int i = 0; i < ConditionOption.All.Length; i++)
            _conditionDropdown.AddItem(ConditionOption.All[i].Label, i);
        _conditionDropdown.ItemSelected += OnConditionSelected;
        row.AddChild(_conditionDropdown);

        var description = new VBoxContainer();
        description.AddThemeConstantOverride("separation", 4);
        stack.AddChild(description);
        _conditionDescription = new Label { ThemeTypeVariation = "LabelSub" };
        _conditionDescription.AddThemeFontSizeOverride("font_size", 13);
        _conditionDescription.AddThemeColorOverride("font_color", WireColors.InkSoft);
        description.AddChild(_conditionDescription);
        _watchedExtras = new VBoxContainer { Visible = false };
        description.AddChild(_watchedExtras);
        return panel;
    }

    private Control BuildStockpileColumn()
    {
        var margin = new MarginContainer { CustomMinimumSize = new Vector2(320, 0) };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);

        var col = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 10);
        margin.AddChild(col);

        var title = new Label
        {
            Text = "Stockpile",
            ThemeTypeVariation = "LabelHand",
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(title);

        var hint = new Label
        {
            Text = "DRAG INTO MANIFEST",
            ThemeTypeVariation = "LabelMono",
        };
        hint.AddThemeFontSizeOverride("font_size", 10);
        hint.AddThemeColorOverride("font_color", WireColors.InkFaint);
        col.AddChild(hint);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddChild(scroll);
        _stockpileList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _stockpileList.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(_stockpileList);

        return margin;
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

    internal void AdjustProportion(string resourceId, float delta)
    {
        if (!_proportions.TryGetValue(resourceId, out var current))
            return;
        float updated = Mathf.Clamp(current + delta, 0.05f, 1f);
        _proportions[resourceId] = updated;
        UpdateScale();

        if (_manifestRows == null) return;
        float capacity = _behavior?.GetCapacity(_originBuildingId) ?? 1200f;
        if (capacity <= 0f) capacity = 1200f;
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
        private Label? _shareLabel;
        private Label? _weightLabel;

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

            var minus = new Button { Text = "−" };
            minus.Pressed += () => Owner?.AdjustProportion(ResourceId, -0.05f);
            counter.AddChild(minus);
            _shareLabel = new Label
            {
                Text = $"{(int)(proportion * 100)}%",
                ThemeTypeVariation = "LabelMono",
            };
            _shareLabel.AddThemeFontSizeOverride("font_size", 14);
            counter.AddChild(_shareLabel);
            var plus = new Button { Text = "+" };
            plus.Pressed += () => Owner?.AdjustProportion(ResourceId, 0.05f);
            counter.AddChild(plus);

            float capacity = Owner?._behavior?.GetCapacity(Owner._originBuildingId) ?? 1200f;
            _weightLabel = new Label
            {
                Text = $"{capacity * proportion:0.#} t",
                ThemeTypeVariation = "LabelMono",
                CustomMinimumSize = new Vector2(80, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _weightLabel.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(_weightLabel);

            var remove = new Button { Text = "✕", ThemeTypeVariation = "ButtonDanger" };
            remove.Pressed += () => Owner?.RemoveFromManifest(ResourceId);
            row.AddChild(remove);
        }

        public void RefreshValues(float proportion, float capacity)
        {
            if (_shareLabel != null)
                _shareLabel.Text = $"{(int)(proportion * 100)}%";
            if (_weightLabel != null)
                _weightLabel.Text = $"{capacity * proportion:0.#} t";
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
