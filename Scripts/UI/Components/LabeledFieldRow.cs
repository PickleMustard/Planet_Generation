using Godot;

namespace UI.Components;

/// <summary>
/// Row with a fixed-width kicker label, a primary value, and an optional faint
/// sub-value (the "TO / WHEN / MANIFEST" pattern). Layout lives in
/// <c>UI/Components/LabeledFieldRow.tscn</c>; styling comes from the
/// wireframe_paper theme.
/// </summary>
public partial class LabeledFieldRow : HBoxContainer
{
    [Export] private Label? _kickerLabel;
    [Export] private Label? _mainLabel;
    [Export] private Label? _subLabel;

    /// <summary>The primary value label, exposed so callers can update it live.</summary>
    public Label? MainLabel => _mainLabel;

    /// <summary>The faint secondary label, exposed so callers can update it live.</summary>
    public Label? SubLabel => _subLabel;

    private static PackedScene? _scene;

    public static LabeledFieldRow Create(string kicker, string main = "", string sub = "")
    {
        _scene ??= GD.Load<PackedScene>("res://UI/Components/LabeledFieldRow.tscn");
        var row = _scene.Instantiate<LabeledFieldRow>();
        row.Bind(kicker, main, sub);
        return row;
    }

    public void Bind(string kicker, string main = "", string sub = "")
    {
        if (_kickerLabel != null) _kickerLabel.Text = kicker;
        if (_mainLabel != null) _mainLabel.Text = main;
        if (_subLabel != null)
        {
            _subLabel.Text = sub;
            _subLabel.Visible = !string.IsNullOrEmpty(sub);
        }
    }
}
