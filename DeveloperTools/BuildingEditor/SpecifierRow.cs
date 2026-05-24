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

    private CheckBox _defaultRadio = null!;
    private SpinBox _valueSpin = null!;
    private LineEdit _labelEdit = null!;
    private Button _deleteButton = null!;

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
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _defaultRadio = new CheckBox
        {
            Text = "default",
            ButtonGroup = _defaultGroup,
            ButtonPressed = _isDefault,
            TooltipText = "Mark this value as the default specifier"
        };
        _defaultRadio.Pressed += () =>
        {
            if (_defaultRadio.ButtonPressed)
                EmitSignal(SignalName.DefaultSelected, _rowIndex);
        };
        AddChild(_defaultRadio);

        _valueSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 1000000,
            Step = 1,
            Value = _value,
            CustomMinimumSize = new Vector2(96, 0),
            AllowGreater = true
        };
        _valueSpin.ValueChanged += OnValueChanged;
        AddChild(_valueSpin);

        _labelEdit = new LineEdit
        {
            Text = _label,
            PlaceholderText = "Label (optional)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(160, 0)
        };
        _labelEdit.TextChanged += OnLabelChanged;
        AddChild(_labelEdit);

        _deleteButton = new Button { Text = "✕", TooltipText = "Remove specifier value" };
        _deleteButton.Pressed += () => EmitSignal(SignalName.RowDeleted, _rowIndex);
        AddChild(_deleteButton);
    }

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
