using Godot;
using ProceduralGeneration.PlanetGeneration;
using UI.PlanetBoard.Modes;
using UtilityLibrary;

namespace UI.PlanetBoard;

/// <summary>
/// Top-level chrome for the PlanetBoard. Holds the active <see cref="CelestialBody"/>
/// and the active <see cref="IPlanetBoardMode"/>; the embedded
/// <see cref="PlanetBoardView"/> does the actual rendering and input.
/// </summary>
public partial class PlanetBoardWindow : PanelContainer
{
    [Signal]
    public delegate void ClosedEventHandler();

    [Export] private NodePath? _viewPath;
    [Export] private NodePath? _modeSwitcherPath;
    [Export] private NodePath? _titleLabelPath;
    [Export] private NodePath? _closeButtonPath;

    private PlanetBoardView? _view;
    private OptionButton? _modeSwitcher;
    private Label? _titleLabel;
    private Button? _closeButton;

    private readonly ResourceLinkPlanningMode _linkMode = new();
    private readonly TransferRoutePlanningMode _routeMode = new();
    private readonly OverviewMode _overviewMode = new();

    public override void _Ready()
    {
        if (_viewPath != null) _view = GetNode<PlanetBoardView>(_viewPath);
        if (_modeSwitcherPath != null) _modeSwitcher = GetNode<OptionButton>(_modeSwitcherPath);
        if (_titleLabelPath != null) _titleLabel = GetNode<Label>(_titleLabelPath);
        if (_closeButtonPath != null) _closeButton = GetNode<Button>(_closeButtonPath);

        if (_modeSwitcher != null)
        {
            _modeSwitcher.Clear();
            _modeSwitcher.AddItem(_linkMode.DisplayName, 0);
            _modeSwitcher.AddItem(_routeMode.DisplayName, 1);
            _modeSwitcher.AddItem(_overviewMode.DisplayName, 2);
            _modeSwitcher.Selected = 0;
            _modeSwitcher.ItemSelected += OnModeChanged;
        }
        if (_closeButton != null)
            _closeButton.Pressed += Close;

        _view?.SetMode(_linkMode);
    }

    public void OpenForBody(CelestialBody body, string mode = "ResourceLink")
    {
        if (_titleLabel != null)
            _titleLabel.Text = $"Planet Board — {body.Name}";
        Visible = true;
        _view?.SetBody(body);
        SelectMode(mode);
    }

    public void Close()
    {
        Visible = false;
        EmitSignal(SignalName.Closed);
    }

    private void SelectMode(string mode)
    {
        IPlanetBoardMode strategy = mode switch
        {
            "TransferRoute" => _routeMode,
            "Overview" => _overviewMode,
            _ => _linkMode,
        };
        _view?.SetMode(strategy);
        if (_modeSwitcher != null)
            _modeSwitcher.Selected = strategy switch
            {
                ResourceLinkPlanningMode => 0,
                TransferRoutePlanningMode => 1,
                OverviewMode => 2,
                _ => 0,
            };
    }

    private void OnModeChanged(long index)
    {
        IPlanetBoardMode strategy = index switch
        {
            1 => _routeMode,
            2 => _overviewMode,
            _ => _linkMode,
        };
        _view?.SetMode(strategy);
    }
}
