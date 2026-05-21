using Godot;

namespace UI.Wireframe;

/// <summary>
/// Visual representation of a dispatch slip. Bound to a <see cref="SlipCardData"/>;
/// emits <see cref="EditRequested"/> / <see cref="DeleteRequested"/> when the
/// row's icon buttons are pressed.
/// </summary>
[GlobalClass]
public partial class SlipCard : PanelContainer
{
    [Signal] public delegate void EditRequestedEventHandler(string scheduleId);
    [Signal] public delegate void DeleteRequestedEventHandler(string scheduleId);

    [Export] public bool ShowDragRail { get; set; } = false;

    private SlipCardData? _data;
    private PaperBackground? _bg;
    private PerforatedEdge? _perf;
    private Label? _slipNumberLabel;
    private Label? _destLabel;
    private Label? _destSubLabel;
    private VBoxContainer? _manifestList;
    private Label? _conditionLabel;
    private HBoxContainer? _watchedRow;
    private Label? _footerLabel;
    private StateDot? _stateDot;
    private Label? _stateLabel;
    private Label? _slotLabel;
    private Button? _editButton;
    private Button? _deleteButton;
    private ProgressBar? _progressBar;
    private Label? _progressLabel;

    public override void _Ready()
    {
        ThemeTypeVariation = "SlipCard";

        _bg = new PaperBackground
        {
            DrawRuledLines = true,
            RuleSpacing = 24f,
            BaseColor = new Color(1f, 1f, 1f, 0.5f),
        };
        _bg.AnchorRight = 1f;
        _bg.AnchorBottom = 1f;
        _bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_bg);
        MoveChild(_bg, 0);

        _perf = new PerforatedEdge();
        _perf.AnchorRight = 1f;
        _perf.OffsetTop = -2f;
        _perf.OffsetBottom = 4f;
        AddChild(_perf);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", ShowDragRail ? 38 : 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 6);
        margin.AddChild(col);

        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 6);
        col.AddChild(headerRow);

        var headerCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headerRow.AddChild(headerCol);

        var dispatchKicker = new Label
        {
            Text = "DISPATCH SLIP · No.",
            ThemeTypeVariation = "LabelMono",
        };
        dispatchKicker.AddThemeFontSizeOverride("font_size", 9);
        dispatchKicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        headerCol.AddChild(dispatchKicker);

        _slipNumberLabel = new Label { Text = "RT-001", ThemeTypeVariation = "LabelHand" };
        _slipNumberLabel.AddThemeFontSizeOverride("font_size", 22);
        headerCol.AddChild(_slipNumberLabel);

        _slotLabel = new Label
        {
            Text = "PRIORITY #1",
            ThemeTypeVariation = "LabelMono",
            HorizontalAlignment = HorizontalAlignment.Right,
            Visible = false,
        };
        _slotLabel.AddThemeFontSizeOverride("font_size", 9);
        _slotLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
        headerRow.AddChild(_slotLabel);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 4);
        headerRow.AddChild(actions);

        _editButton = new Button { Text = "🔨", TooltipText = "Edit slip" };
        _editButton.Pressed += () =>
        {
            if (_data != null) EmitSignal(SignalName.EditRequested, _data.ScheduleId);
        };
        actions.AddChild(_editButton);

        _deleteButton = new Button
        {
            Text = "✕",
            TooltipText = "Delete slip",
            ThemeTypeVariation = "ButtonDanger",
        };
        _deleteButton.Pressed += () =>
        {
            if (_data != null) EmitSignal(SignalName.DeleteRequested, _data.ScheduleId);
        };
        actions.AddChild(_deleteButton);

        col.AddChild(BuildLabeledRow("TO", out _destLabel, out _destSubLabel));

        var manifestRow = new HBoxContainer();
        manifestRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(manifestRow);

        var manifestKicker = new Label
        {
            Text = "MANIFEST",
            ThemeTypeVariation = "LabelMono",
            CustomMinimumSize = new Vector2(70f, 0f),
        };
        manifestKicker.AddThemeFontSizeOverride("font_size", 9);
        manifestKicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        manifestRow.AddChild(manifestKicker);

        _manifestList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _manifestList.AddThemeConstantOverride("separation", 2);
        manifestRow.AddChild(_manifestList);

        var conditionRow = new HBoxContainer();
        conditionRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(conditionRow);

        var conditionKicker = new Label
        {
            Text = "WHEN",
            ThemeTypeVariation = "LabelMono",
            CustomMinimumSize = new Vector2(70f, 0f),
        };
        conditionKicker.AddThemeFontSizeOverride("font_size", 9);
        conditionKicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        conditionRow.AddChild(conditionKicker);

        _conditionLabel = new Label { ThemeTypeVariation = "LabelHand" };
        _conditionLabel.AddThemeFontSizeOverride("font_size", 15);
        _conditionLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        conditionRow.AddChild(_conditionLabel);

        _watchedRow = new HBoxContainer();
        _watchedRow.AddThemeConstantOverride("separation", 4);
        conditionRow.AddChild(_watchedRow);

        var footerSep = new HSeparator();
        col.AddChild(footerSep);

        var footerRow = new HBoxContainer();
        footerRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(footerRow);

        _footerLabel = new Label { ThemeTypeVariation = "LabelMono" };
        _footerLabel.AddThemeFontSizeOverride("font_size", 10);
        _footerLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
        _footerLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footerRow.AddChild(_footerLabel);

        _stateDot = new StateDot { Radius = 4f };
        footerRow.AddChild(_stateDot);

        _stateLabel = new Label { ThemeTypeVariation = "LabelHand" };
        _stateLabel.AddThemeFontSizeOverride("font_size", 13);
        footerRow.AddChild(_stateLabel);

        var progressRow = new HBoxContainer();
        progressRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(progressRow);

        _progressBar = new ProgressBar
        {
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 8),
        };
        progressRow.AddChild(_progressBar);

        _progressLabel = new Label { ThemeTypeVariation = "LabelMono" };
        _progressLabel.AddThemeFontSizeOverride("font_size", 10);
        _progressLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
        _progressLabel.CustomMinimumSize = new Vector2(110f, 0f);
        _progressLabel.HorizontalAlignment = HorizontalAlignment.Right;
        progressRow.AddChild(_progressLabel);

        if (ShowDragRail)
        {
            BuildDragRail();
        }
    }

    private static HBoxContainer BuildLabeledRow(string kicker, out Label main, out Label sub)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var k = new Label
        {
            Text = kicker,
            ThemeTypeVariation = "LabelMono",
            CustomMinimumSize = new Vector2(70f, 0f),
        };
        k.AddThemeFontSizeOverride("font_size", 9);
        k.AddThemeColorOverride("font_color", WireColors.InkFaint);
        row.AddChild(k);

        main = new Label { ThemeTypeVariation = "LabelHand" };
        main.AddThemeFontSizeOverride("font_size", 18);
        row.AddChild(main);

        sub = new Label { ThemeTypeVariation = "LabelMono" };
        sub.AddThemeFontSizeOverride("font_size", 10);
        sub.AddThemeColorOverride("font_color", WireColors.InkFaint);
        sub.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(sub);

        return row;
    }

    private void BuildDragRail()
    {
        var rail = new ColorRect
        {
            Color = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.06f),
            AnchorBottom = 1f,
        };
        rail.OffsetRight = 26f;
        rail.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(rail);
        MoveChild(rail, 1);

        var slotStack = new VBoxContainer
        {
            AnchorBottom = 1f,
            OffsetRight = 26f,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        slotStack.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(slotStack);

        var slotNum = new Label
        {
            ThemeTypeVariation = "LabelMono",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        slotNum.AddThemeFontSizeOverride("font_size", 22);
        slotNum.AddThemeColorOverride("font_color", WireColors.Orange);
        slotNum.Name = "SlotNumber";
        slotStack.AddChild(slotNum);

        var handle = new Label
        {
            Text = "≡",
            ThemeTypeVariation = "LabelMono",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        handle.AddThemeFontSizeOverride("font_size", 16);
        handle.AddThemeColorOverride("font_color", WireColors.InkFaint);
        slotStack.AddChild(handle);
    }

    public void Bind(SlipCardData data)
    {
        _data = data;
        if (_slipNumberLabel != null) _slipNumberLabel.Text = $"RT-{data.Priority:D3}";
        if (_destLabel != null) _destLabel.Text = data.DestinationName;
        if (_destSubLabel != null)
        {
            string code = string.IsNullOrEmpty(data.DestinationCode) ? "" : data.DestinationCode + " · ";
            string via = string.IsNullOrEmpty(data.DestinationVia) ? "" : "via " + data.DestinationVia + " · ";
            _destSubLabel.Text = (code + via + data.DestinationDistance).TrimEnd(' ', '·');
        }
        if (_manifestList != null)
        {
            foreach (var c in _manifestList.GetChildren()) c.QueueFree();
            foreach (var entry in data.Manifest)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                var icon = new Label
                {
                    Text = $"[{entry.Icon}]",
                    ThemeTypeVariation = "LabelMono",
                };
                icon.AddThemeFontSizeOverride("font_size", 10);
                icon.AddThemeColorOverride("font_color", WireColors.Ink);
                row.AddChild(icon);

                var lbl = new Label
                {
                    Text = entry.Label,
                    ThemeTypeVariation = "LabelHand",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                lbl.AddThemeFontSizeOverride("font_size", 14);
                row.AddChild(lbl);

                var qty = new Label
                {
                    Text = $"×{entry.Units}",
                    ThemeTypeVariation = "LabelMono",
                };
                qty.AddThemeFontSizeOverride("font_size", 12);
                row.AddChild(qty);
                _manifestList.AddChild(row);
            }
        }

        if (_conditionLabel != null) _conditionLabel.Text = data.ConditionLabel;

        if (_watchedRow != null)
        {
            foreach (var c in _watchedRow.GetChildren()) c.QueueFree();
            foreach (var icon in data.WatchedResourceIcons)
            {
                var pill = new PanelContainer { ThemeTypeVariation = "Pill" };
                pill.AddThemeStyleboxOverride("panel", BuildWatchedPillStyle());
                var lbl = new Label { Text = icon, ThemeTypeVariation = "LabelMono" };
                lbl.AddThemeFontSizeOverride("font_size", 9);
                pill.AddChild(lbl);
                _watchedRow.AddChild(pill);
            }
        }

        if (_footerLabel != null)
            _footerLabel.Text = $"weight {data.WeightTons:0.#} t · last {data.LastRun} ago";

        ApplyRuntime(data.Status, data.ProgressFraction, data.ProgressLabel, data.StatusLabel, data.State);

        if (ShowDragRail && _slotLabel != null)
            _slotLabel.Text = $"#{data.Priority}";

        var slotNumber = GetNodeOrNull<Label>("SlotNumber");
        if (slotNumber != null) slotNumber.Text = data.Priority.ToString();
    }

    /// <summary>
    /// Lightweight per-tick update: refreshes status text, color, state dot, and progress bar
    /// without rebuilding the manifest / destination rows. Caller is responsible for keeping
    /// the bound <see cref="SlipCardData"/> in sync.
    /// </summary>
    public void UpdateRuntime(SlipStatus status, float progress, string progressLabel, string statusLabel)
    {
        if (_data != null)
        {
            _data.Status = status;
            _data.ProgressFraction = progress;
            _data.ProgressLabel = progressLabel;
            _data.StatusLabel = statusLabel;
            _data.State = MapDotState(status, _data.State);
        }
        ApplyRuntime(status, progress, progressLabel, statusLabel, _data?.State ?? StateDot.DotState.Idle);
    }

    private void ApplyRuntime(SlipStatus status, float progress, string progressLabel, string statusLabel, StateDot.DotState dotState)
    {
        if (_stateDot != null) _stateDot.State = dotState;
        if (_stateLabel != null)
        {
            _stateLabel.Text = statusLabel;
            _stateLabel.AddThemeColorOverride("font_color", StatusColor(status));
        }
        if (_progressLabel != null)
        {
            _progressLabel.Text = progressLabel;
            _progressLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
        }
        if (_progressBar != null)
        {
            bool indeterminate = progress < 0f;
            _progressBar.Value = indeterminate ? 0 : Mathf.Clamp(progress, 0f, 1f);
            Color tint = StatusColor(status);
            if (indeterminate) tint = new Color(tint, 0.5f);
            _progressBar.Modulate = tint;
        }
    }

    private static Color StatusColor(SlipStatus status) => status switch
    {
        SlipStatus.InTransit => WireColors.Green,
        SlipStatus.Loading => WireColors.Orange,
        SlipStatus.Blocked => WireColors.Red,
        _ => WireColors.InkFaint,
    };

    private static StateDot.DotState MapDotState(SlipStatus status, StateDot.DotState fallback)
    {
        if (status == SlipStatus.Blocked) return StateDot.DotState.Block;
        if (status == SlipStatus.InTransit || status == SlipStatus.Loading) return StateDot.DotState.Run;
        return fallback;
    }

    private static StyleBoxFlat BuildWatchedPillStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.16f),
            BorderColor = WireColors.Orange,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 5,
            ContentMarginTop = 1,
            ContentMarginRight = 5,
            ContentMarginBottom = 1,
        };
    }
}
