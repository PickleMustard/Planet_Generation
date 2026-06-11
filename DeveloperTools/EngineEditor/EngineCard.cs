#if DEBUG
using System;
using Godot;

namespace DeveloperTools.EngineEditor;

/// <summary>
/// Per-engine editing card: name, specific_impulse, thrust, and description.
/// Layout lives in <c>EngineCard.tscn</c>; this script binds values and wires
/// edits to the model. Instantiate via <see cref="Create"/>. Edits mutate the
/// model entry in place and flag it dirty; reorder/delete emit
/// <see cref="CardsNeedRebuild"/>.
/// </summary>
public partial class EngineCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private EngineEditorModel _model = null!;
    private string _categoryName = "";
    private int _engineIndex;
    private EngineEditorModel.EngineEditEntry _entry = null!;

    [Export] private LineEdit _nameEdit = null!;
    [Export] private Button _moveUpButton = null!;
    [Export] private Button _moveDownButton = null!;
    [Export] private SpinBox _ispSpin = null!;
    [Export] private SpinBox _thrustSpin = null!;
    [Export] private TextEdit _descriptionEdit = null!;

    private static PackedScene? _scene;

    public static EngineCard Create(EngineEditorModel model, string categoryName, int engineIndex,
        EngineEditorModel.EngineEditEntry entry)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/EngineEditor/EngineCard.tscn");
        var card = _scene.Instantiate<EngineCard>();
        card.Initialize(model, categoryName, engineIndex, entry);
        return card;
    }

    public void Initialize(EngineEditorModel model, string categoryName, int engineIndex,
        EngineEditorModel.EngineEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _categoryName = categoryName;
        _engineIndex = engineIndex;
        _entry = entry;
        Name = $"EngineCard_{entry.Name}";
    }

    public override void _Ready()
    {
        base._Ready();
        _nameEdit.Text = _entry.Name;
        _ispSpin.Value = _entry.SpecificImpulse;
        _thrustSpin.Value = _entry.Thrust;
        _descriptionEdit.Text = _entry.Description;
        UpdateMoveButtons();
    }

    private void MarkDirty() => _model.MarkDirty(_categoryName, _engineIndex);

    private void OnNameChanged(string text) { _entry.Name = text; MarkDirty(); }
    private void OnIspChanged(double value) { _entry.SpecificImpulse = (float)value; MarkDirty(); }
    private void OnThrustChanged(double value) { _entry.Thrust = (float)value; MarkDirty(); }
    private void OnDescriptionChanged() { _entry.Description = _descriptionEdit.Text; MarkDirty(); }

    private void UpdateMoveButtons()
    {
        if (_model.Categories.TryGetValue(_categoryName, out var cat))
        {
            _moveUpButton.Disabled = _engineIndex <= 0;
            _moveDownButton.Disabled = _engineIndex >= cat.Engines.Count - 1;
        }
    }

    private void OnMoveUp()
    {
        if (_engineIndex <= 0) return;
        _model.MoveEngine(_categoryName, _engineIndex, _engineIndex - 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnMoveDown()
    {
        var list = _model.Categories[_categoryName].Engines;
        if (_engineIndex >= list.Count - 1) return;
        _model.MoveEngine(_categoryName, _engineIndex, _engineIndex + 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnDelete()
    {
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Engine",
            DialogText = $"Delete engine '{_entry.Name}'? Buffered until Save."
        };
        dialog.Confirmed += () =>
        {
            _model.DeleteEngine(_categoryName, _engineIndex);
            EmitSignal(SignalName.CardsNeedRebuild);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(450, 150));
    }
}
#endif
