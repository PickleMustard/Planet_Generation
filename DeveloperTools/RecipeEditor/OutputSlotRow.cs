#if DEBUG
using System;
using Godot;
using DeveloperTools.Common;

namespace DeveloperTools.RecipeEditor;

/// <summary>
/// Row control for one recipe output slot: Kind toggle (Resource | Tag),
/// key dropdown populated from ResourceDatabase / resource tags, amount, delete.
/// Layout lives in <c>OutputSlotRow.tscn</c>; instantiate via <see cref="Create"/>.
/// </summary>
public partial class OutputSlotRow : HBoxContainer
{
    [Signal]
    public delegate void SlotChangedEventHandler(int slotIndex, int kind, string key, float amount);

    [Signal]
    public delegate void SlotDeletedEventHandler(int slotIndex);

    private int _slotIndex;
    private RecipeEditorModel.SlotKind _kind;
    private string _key = "";
    private float _amount;

    [Export]
    private OptionButton _kindButton = null!;

    [Export]
    private Button _resourceButton = null!;

    [Export]
    private OptionButton _tagOption = null!;

    [Export]
    private SpinBox _amountSpin = null!;

    private static PackedScene? _scene;

    public static OutputSlotRow Create(int slotIndex, RecipeEditorModel.OutputSlot slot)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/RecipeEditor/OutputSlotRow.tscn");
        var row = _scene.Instantiate<OutputSlotRow>();
        row.Configure(slotIndex, slot);
        return row;
    }

    public void Configure(int slotIndex, RecipeEditorModel.OutputSlot slot)
    {
        _slotIndex = slotIndex;
        _kind = slot.Kind;
        _key = slot.Key;
        _amount = slot.Amount;
    }

    public override void _Ready()
    {
        base._Ready();
        _kindButton.AddItem("Resource", (int)RecipeEditorModel.SlotKind.Resource);
        _kindButton.AddItem("Tag", (int)RecipeEditorModel.SlotKind.Tag);
        _kindButton.Select((int)_kind);
        _amountSpin.SetValueNoSignal(_amount);
        UpdateKeyControl();
    }

    private void OnDeletePressed() => EmitSignal(SignalName.SlotDeleted, _slotIndex);

    private void OnKindSelected(long index)
    {
        _kind = (RecipeEditorModel.SlotKind)_kindButton.GetItemId((int)index);
        // Reset the key when switching kinds because options change.
        _key = "";
        UpdateKeyControl();
        EmitChange();
    }

    private void OnTagSelected(long index)
    {
        _key = _tagOption.GetItemText((int)index);
        EmitChange();
    }

    private void OnPickResource()
    {
        var popup = ResourcePickerPopup.Create();
        popup.ResourcePicked += id =>
        {
            _key = id;
            UpdateKeyControl();
            EmitChange();
        };
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }

    private void OnAmountChanged(double value)
    {
        _amount = (float)value;
        EmitChange();
    }

    /// <summary>
    /// Shows the resource picker button (Resource kind) or the tag dropdown
    /// (Tag kind) and syncs the active control to the current key.
    /// </summary>
    private void UpdateKeyControl()
    {
        bool isTag = _kind == RecipeEditorModel.SlotKind.Tag;
        _resourceButton.Visible = !isTag;
        _tagOption.Visible = isTag;

        if (isTag)
        {
            _tagOption.Clear();
            var options = RecipeEditorModel.GetAllResourceTags();
            int selectedIndex = -1;
            for (int i = 0; i < options.Count; i++)
            {
                _tagOption.AddItem(options[i], i);
                if (options[i] == _key)
                    selectedIndex = i;
            }
            if (selectedIndex < 0 && options.Count > 0)
            {
                if (!string.IsNullOrEmpty(_key))
                {
                    _tagOption.AddItem($"{_key} (unknown)", options.Count);
                    _tagOption.Select(options.Count);
                }
                else
                {
                    _tagOption.Select(0);
                    _key = options[0];
                }
            }
            else if (selectedIndex >= 0)
            {
                _tagOption.Select(selectedIndex);
            }
        }
        else
        {
            _resourceButton.Text = string.IsNullOrEmpty(_key) ? "(select resource)" : _key;
            _resourceButton.TooltipText = _key;
        }
    }

    private void EmitChange()
    {
        EmitSignal(SignalName.SlotChanged, _slotIndex, (int)_kind, _key, _amount);
    }
}
#endif
