#if DEBUG
using System;
using Godot;

namespace DeveloperTools.RecipeEditor;

/// <summary>
/// Row control for one recipe input slot: Kind toggle (Resource | Tag),
/// key dropdown populated from ResourceDatabase / resource tags, amount, delete.
/// Built programmatically — no scene file.
/// </summary>
public partial class InputSlotRow : HBoxContainer
{
    [Signal]
    public delegate void SlotChangedEventHandler(int slotIndex,
        int kind, string key, float amount);

    [Signal]
    public delegate void SlotDeletedEventHandler(int slotIndex);

    private int _slotIndex;
    private RecipeEditorModel.SlotKind _kind;
    private string _key = "";
    private float _amount;

    private OptionButton _kindButton = null!;
    private OptionButton _keyButton = null!;
    private SpinBox _amountSpin = null!;
    private Button _deleteButton = null!;

    public void Configure(int slotIndex, RecipeEditorModel.InputSlot slot)
    {
        _slotIndex = slotIndex;
        _kind = slot.Kind;
        _key = slot.Key;
        _amount = slot.Amount;
    }

    public override void _Ready()
    {
        base._Ready();
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _kindButton = new OptionButton { TooltipText = "Resource = specific item; Tag = any item with this tag" };
        _kindButton.AddItem("Resource", (int)RecipeEditorModel.SlotKind.Resource);
        _kindButton.AddItem("Tag", (int)RecipeEditorModel.SlotKind.Tag);
        _kindButton.Select((int)_kind);
        _kindButton.ItemSelected += OnKindSelected;
        AddChild(_kindButton);

        _keyButton = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 0)
        };
        _keyButton.ItemSelected += OnKeySelected;
        AddChild(_keyButton);

        _amountSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 1000000,
            Step = 0.01f,
            Value = _amount,
            CustomMinimumSize = new Vector2(96, 0),
            AllowGreater = true
        };
        _amountSpin.ValueChanged += OnAmountChanged;
        AddChild(_amountSpin);

        _deleteButton = new Button { Text = "✕", TooltipText = "Remove input" };
        _deleteButton.Pressed += () => EmitSignal(SignalName.SlotDeleted, _slotIndex);
        AddChild(_deleteButton);

        RebuildKeyOptions();
    }

    private void OnKindSelected(long index)
    {
        _kind = (RecipeEditorModel.SlotKind)_kindButton.GetItemId((int)index);
        // Reset the key when switching kinds because options change.
        _key = "";
        RebuildKeyOptions();
        EmitChange();
    }

    private void OnKeySelected(long index)
    {
        _key = _keyButton.GetItemText((int)index);
        EmitChange();
    }

    private void OnAmountChanged(double value)
    {
        _amount = (float)value;
        EmitChange();
    }

    private void RebuildKeyOptions()
    {
        _keyButton.Clear();
        var options = _kind == RecipeEditorModel.SlotKind.Tag
            ? RecipeEditorModel.GetAllResourceTags()
            : RecipeEditorModel.GetAllResourceIds();

        int selectedIndex = -1;
        for (int i = 0; i < options.Count; i++)
        {
            _keyButton.AddItem(options[i], i);
            if (options[i] == _key) selectedIndex = i;
        }

        if (selectedIndex < 0 && options.Count > 0)
        {
            // Unknown key — add it explicitly so it's not silently lost.
            if (!string.IsNullOrEmpty(_key))
            {
                _keyButton.AddItem($"{_key} (unknown)", options.Count);
                _keyButton.Select(options.Count);
            }
            else
            {
                _keyButton.Select(0);
                _key = options[0];
            }
        }
        else if (selectedIndex >= 0)
        {
            _keyButton.Select(selectedIndex);
        }
    }

    private void EmitChange()
    {
        EmitSignal(SignalName.SlotChanged, _slotIndex, (int)_kind, _key, _amount);
    }
}
#endif
