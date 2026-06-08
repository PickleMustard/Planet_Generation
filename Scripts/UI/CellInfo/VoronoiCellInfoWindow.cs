using Constructables;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace UI.CellInfo;

/// <summary>
/// Main window for displaying detailed Voronoi cell information.
/// Blocks game input while open. Auto-populates when a cell is selected.
/// </summary>
public partial class VoronoiCellInfoWindow : Control
{
    public static VoronoiCellInfoWindow? Instance { get; private set; }

    [Export] private Control? _blockInput;
    [Export] private Button? _closeButton;
    [Export] private Button? _backButton;
    [Export] private Label? _headerLabel;

    [Export] private CellGeneralInfoPanel? _cellGeneralInfoPanel;
    [Export] private CellResourcePanel? _cellResourcePanel;
    [Export] private BuildingSummaryWidget? _buildingSummary;

    [Export] private Button? _openBuildingButton;
    [Export] private Button? _openOrbitalBodyButton;

    private VoronoiCell? _currentCell;
    private Node3D? _currentBody;

    public bool WindowIsVisible => Visible;

    [Signal] public delegate void WindowCloseRequestedEventHandler();
    [Signal] public delegate void BackRequestedEventHandler();
    [Signal] public delegate void BuildingDetailsRequestedEventHandler(Building building);
    [Signal] public delegate void OrbitalBodyViewRequestedEventHandler();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        Hide();

        if (_closeButton != null) _closeButton.Pressed += OnClosePressed;
        if (_backButton != null) _backButton.Pressed += OnBackPressed;
        if (_openBuildingButton != null) _openBuildingButton.Pressed += OnOpenBuildingPressed;
        if (_openOrbitalBodyButton != null) _openOrbitalBodyButton.Pressed += OnOpenOrbitalBodyPressed;

        if (_blockInput != null)
            _blockInput.GuiInput += OnBackdropInput;

        GameLogger.Info("VoronoiCellInfoWindow initialized");
    }

    public void ShowWindow(VoronoiCell cell, Node3D body)
    {
        _currentCell = cell;
        _currentBody = body;

        Show();

        PopulatePanels();

        GameLogger.Info($"VoronoiCellInfoWindow shown for cell {cell.Index}");
    }

    public void HideWindow()
    {
        Hide();
        GameLogger.Info("VoronoiCellInfoWindow hidden");
    }

    public void Clear()
    {
        _currentCell = null;
        _currentBody = null;

        if (_headerLabel != null) _headerLabel.Text = "-";
        _cellGeneralInfoPanel?.ClearDisplay();
        _cellResourcePanel?.ClearDisplay();
        _buildingSummary?.Clear();
        if (_openBuildingButton != null) _openBuildingButton.Visible = false;
    }

    private void PopulatePanels()
    {
        if (_currentCell == null || _currentBody == null)
            return;

        if (_headerLabel != null)
            _headerLabel.Text = $"Cell {_currentCell.Index} • {_currentCell.Biome} • {_currentBody.Name}";

        _cellGeneralInfoPanel?.UpdateFromCell(_currentCell);
        _cellResourcePanel?.UpdateFromCell(_currentCell);
        _buildingSummary?.UpdateFromCell(_currentCell);

        if (_openBuildingButton != null)
            _openBuildingButton.Visible = _currentCell.Building != null;
    }

    private void OnClosePressed() => EmitSignal(SignalName.WindowCloseRequested);
    private void OnBackPressed() => EmitSignal(SignalName.BackRequested);

    private void OnOpenBuildingPressed()
    {
        var building = _currentCell?.Building;
        if (building == null) return;
        EmitSignal(SignalName.BuildingDetailsRequested, building);
    }

    private void OnOpenOrbitalBodyPressed()
    {
        EmitSignal(SignalName.OrbitalBodyViewRequested);
    }

    private void OnBackdropInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent &&
            mouseEvent.ButtonIndex == MouseButton.Left &&
            mouseEvent.Pressed)
        {
            EmitSignal(SignalName.WindowCloseRequested);
        }
    }
}
