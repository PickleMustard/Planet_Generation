#if DEBUG
using System;
using Godot;
using DeveloperTools.Common;

namespace DeveloperTools.RecipeEditor;

/// <summary>
/// Row control for one recipe output slot: Kind toggle (Resource | Tag),
/// key dropdown populated from ResourceDatabase / resource tags, amount, delete.
/// Built programmatically — no scene file.
/// </summary>
public partial class OutputSlotRow : HBoxContainer
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
    private Button _resourceButton = null!;
    private OptionButton _tagOption = null!;
    private SpinBox _amountSpin = null!;
    private Button _deleteButton = null!;

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
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _kindButton = new OptionButton { TooltipText = "Resource = specific item produced; Tag = produce a tag-discriminated resource (resolved at runtime)" };
        _kindButton.AddItem("Resource", (int)RecipeEditorModel.SlotKind.Resource);
        _kindButton.AddItem("Tag", (int)RecipeEditorModel.SlotKind.Tag);
        _kindButton.Select((int)_kind);
        _kindButton.ItemSelected += OnKindSelected;
        AddChild(_kindButton);

        _resourceButton = new Button
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 0),
            ClipText = true
        };
        _resourceButton.Pressed += OnPickResource;
        AddChild(_resourceButton);

        _tagOption = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 0)
        };
        _tagOption.ItemSelected += OnTagSelected;
        AddChild(_tagOption);

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

        _deleteButton = new Button { Text = "✕", TooltipText = "Remove output" };
        _deleteButton.Pressed += () => EmitSignal(SignalName.SlotDeleted, _slotIndex);
        AddChild(_deleteButton);

        UpdateKeyControl();
    }

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
        var popup = new ResourcePickerPopup();
        popup.ResourcePicked += id => { _key = id; UpdateKeyControl(); EmitChange(); };
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
                if (options[i] == _key) selectedIndex = i;
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
