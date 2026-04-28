using Godot;
using Logistics.Resources;
using Structures.Logistics;
using UtilityLibrary;
using UtilityLibrary.NameGeneration;

namespace UI.ConstructionYard;

/// <summary>
/// Modal-style ship picker shown over the ConstructionYardWindow. Lists all
/// ShipDefinitions from ShipDatabase; a confirmed selection emits ShipSelected
/// with the chosen definition and a user-editable name.
/// </summary>
public partial class ShipPickerPanel : PanelContainer
{
    [Signal]
    public delegate void ShipSelectedEventHandler(string shipDefinitionName, string shipName);

    [Signal]
    public delegate void CancelledEventHandler();

    [Export]
    private VBoxContainer? _pickerList;

    [Export]
    private PackedScene? _pickerItemScene;

    [Export]
    private LineEdit? _nameEdit;

    [Export]
    private Button? _confirmButton;

    [Export]
    private Button? _cancelButton;

    [Export]
    private Label? _selectionLabel;

    private ShipDefinition? _selectedDefinition;

    public override void _Ready()
    {
        if (_confirmButton != null)
        {
            _confirmButton.Disabled = true;
            _confirmButton.Pressed += OnConfirmPressed;
        }

        if (_cancelButton != null)
            _cancelButton.Pressed += OnCancelPressed;
    }

    public override void _ExitTree()
    {
        if (_confirmButton != null)
            _confirmButton.Pressed -= OnConfirmPressed;
        if (_cancelButton != null)
            _cancelButton.Pressed -= OnCancelPressed;
    }

    public void Populate()
    {
        if (_pickerList == null || _pickerItemScene == null)
        {
            GameLogger.Warning("ShipPickerPanel: missing picker list or item scene");
            return;
        }

        foreach (var child in _pickerList.GetChildren())
            child.QueueFree();

        _selectedDefinition = null;
        if (_confirmButton != null)
            _confirmButton.Disabled = true;
        if (_selectionLabel != null)
            _selectionLabel.Text = "Select a ship above";
        if (_nameEdit != null)
            _nameEdit.Text = string.Empty;

        var db = ShipDatabase.Instance;
        if (db == null || !db.IsLoaded)
        {
            GameLogger.Warning("ShipPickerPanel: ShipDatabase not loaded");
            return;
        }

        foreach (var kvp in db.GetAllShips())
        {
            var item = _pickerItemScene.Instantiate<ShipPickerItem>();
            _pickerList.AddChild(item);
            item.Bind(kvp.Value);
            item.Selected += OnItemSelected;
        }
    }

    private void OnItemSelected(string shipDefinitionName)
    {
        var db = ShipDatabase.Instance;
        if (db == null || !db.TryGetShip(shipDefinitionName, out var def) || def == null)
            return;

        _selectedDefinition = def;

        if (_selectionLabel != null)
            _selectionLabel.Text = $"Selected: {def.Name}";

        if (_nameEdit != null)
            _nameEdit.Text = $"{def.Name}-{NameGenerator.GenerateShipName(includeAdjective: false)}";

        if (_confirmButton != null)
            _confirmButton.Disabled = false;
    }

    private void OnConfirmPressed()
    {
        if (_selectedDefinition == null)
            return;

        string name = _nameEdit?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            name = NameGenerator.GenerateShipName();

        EmitSignal(SignalName.ShipSelected, _selectedDefinition.Name, name);
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.Cancelled);
    }
}
