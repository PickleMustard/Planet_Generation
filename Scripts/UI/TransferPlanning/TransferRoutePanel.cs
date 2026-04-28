using Godot;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using Structures.Transfers;
using UtilityLibrary;

namespace UI.TransferPlanning;

/// <summary>
/// 3D globe preview showing origin/destination with route info labels.
/// Reuses the ContinentViewPanel SubViewport + inspection camera pattern.
/// </summary>
public partial class TransferRoutePanel : Control
{
    private SubViewport? _subViewport;
    private Camera3D? _viewportCamera;
    private Label? _travelTimeLabel;
    private Label? _speedLabel;
    private Label? _distanceLabel;

    private ISelectableBody? _currentBody;
    private Continent? _originContinent;

    [Export] private float _cameraFov = 30.0f;

    public override void _Ready()
    {
        _subViewport = GetNodeOrNull<SubViewport>(
            "VBoxContainer/SubViewportContainer/SubViewport");
        _travelTimeLabel = GetNodeOrNull<Label>("VBoxContainer/StatsContainer/TravelTimeLabel");
        _speedLabel = GetNodeOrNull<Label>("VBoxContainer/StatsContainer/SpeedLabel");
        _distanceLabel = GetNodeOrNull<Label>("VBoxContainer/StatsContainer/DistanceLabel");
    }

    /// <summary>
    /// Sets up the origin body and continent for the 3D preview.
    /// </summary>
    public void SetOrigin(ISelectableBody body, Continent continent)
    {
        _currentBody = body;
        _originContinent = continent;

        Node3D cameraAnchor = body.GetOrCreateCameraAnchor();
        body.FocusInspectionCameraOnContinent(continent);

        if (_subViewport != null)
        {
            _subViewport.World3D = ((Node3D)body).GetWorld3D();

            if (_viewportCamera == null)
            {
                _viewportCamera = new Camera3D();
                _viewportCamera.Name = "TransferRouteCamera";
                _subViewport.AddChild(_viewportCamera);
            }

            _viewportCamera.Fov = _cameraFov;
            _viewportCamera.GlobalTransform = cameraAnchor.GlobalTransform;
            _viewportCamera.Current = true;
        }

        body.Mesh?.SetSelectedContinent(continent.StartingIndex);

        ClearStats();
    }

    /// <summary>
    /// Updates the preview to show the destination and route stats.
    /// </summary>
    public void SetDestination(ISelectableBody body, TransferDestination destination,
        float travelTime, float speed)
    {
        if (_originContinent == null) return;

        // For continent destinations, try to position camera to see both
        if (!destination.IsOrbitalStation && destination.ContinentIndex.HasValue
            && body.Mesh?.Continents != null)
        {
            if (body.Mesh.Continents.TryGetValue(destination.ContinentIndex.Value, out var destContinent))
            {
                // Average the two continent centers and focus there (zoomed out)
                var midpoint = (_originContinent.averagedCenter + destContinent.averagedCenter) * 0.5f;
                // Re-focus on origin but set selected to destination for shader highlighting
                body.FocusInspectionCameraOnContinent(_originContinent);
                body.Mesh.SetSelectedContinent(destination.ContinentIndex.Value);
            }
        }

        // Sync camera
        if (_viewportCamera != null && body.CameraAnchor != null)
        {
            _viewportCamera.GlobalTransform = body.CameraAnchor.GlobalTransform;
        }

        // Update stats
        float distance = travelTime * speed;
        if (_travelTimeLabel != null)
            _travelTimeLabel.Text = $"Travel Time: {FormatTime(travelTime)}";
        if (_speedLabel != null)
            _speedLabel.Text = $"Speed: {speed:F1} u/s";
        if (_distanceLabel != null)
            _distanceLabel.Text = $"Distance: {distance:F1}";
    }

    public void ClearPreview()
    {
        _currentBody?.Mesh?.ClearSelectedContinent();

        if (_viewportCamera != null)
        {
            _viewportCamera.QueueFree();
            _viewportCamera = null;
        }

        if (_subViewport != null)
            _subViewport.World3D = null;

        _currentBody = null;
        _originContinent = null;
        ClearStats();
    }

    private void ClearStats()
    {
        if (_travelTimeLabel != null) _travelTimeLabel.Text = "Travel Time: --";
        if (_speedLabel != null) _speedLabel.Text = "Speed: --";
        if (_distanceLabel != null) _distanceLabel.Text = "Distance: --";
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 60f)
            return $"{seconds:F1}s";
        if (seconds < 3600f)
            return $"{seconds / 60f:F1}m";
        return $"{seconds / 3600f:F1}h";
    }

    public override void _Process(double delta)
    {
        if (_viewportCamera != null && _currentBody?.CameraAnchor != null)
        {
            _viewportCamera.GlobalTransform = _currentBody.CameraAnchor.GlobalTransform;
        }
    }

    public override void _ExitTree()
    {
        ClearPreview();
    }
}
