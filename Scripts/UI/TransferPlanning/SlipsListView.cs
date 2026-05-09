using System.Collections.Generic;
using Constructables;
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

    private VBoxContainer? _sidebarList;
    private GridContainer? _cardGrid;
    private Label? _summaryLabel;
    private BodyTransferManager? _mgr;
    private string _originBuildingId = "";

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    private void BuildLayout()
    {
        var split = new HSplitContainer
        {
            SplitOffset = 320,
            Collapsed = true,
        };
        split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(split);

        var sidebar = BuildSidebar();
        split.AddChild(sidebar);

        var cardArea = BuildCardArea();
        split.AddChild(cardArea);
    }

    private Control BuildSidebar()
    {
        var sidebar = new MarginContainer
        {
            CustomMinimumSize = new Vector2(320, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        sidebar.AddThemeConstantOverride("margin_left", 14);
        sidebar.AddThemeConstantOverride("margin_right", 14);
        sidebar.AddThemeConstantOverride("margin_top", 14);
        sidebar.AddThemeConstantOverride("margin_bottom", 14);

        var col = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 10);
        sidebar.AddChild(col);

        var kicker = new Label
        {
            Text = "FOREMAN'S DESK",
            ThemeTypeVariation = "LabelMono",
        };
        kicker.AddThemeFontSizeOverride("font_size", 9);
        kicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        col.AddChild(kicker);

        var title = new Label
        {
            Text = "Outbound Slips",
            ThemeTypeVariation = "LabelHand",
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(title);

        _summaryLabel = new Label
        {
            Text = "0 on file",
            ThemeTypeVariation = "LabelSub",
        };
        col.AddChild(_summaryLabel);

        var listScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddChild(listScroll);

        _sidebarList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sidebarList.AddThemeConstantOverride("separation", 5);
        listScroll.AddChild(_sidebarList);

        var divider = new HSeparator();
        col.AddChild(divider);

        var quickKicker = new Label
        {
            Text = "QUICK ACTIONS",
            ThemeTypeVariation = "LabelMono",
        };
        quickKicker.AddThemeFontSizeOverride("font_size", 9);
        quickKicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        col.AddChild(quickKicker);

        var addBtn = new Button { Text = "＋ Add Route", ThemeTypeVariation = "ButtonPrimary" };
        addBtn.Pressed += () => EmitSignal(SignalName.AddRouteRequested);
        col.AddChild(addBtn);

        var priBtn = new Button { Text = "≡ Edit Priority" };
        priBtn.Pressed += () => EmitSignal(SignalName.EditPriorityRequested);
        col.AddChild(priBtn);

        return sidebar;
    }

    private Control BuildCardArea()
    {
        var area = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        area.AddThemeConstantOverride("margin_left", 16);
        area.AddThemeConstantOverride("margin_right", 16);
        area.AddThemeConstantOverride("margin_top", 14);
        area.AddThemeConstantOverride("margin_bottom", 14);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        area.AddChild(scroll);

        _cardGrid = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _cardGrid.AddThemeConstantOverride("h_separation", 12);
        _cardGrid.AddThemeConstantOverride("v_separation", 12);
        scroll.AddChild(_cardGrid);

        return area;
    }

    public void Bind(BodyTransferManager? mgr, string originBuildingId)
    {
        _mgr = mgr;
        _originBuildingId = originBuildingId ?? "";
    }

    public void Refresh()
    {
        if (_sidebarList == null || _cardGrid == null) return;
        foreach (var c in _sidebarList.GetChildren()) c.QueueFree();
        foreach (var c in _cardGrid.GetChildren()) c.QueueFree();

        if (_mgr == null)
        {
            ShowEmpty("Transfer manager unavailable");
            return;
        }

        var schedules = _mgr.GetSchedulesForOrigin(_originBuildingId);
        if (schedules.Count == 0)
        {
            ShowEmpty("No slips on file. Tap ＋ Add Route to begin a manifest.");
            UpdateSummary(0);
            return;
        }

        var resourceDb = ResourceDatabase.Instance;
        var ordered = new List<TransferSchedule>(schedules);
        ordered.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        for (int i = 0; i < ordered.Count; i++)
        {
            var schedule = ordered[i];
            schedule.Priority = i + 1;
            var data = SlipDataBuilder.BuildFromSchedule(schedule, _mgr, resourceDb);

            var card = new SlipCard();
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.EditRequested += id => EmitSignal(SignalName.EditSlipRequested, id);
            card.DeleteRequested += id => EmitSignal(SignalName.DeleteSlipRequested, id);
            _cardGrid!.AddChild(card);
            card.Bind(data);

            var indexRow = BuildIndexRow(data);
            _sidebarList!.AddChild(indexRow);
        }

        UpdateSummary(ordered.Count);
    }

    private Control BuildIndexRow(SlipCardData data)
    {
        var row = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var stylebox = new StyleBoxFlat
        {
            BgColor = data.Priority == 1
                ? new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.10f)
                : new Color(1f, 1f, 1f, 0.4f),
            BorderColor = data.Priority == 1 ? WireColors.Orange : new Color(WireColors.Ink, 0.5f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 5,
            ContentMarginBottom = 5,
        };
        row.AddThemeStyleboxOverride("panel", stylebox);

        var rowBox = new HBoxContainer();
        rowBox.AddThemeConstantOverride("separation", 8);
        row.AddChild(rowBox);

        var num = new Label
        {
            Text = $"RT-{data.Priority:D3}",
            ThemeTypeVariation = "LabelMono",
        };
        num.AddThemeFontSizeOverride("font_size", 10);
        num.AddThemeColorOverride("font_color", WireColors.InkFaint);
        rowBox.AddChild(num);

        var name = new Label
        {
            Text = data.DestinationName,
            ThemeTypeVariation = "LabelHand",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
        };
        name.AddThemeFontSizeOverride("font_size", 15);
        rowBox.AddChild(name);

        var dot = new StateDot { State = data.State, Radius = 3.5f };
        rowBox.AddChild(dot);

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
