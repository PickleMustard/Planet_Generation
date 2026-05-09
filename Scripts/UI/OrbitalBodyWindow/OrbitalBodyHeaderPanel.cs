using Constructables;
using Godot;

namespace UI;

/// <summary>
/// Top-left panel displaying body name, type/subtype, and constructable counts.
/// </summary>
public partial class OrbitalBodyHeaderPanel : PanelContainer
{
    [Export]
    private Label? _bodyNameLabel;

    [Export]
    private TextureRect? _typeIcon;

    [Export]
    private Label? _typeLabel;

    [Export]
    private Label? _buildingCountLabel;

    [Export]
    private Label? _stationCountLabel;

    [Export]
    private Label? _transferCountLabel;

    public void Populate(IOrbitalBody body)
    {
        GD.Print($"Populating body: {body.BodyName}");
        if (_bodyNameLabel != null)
        {
            GD.Print($"Populating body name: {body.BodyName}");
            _bodyNameLabel.Text = body.BodyName;
        }

        if (_typeLabel != null)
        {
            string typeName = body.Classification.TypeName;
            string? subtypeName = body.Classification.SubtypeAsObject?.ToString();
            _typeLabel.Text = subtypeName != null ? $"{typeName} - {subtypeName}" : typeName;
        }

        // Count stations
        int stationCount = 0;
        if (body.SatellitesContainer != null)
        {
            foreach (var child in body.SatellitesContainer.GetChildren())
            {
                if (child is StationSatellite)
                    stationCount++;
            }
        }

        // Count buildings across continents via economy registrations.
        // Cell-based iteration would overcount multi-cell buildings (one building
        // sets cell.Building on every OccupiedCell).
        int buildingCount = 0;
        if (body.Mesh?.Continents != null)
        {
            foreach (var continent in body.Mesh.Continents.Values)
            {
            }
        }

        // Active transfer count
        int transferCount = body.TransferMgr?.ActiveTransferCount ?? 0;

        if (_buildingCountLabel != null)
            _buildingCountLabel.Text = buildingCount.ToString();
        if (_stationCountLabel != null)
            _stationCountLabel.Text = stationCount.ToString();
        if (_transferCountLabel != null)
            _transferCountLabel.Text = transferCount.ToString();
    }

    public void Clear()
    {
        if (_bodyNameLabel != null)
            _bodyNameLabel.Text = "";
        if (_typeLabel != null)
            _typeLabel.Text = "";
        if (_buildingCountLabel != null)
            _buildingCountLabel.Text = "0";
        if (_stationCountLabel != null)
            _stationCountLabel.Text = "0";
        if (_transferCountLabel != null)
            _transferCountLabel.Text = "0";
    }
}
