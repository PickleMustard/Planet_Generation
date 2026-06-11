#if DEBUG
using System;
using Godot;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Per-building card with inline editing for scalar fields plus the four
/// composite subsections (required resources, placement, behaviors, visual)
/// and an icon block. Built programmatically; subsections live in their own
/// child classes (RequiredResourceRow, PlacementRequirementsSection,
/// BehaviorRow, VisualSection, IconSection) and are wired in later passes.
/// </summary>
public partial class BuildingCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private BuildingEditorModel? _model;
    private string _categoryName = "";
    private int _buildingIndex;
    private BuildingEditorModel.BuildingEditEntry? _entry;

    [Export] private LineEdit _idNameEdit = null!;
    [Export] private LineEdit _displayNameEdit = null!;
    [Export] private TextEdit _descriptionEdit = null!;
    [Export] private LineEdit _categoryEdit = null!;
    [Export] private SpinBox _maxResourceTierSpin = null!;
    [Export] private SpinBox _workRequiredSpin = null!;
    [Export] private SpinBox _buildingLimitSpin = null!;
    [Export] private CheckBox _demolishableCheck = null!;
    [Export] private LineEdit _linkProfileEdit = null!;
    [Export] private LineEdit _allowedRecipeCategoryEdit = null!;

    [Export] private VBoxContainer _requiredResourcesContainer = null!;
    [Export] private Button _addRequiredResourceButton = null!;

    [Export] private VBoxContainer _placementContainer = null!;
    [Export] private VBoxContainer _behaviorsContainer = null!;
    [Export] private Button _addBehaviorButton = null!;
    [Export] private VBoxContainer _visualContainer = null!;
    [Export] private VBoxContainer _iconContainer = null!;

    [Export] private CheckBox _specifierEnabledCheck = null!;
    [Export] private Button _addSpecifierButton = null!;
    [Export] private VBoxContainer _specifierRowsContainer = null!;
    private ButtonGroup? _specifierDefaultGroup;

    [Export] private Button _moveUpButton = null!;
    [Export] private Button _moveDownButton = null!;
    [Export] private Button _deleteButton = null!;

    private PlacementRequirementsSection? _placementSection;
    private VisualSection? _visualSection;
    private IconSection? _iconSection;

    private static PackedScene? _scene;

    public static BuildingCard Create(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        BuildingEditorModel.BuildingEditEntry entry)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BuildingEditor/BuildingCard.tscn");
        var card = _scene.Instantiate<BuildingCard>();
        card.Initialize(model, categoryName, buildingIndex, entry);
        return card;
    }

    public void Initialize(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        BuildingEditorModel.BuildingEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _categoryName = categoryName;
        _buildingIndex = buildingIndex;
        _entry = entry;
        Name = $"BuildingCard_{entry.IdName}";
    }

    public override void _Ready()
    {
        base._Ready();
        _idNameEdit.TextChanged += t => OnFieldEdited("IdName", t);
        _displayNameEdit.TextChanged += t => OnFieldEdited("DisplayName", t);
        _categoryEdit.TextChanged += t => OnFieldEdited("Category", t);
        _descriptionEdit.TextChanged += () => OnFieldEdited("Description", _descriptionEdit.Text);
        _maxResourceTierSpin.ValueChanged += v => OnFieldEdited("MaxResourceTier", (int)v);
        _workRequiredSpin.ValueChanged += v => OnFieldEdited("WorkRequired", (float)v);
        _buildingLimitSpin.ValueChanged += v => OnFieldEdited("BuildingLimit", (int)v);
        _demolishableCheck.Toggled += b => OnFieldEdited("Demolishable", b);
        _linkProfileEdit.TextChanged += t => OnFieldEdited("LinkProfile", t);
        _allowedRecipeCategoryEdit.TextChanged += t => OnFieldEdited("AllowedRecipeCategory", t);

        _addRequiredResourceButton.Pressed += OnAddRequiredResourcePressed;
        _addBehaviorButton.Pressed += OnAddBehaviorPressed;
        _specifierEnabledCheck.Toggled += OnSpecifierEnabledToggled;
        _addSpecifierButton.Pressed += OnAddSpecifierPressed;
        _moveUpButton.Pressed += OnMoveUpPressed;
        _moveDownButton.Pressed += OnMoveDownPressed;
        _deleteButton.Pressed += OnDeletePressed;

        RefreshControls();
    }

    private void RefreshControls()
    {
        if (_entry == null || _model == null) return;

        if (_idNameEdit.Text != _entry.IdName) _idNameEdit.Text = _entry.IdName;
        if (_displayNameEdit.Text != _entry.DisplayName) _displayNameEdit.Text = _entry.DisplayName;
        if (_descriptionEdit.Text != _entry.Description) _descriptionEdit.Text = _entry.Description;
        if (_categoryEdit.Text != _entry.Category) _categoryEdit.Text = _entry.Category;
        _maxResourceTierSpin.SetValueNoSignal(_entry.MaxResourceTier);
        _workRequiredSpin.SetValueNoSignal(_entry.WorkRequired);
        _buildingLimitSpin.SetValueNoSignal(_entry.BuildingLimit);
        _demolishableCheck.SetPressedNoSignal(_entry.Demolishable);
        string lp = _entry.LinkProfile ?? "";
        if (_linkProfileEdit.Text != lp) _linkProfileEdit.Text = lp;
        string arc = _entry.AllowedRecipeCategory ?? "";
        if (_allowedRecipeCategoryEdit.Text != arc) _allowedRecipeCategoryEdit.Text = arc;

        RebuildRequiredResourceRows();
        RebuildPlacementSection();
        RebuildBehaviorRows();
        RebuildSpecifierRows();
        RebuildVisualSection();
        RebuildIconSection();

        if (_model.Categories.TryGetValue(_categoryName, out var cat))
        {
            _moveUpButton.Disabled = _buildingIndex <= 0;
            _moveDownButton.Disabled = _buildingIndex >= cat.Buildings.Count - 1;
        }
    }

    // ─── Subsection rebuilds (filled in by later passes) ─────────────────

    private void RebuildRequiredResourceRows()
    {
        foreach (var c in _requiredResourcesContainer.GetChildren()) c.QueueFree();
        if (_entry == null || _model == null) return;
        for (int i = 0; i < _entry.RequiredResources.Count; i++)
        {
            var row = RequiredResourceRow.Create(i, _entry.RequiredResources[i]);
            row.SlotChanged += OnRequiredResourceChanged;
            row.SlotDeleted += OnRequiredResourceDeleted;
            _requiredResourcesContainer.AddChild(row);
        }
    }

    private void RebuildPlacementSection()
    {
        foreach (var c in _placementContainer.GetChildren()) c.QueueFree();
        _placementSection = null;
        if (_entry == null || _model == null) return;
        _placementSection = PlacementRequirementsSection.Create(_model, _categoryName, _buildingIndex, _entry);
        _placementContainer.AddChild(_placementSection);
    }

    private void RebuildBehaviorRows()
    {
        foreach (var c in _behaviorsContainer.GetChildren()) c.QueueFree();
        if (_entry == null || _model == null) return;
        for (int i = 0; i < _entry.Behaviors.Count; i++)
        {
            var row = BehaviorRow.Create(_model, _categoryName, _buildingIndex, i, _entry.Behaviors[i]);
            row.RowDeleted += OnBehaviorRowDeleted;
            _behaviorsContainer.AddChild(row);
        }
    }

    private void RebuildVisualSection()
    {
        foreach (var c in _visualContainer.GetChildren()) c.QueueFree();
        _visualSection = null;
        if (_entry == null || _model == null) return;
        _visualSection = VisualSection.Create(_model, _categoryName, _buildingIndex, _entry);
        _visualContainer.AddChild(_visualSection);
    }

    private void RebuildIconSection()
    {
        foreach (var c in _iconContainer.GetChildren()) c.QueueFree();
        _iconSection = null;
        if (_entry == null || _model == null) return;
        _iconSection = IconSection.Create(_model, _categoryName, _buildingIndex, _entry);
        _iconContainer.AddChild(_iconSection);
    }

    private void RebuildSpecifierRows()
    {
        foreach (var c in _specifierRowsContainer.GetChildren()) c.QueueFree();
        if (_entry == null || _model == null) return;

        _specifierEnabledCheck.SetPressedNoSignal(_entry.SpecifierEnabled);
        _specifierRowsContainer.Visible = _entry.SpecifierEnabled;
        _addSpecifierButton.Disabled = !_entry.SpecifierEnabled;

        if (!_entry.SpecifierEnabled) return;

        _specifierDefaultGroup = new ButtonGroup();
        for (int i = 0; i < _entry.SpecifierEntries.Count; i++)
        {
            var slot = _entry.SpecifierEntries[i];
            var row = SpecifierRow.Create(i, slot, slot.Value == _entry.SpecifierDefault, _specifierDefaultGroup);
            row.RowChanged += OnSpecifierRowChanged;
            row.DefaultSelected += OnSpecifierDefaultSelected;
            row.RowDeleted += OnSpecifierRowDeleted;
            _specifierRowsContainer.AddChild(row);
        }
    }

    private void OnSpecifierEnabledToggled(bool enabled)
    {
        if (_model == null) return;
        _model.SetSpecifierEnabled(_categoryName, _buildingIndex, enabled);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildSpecifierRows();
    }

    private void OnAddSpecifierPressed()
    {
        if (_model == null || _entry == null) return;
        int nextValue = 1;
        if (_entry.SpecifierEntries.Count > 0)
        {
            int max = 0;
            foreach (var s in _entry.SpecifierEntries)
                if (s.Value > max) max = s.Value;
            nextValue = max + 1;
        }
        _model.AddSpecifierEntry(_categoryName, _buildingIndex,
            new BuildingEditorModel.SpecifierEntryEdit { Value = nextValue, Label = "" });
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildSpecifierRows();
    }

    private void OnSpecifierRowChanged(int rowIndex, int value, string label)
    {
        if (_model == null) return;
        _model.UpdateSpecifierEntry(_categoryName, _buildingIndex, rowIndex, value, label);
    }

    private void OnSpecifierDefaultSelected(int rowIndex)
    {
        if (_model == null || _entry == null) return;
        if (rowIndex < 0 || rowIndex >= _entry.SpecifierEntries.Count) return;
        int value = _entry.SpecifierEntries[rowIndex].Value;
        _model.SetSpecifierDefault(_categoryName, _buildingIndex, value);
    }

    private void OnSpecifierRowDeleted(int rowIndex)
    {
        if (_model == null) return;
        _model.DeleteSpecifierEntry(_categoryName, _buildingIndex, rowIndex);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildSpecifierRows();
    }

    // ─── Field edits ─────────────────────────────────────────────────────

    private void OnFieldEdited(string fieldName, object value)
    {
        if (_model == null || _entry == null) return;
        _model.UpdateBuildingField(_categoryName, _buildingIndex, fieldName, value);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
    }

    private void OnAddRequiredResourcePressed()
    {
        if (_model == null) return;
        _model.AddRequiredResource(_categoryName, _buildingIndex,
            new BuildingEditorModel.RequiredResourceEdit { ResourceId = "", Amount = 1 });
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildRequiredResourceRows();
    }

    private void OnRequiredResourceChanged(int slotIndex, string resourceId, int amount)
    {
        if (_model == null) return;
        _model.UpdateRequiredResource(_categoryName, _buildingIndex, slotIndex, resourceId, amount);
    }

    private void OnRequiredResourceDeleted(int slotIndex)
    {
        if (_model == null) return;
        _model.RemoveRequiredResource(_categoryName, _buildingIndex, slotIndex);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildRequiredResourceRows();
    }

    private void OnAddBehaviorPressed()
    {
        if (_model == null) return;
        var known = new System.Collections.Generic.List<string>(BehaviorSchemaRegistry.KnownBehaviorIds);
        if (known.Count == 0) return;
        known.Sort();
        _model.AddBehavior(_categoryName, _buildingIndex, known[0]);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildBehaviorRows();
    }

    private void OnBehaviorRowDeleted(int rowIndex)
    {
        if (_model == null) return;
        _model.RemoveBehavior(_categoryName, _buildingIndex, rowIndex);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
        RebuildBehaviorRows();
    }

    private void OnMoveUpPressed()
    {
        if (_model == null || _buildingIndex <= 0) return;
        _model.MoveBuilding(_categoryName, _buildingIndex, _buildingIndex - 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnMoveDownPressed()
    {
        if (_model == null) return;
        var list = _model.Categories[_categoryName].Buildings;
        if (_buildingIndex >= list.Count - 1) return;
        _model.MoveBuilding(_categoryName, _buildingIndex, _buildingIndex + 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnDeletePressed()
    {
        if (_entry == null) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Building",
            DialogText = $"Delete building '{_entry.IdName}'? This is buffered until Save."
        };
        dialog.Confirmed += () =>
        {
            if (_model == null) return;
            _model.DeleteBuilding(_categoryName, _buildingIndex);
            EmitSignal(SignalName.CardsNeedRebuild);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(450, 150));
    }
}
#endif
