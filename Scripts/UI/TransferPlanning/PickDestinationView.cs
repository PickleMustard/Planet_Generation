using System.Collections.Generic;
using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.Transfers;
using UI.PlanetBoard;
using UI.PlanetBoard.Modes;
using UI.Wireframe;

namespace UI.TransferPlanning;

/// <summary>
/// View 3 — Pick Destination. Embeds <see cref="PlanetBoardView"/> in
/// <see cref="TransferRoutePlanningMode"/> on the left; the right column shows
/// a destination card and the Continue / Cancel actions.
/// </summary>
public partial class PickDestinationView : Control
{
    [Signal] public delegate void DestinationConfirmedEventHandler(string destinationBuildingId);
    [Signal] public delegate void CancelledEventHandler();

    private BodyTransferManager? _mgr;
    private string _originBuildingId = "";
    private CelestialBody? _body;

    private StepIndicator? _steps;
    private PlanetBoardView? _board;
    private TransferRoutePlanningMode? _mode;
    private VBoxContainer? _destCardContent;
    private Label? _destNameLabel;
    private Label? _destCodeLabel;
    private Label? _destDistanceLabel;
    private Label? _existingRoutesLabel;
    private Button? _continueBtn;
    private string _pendingDestinationId = "";

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    public void Bind(BodyTransferManager? mgr, string originBuildingId, Node3D body)
    {
        _mgr = mgr;
        _originBuildingId = originBuildingId ?? "";
        _body = body as CelestialBody;
        _pendingDestinationId = "";
        if (_mode != null) _mode.OriginBuildingId = _originBuildingId;
    }

    public void Refresh()
    {
        if (_board != null && _body != null)
            _board.SetBody(_body);
        UpdateCard("");
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
        stepBar.AddThemeConstantOverride("margin_top", 8);
        stepBar.AddThemeConstantOverride("margin_bottom", 8);
        col.AddChild(stepBar);
        _steps = new StepIndicator();
        stepBar.AddChild(_steps);
        _steps.SetSteps(new List<StepIndicator.Step>
        {
            new() { Label = "Pick destination", State = StepIndicator.StepState.Active },
            new() { Label = "Build manifest", State = StepIndicator.StepState.Pending },
            new() { Label = "Set condition", State = StepIndicator.StepState.Pending },
            new() { Label = "Confirm", State = StepIndicator.StepState.Pending },
        });

        var split = new HSplitContainer
        {
            SplitOffset = -360,
            Collapsed = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        col.AddChild(split);

        var leftMargin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        leftMargin.AddThemeConstantOverride("margin_left", 16);
        leftMargin.AddThemeConstantOverride("margin_right", 16);
        leftMargin.AddThemeConstantOverride("margin_top", 8);
        leftMargin.AddThemeConstantOverride("margin_bottom", 8);
        split.AddChild(leftMargin);

        var leftCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        leftCol.AddThemeConstantOverride("separation", 8);
        leftMargin.AddChild(leftCol);

        var heading = new HBoxContainer();
        var headTitle = new Label
        {
            Text = "Industry Diagram Board",
            ThemeTypeVariation = "LabelHand",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        headTitle.AddThemeFontSizeOverride("font_size", 22);
        heading.AddChild(headTitle);
        leftCol.AddChild(heading);

        var hint = new Label
        {
            Text = "TAP A TRANSFER-STATION BUILDING TO MAKE IT THE DESTINATION",
            ThemeTypeVariation = "LabelMono",
        };
        hint.AddThemeFontSizeOverride("font_size", 9);
        hint.AddThemeColorOverride("font_color", WireColors.InkFaint);
        leftCol.AddChild(hint);

        var boardPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        boardPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.35f),
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
        leftCol.AddChild(boardPanel);

        _board = new PlanetBoardView { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        boardPanel.AddChild(_board);
        _mode = new TransferRoutePlanningMode { OriginBuildingId = _originBuildingId };
        _mode.DestinationPicked += OnModeDestinationPicked;
        _board.SetMode(_mode);

        var rightMargin = new MarginContainer { CustomMinimumSize = new Vector2(360, 0) };
        rightMargin.AddThemeConstantOverride("margin_left", 14);
        rightMargin.AddThemeConstantOverride("margin_right", 14);
        rightMargin.AddThemeConstantOverride("margin_top", 14);
        rightMargin.AddThemeConstantOverride("margin_bottom", 14);
        split.AddChild(rightMargin);

        var rightCol = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        rightCol.AddThemeConstantOverride("separation", 10);
        rightMargin.AddChild(rightCol);

        var kicker = new Label
        {
            Text = "SELECTED HUB",
            ThemeTypeVariation = "LabelMono",
        };
        kicker.AddThemeFontSizeOverride("font_size", 9);
        kicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        rightCol.AddChild(kicker);

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(WireColors.Orange.R, WireColors.Orange.G, WireColors.Orange.B, 0.08f),
            BorderColor = WireColors.Orange,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        });
        rightCol.AddChild(card);

        _destCardContent = new VBoxContainer();
        _destCardContent.AddThemeConstantOverride("separation", 6);
        card.AddChild(_destCardContent);

        _destCodeLabel = new Label
        {
            Text = "—",
            ThemeTypeVariation = "LabelMono",
        };
        _destCodeLabel.AddThemeFontSizeOverride("font_size", 10);
        _destCodeLabel.AddThemeColorOverride("font_color", WireColors.InkFaint);
        _destCardContent.AddChild(_destCodeLabel);

        _destNameLabel = new Label
        {
            Text = "Pick a destination on the board",
            ThemeTypeVariation = "LabelHand",
        };
        _destNameLabel.AddThemeFontSizeOverride("font_size", 24);
        _destCardContent.AddChild(_destNameLabel);

        _destDistanceLabel = new Label
        {
            ThemeTypeVariation = "LabelMono",
        };
        _destDistanceLabel.AddThemeFontSizeOverride("font_size", 11);
        _destDistanceLabel.AddThemeColorOverride("font_color", WireColors.InkSoft);
        _destCardContent.AddChild(_destDistanceLabel);

        var existingKicker = new Label
        {
            Text = "EXISTING ROUTES TO HERE",
            ThemeTypeVariation = "LabelMono",
        };
        existingKicker.AddThemeFontSizeOverride("font_size", 9);
        existingKicker.AddThemeColorOverride("font_color", WireColors.InkFaint);
        _destCardContent.AddChild(existingKicker);

        _existingRoutesLabel = new Label
        {
            Text = "—",
            ThemeTypeVariation = "LabelHand",
        };
        _existingRoutesLabel.AddThemeFontSizeOverride("font_size", 14);
        _existingRoutesLabel.AddThemeColorOverride("font_color", WireColors.InkSoft);
        _destCardContent.AddChild(_existingRoutesLabel);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        rightCol.AddChild(spacer);

        var actionBar = new TransferActionBar();
        rightCol.AddChild(actionBar);

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.Pressed += () => EmitSignal(SignalName.Cancelled);
        actionBar.LeftSlot.AddChild(cancelBtn);

        _continueBtn = new Button { Text = "Continue → Manifest", ThemeTypeVariation = "ButtonPrimary", Disabled = true };
        _continueBtn.Pressed += OnContinuePressed;
        actionBar.RightSlot.AddChild(_continueBtn);
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
        if (_destDistanceLabel != null && _mgr != null)
        {
            float travel = _mgr.ComputeTravelTime(_originBuildingId, dest);
            _destDistanceLabel.Text = $"via rail · est. transit {travel:0.#}s";
        }
        if (_existingRoutesLabel != null && _mgr != null)
        {
            int existing = _mgr.GetSchedulesForDestination(dest).Count;
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
