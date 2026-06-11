using Godot;
using UI.Components;

namespace UI.Wireframe;

/// <summary>
/// Visual representation of a dispatch slip. Layout lives in <c>SlipCard.tscn</c>;
/// this script binds a <see cref="SlipCardData"/> and applies runtime status
/// styling. Emits <see cref="EditRequested"/> / <see cref="DeleteRequested"/> when
/// the row's icon buttons are pressed. Instantiate via <see cref="Create"/>.
/// </summary>
[GlobalClass]
public partial class SlipCard : PanelContainer
{
    [Signal] public delegate void EditRequestedEventHandler(string scheduleId);
    [Signal] public delegate void DeleteRequestedEventHandler(string scheduleId);

    [Export] public bool ShowDragRail { get; set; } = false;

    [Export] private Label? _slipNumberLabel;
    [Export] private Label? _badgeLabel;
    [Export] private Label? _slotLabel;
    [Export] private LabeledFieldRow? _toRow;
    [Export] private VBoxContainer? _manifestList;
    [Export] private Label? _conditionLabel;
    [Export] private HBoxContainer? _watchedRow;
    [Export] private Label? _footerLabel;
    [Export] private StateDot? _stateDot;
    [Export] private Label? _stateLabel;
    [Export] private ProgressBar? _progressBar;
    [Export] private Label? _progressLabel;
    [Export] private Button? _editButton;
    [Export] private Button? _deleteButton;
    [Export] private MarginContainer? _margin;
    [Export] private Control? _dragRail;
    [Export] private Label? _slotNum;

    private SlipCardData? _data;

    private static PackedScene? _scene;
    private static readonly PackedScene ManifestRowScene =
        GD.Load<PackedScene>("res://UI/Wireframe/ManifestRow.tscn");

    public static SlipCard Create(bool showDragRail = false)
    {
        _scene ??= GD.Load<PackedScene>("res://UI/Wireframe/SlipCard.tscn");
        var card = _scene.Instantiate<SlipCard>();
        card.ShowDragRail = showDragRail;
        return card;
    }

    public override void _Ready()
    {
        if (_dragRail != null) _dragRail.Visible = ShowDragRail;
        if (ShowDragRail && _margin != null)
            _margin.AddThemeConstantOverride("margin_left", 38);
    }

    private void OnEditPressed()
    {
        if (_data != null) EmitSignal(SignalName.EditRequested, _data.ScheduleId);
    }

    private void OnDeletePressed()
    {
        if (_data != null) EmitSignal(SignalName.DeleteRequested, _data.ScheduleId);
    }

    public void Bind(SlipCardData data)
    {
        _data = data;
        string prefix = data.IsOneTime ? "OT" : "RT";
        if (_slipNumberLabel != null) _slipNumberLabel.Text = $"{prefix}-{data.Priority:D3}";
        if (_badgeLabel != null) _badgeLabel.Visible = data.IsOneTime;
        if (_editButton != null) _editButton.Visible = !data.IsOneTime;
        if (_deleteButton != null) _deleteButton.Visible = !data.IsOneTime;
        Modulate = new Color(1f, 1f, 1f, data.IsCompleted ? 0.45f : 1f);

        if (_toRow != null)
        {
            string code = string.IsNullOrEmpty(data.DestinationCode) ? "" : data.DestinationCode + " · ";
            string via = string.IsNullOrEmpty(data.DestinationVia) ? "" : "via " + data.DestinationVia + " · ";
            string sub = (code + via + data.DestinationDistance).TrimEnd(' ', '·');
            _toRow.Bind("TO", data.DestinationName, sub);
        }

        if (_manifestList != null)
        {
            foreach (var c in _manifestList.GetChildren()) c.QueueFree();
            foreach (var entry in data.Manifest)
            {
                var row = ManifestRowScene.Instantiate<HBoxContainer>();
                row.GetNode<Label>("Icon").Text = $"[{entry.Icon}]";
                row.GetNode<Label>("NameLabel").Text = entry.Label;
                row.GetNode<Label>("Qty").Text = $"×{entry.Units}";
                _manifestList.AddChild(row);
            }
        }

        if (_conditionLabel != null) _conditionLabel.Text = data.ConditionLabel;

        if (_watchedRow != null)
        {
            foreach (var c in _watchedRow.GetChildren()) c.QueueFree();
            foreach (var icon in data.WatchedResourceIcons)
            {
                var pill = new PanelContainer { ThemeTypeVariation = "PillOrange" };
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
        if (_slotNum != null) _slotNum.Text = data.Priority.ToString();
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
        if (_progressLabel != null) _progressLabel.Text = progressLabel;
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
}
