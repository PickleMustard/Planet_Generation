using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Godot;
using Structures.Resources;
using Structures.Transfers;
using UI.Wireframe;

namespace UI.TransferPlanning;

/// <summary>
/// View 1C — Dispatch Slips. Sidebar list of slip indices on the left, two-column
/// grid of <see cref="SlipCard"/>s on the right. Owns no state — driven by the
/// list of <see cref="TransferSchedule"/>s passed via <see cref="Refresh"/>.
/// </summary>
public partial class SlipsListView : Control
{
    [Signal] public delegate void AddRouteRequestedEventHandler();
    [Signal] public delegate void EditPriorityRequestedEventHandler();
    [Signal] public delegate void EditSlipRequestedEventHandler(string scheduleId);
    [Signal] public delegate void DeleteSlipRequestedEventHandler(string scheduleId);

    [Export] private VBoxContainer? _sidebarList;
    [Export] private GridContainer? _cardGrid;
    [Export] private Label? _summaryLabel;
    private TransferStationBehavior? _behavior;
    private string _originBuildingId = "";
    private const float RuntimeTickSeconds = 0.25f;
    private const int CompletedOneTimeCap = 6;
    private Timer? _runtimeTimer;
    private readonly Dictionary<string, SlipCard> _cardsByScheduleId = new();
    private readonly Dictionary<string, SlipCardData> _dataByScheduleId = new();
    private readonly Dictionary<string, SlipCard> _cardsByOrderId = new();
    private readonly Dictionary<string, TransferOrder> _activeOneTimeOrders = new();
    private readonly Dictionary<string, SlipCardData> _activeOneTimeData = new();
    private readonly List<SlipCardData> _completedOneTimeHistory = new();

    private static PackedScene? _scene;
    private static readonly PackedScene IndexRowScene =
        GD.Load<PackedScene>("res://UI/TransferPlanning/SlipIndexRow.tscn");

    public static SlipsListView Create()
    {
        _scene ??= GD.Load<PackedScene>("res://UI/TransferPlanning/SlipsListView.tscn");
        return _scene.Instantiate<SlipsListView>();
    }

    private void OnAddPressed() => EmitSignal(SignalName.AddRouteRequested);
    private void OnEditPriorityPressed() => EmitSignal(SignalName.EditPriorityRequested);

    public override void _Ready()
    {
        _runtimeTimer = new Timer
        {
            WaitTime = RuntimeTickSeconds,
            Autostart = false,
            OneShot = false,
        };
        _runtimeTimer.Timeout += OnRuntimeTick;
        AddChild(_runtimeTimer);
        VisibilityChanged += OnVisibilityChanged;
        OnVisibilityChanged();
    }

    private void OnVisibilityChanged()
    {
        if (_runtimeTimer == null) return;
        if (Visible && _behavior != null) _runtimeTimer.Start();
        else _runtimeTimer.Stop();
    }

    private void OnRuntimeTick()
    {
        if (_behavior == null) return;

        if (_cardsByScheduleId.Count > 0)
        {
            var schedules = _behavior.GetSchedulesForOrigin(_originBuildingId);
            foreach (var schedule in schedules)
            {
                if (!_cardsByScheduleId.TryGetValue(schedule.ScheduleId, out var card)) continue;
                if (!_dataByScheduleId.TryGetValue(schedule.ScheduleId, out var data)) continue;
                SlipDataBuilder.ApplyRuntime(data, schedule, _behavior);
                card.UpdateRuntime(data.Status, data.ProgressFraction, data.ProgressLabel, data.StatusLabel);
            }
        }

        if (_cardsByOrderId.Count > 0)
        {
            var stillActive = new HashSet<string>();
            foreach (var active in _behavior.GetActiveTransfers())
            {
                var order = active.Order;
                if (order.SourceScheduleId != null) continue;
                stillActive.Add(order.OrderId);
                if (!_cardsByOrderId.TryGetValue(order.OrderId, out var card)) continue;
                if (!_activeOneTimeData.TryGetValue(order.OrderId, out var data)) continue;
                _activeOneTimeOrders[order.OrderId] = order;
                SlipDataBuilder.ApplyOrderRuntime(data, order, completed: false);
                card.UpdateRuntime(data.Status, data.ProgressFraction, data.ProgressLabel, data.StatusLabel);
            }

            bool sawDisappearance = false;
            foreach (var prevId in _activeOneTimeOrders.Keys)
            {
                if (!stillActive.Contains(prevId)) { sawDisappearance = true; break; }
            }
            if (sawDisappearance) Refresh();
        }
    }

    public void Bind(TransferStationBehavior? behavior, string originBuildingId)
    {
        _behavior = behavior;
        _originBuildingId = originBuildingId ?? "";
        OnVisibilityChanged();
    }

    public void Refresh()
    {
        if (_sidebarList == null || _cardGrid == null) return;
        foreach (var c in _sidebarList.GetChildren()) c.QueueFree();
        foreach (var c in _cardGrid.GetChildren()) c.QueueFree();
        _cardsByScheduleId.Clear();
        _dataByScheduleId.Clear();
        _cardsByOrderId.Clear();

        if (_behavior == null)
        {
            ShowEmpty("Transfer behavior unavailable");
            return;
        }

        var resourceDb = ResourceDatabase.Instance;
        var schedules = _behavior.GetSchedulesForOrigin(_originBuildingId);

        // Promote any one-time orders that disappeared since last refresh into history.
        SyncOneTimeOrders();

        int totalCards = 0;

        var ordered = new List<TransferSchedule>(schedules);
        ordered.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        for (int i = 0; i < ordered.Count; i++)
        {
            var schedule = ordered[i];
            schedule.Priority = i + 1;
            var data = SlipDataBuilder.BuildFromSchedule(schedule, _behavior, resourceDb);

            var card = SlipCard.Create();
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.EditRequested += id => EmitSignal(SignalName.EditSlipRequested, id);
            card.DeleteRequested += id => EmitSignal(SignalName.DeleteSlipRequested, id);
            _cardGrid!.AddChild(card);
            card.Bind(data);

            _cardsByScheduleId[schedule.ScheduleId] = card;
            _dataByScheduleId[schedule.ScheduleId] = data;
            _sidebarList!.AddChild(BuildIndexRow(data));
            totalCards++;
        }

        int oneTimeIndex = 1;
        foreach (var kvp in _activeOneTimeOrders)
        {
            var order = kvp.Value;
            var data = SlipDataBuilder.BuildFromOrder(order, _behavior, resourceDb, completed: false, displayPriority: oneTimeIndex);
            _activeOneTimeData[order.OrderId] = data;

            var card = SlipCard.Create();
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _cardGrid!.AddChild(card);
            card.Bind(data);

            _cardsByOrderId[order.OrderId] = card;
            _sidebarList!.AddChild(BuildIndexRow(data));
            oneTimeIndex++;
            totalCards++;
        }

        foreach (var data in _completedOneTimeHistory)
        {
            data.Priority = oneTimeIndex;
            var card = SlipCard.Create();
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _cardGrid!.AddChild(card);
            card.Bind(data);
            _sidebarList!.AddChild(BuildIndexRow(data));
            oneTimeIndex++;
            totalCards++;
        }

        if (totalCards == 0)
        {
            ShowEmpty("No slips on file. Tap ＋ Add Route to begin a manifest.");
            UpdateSummary(0);
            return;
        }

        UpdateSummary(totalCards);
        OnVisibilityChanged();
    }

    private void SyncOneTimeOrders()
    {
        if (_behavior == null) return;

        var currentActive = new Dictionary<string, TransferOrder>();
        foreach (var active in _behavior.GetActiveTransfers())
        {
            var order = active.Order;
            if (order.SourceScheduleId != null) continue;
            if (order.OriginBuildingId != _originBuildingId) continue;
            currentActive[order.OrderId] = order;
        }

        var disappeared = new List<string>();
        foreach (var prevId in _activeOneTimeOrders.Keys)
            if (!currentActive.ContainsKey(prevId)) disappeared.Add(prevId);

        foreach (var orderId in disappeared)
        {
            if (_activeOneTimeData.TryGetValue(orderId, out var data))
            {
                data.IsCompleted = true;
                data.Status = SlipStatus.Waiting;
                data.ProgressFraction = 1f;
                data.ProgressLabel = "delivered";
                data.StatusLabel = "delivered";
                data.State = StateDot.DotState.Idle;
                _completedOneTimeHistory.Add(data);
                while (_completedOneTimeHistory.Count > CompletedOneTimeCap)
                    _completedOneTimeHistory.RemoveAt(0);
            }
            _activeOneTimeOrders.Remove(orderId);
            _activeOneTimeData.Remove(orderId);
        }

        foreach (var kvp in currentActive)
            _activeOneTimeOrders[kvp.Key] = kvp.Value;
    }

    private Control BuildIndexRow(SlipCardData data)
    {
        var row = IndexRowScene.Instantiate<PanelContainer>();
        // Priority-1 slip gets an orange-tinted runtime stylebox; others a faint paper card.
        row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = data.Priority == 1
                ? new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.10f)
                : new Color(1f, 1f, 1f, 0.4f),
            BorderColor = data.Priority == 1 ? WireColors.Orange : new Color(WireColors.Ink, 0.5f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5,
        });

        string prefix = data.IsOneTime ? "OT" : "RT";
        row.GetNode<Label>("RowBox/Num").Text = $"{prefix}-{data.Priority:D3}";
        row.GetNode<Label>("RowBox/Name").Text = data.DestinationName;
        row.GetNode<StateDot>("RowBox/Dot").State = data.State;
        return row;
    }

    private void ShowEmpty(string message)
    {
        if (_cardGrid == null) return;
        var empty = new Label
        {
            Text = message,
            ThemeTypeVariation = "LabelHand",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        empty.AddThemeFontSizeOverride("font_size", 18);
        empty.AddThemeColorOverride("font_color", WireColors.InkFaint);
        _cardGrid.AddChild(empty);
    }

    private void UpdateSummary(int count)
    {
        if (_summaryLabel != null)
            _summaryLabel.Text = count == 1 ? "1 on file" : $"{count} on file";
    }
}
