using Godot;

namespace UI.Components;

/// <summary>
/// Display row for a single resource quantity: optional icon, a name, and a
/// quantity string. Layout lives in <c>UI/Components/ResourceCostRow.tscn</c>;
/// styling comes from the wireframe_paper theme. Instantiate via
/// <see cref="Create"/> and populate with <see cref="Bind"/>.
/// </summary>
public partial class ResourceCostRow : HBoxContainer
{
    [Export] private TextureRect? _icon;
    [Export] private Label? _nameLabel;
    [Export] private Label? _qtyLabel;

    private static PackedScene? _scene;

    public static ResourceCostRow Create(Texture2D? icon, string name, string qty)
    {
        _scene ??= GD.Load<PackedScene>("res://UI/Components/ResourceCostRow.tscn");
        var row = _scene.Instantiate<ResourceCostRow>();
        row.Bind(icon, name, qty);
        return row;
    }

    public void Bind(Texture2D? icon, string name, string qty)
    {
        if (_icon != null)
        {
            _icon.Texture = icon;
            _icon.Visible = icon != null;
        }
        if (_nameLabel != null) _nameLabel.Text = name;
        if (_qtyLabel != null) _qtyLabel.Text = qty;
    }
}
