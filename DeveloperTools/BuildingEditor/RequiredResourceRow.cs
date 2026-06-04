#if DEBUG
using Godot;
using DeveloperTools.Common;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Row for one required_resources entry: resource picker button + integer amount
/// + delete. Built programmatically. The resource button opens the shared
/// <see cref="ResourcePickerPopup"/> (grid, grouping, fuzzy search, filters).
/// </summary>
public partial class RequiredResourceRow : HBoxContainer
{
    [Signal]
    public delegate void SlotChangedEventHandler(int slotIndex, string resourceId, int amount);

    [Signal]
    public delegate void SlotDeletedEventHandler(int slotIndex);

    private int _slotIndex;
    private string _resourceId = "";
    private int _amount = 1;

    private Button _resourceButton = null!;
    private SpinBox _amountSpin = null!;
    private Button _deleteButton = null!;

    public void Configure(int slotIndex, BuildingEditorModel.RequiredResourceEdit slot)
    {
        _slotIndex = slotIndex;
        _resourceId = slot.ResourceId;
        _amount = slot.Amount;
    }

    public override void _Ready()
    {
        base._Ready();
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _resourceButton = new Button
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 0),
            ClipText = true
        };
        _resourceButton.Pressed += OnPickResource;
        AddChild(_resourceButton);

        _amountSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 1000000,
            Step = 1,
            Value = _amount,
            CustomMinimumSize = new Vector2(96, 0),
            AllowGreater = true
        };
        _amountSpin.ValueChanged += OnAmountChanged;
        AddChild(_amountSpin);

        _deleteButton = new Button { Text = "✕", TooltipText = "Remove required resource" };
        _deleteButton.Pressed += () => EmitSignal(SignalName.SlotDeleted, _slotIndex);
        AddChild(_deleteButton);

        UpdateResourceButtonText();
    }

    private void OnPickResource()
    {
        var popup = new ResourcePickerPopup();
        popup.ResourcePicked += id =>
        {
            _resourceId = id;
            UpdateResourceButtonText();
            EmitChange();
        };
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }

    private void UpdateResourceButtonText()
    {
        _resourceButton.Text = string.IsNullOrEmpty(_resourceId) ? "(select resource)" : _resourceId;
        _resourceButton.TooltipText = _resourceId;
    }

    private void OnAmountChanged(double value)
    {
        _amount = (int)value;
        EmitChange();
    }

    private void EmitChange()
    {
        EmitSignal(SignalName.SlotChanged, _slotIndex, _resourceId, _amount);
    }
}
#endif
