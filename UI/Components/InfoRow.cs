#if DEBUG
using Godot;

namespace UI.Components;

/// <summary>
/// Reusable info row component for displaying label-value pairs in debug panels.
/// </summary>
public partial class InfoRow : HBoxContainer
{
    private Label? _labelNode;
    private Label? _valueNode;

    [Export]
    public string LabelText
    {
        get => _labelNode?.Text ?? "";
        set
        {
            if (_labelNode == null)
                _labelNode = GetNodeOrNull<Label>("Label");
            if (_labelNode != null)
                _labelNode.Text = value;
        }
    }

    [Export]
    public string ValueText
    {
        get => _valueNode?.Text ?? "";
        set
        {
            if (_valueNode == null)
                _valueNode = GetNodeOrNull<Label>("Value");
            if (_valueNode != null)
                _valueNode.Text = value;
        }
    }

    public override void _Ready()
    {
        // Cache references
        _labelNode = GetNodeOrNull<Label>("Label");
        _valueNode = GetNodeOrNull<Label>("Value");
    }
}
#endif
