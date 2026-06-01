#if DEBUG
using System;
using Godot;

namespace DeveloperTools.EngineEditor;

/// <summary>
/// Per-engine editing card: name, specific_impulse, thrust, and description.
/// Built programmatically. Edits mutate the model entry in place and flag it
/// dirty; reorder/delete emit <see cref="CardsNeedRebuild"/>.
/// </summary>
public partial class EngineCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private EngineEditorModel _model = null!;
    private string _categoryName = "";
    private int _engineIndex;
    private EngineEditorModel.EngineEditEntry _entry = null!;

    private Button _moveUpButton = null!;
    private Button _moveDownButton = null!;

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
        BuildLayout();
    }

    private void MarkDirty() => _model.MarkDirty(_categoryName, _engineIndex);

    private void BuildLayout()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.16f, 0.19f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            BorderColor = new Color(0.3f, 0.3f, 0.35f),
            ContentMarginLeft = 8, ContentMarginTop = 6, ContentMarginRight = 8, ContentMarginBottom = 6,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3, CornerRadiusBottomLeft = 3
        });

        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);
        var nameEdit = new LineEdit
        {
            Text = _entry.Name, PlaceholderText = "name", SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        nameEdit.TextChanged += t => { _entry.Name = t; MarkDirty(); };
        header.AddChild(nameEdit);
        var actions = new VBoxContainer();
        _moveUpButton = new Button { Text = "▲", TooltipText = "Move up" };
        _moveUpButton.Pressed += OnMoveUp;
        actions.AddChild(_moveUpButton);
        _moveDownButton = new Button { Text = "▼", TooltipText = "Move down" };
        _moveDownButton.Pressed += OnMoveDown;
        actions.AddChild(_moveDownButton);
        var deleteButton = new Button { Text = "✕", TooltipText = "Delete engine" };
        deleteButton.Pressed += OnDelete;
        actions.AddChild(deleteButton);
        header.AddChild(actions);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(grid);

        grid.AddChild(new Label { Text = "Specific Impulse (s)" });
        var isp = new SpinBox
        {
            MinValue = 0, MaxValue = 1000000, Step = 1, AllowGreater = true,
            Value = _entry.SpecificImpulse, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        isp.ValueChanged += v => { _entry.SpecificImpulse = (float)v; MarkDirty(); };
        grid.AddChild(isp);

        grid.AddChild(new Label { Text = "Thrust (N)" });
        var thrust = new SpinBox
        {
            MinValue = 0, MaxValue = 100000000, Step = 1, AllowGreater = true,
            Value = _entry.Thrust, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        thrust.ValueChanged += v => { _entry.Thrust = (float)v; MarkDirty(); };
        grid.AddChild(thrust);

        grid.AddChild(new Label { Text = "Description" });
        var description = new TextEdit
        {
            Text = _entry.Description,
            PlaceholderText = "Engine description",
            CustomMinimumSize = new Vector2(0, 50),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        description.TextChanged += () => { _entry.Description = description.Text; MarkDirty(); };
        grid.AddChild(description);

        UpdateMoveButtons();
    }

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
