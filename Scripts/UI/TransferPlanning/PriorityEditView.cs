using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Resources;
using Structures.Transfers;
using UI.Wireframe;

namespace UI.TransferPlanning;

/// <summary>
/// View 2 — Edit Priority. Renders the same slip cards but with drag-rail
/// chrome and reorders them via drag/drop. Persists changes through
/// <see cref="BodyTransferManager.ReorderSchedules"/>.
/// </summary>
public partial class PriorityEditView : Control
{
    [Signal] public delegate void DoneRequestedEventHandler();

    private BodyTransferManager? _mgr;
    private string _originBuildingId = "";

    private Label? _titleLabel;
    private GridContainer? _cardGrid;
    private readonly List<string> _orderedIds = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    public void Bind(BodyTransferManager? mgr, string originBuildingId)
    {
        _mgr = mgr;
        _originBuildingId = originBuildingId ?? "";
    }

    public void Refresh()
    {
        if (_cardGrid == null || _mgr == null) return;
        foreach (var c in _cardGrid.GetChildren()) c.QueueFree();
        _orderedIds.Clear();

        var schedules = new List<TransferSchedule>(_mgr.GetSchedulesForOrigin(_originBuildingId));
        schedules.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        var resourceDb = ResourceDatabase.Instance;

        for (int i = 0; i < schedules.Count; i++)
        {
            var schedule = schedules[i];
            schedule.Priority = i + 1;
            _orderedIds.Add(schedule.ScheduleId);

            var data = SlipDataBuilder.BuildFromSchedule(schedule, _mgr, resourceDb);
            var card = new DraggableSlipCard
            {
                ScheduleId = schedule.ScheduleId,
                ParentView = this,
            };
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _cardGrid.AddChild(card);
            card.Bind(data);
        }

        if (_titleLabel != null)
            _titleLabel.Text = $"Route Rank · {schedules.Count} slips on file";
    }

    private void BuildLayout()
    {
        var col = new VBoxContainer();
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.AddThemeConstantOverride("separation", 10);
        AddChild(col);

        var banner = new PanelContainer();
        var bannerStyle = new StyleBoxFlat
        {
            BgColor = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.10f),
            BorderColor = WireColors.Orange,
            BorderWidthBottom = 2,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        };
        banner.AddThemeStyleboxOverride("panel", bannerStyle);
        col.AddChild(banner);

        var bannerRow = new HBoxContainer();
        bannerRow.AddThemeConstantOverride("separation", 10);
        banner.AddChild(bannerRow);
        var bannerTitle = new Label
        {
            Text = "PRIORITY EDIT MODE",
            ThemeTypeVariation = "LabelHand",
        };
        bannerTitle.AddThemeFontSizeOverride("font_size", 20);
        bannerTitle.AddThemeColorOverride("font_color", WireColors.Orange);
        bannerRow.AddChild(bannerTitle);
        var bannerHint = new Label
        {
            Text = "drag slips to reorder — slot 1 dispatches first",
            ThemeTypeVariation = "LabelSub",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        bannerHint.AddThemeFontSizeOverride("font_size", 14);
        bannerRow.AddChild(bannerHint);

        var head = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddThemeConstantOverride("separation", 10);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddChild(head);
        col.AddChild(margin);

        _titleLabel = new Label
        {
            Text = "Route Rank",
            ThemeTypeVariation = "LabelHand",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 22);
        head.AddChild(_titleLabel);

        var scrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var scrollMargin = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        scrollMargin.AddThemeConstantOverride("margin_left", 18);
        scrollMargin.AddThemeConstantOverride("margin_right", 18);
        scrollMargin.AddThemeConstantOverride("margin_top", 4);
        scrollMargin.AddChild(scrollContainer);
        col.AddChild(scrollMargin);

        _cardGrid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _cardGrid.AddThemeConstantOverride("h_separation", 12);
        _cardGrid.AddThemeConstantOverride("v_separation", 12);
        scrollContainer.AddChild(_cardGrid);

        var actionBar = new TransferActionBar();
        col.AddChild(actionBar);

        var resetBtn = new Button { Text = "⟲ Reset" };
        resetBtn.Pressed += Refresh;
        actionBar.LeftSlot.AddChild(resetBtn);

        var doneBtn = new Button { Text = "← Done", ThemeTypeVariation = "ButtonPrimary" };
        doneBtn.Pressed += () => EmitSignal(SignalName.DoneRequested);
        actionBar.RightSlot.AddChild(doneBtn);
    }

    internal void OnCardSwapRequested(string draggedId, string targetId)
    {
        if (_mgr == null) return;
        int dragIdx = _orderedIds.IndexOf(draggedId);
        int targetIdx = _orderedIds.IndexOf(targetId);
        if (dragIdx < 0 || targetIdx < 0 || dragIdx == targetIdx) return;
        _orderedIds.RemoveAt(dragIdx);
        _orderedIds.Insert(targetIdx, draggedId);
        _mgr.ReorderSchedules(_originBuildingId, _orderedIds);
        Refresh();
    }

    internal partial class DraggableSlipCard : SlipCard
    {
        public string ScheduleId = string.Empty;
        public PriorityEditView? ParentView;

        public override void _Ready()
        {
            ShowDragRail = true;
            base._Ready();
            MouseDefaultCursorShape = CursorShape.Drag;
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            var preview = new PanelContainer();
            preview.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.6f),
                BorderColor = WireColors.Ink,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 4,
                ContentMarginBottom = 4,
            });
            var lbl = new Label { Text = $"RT · {ScheduleId[..System.Math.Min(6, ScheduleId.Length)]}", ThemeTypeVariation = "LabelHand" };
            preview.AddChild(lbl);
            SetDragPreview(preview);
            return ScheduleId;
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return data.VariantType == Variant.Type.String;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (data.VariantType != Variant.Type.String) return;
            string draggedId = data.AsString();
            ParentView?.OnCardSwapRequested(draggedId, ScheduleId);
        }
    }
}
