using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Godot;
using Structures.Resources;
using Structures.Transfers;
using UI.Wireframe;

namespace UI.TransferPlanning;

/// <summary>
/// View 2 — Edit Priority. Renders the same slip cards but with drag-rail
/// chrome and reorders them via drag/drop. Persists changes through
/// <see cref="TransferStationBehavior.ReorderSchedules"/>.
/// </summary>
public partial class PriorityEditView : Control
{
    [Signal] public delegate void DoneRequestedEventHandler();

    private TransferStationBehavior? _behavior;
    private string _originBuildingId = "";

    [Export] private Label? _titleLabel;
    [Export] private GridContainer? _cardGrid;
    [Export] private TransferActionBar? _actionBar;
    private readonly List<string> _orderedIds = new();

    private static PackedScene? _scene;

    public static PriorityEditView Create()
    {
        _scene ??= GD.Load<PackedScene>("res://UI/TransferPlanning/PriorityEditView.tscn");
        return _scene.Instantiate<PriorityEditView>();
    }

    public override void _Ready()
    {
        if (_actionBar == null) return;
        var resetBtn = new Button { Text = "⟲ Reset" };
        resetBtn.Pressed += Refresh;
        _actionBar.LeftSlot.AddChild(resetBtn);

        var doneBtn = new Button { Text = "← Done", ThemeTypeVariation = "ButtonPrimary" };
        doneBtn.Pressed += () => EmitSignal(SignalName.DoneRequested);
        _actionBar.RightSlot.AddChild(doneBtn);
    }

    public void Bind(TransferStationBehavior? behavior, string originBuildingId)
    {
        _behavior = behavior;
        _originBuildingId = originBuildingId ?? "";
    }

    public void Refresh()
    {
        if (_cardGrid == null || _behavior == null) return;
        foreach (var c in _cardGrid.GetChildren()) c.QueueFree();
        _orderedIds.Clear();

        var schedules = new List<TransferSchedule>(_behavior.GetSchedulesForOrigin(_originBuildingId));
        schedules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        var resourceDb = ResourceDatabase.Instance;

        for (int i = 0; i < schedules.Count; i++)
        {
            var schedule = schedules[i];
            schedule.Priority = i + 1;
            _orderedIds.Add(schedule.ScheduleId);

            var data = SlipDataBuilder.BuildFromSchedule(schedule, _behavior, resourceDb);
            var card = DraggableSlipCard.Create();
            card.ScheduleId = schedule.ScheduleId;
            card.ParentView = this;
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _cardGrid.AddChild(card);
            card.Bind(data);
        }

        if (_titleLabel != null)
            _titleLabel.Text = $"Route Rank · {schedules.Count} slips on file";
    }

    internal void OnCardSwapRequested(string draggedId, string targetId)
    {
        if (_behavior == null) return;
        int dragIdx = _orderedIds.IndexOf(draggedId);
        int targetIdx = _orderedIds.IndexOf(targetId);
        if (dragIdx < 0 || targetIdx < 0 || dragIdx == targetIdx) return;
        _orderedIds.RemoveAt(dragIdx);
        _orderedIds.Insert(targetIdx, draggedId);
        _behavior.ReorderSchedules(_originBuildingId, _orderedIds);
        Refresh();
    }

}
