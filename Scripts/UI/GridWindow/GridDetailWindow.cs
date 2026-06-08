using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Constructables.Power;
using Godot;
using UI;
using UI.Components;
using UtilityLibrary;

namespace UI.GridWindow;

/// <summary>
/// Free-standing modal that inspects a single <see cref="PowerGrid"/>. Opened on top
/// of any existing window via <see cref="ShowGrid"/>; closes via the close button or
/// Escape without disturbing the underlying HSM state. Read-only inspection: no game
/// state mutations happen here.
///
/// Polls the grid every 10 physics ticks for live values and rebuilds the line chart
/// from a snapshot of <see cref="GridStatistics.GetHistorySnapshot"/>.
/// </summary>
public partial class GridDetailWindow : Control, IOverlayPanel
{
    public static GridDetailWindow? Instance { get; private set; }

    private static readonly Color StatusOk = new(0.29f, 0.65f, 0.32f);
    private static readonly Color StatusDeficit = new(0.85f, 0.55f, 0.25f);
    private static readonly Color StatusBrownout = new(0.85f, 0.20f, 0.20f);
    private static readonly Color DotIdle = new(0.55f, 0.55f, 0.6f);
    private static readonly Color DotActive = new(0.29f, 0.65f, 0.32f);
    private static readonly Color HighlightYellow = new(1f, 0.85f, 0.4f);

    private PanelContainer? _frame;
    private Label? _titleLabel;
    private Label? _statusPill;
    private Label? _genLabel;
    private Label? _drawLabel;
    private Label? _netLabel;
    private Label? _batteryLabel;
    private Label? _cellsLabel;
    private LineChart? _chart;
    private Label? _producersHeader;
    private Label? _consumersHeader;
    private Label? _batteriesHeader;
    private VBoxContainer? _producersList;
    private VBoxContainer? _consumersList;
    private VBoxContainer? _batteriesList;

    private PowerGrid? _grid;
    private int _refreshCounter;

    public bool IsOpen { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        BuildLayout();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowGrid(PowerGrid grid)
    {
        _grid = grid;
        IsOpen = true;
        // Render on top of any active HSM panel: move to the end of the parent's
        // child list so tree order places this overlay last (frontmost).
        GetParent()?.MoveChild(this, -1);
        Visible = true;
        _refreshCounter = 0;
        Refresh();
        GameLogger.Info($"[GridDetailWindow] Opened for grid {grid.Id}");
    }

    public void HideWindow()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Visible = false;
        _grid = null;
        GameLogger.Info("[GridDetailWindow] Closed");
    }

    /// <summary>Back one level. No inner stack, so equivalent to <see cref="RequestClose"/>.</summary>
    public void RequestBack() => HideWindow();

    /// <summary>Close the overlay entirely.</summary>
    public void RequestClose() => HideWindow();

    public override void _PhysicsProcess(double delta)
    {
        if (!IsOpen) return;
        _refreshCounter++;
        if (_refreshCounter >= 10)
        {
            _refreshCounter = 0;
            Refresh();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen) return;
        if (@event is InputEventKey k && k.Pressed && k.Keycode == Key.Escape)
        {
            RequestClose();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildLayout()
    {
        // Full-screen overlay; centered frame.
        AnchorRight = 1f; AnchorBottom = 1f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;

        var dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.45f),
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(dim);

        _frame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(720, 540),
            AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            OffsetLeft = -360, OffsetTop = -270, OffsetRight = 360, OffsetBottom = 270,
        };
        AddChild(_frame);

        var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        vbox.AddThemeConstantOverride("separation", 10);
        _frame.AddChild(vbox);

        // ── Header
        var headerRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headerRow.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(headerRow);

        _titleLabel = new Label { Text = "GRID", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _titleLabel.AddThemeFontSizeOverride("font_size", 18);
        headerRow.AddChild(_titleLabel);

        _statusPill = new Label { Text = "—" };
        _statusPill.AddThemeFontSizeOverride("font_size", 12);
        headerRow.AddChild(_statusPill);

        var closeBtn = new Button { Text = "X", CustomMinimumSize = new Vector2(28, 28) };
        closeBtn.Pressed += RequestClose;
        headerRow.AddChild(closeBtn);

        vbox.AddChild(new HSeparator());

        // ── Stat strip
        var stats = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stats.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(stats);
        _genLabel = AddStatTile(stats, "Generation");
        _drawLabel = AddStatTile(stats, "Draw");
        _netLabel = AddStatTile(stats, "Net");
        _batteryLabel = AddStatTile(stats, "Battery");
        _cellsLabel = AddStatTile(stats, "Cells");

        // ── Chart
        _chart = new LineChart
        {
            CustomMinimumSize = new Vector2(680, 200),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        vbox.AddChild(_chart);

        // ── Members
        var membersRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        membersRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(membersRow);

        (_producersHeader, _producersList) = AddMembersColumn(membersRow, "PRODUCERS");
        (_consumersHeader, _consumersList) = AddMembersColumn(membersRow, "CONSUMERS");
        (_batteriesHeader, _batteriesList) = AddMembersColumn(membersRow, "BATTERIES");
    }

    private static Label AddStatTile(Container parent, string caption)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 2);
        parent.AddChild(box);
        var cap = new Label { Text = caption };
        cap.AddThemeFontSizeOverride("font_size", 10);
        cap.Modulate = new Color(0.7f, 0.7f, 0.75f);
        box.AddChild(cap);
        var val = new Label { Text = "—" };
        val.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(val);
        return val;
    }

    private static (Label header, VBoxContainer list) AddMembersColumn(Container parent, string title)
    {
        var col = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddThemeConstantOverride("separation", 4);
        parent.AddChild(col);

        var header = new Label { Text = title };
        header.AddThemeFontSizeOverride("font_size", 11);
        col.AddChild(header);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddChild(scroll);

        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(list);

        return (header, list);
    }

    private void Refresh()
    {
        if (_grid == null) return;
        if (_titleLabel != null) _titleLabel.Text = $"GRID · {_grid.Id}";

        if (_statusPill != null)
        {
            float net = _grid.LastGeneration - _grid.LastDraw;
            if (_grid.IsBrownedOut) { _statusPill.Text = "BROWNOUT"; _statusPill.Modulate = StatusBrownout; }
            else if (net < 0f) { _statusPill.Text = "DEFICIT"; _statusPill.Modulate = StatusDeficit; }
            else { _statusPill.Text = "OK"; _statusPill.Modulate = StatusOk; }
        }

        if (_genLabel != null) _genLabel.Text = $"{_grid.LastGeneration:F0} kW";
        if (_drawLabel != null) _drawLabel.Text = $"{_grid.LastDraw:F0} kW";
        if (_netLabel != null)
        {
            float net = _grid.LastGeneration - _grid.LastDraw;
            _netLabel.Text = (net >= 0f ? "+" : "") + $"{net:F0} kW";
        }
        if (_batteryLabel != null)
            _batteryLabel.Text = _grid.BatteryCapacity > 0f
                ? $"{_grid.BatteryStored:F0} / {_grid.BatteryCapacity:F0} kWh"
                : "—";
        if (_cellsLabel != null) _cellsLabel.Text = _grid.CoveredCells.Count.ToString();

        _chart?.SetSamples(_grid.Stats.GetHistorySnapshot());

        if (_producersHeader != null) _producersHeader.Text = $"PRODUCERS · {_grid.LastGeneration:F0} kW";
        if (_consumersHeader != null) _consumersHeader.Text = $"CONSUMERS · {_grid.LastDraw:F0} kW";
        if (_batteriesHeader != null) _batteriesHeader.Text = $"BATTERIES · {_grid.BatteryStored:F0} / {_grid.BatteryCapacity:F0} kWh";

        ClearChildren(_producersList);
        ClearChildren(_consumersList);
        ClearChildren(_batteriesList);

        foreach (var b in _grid.Contributors)
        {
            var prod = b.GetBehavior<PowerProducerBehavior>();
            var bat = b.GetBehavior<BatteryBehavior>();
            if (prod != null)
                AddMemberRow(_producersList, b, $"{prod.EffectiveOutput:F0} / {prod.Output:F0} kW", prod.IsProducing);
            if (bat != null && bat.Capacity > 0f)
                AddMemberRow(_batteriesList, b, $"{bat.Stored:F0}/{bat.Capacity:F0} kWh", bat.Stored > 0f);
        }
        foreach (var b in _grid.Consumers)
        {
            var cons = b.GetBehavior<PowerConsumerBehavior>();
            float draw = cons?.GetCurrentDraw() ?? 0f;
            AddMemberRow(_consumersList, b, $"{draw:F0} kW", draw > 0f);
        }
    }

    private static void AddMemberRow(VBoxContainer? list, Building b, string value, bool active)
    {
        if (list == null) return;
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);

        var dot = new ColorRect
        {
            CustomMinimumSize = new Vector2(8, 8),
            Color = active ? DotActive : DotIdle,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        row.AddChild(dot);

        var name = new Label { Text = b.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(name);

        var val = new Label { Text = value };
        row.AddChild(val);

        list.AddChild(row);
    }

    private static void ClearChildren(Node? container)
    {
        if (container == null) return;
        foreach (var child in container.GetChildren()) child.QueueFree();
    }
}
