using Constructables;
using Godot;

namespace UI.StationWindow;

/// <summary>
/// Top-left panel displaying station name, type, status, and construction progress.
/// </summary>
public partial class StationHeaderPanel : PanelContainer
{
    [Export] private Label? _stationNameLabel;
    [Export] private Label? _stationTypeLabel;
    [Export] private Label? _statusLabel;
    [Export] private Label? _bandLabel;
    [Export] private ProgressBar? _constructionProgressBar;

    public void Populate(StationSatellite station)
    {
        if (_stationNameLabel != null)
            _stationNameLabel.Text = station.Name;

        if (_stationTypeLabel != null)
            _stationTypeLabel.Text = station.StationType;

        if (_statusLabel != null)
        {
            if (station.IsUnderConstruction)
                _statusLabel.Text = $"Under Construction ({station.GetProgress() * 100:F0}%)";
            else if (station.IsActive)
                _statusLabel.Text = "Active";
            else
                _statusLabel.Text = "Inactive";
        }

        if (_bandLabel != null)
            _bandLabel.Text = $"Band {station.BandIndex}";

        if (_constructionProgressBar != null)
        {
            _constructionProgressBar.Visible = station.IsUnderConstruction;
            _constructionProgressBar.Value = station.GetProgress() * 100;
        }
    }

    public void Clear()
    {
        if (_stationNameLabel != null) _stationNameLabel.Text = "";
        if (_stationTypeLabel != null) _stationTypeLabel.Text = "";
        if (_statusLabel != null) _statusLabel.Text = "";
        if (_bandLabel != null) _bandLabel.Text = "";
        if (_constructionProgressBar != null)
        {
            _constructionProgressBar.Visible = false;
            _constructionProgressBar.Value = 0;
        }
    }
}
