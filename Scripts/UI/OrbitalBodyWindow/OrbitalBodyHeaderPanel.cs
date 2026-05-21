using Godot;
using ProceduralGeneration;
using UI.Components;

namespace UI;

/// <summary>
/// Top-left cartouche showing body name, designation, and a paper-style
/// stats grid. Public API (Populate/Clear) is preserved so the parent window
/// is unaffected.
/// </summary>
public partial class OrbitalBodyHeaderPanel : Control
{
    [Export]
    private ChamferedPanel? _namePlate;

    [Export]
    private Label? _atlasBannerLabel;

    [Export]
    private Label? _bodyNameLabel;

    [Export]
    private Label? _aliasLabel;

    [Export]
    private Label? _designationLabel;

    [Export]
    private ChamferedPanel? _statsBlock;

    [Export]
    private GridContainer? _statsGrid;

    public void Populate(IOrbitalBody body)
    {
        if (_bodyNameLabel != null)
            _bodyNameLabel.Text = body.BodyName;

        if (_aliasLabel != null)
            _aliasLabel.Text = PlanetCartoucheData.GetClassification(body);

        if (_designationLabel != null)
        {
            string sector = PlanetCartoucheData.GetDesignation(body);
            _designationLabel.Text = string.IsNullOrEmpty(sector)
                ? "BY THE SURVEYOR GENERAL"
                : $"BY THE SURVEYOR GENERAL · {sector}";
        }

        if (_statsGrid != null)
        {
            ClearStatsGrid();
            var rows = PlanetCartoucheData.GetStatRows(body);
            // 4-column grid: key/value/key/value so 8 rows fit in 4 visual rows.
            foreach (var row in rows)
            {
                _statsGrid.AddChild(BuildStatLabel(row.Key, isKey: true));
                _statsGrid.AddChild(BuildStatLabel(row.Value, isKey: false));
            }
        }
    }

    public void Clear()
    {
        if (_bodyNameLabel != null)
            _bodyNameLabel.Text = "";
        if (_aliasLabel != null)
            _aliasLabel.Text = "";
        if (_designationLabel != null)
            _designationLabel.Text = "";
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
