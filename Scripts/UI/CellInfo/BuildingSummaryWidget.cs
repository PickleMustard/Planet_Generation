using Constructables;
using Constructables.Buildings.Behaviors;
using Godot;
using Structures.GameState;

namespace UI.CellInfo;

public partial class BuildingSummaryWidget : Control
{
    [Export] private Label? _nameValue;
    [Export] private Label? _statusBadge;
    [Export] private Label? _typeValue;
    [Export] private HBoxContainer? _recipeRow;
    [Export] private Label? _recipeValue;
    [Export] private Label? _noBuildingLabel;
    [Export] private VBoxContainer? _detailsBox;

    public void UpdateFromCell(VoronoiCell cell)
    {
        if (cell.Building != null)
            SetBuilding(cell.Building);
        else
            ShowNoBuilding();
    }

    public void SetBuilding(Building building)
    {
        if (_noBuildingLabel != null) _noBuildingLabel.Visible = false;
        if (_detailsBox != null) _detailsBox.Visible = true;

        var def = building.Definition;
        var displayName = def?.DisplayName ?? def?.IdName ?? building.Name;
        var tier = def?.MaxResourceTier ?? 0;
        if (_nameValue != null)
            _nameValue.Text = tier > 0 ? $"{displayName} [T{tier}]" : displayName;

        if (_statusBadge != null)
            _statusBadge.Text = ResolveStatus(building);

        if (_typeValue != null)
            _typeValue.Text = string.IsNullOrEmpty(def?.Category) ? "—" : def!.Category!;

        var (recipeId, showRecipe) = ResolveRecipe(building);
        if (_recipeRow != null) _recipeRow.Visible = showRecipe;
        if (_recipeValue != null) _recipeValue.Text = recipeId;
    }

    public void ShowNoBuilding()
    {
        if (_detailsBox != null) _detailsBox.Visible = false;
        if (_noBuildingLabel != null) _noBuildingLabel.Visible = true;
    }

    public void Clear()
    {
        if (_nameValue != null) _nameValue.Text = "-";
        if (_statusBadge != null) _statusBadge.Text = "-";
        if (_typeValue != null) _typeValue.Text = "-";
        if (_recipeRow != null) _recipeRow.Visible = false;
        if (_recipeValue != null) _recipeValue.Text = "-";
        if (_noBuildingLabel != null) _noBuildingLabel.Visible = false;
        if (_detailsBox != null) _detailsBox.Visible = true;
    }

    private static string ResolveStatus(Building building)
    {
        if (building.IsUnderConstruction) return "Under Construction";
        if (!building.PoweredOn) return "No Power";
        return "Operational";
    }

    private static (string label, bool show) ResolveRecipe(Building building)
    {
        var mfg = building.GetBehavior<ManufacturingBehavior>();
        if (mfg != null)
            return (building.ActiveRecipeId ?? mfg.DefaultRecipe ?? "—", true);

        var ext = building.GetBehavior<ExtractionBehavior>();
        if (ext != null)
            return (building.ActiveRecipeId ?? ext.DefaultRecipe ?? "—", true);

        return ("—", false);
    }
}
