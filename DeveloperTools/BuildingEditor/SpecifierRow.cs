#if DEBUG
using Godot;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// One row for a building's specifier choice: default-radio (CheckBox in a
/// shared ButtonGroup), int value SpinBox, string label LineEdit, delete.
/// Built programmatically.
/// </summary>
public partial class SpecifierRow : HBoxContainer
{
    [Signal]
    public delegate void RowChangedEventHandler(int rowIndex, int value, string label);

    [Signal]
    public delegate void DefaultSelectedEventHandler(int rowIndex);

    [Signal]
    public delegate void RowDeletedEventHandler(int rowIndex);

    private int _rowIndex;
    private int _value;
    private string _label = "";
    private bool _isDefault;
    private ButtonGroup? _defaultGroup;

    [Export] private CheckBox _defaultRadio = null!;
    [Export] private SpinBox _valueSpin = null!;
    [Export] private LineEdit _labelEdit = null!;

    private static PackedScene? _scene;

    public static SpecifierRow Create(int rowIndex, BuildingEditorModel.SpecifierEntryEdit entry,
        bool isDefault, ButtonGroup defaultGroup)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BuildingEditor/SpecifierRow.tscn");
        var row = _scene.Instantiate<SpecifierRow>();
        row.Configure(rowIndex, entry, isDefault, defaultGroup);
        return row;
    }

    public void Configure(int rowIndex, BuildingEditorModel.SpecifierEntryEdit entry,
        bool isDefault, ButtonGroup defaultGroup)
    {
        _rowIndex = rowIndex;
        _value = entry.Value;
        _label = entry.Label;
        _isDefault = isDefault;
        _defaultGroup = defaultGroup;
    }

    public override void _Ready()
    {
        base._Ready();
        _defaultRadio.ButtonGroup = _defaultGroup;
        _defaultRadio.SetPressedNoSignal(_isDefault);
        _valueSpin.SetValueNoSignal(_value);
        _labelEdit.Text = _label;
    }

    private void OnDefaultPressed()
    {
        if (_defaultRadio.ButtonPressed)
            EmitSignal(SignalName.DefaultSelected, _rowIndex);
    }

    private void OnDeletePressed() => EmitSignal(SignalName.RowDeleted, _rowIndex);

    private void OnValueChanged(double v)
    {
        _value = (int)v;
        EmitSignal(SignalName.RowChanged, _rowIndex, _value, _label);
    }

    private void OnLabelChanged(string text)
    {
        _label = text;
        EmitSignal(SignalName.RowChanged, _rowIndex, _value, _label);
    }
}
#endif
