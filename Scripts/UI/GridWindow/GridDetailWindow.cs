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

    [Export] private Label? _titleLabel;
    [Export] private Label? _statusPill;
    [Export] private Label? _genLabel;
    [Export] private Label? _drawLabel;
    [Export] private Label? _netLabel;
    [Export] private Label? _batteryLabel;
    [Export] private Label? _cellsLabel;
    [Export] private LineChart? _chart;
    [Export] private Label? _producersHeader;
    [Export] private Label? _consumersHeader;
    [Export] private Label? _batteriesHeader;
    [Export] private VBoxContainer? _producersList;
    [Export] private VBoxContainer? _consumersList;
    [Export] private VBoxContainer? _batteriesList;

    private static readonly PackedScene MemberRowScene =
        GD.Load<PackedScene>("res://UI/BuildingInfo/GridStatusRow.tscn");

    private PowerGrid? _grid;
    private int _refreshCounter;

    public bool IsOpen { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        Visible = false;
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
        var row = MemberRowScene.Instantiate<HBoxContainer>();
        row.GetNode<ColorRect>("Dot").Color = active ? DotActive : DotIdle;
        row.GetNode<Label>("NameLabel").Text = b.Name;
        row.GetNode<Label>("ValueLabel").Text = value;
        list.AddChild(row);
    }

    private static void ClearChildren(Node? container)
    {
        if (container == null) return;
        foreach (var child in container.GetChildren()) child.QueueFree();
    }
}
