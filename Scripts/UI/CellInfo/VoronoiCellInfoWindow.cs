using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UI.CellView;
using UI.BuildingInfo;
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
    [Export] private PanelContainer? _panelContainer;
    [Export] private TextureButton? _closeButton;
    [Export] private Button? _continentViewButton;

    [Export] private CellViewPanel? _cellViewPanel;
    [Export] private CellGeneralInfoPanel? _cellGeneralInfoPanel;
    [Export] private CellResourcePanel? _cellResourcePanel;
    [Export] private BuildingInfoPanel? _buildingInfoPanel;

    [Export] private Label? _noBuildingLabel; // Shown when cell has no building

    private VoronoiCell? _currentCell;
    private Node3D? _currentBody;
    private Continent? _currentContinent;

    public bool WindowIsVisible => Visible;

    [Signal]
    public delegate void WindowCloseRequestedEventHandler();

    [Signal]
    public delegate void ContinentViewRequestedEventHandler();

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
        // Initial state: hidden
        Hide();

        // Connect signals
        if (_closeButton != null)
            _closeButton.Pressed += OnClosePressed;

        if (_continentViewButton != null)
            _continentViewButton.Pressed += OnContinentViewPressed;

        // Block input backdrop
        if (_blockInput != null)
        {
            _blockInput.GuiInput += OnBackdropInput;
        }

        GameLogger.Info("VoronoiCellInfoWindow initialized");
    }

    /// <summary>
    /// Shows the window and populates it with cell data.
    /// </summary>
    public void ShowWindow(VoronoiCell cell, Node3D body, Continent? continent)
    {
        _currentCell = cell;
        _currentBody = body;
        _currentContinent = continent;

        // Show the window
        Show();

        // Capture mouse for UI interaction
        Input.SetMouseMode(Input.MouseModeEnum.Visible);

        // Populate panels
        PopulatePanels();

        GameLogger.Info($"VoronoiCellInfoWindow shown for cell {cell.Index}");
    }

    /// <summary>
    /// Hides the window and clears selection.
    /// </summary>
    public void HideWindow()
    {
        Hide();

        // Selection clearing is handled by CellOverviewState._Exit()

        GameLogger.Info("VoronoiCellInfoWindow hidden");
    }

    /// <summary>
    /// Clears all panel data.
    /// </summary>
    public void Clear()
    {
        _currentCell = null;
        _currentBody = null;
        _currentContinent = null;

        _cellGeneralInfoPanel?.ClearDisplay();
        _cellResourcePanel?.ClearDisplay();
        _cellViewPanel?.Close();

        // Show "No Building" label, hide BuildingInfoPanel
        if (_noBuildingLabel != null)
            _noBuildingLabel.Show();
        _buildingInfoPanel?.Clear();
    }

    private void PopulatePanels()
    {
        if (_currentCell == null || _currentBody == null)
            return;

        // Cell View Panel
        if (_cellViewPanel != null && _currentBody is ISelectableBody selectableBody)
        {
            _cellViewPanel.Initialize(selectableBody, _currentCell);
        }

        // General Info Panel
        _cellGeneralInfoPanel?.UpdateFromCell(_currentCell);

        // Resource Panel
        _cellResourcePanel?.UpdateFromCell(_currentCell);

        // Building Info Panel
        PopulateBuildingInfo();
    }

    private void PopulateBuildingInfo()
    {
        var building = _currentCell?.Building;

        if (building != null && _currentContinent?.Economy != null)
        {
            _noBuildingLabel?.Hide();
            _buildingInfoPanel?.SetBuilding(building, _currentContinent.Economy);
        }
        else
        {
            _noBuildingLabel?.Show();
            _buildingInfoPanel?.Clear();
        }
    }

    private void OnClosePressed()
    {
        EmitSignal(SignalName.WindowCloseRequested);
    }

    private void OnContinentViewPressed()
    {
        EmitSignal(SignalName.ContinentViewRequested);
    }

    private void OnBackdropInput(InputEvent @event)
    {
        // Clicking the backdrop closes the window
        if (@event is InputEventMouseButton mouseEvent &&
            mouseEvent.ButtonIndex == MouseButton.Left &&
            mouseEvent.Pressed)
        {
            EmitSignal(SignalName.WindowCloseRequested);
        }
    }
}
