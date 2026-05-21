#if DEBUG
using Godot;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Row for one required_resources entry: resource dropdown + integer amount + delete.
/// Built programmatically. Mirrors RecipeEditor's OutputSlotRow.
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

    private OptionButton _resourceButton = null!;
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

        _resourceButton = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 0)
        };
        _resourceButton.ItemSelected += OnResourceSelected;
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

        RebuildResourceOptions();
    }

    private void OnResourceSelected(long index)
    {
        _resourceId = _resourceButton.GetItemText((int)index);
        EmitChange();
    }

    private void OnAmountChanged(double value)
    {
        _amount = (int)value;
        EmitChange();
    }

    private void RebuildResourceOptions()
    {
        _resourceButton.Clear();
        var options = BuildingEditorModel.GetAllResourceIds();
        int selectedIndex = -1;
        for (int i = 0; i < options.Count; i++)
        {
            _resourceButton.AddItem(options[i], i);
            if (options[i] == _resourceId) selectedIndex = i;
        }
        if (selectedIndex < 0)
        {
            if (!string.IsNullOrEmpty(_resourceId))
            {
                _resourceButton.AddItem($"{_resourceId} (unknown)", options.Count);
                _resourceButton.Select(options.Count);
            }
            else if (options.Count > 0)
            {
                _resourceButton.Select(0);
                _resourceId = options[0];
            }
        }
        else
        {
            _resourceButton.Select(selectedIndex);
        }
    }

    private void EmitChange()
    {
        EmitSignal(SignalName.SlotChanged, _slotIndex, _resourceId, _amount);
    }
}
#endif
