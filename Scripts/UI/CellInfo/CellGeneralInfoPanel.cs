using Godot;
using Structures.GameState;
using UtilityLibrary;

namespace UI.CellInfo;

/// <summary>
/// A reusable UI sub-panel that displays core generated attributes of a selected Voronoi cell:
/// Biome, Elevation (Height), and Slope. This panel is designed as a sub-component of a larger
/// GUI element and does not manage its own visibility or lifecycle.
/// </summary>
public partial class CellGeneralInfoPanel : Control
{
    [Export]
    private NodePath? _biomeValuePath;

    [Export]
    private NodePath? _elevationValuePath;

    [Export]
    private NodePath? _slopeValuePath;

    private Label? _biomeValueLabel;
    private Label? _elevationValueLabel;
    private Label? _slopeValueLabel;

    public override void _Ready()
    {
        // Cache node references - use exported paths if available, otherwise use default names
        if (_biomeValuePath != null)
        {
            _biomeValueLabel = GetNodeOrNull<Label>(_biomeValuePath);
        }
        else
        {
            _biomeValueLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/BiomeRow/BiomeValue");
        }

        if (_elevationValuePath != null)
        {
            _elevationValueLabel = GetNodeOrNull<Label>(_elevationValuePath);
        }
        else
        {
            _elevationValueLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/ElevationRow/ElevationValue");
        }

        if (_slopeValuePath != null)
        {
            _slopeValueLabel = GetNodeOrNull<Label>(_slopeValuePath);
        }
        else
        {
            _slopeValueLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/SlopeRow/SlopeValue");
        }

        // Initialize with cleared display
        ClearDisplay();
    }

    /// <summary>
    /// Updates the panel display with data from the selected Voronoi cell.
    /// Called automatically when CellSelectionManager emits CellSelected signal.
    /// </summary>
    /// <param name="cell">The VoronoiCell to display information for</param>
    public void UpdateFromCell(VoronoiCell cell)
    {
        if (_biomeValueLabel != null)
        {
            _biomeValueLabel.Text = cell.Biome.ToString();
        }

        if (_elevationValueLabel != null)
        {
            _elevationValueLabel.Text = $"{cell.Height:P1}";
        }

        if (_slopeValueLabel != null)
        {
            float slope = cell.GetSlope();
            _slopeValueLabel.Text = $"{slope:F1}°";
        }

        GameLogger.Debug($"CellGeneralInfoPanel updated: Biome={cell.Biome}, Height={cell.Height:P1}, Slope={cell.GetSlope():F1}°");
    }

    /// <summary>
    /// Clears all displayed values, resetting to default "-" state.
    /// Called automatically when selection is cleared.
    /// </summary>
    public void ClearDisplay()
    {
        if (_biomeValueLabel != null)
        {
            _biomeValueLabel.Text = "-";
        }

        if (_elevationValueLabel != null)
        {
            _elevationValueLabel.Text = "-";
        }

        if (_slopeValueLabel != null)
        {
            _slopeValueLabel.Text = "-";
        }

        GameLogger.Debug("CellGeneralInfoPanel display cleared");
    }

}
