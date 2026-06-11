using System.Collections.Generic;
using Constructables;
using Constructables.Buildings.Behaviors;
using Godot;
using Structures.Transfers;
using UI.PlanetBoard;
using UI.PlanetBoard.Modes;
using UI.Wireframe;

namespace UI.TransferPlanning;

/// <summary>
/// View 3 — Pick Destination. Embeds <see cref="PlanetBoardView"/> in
/// <see cref="TransferRoutePlanningMode"/> on the left; the right column shows
/// a destination card and the Continue / Cancel actions. The layout is defined
/// in <c>PickDestinationView.tscn</c>; this class only handles behaviour.
/// When constructed via <c>new</c> (not from .tscn), the layout is built
/// programmatically in <see cref="_Ready"/> as a fallback.
/// </summary>
public partial class PickDestinationView : Control
{
    [Signal] public delegate void DestinationConfirmedEventHandler(string destinationBuildingId);
    [Signal] public delegate void CancelledEventHandler();

    private TransferStationBehavior? _behavior;
    private string _originBuildingId = "";
    private IOrbitalBody? _body;

    [Export] private StepIndicator? _steps;
    [Export] private PlanetBoardView? _board;
    [Export] private VBoxContainer? _destCardContent;
    [Export] private Label? _destNameLabel;
    [Export] private Label? _destCodeLabel;
    [Export] private Label? _destDistanceLabel;
    [Export] private Label? _existingRoutesLabel;
    [Export] private Button? _continueBtn;

    private TransferRoutePlanningMode? _mode;
    private string _pendingDestinationId = "";

    private static PackedScene? _scene;

    public static PickDestinationView Create()
    {
        _scene ??= GD.Load<PackedScene>("res://UI/TransferPlanning/PickDestinationView.tscn");
        return _scene.Instantiate<PickDestinationView>();
    }

    public override void _Ready()
    {
        _mode = new TransferRoutePlanningMode { OriginBuildingId = _originBuildingId };
        _mode.DestinationPicked += OnModeDestinationPicked;
        _board?.SetMode(_mode);

        _steps?.SetSteps(new List<StepIndicator.Step>
        {
            new() { Label = "Pick destination", State = StepIndicator.StepState.Active },
            new() { Label = "Build manifest", State = StepIndicator.StepState.Pending },
            new() { Label = "Set condition", State = StepIndicator.StepState.Pending },
            new() { Label = "Confirm", State = StepIndicator.StepState.Pending },
        });

        // Connect action buttons if they exist (from .tscn or fallback)
        var cancelBtn = GetNodeOrNull<Button>("RootVBox/MainSplit/RightMargin/RightCol/ActionBar/Root/LeftSlot/CancelBtn");
        if (cancelBtn != null)
            cancelBtn.Pressed += () => EmitSignal(SignalName.Cancelled);

        if (_continueBtn != null)
            _continueBtn.Pressed += OnContinuePressed;
    }

    public void Bind(TransferStationBehavior? behavior, string originBuildingId, Node3D body)
    {
        _behavior = behavior;
        _originBuildingId = originBuildingId ?? "";
        _body = body as IOrbitalBody;
        _pendingDestinationId = "";
        if (_mode != null) _mode.OriginBuildingId = _originBuildingId;
    }

    public void Refresh()
    {
        if (_board != null && _body != null)
            _board.SetBody(_body);
        UpdateCard("");
    }

    private void OnModeDestinationPicked(string destinationBuildingId)
    {
        _pendingDestinationId = destinationBuildingId ?? "";
        UpdateCard(_pendingDestinationId);
    }

    private void UpdateCard(string destinationId)
    {
        bool valid = !string.IsNullOrEmpty(destinationId);
        if (_continueBtn != null) _continueBtn.Disabled = !valid;

        if (!valid)
        {
            if (_destCodeLabel != null) _destCodeLabel.Text = "—";
            if (_destNameLabel != null) _destNameLabel.Text = "Pick a destination on the board";
            if (_destDistanceLabel != null) _destDistanceLabel.Text = "";
            if (_existingRoutesLabel != null) _existingRoutesLabel.Text = "—";
            return;
        }

        var dest = TransferDestination.ForBuilding(destinationId);
        if (_destCodeLabel != null)
            _destCodeLabel.Text = SlipDataBuilder.ShortDestinationCode(dest);
        if (_destNameLabel != null)
            _destNameLabel.Text = SlipDataBuilder.DescribeDestination(dest);
        if (_destDistanceLabel != null && _behavior != null)
        {
            float travel = _behavior.ComputeTravelTime(_originBuildingId, dest);
            _destDistanceLabel.Text = $"via rail · est. transit {travel:0.#}s";
        }
        if (_existingRoutesLabel != null && _behavior != null)
        {
            int existing = _behavior.GetSchedulesForDestination(dest).Count;
            _existingRoutesLabel.Text = existing == 0
                ? "no routes filed"
                : existing == 1
                    ? "1 route already filed"
                    : $"{existing} routes already filed";
        }
    }

    private void OnContinuePressed()
    {
        if (string.IsNullOrEmpty(_pendingDestinationId)) return;
        EmitSignal(SignalName.DestinationConfirmed, _pendingDestinationId);
    }
}
