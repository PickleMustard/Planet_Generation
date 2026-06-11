#if DEBUG
using System;
using Godot;

namespace DeveloperTools.Common;

/// <summary>
/// One editable required-resource row (resource picker + amount + delete) used by
/// the editor cards. Layout lives in <c>EditorResourceRow.tscn</c>. Mutates the
/// supplied <see cref="EditorResourceAmount"/> in place and invokes the callbacks
/// so the owning card can flag dirty / rebuild. Instantiate via <see cref="Create"/>.
/// </summary>
public partial class EditorResourceRow : HBoxContainer
{
    [Export]
    private Button _idButton = null!;

    [Export]
    private SpinBox _amount = null!;

    [Export]
    private Button _deleteButton = null!;

    private EditorResourceAmount _slot = null!;
    private Action _onChanged = null!;
    private Action _onRemove = null!;

    private static PackedScene? _scene;

    public static EditorResourceRow Create(
        EditorResourceAmount slot,
        Action onChanged,
        Action onRemove
    )
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/Common/EditorResourceRow.tscn");
        var row = _scene.Instantiate<EditorResourceRow>();
        row._slot = slot;
        row._onChanged = onChanged;
        row._onRemove = onRemove;
        return row;
    }

    public override void _Ready()
    {
        base._Ready();
        _idButton.Text = string.IsNullOrEmpty(_slot.ResourceId)
            ? "(select resource)"
            : _slot.ResourceId;
        _idButton.TooltipText = _slot.ResourceId;
        _amount.SetValueNoSignal(_slot.Amount);
    }

    private void OnIdPressed()
    {
        var popup = ResourcePickerPopup.Create();
        popup.ResourcePicked += id =>
        {
            _slot.ResourceId = id;
            _idButton.Text = id;
            _idButton.TooltipText = id;
            _onChanged();
        };
        GetTree().Root.AddChild(popup);
        popup.PopupCentered();
    }

    private void OnAmountChanged(double value)
    {
        _slot.Amount = (int)value;
        _onChanged();
    }

    private void OnDeletePressed() => _onRemove();
}
#endif
