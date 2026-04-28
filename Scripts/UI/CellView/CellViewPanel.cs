using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary;

namespace UI.CellView;

/// <summary>
/// A reusable UI panel that displays a focused view of a specific VoronoiCell
/// using a dedicated inspection camera attached to the orbital body.
/// The panel shows a SubViewport with a pulsing highlight effect on the selected cell.
/// </summary>
public partial class CellViewPanel : Control
{
    private ISelectableBody? _currentBody;
    private int _currentCellId = -1;
    private Camera3D? _viewportCamera;

    // Node references
    private SubViewport? _subViewport;
    private Button? _closeButton;
    private Label? _titleLabel;
    private Control? _header;

    [Export]
    private bool _showHeader = true;

    [Export]
    private float _cameraFov = 30.0f;

    public override void _Ready()
    {
        // Get node references
        _subViewport = GetNode<SubViewport>(
            "PanelContainer/MarginContainer/VBoxContainer/SubViewportContainer/SubViewport"
        );
        _closeButton = GetNode<Button>(
            "PanelContainer/MarginContainer/VBoxContainer/Header/CloseButton"
        );
        _titleLabel = GetNode<Label>(
            "PanelContainer/MarginContainer/VBoxContainer/Header/TitleLabel"
        );
        _header = GetNode<Control>("PanelContainer/MarginContainer/VBoxContainer/Header");

        // Show/hide header based on mode
        if (_header != null)
            _header.Visible = _showHeader;

        // Connect close button signal (only if header is visible)
        if (_closeButton != null && _showHeader)
        {
            _closeButton.Pressed += Close;
        }

        // Initially hidden
        Hide();
    }

    /// <summary>
    /// Initializes the panel to display a specific VoronoiCell on an orbital body.
    /// Positions the inspection camera, activates pulse shader, and renders to SubViewport.
    /// </summary>
    /// <param name="body">The orbital body containing the cell</param>
    /// <param name="voronoiCellId">The Index of the VoronoiCell to display</param>
    public void Initialize(ISelectableBody body, VoronoiCell cell)
    {
        if (body == null)
        {
            GameLogger.Error("CellViewPanel.Initialize: body is null");
            return;
        }

        // Store current state
        _currentBody = body;

        // Get or create camera anchor
        Node3D cameraAnchor = body.GetOrCreateCameraAnchor();

        // Position camera to focus on cell
        body.FocusInspectionCameraOnCell(cell);

        // Set up SubViewport with its own camera
        if (_subViewport != null)
        {
            // Get world from the body's node
            _subViewport.World3D = ((Node3D)body).GetWorld3D();

            // Create a camera inside the SubViewport to render from
            if (_viewportCamera == null)
            {
                _viewportCamera = new Camera3D();
                _viewportCamera.Name = "ViewportCamera";
                _subViewport.AddChild(_viewportCamera);
            }
            _viewportCamera.Fov = _cameraFov;
            _viewportCamera.GlobalTransform = cameraAnchor.GlobalTransform;
            _viewportCamera.Current = true;
        }

        // Enable pulse animation on the existing cell highlight shader
        body.Mesh?.SetPulseEnabled(true);

        // Update title
        if (_titleLabel != null)
        {
            _titleLabel.Text = $"Cell {cell.Index}";
        }

        // Show the panel so SubViewport renders
        Show();

        GameLogger.Info($"CellViewPanel initialized for cell {cell} on body");
    }

    /// <summary>
    /// Closes the panel and cleans up resources.
    /// </summary>
    public void Close()
    {
        // Disable pulse on the cell highlight shader
        _currentBody?.Mesh?.SetPulseEnabled(false);

        // Clear state
        _currentBody = null;
        _currentCellId = -1;

        // Clean up viewport camera
        if (_viewportCamera != null)
        {
            _viewportCamera.QueueFree();
            _viewportCamera = null;
        }

        // Clear SubViewport world
        if (_subViewport != null)
        {
            _subViewport.World3D = null;
        }

        // Hide panel
        Hide();

        GameLogger.Info("CellViewPanel closed");
    }

    public override void _Process(double delta)
    {
        // Sync viewport camera to the camera anchor each frame
        // so the view tracks the body as it orbits
        if (_viewportCamera != null && _currentBody?.CameraAnchor != null)
        {
            _viewportCamera.GlobalTransform = _currentBody.CameraAnchor.GlobalTransform;
        }
    }

    public override void _ExitTree()
    {
        // Clean up on exit
        Close();

        // Disconnect signals (only if we connected them)
        if (_closeButton != null && _showHeader)
        {
            _closeButton.Pressed -= Close;
        }
    }
}
