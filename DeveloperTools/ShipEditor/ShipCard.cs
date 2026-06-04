#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using DeveloperTools.Common;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.ShipEditor;

/// <summary>
/// Per-ship editing card. GUI skeleton defined in ShipCard.tscn; data-driven
/// content (spin boxes, engine dropdown, required resources, visual, icon)
/// built at runtime. Edits mutate the model entry in place and flag it dirty;
/// reorder/delete emit <see cref="CardsNeedRebuild"/>.
/// </summary>
public partial class ShipCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private ShipEditorModel _model = null!;
    private string _categoryName = "";
    private int _shipIndex;
    private ShipEditorModel.ShipEditEntry _entry = null!;

    // Scene node references — wired in the Godot editor via the inspector.
    [Export] public LineEdit NameEdit = null!;
    [Export] public Button MoveUpButton = null!;
    [Export] public Button MoveDownButton = null!;
    [Export] public GridContainer ScalarGrid = null!;
    [Export] public VBoxContainer RequiredResourcesContainer = null!;
    [Export] public VBoxContainer Root = null!;

    // Built at runtime, not a scene ref.
    private OptionButton _engineOption = null!;

    private static PackedScene? _cardScene;

    /// <summary>
    /// Factory: loads the .tscn and instantiates it so editor-wired [Export]
    /// references resolve. Replaces direct <c>new ShipCard()</c> + <c>Initialize()</c>.
    /// </summary>
    public static ShipCard Create(ShipEditorModel model, string categoryName, int shipIndex,
        ShipEditorModel.ShipEditEntry entry)
    {
        _cardScene ??= GD.Load<PackedScene>("res://DeveloperTools/ShipEditor/ShipCard.tscn");
        var card = _cardScene.Instantiate<ShipCard>();
        card.Initialize(model, categoryName, shipIndex, entry);
        return card;
    }

    public void Initialize(ShipEditorModel model, string categoryName, int shipIndex,
        ShipEditorModel.ShipEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _categoryName = categoryName;
        _shipIndex = shipIndex;
        _entry = entry;
        Name = $"ShipCard_{entry.Name}";
    }

    public override void _Ready()
    {
        base._Ready();
        BuildDynamicContent();
    }

    private void MarkDirty() => _model.MarkDirty(_categoryName, _shipIndex);

    private void BuildDynamicContent()
    {
        NameEdit.Text = _entry.Name;

        // Description
        ScalarGrid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Description" });
        var description = new TextEdit
        {
            Text = _entry.Description,
            PlaceholderText = "Ship description",
            CustomMinimumSize = new Vector2(0, 50),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        description.TextChanged += () => { _entry.Description = description.Text; MarkDirty(); };
        ScalarGrid.AddChild(description);

        // Scalar grid: spin boxes + engine option
        AddSpin(ScalarGrid, "Dry Mass", _entry.DryMass, v => { _entry.DryMass = (float)v; MarkDirty(); });
        AddSpin(ScalarGrid, "Cargo Capacity", _entry.CargoCapacity, v => { _entry.CargoCapacity = (float)v; MarkDirty(); });
        AddSpin(ScalarGrid, "Fuel Capacity", _entry.FuelCapacity, v => { _entry.FuelCapacity = (float)v; MarkDirty(); });
        AddSpin(ScalarGrid, "Work Required", _entry.WorkRequired, v => { _entry.WorkRequired = (float)v; MarkDirty(); });

        ScalarGrid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Engine Category" });
        _engineOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        PopulateEngineOptions();
        _engineOption.ItemSelected += OnEngineSelected;
        ScalarGrid.AddChild(_engineOption);

        // Required resources
        RebuildRequiredResources();

        // Visual (full 3D preview parity) + Icon
        var visualSection = new EditorVisualSection();
        visualSection.Initialize(_entry.Visual, MarkDirty);
        Root.AddChild(visualSection);
        EditorCardControls.BuildIcon(Root, _entry.Icon, MarkDirty);

        UpdateMoveButtons();
    }

    private void OnNameChanged(string newText)
    {
        _entry.Name = newText;
        MarkDirty();
    }

    private void PopulateEngineOptions()
    {
        _engineOption.Clear();
        var categories = new List<string>(EngineConfigLoader.GetEngineCategories());
        categories.Sort();

        // Keep the entry's current value selectable even if not a known category.
        if (!string.IsNullOrEmpty(_entry.EngineCategory) && !categories.Contains(_entry.EngineCategory))
            categories.Insert(0, _entry.EngineCategory);

        int selected = -1;
        for (int i = 0; i < categories.Count; i++)
        {
            _engineOption.AddItem(categories[i]);
            if (categories[i] == _entry.EngineCategory) selected = i;
        }
        if (selected >= 0) _engineOption.Select(selected);
    }

    private void OnEngineSelected(long index)
    {
        _entry.EngineCategory = _engineOption.GetItemText((int)index);
        MarkDirty();
    }

    private void RebuildRequiredResources()
    {
        foreach (var c in RequiredResourcesContainer.GetChildren()) c.QueueFree();
        EditorCardControls.BuildRequiredResources(
            RequiredResourcesContainer, _entry.RequiredResources, MarkDirty, RebuildRequiredResources);
    }

    private void AddSpin(GridContainer grid, string label, float value, Action<double> onChanged)
    {
        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = label });
        var spin = new SpinBox
        {
            MinValue = 0, MaxValue = 10000000, Step = 0.5f, AllowGreater = true,
            Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        spin.ValueChanged += v => onChanged(v);
        grid.AddChild(spin);
    }

    private void UpdateMoveButtons()
    {
        if (_model.Categories.TryGetValue(_categoryName, out var cat))
        {
            MoveUpButton.Disabled = _shipIndex <= 0;
            MoveDownButton.Disabled = _shipIndex >= cat.Ships.Count - 1;
        }
    }

    private void OnMoveUp()
    {
        if (_shipIndex <= 0) return;
        _model.MoveShip(_categoryName, _shipIndex, _shipIndex - 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnMoveDown()
    {
        var list = _model.Categories[_categoryName].Ships;
        if (_shipIndex >= list.Count - 1) return;
        _model.MoveShip(_categoryName, _shipIndex, _shipIndex + 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnDelete()
    {
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Ship",
            DialogText = $"Delete ship '{_entry.Name}'? Buffered until Save."
        };
        dialog.Confirmed += () =>
        {
            _model.DeleteShip(_categoryName, _shipIndex);
            EmitSignal(SignalName.CardsNeedRebuild);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(450, 150));
    }
}
#endif
