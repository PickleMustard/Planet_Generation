using Constructables;
using Godot;
using ProceduralGeneration;
using UI.Components;

namespace UI.StationWindow;

/// <summary>
/// Station-side equivalent of <see cref="OrbitalBodyHeaderPanel"/>. Renders the station's
/// name, classification, designation, and an 8-row stats grid driven by
/// <see cref="StationCartoucheData"/>.
/// </summary>
public partial class StationCartouchePanel : Control
{
    [Export]
    private ChamferedPanel? _namePlate;

    [Export]
    private Label? _atlasBannerLabel;

    [Export]
    private Label? _stationNameLabel;

    [Export]
    private Label? _aliasLabel;

    [Export]
    private Label? _designationLabel;

    [Export]
    private ChamferedPanel? _statsBlock;

    [Export]
    private GridContainer? _statsGrid;

    public void Populate(StationSatellite station)
    {
        if (_stationNameLabel != null)
            _stationNameLabel.Text = station.Name;

        if (_aliasLabel != null)
            _aliasLabel.Text = StationCartoucheData.GetClassification(station);

        if (_designationLabel != null)
            _designationLabel.Text = StationCartoucheData.GetDesignation(station);

        if (_statsGrid != null)
        {
            ClearStatsGrid();
            var rows = StationCartoucheData.GetStatRows(station);
            foreach (var row in rows)
            {
                _statsGrid.AddChild(BuildStatLabel(row.Key, isKey: true));
                _statsGrid.AddChild(BuildStatLabel(row.Value, isKey: false));
            }
        }
    }

    public void Clear()
    {
        if (_stationNameLabel != null) _stationNameLabel.Text = "";
        if (_aliasLabel != null) _aliasLabel.Text = "";
        if (_designationLabel != null) _designationLabel.Text = "";
        ClearStatsGrid();
    }

    private void ClearStatsGrid()
    {
        if (_statsGrid == null)
            return;
        foreach (var child in _statsGrid.GetChildren())
            child.QueueFree();
    }

    private static Label BuildStatLabel(string text, bool isKey)
    {
        return new Label
        {
            Text = text,
            ThemeTypeVariation = isKey ? "LabelFaint" : "LabelMono",
        };
    }
}
