using Godot;
using Structures.Logistics;

namespace UI.ConstructionYard;

/// <summary>
/// Single selectable ship-definition row in the ShipPickerPanel list.
/// Emits Selected when its Select button is pressed.
/// </summary>
public partial class ShipPickerItem : HBoxContainer
{
    [Signal]
    public delegate void SelectedEventHandler(string shipDefinitionName);

    [Export]
    private Label? _nameLabel;

    [Export]
    private Label? _dryMassLabel;

    [Export]
    private Label? _timeLabel;

    [Export]
    private Label? _resourcesLabel;

    [Export]
    private Button? _selectButton;

    private ShipDefinition? _definition;

    public override void _Ready()
    {
        if (_selectButton != null)
            _selectButton.Pressed += OnSelectPressed;
    }

    public override void _ExitTree()
    {
        if (_selectButton != null)
            _selectButton.Pressed -= OnSelectPressed;
    }

    public void Bind(ShipDefinition definition)
    {
        _definition = definition;

        if (_nameLabel != null)
            _nameLabel.Text = definition.Name;
        if (_dryMassLabel != null)
            _dryMassLabel.Text = $"{definition.DryMass:F0} kg";
        if (_timeLabel != null)
            _timeLabel.Text = $"{definition.ConstructionTime:F0}s";

        if (_resourcesLabel != null)
        {
            var parts = new System.Text.StringBuilder();
            bool first = true;
            foreach (var kvp in definition.RequiredResources)
            {
                if (!first)
                    parts.Append(", ");
                parts.Append($"{kvp.Value} {kvp.Key}");
                first = false;
            }
            _resourcesLabel.Text = parts.Length > 0 ? parts.ToString() : "(no resources)";
        }
    }

    private void OnSelectPressed()
    {
        if (_definition != null)
            EmitSignal(SignalName.Selected, _definition.Name);
    }
}
