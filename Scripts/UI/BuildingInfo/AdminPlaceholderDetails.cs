using Godot;

namespace UI.BuildingInfo;

/// <summary>
/// Placeholder for administrative buildings while the bespoke admin panel is designed.
/// </summary>
public partial class AdminPlaceholderDetails : BaseBuildingDetails
{
    private Label? _nameLabel;
    private Label? _categoryLabel;

    public override void _Ready()
    {
        _nameLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/NameLabel");
        _categoryLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/CategoryLabel");
    }

    protected override void UpdateDisplay()
    {
        if (_nameLabel != null)
            _nameLabel.Text = _building?.Name ?? "Administrative Building";
        if (_categoryLabel != null)
            _categoryLabel.Text = _building?.Definition?.Category ?? "administration";
    }

    public override void Clear()
    {
        base.Clear();
        if (_nameLabel != null) _nameLabel.Text = "";
        if (_categoryLabel != null) _categoryLabel.Text = "";
    }
}
