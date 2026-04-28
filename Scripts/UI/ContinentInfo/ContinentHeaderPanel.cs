using Godot;
using Structures.GameState;

namespace UI.ContinentInfo;

/// <summary>
/// Top-left panel displaying continent name, building count, power gen/use, transfers.
/// </summary>
public partial class ContinentHeaderPanel : PanelContainer
{
    [Export]
    private Label? _continentNameLabel;

    [Export]
    private Label? _typeLabel;

    [Export]
    private Label? _buildingCountLabel;

    [Export]
    private Label? _powerGenLabel;

    [Export]
    private Label? _powerUseLabel;

    [Export]
    private Label? _transferCountLabel;

    public void Populate(int continentIndex, Continent continent, IOrbitalBody body)
    {
        if (_continentNameLabel != null)
            _continentNameLabel.Text = $"Continent {continentIndex}";

        if (_typeLabel != null)
            _typeLabel.Text = continent.elevation.ToString();

        int buildings = continent.Economy?.ActiveBuildingCount ?? 0;
        float gen = continent.Economy?.PowerGeneration ?? 0f;
        float use = continent.Economy?.PowerConsumption ?? 0f;
        int transfers = body.TransferMgr?.GetActiveTransferCountForContinent(continentIndex) ?? 0;

        if (_buildingCountLabel != null)
            _buildingCountLabel.Text = buildings.ToString();
        if (_powerGenLabel != null)
            _powerGenLabel.Text = $"{gen:F1}";
        if (_powerUseLabel != null)
            _powerUseLabel.Text = $"{use:F1}";
        if (_transferCountLabel != null)
            _transferCountLabel.Text = transfers.ToString();
    }

    public void Clear()
    {
        if (_continentNameLabel != null)
            _continentNameLabel.Text = "";
        if (_typeLabel != null)
            _typeLabel.Text = "";
        if (_buildingCountLabel != null)
            _buildingCountLabel.Text = "0";
        if (_powerGenLabel != null)
            _powerGenLabel.Text = "0";
        if (_powerUseLabel != null)
            _powerUseLabel.Text = "0";
        if (_transferCountLabel != null)
            _transferCountLabel.Text = "0";
    }
}
