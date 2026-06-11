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

    [Export] private Button _resourceButton = null!;
    [Export] private SpinBox _amountSpin = null!;

    private static PackedScene? _scene;

    public static RequiredResourceRow Create(int slotIndex, BuildingEditorModel.RequiredResourceEdit slot)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BuildingEditor/RequiredResourceRow.tscn");
        var row = _scene.Instantiate<RequiredResourceRow>();
        row.Configure(slotIndex, slot);
        return row;
    }

    public void Configure(int slotIndex, BuildingEditorModel.RequiredResourceEdit slot)
    {
        _slotIndex = slotIndex;
        _resourceId = slot.ResourceId;
        _amount = slot.Amount;
    }

    public override void _Ready()
    {
        base._Ready();
        _amountSpin.SetValueNoSignal(_amount);
        UpdateResourceButtonText();
    }

    private void OnDeletePressed() => EmitSignal(SignalName.SlotDeleted, _slotIndex);

    private void OnPickResource()
    {
        var popup = ResourcePickerPopup.Create();
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
        _resourceButton.Text = string.IsNullOrEmpty(_resourceId)
            ? "(select resource)"
            : _resourceId;
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
