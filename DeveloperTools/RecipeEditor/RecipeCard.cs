#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using Structures.Enums;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using DeveloperTools.ResourceEditor;

namespace DeveloperTools.RecipeEditor;

/// <summary>
/// Per-recipe card with inline editing for recipe fields and input/output slots.
/// Layout lives in <c>RecipeCard.tscn</c>; this script wires field edits and
/// rebuilds the slot lists. Instantiate via <see cref="Create"/>.
/// </summary>
public partial class RecipeCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private RecipeEditorModel? _model;
    private string _categoryName = "";
    private int _recipeIndex;
    private RecipeEditorModel.RecipeEditEntry? _entry;

    [Export] private TextureRect _iconRect = null!;
    [Export] private LineEdit _recipeIdEdit = null!;
    [Export] private LineEdit _displayNameEdit = null!;
    [Export] private TextEdit _descriptionEdit = null!;
    [Export] private LineEdit _categoryEdit = null!;
    [Export] private SpinBox _workRequiredSpin = null!;
    [Export] private Button _tagsButton = null!;
    [Export] private VBoxContainer _inputsContainer = null!;
    [Export] private VBoxContainer _outputsContainer = null!;
    [Export] private VBoxContainer _conditionalsContainer = null!;
    [Export] private Button _addInputButton = null!;
    [Export] private Button _addOutputButton = null!;
    [Export] private Button _addConditionalButton = null!;
    [Export] private Button _moveUpButton = null!;
    [Export] private Button _moveDownButton = null!;
    [Export] private Button _deleteButton = null!;

    private PackedScene? _iconPickerScene;

    private static PackedScene? _scene;

    public static RecipeCard Create(
        RecipeEditorModel model,
        string categoryName,
        int recipeIndex,
        RecipeEditorModel.RecipeEditEntry entry)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/RecipeEditor/RecipeCard.tscn");
        var card = _scene.Instantiate<RecipeCard>();
        card.Initialize(model, categoryName, recipeIndex, entry);
        return card;
    }

    public void Initialize(
        RecipeEditorModel model,
        string categoryName,
        int recipeIndex,
        RecipeEditorModel.RecipeEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _categoryName = categoryName;
        _recipeIndex = recipeIndex;
        _entry = entry;
        Name = $"RecipeCard_{_entry.RecipeId}";
    }

    public override void _Ready()
    {
        base._Ready();
        _iconRect.GuiInput += OnIconInput;
        _recipeIdEdit.TextChanged += text => OnFieldEdited("RecipeId", text);
        _displayNameEdit.TextChanged += text => OnFieldEdited("DisplayName", text);
        _categoryEdit.TextChanged += text => OnFieldEdited("Category", text);
        _workRequiredSpin.ValueChanged += value => OnFieldEdited("WorkRequired", (float)value);
        _descriptionEdit.TextChanged += () => OnFieldEdited("Description", _descriptionEdit.Text);
        _tagsButton.Pressed += OnTagsPressed;
        _addInputButton.Pressed += OnAddInputPressed;
        _addOutputButton.Pressed += OnAddOutputPressed;
        _addConditionalButton.Pressed += OnAddConditionalPressed;
        _moveUpButton.Pressed += OnMoveUpPressed;
        _moveDownButton.Pressed += OnMoveDownPressed;
        _deleteButton.Pressed += OnDeletePressed;

        _iconPickerScene = GD.Load<PackedScene>(
            "res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn");
        RefreshControls();
    }

    public void Refresh(RecipeEditorModel.RecipeEditEntry entry, int newIndex)
    {
        _entry = entry;
        _recipeIndex = newIndex;
        Name = $"RecipeCard_{_entry.RecipeId}";
        RefreshControls();
    }

    private void RefreshControls()
    {
        if (_entry == null || _model == null) return;

        LoadIcon();

        if (_recipeIdEdit.Text != _entry.RecipeId)
            _recipeIdEdit.Text = _entry.RecipeId;
        if (_displayNameEdit.Text != _entry.DisplayName)
            _displayNameEdit.Text = _entry.DisplayName;
        if (_descriptionEdit.Text != _entry.Description)
            _descriptionEdit.Text = _entry.Description;
        if (_categoryEdit.Text != _entry.Category)
            _categoryEdit.Text = _entry.Category;
        _workRequiredSpin.SetValueNoSignal(_entry.WorkRequired);

        _tagsButton.Text = $"Tags ({_entry.Tags.Count})";

        RebuildInputRows();
        RebuildOutputRows();
        RebuildConditionalOutputRows();

        if (_model.Categories.ContainsKey(_categoryName))
        {
            var recipes = _model.Categories[_categoryName].Recipes;
            _moveUpButton.Disabled = _recipeIndex <= 0;
            _moveDownButton.Disabled = _recipeIndex >= recipes.Count - 1;
        }
    }

    private void RebuildInputRows()
    {
        foreach (var child in _inputsContainer.GetChildren())
            child.QueueFree();
        if (_entry == null) return;

        for (int i = 0; i < _entry.Inputs.Count; i++)
        {
            var row = InputSlotRow.Create(i, _entry.Inputs[i]);
            row.SlotChanged += OnInputSlotChanged;
            row.SlotDeleted += OnInputSlotDeleted;
            _inputsContainer.AddChild(row);
        }
    }

    private void RebuildOutputRows()
    {
        foreach (var child in _outputsContainer.GetChildren())
            child.QueueFree();
        if (_entry == null) return;

        for (int i = 0; i < _entry.Outputs.Count; i++)
        {
            var row = OutputSlotRow.Create(i, _entry.Outputs[i]);
            row.SlotChanged += OnOutputSlotChanged;
            row.SlotDeleted += OnOutputSlotDeleted;
            _outputsContainer.AddChild(row);
        }
    }

    private void RebuildConditionalOutputRows()
    {
        foreach (var child in _conditionalsContainer.GetChildren())
            child.QueueFree();
        if (_entry == null) return;

        for (int i = 0; i < _entry.ConditionalOutputs.Count; i++)
        {
            var row = ConditionalOutputRow.Create(i, _entry.ConditionalOutputs[i]);
            row.SlotChanged += OnConditionalSlotChanged;
            row.SlotDeleted += OnConditionalSlotDeleted;
            _conditionalsContainer.AddChild(row);
        }
    }

    private void LoadIcon()
    {
        if (_entry == null) return;
        Texture2D? texture = null;
        if (!string.IsNullOrEmpty(_entry.IconResourcePath))
        {
            texture = IconDataLoader.LoadIconTexture(
                _entry.IconResourcePath, _entry.RecipeId);
        }
        _iconRect.Texture = texture ?? IconDataLoader.GetFallbackIcon();
    }

    private void OnFieldEdited(string fieldName, object value)
    {
        if (_model == null || _entry == null) return;
        _model.UpdateRecipeField(_categoryName, _recipeIndex, fieldName, value);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
    }

    private void OnIconInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            OpenIconPicker();
        }
    }

    private void OpenIconPicker()
    {
        if (_iconPickerScene == null || _entry == null) return;
        var popup = _iconPickerScene.Instantiate<IconPickerPopup>();
        AddChild(popup);
        popup.IconSelected += OnIconSelected;
        var rect = _iconRect.GetGlobalRect();
        popup.Position = new Vector2I((int)rect.End.X + 4, (int)rect.Position.Y);
        popup.OpenPicker();
    }

    private void OnIconSelected(string basePath)
    {
        if (_model == null || _entry == null) return;
        _model.UpdateRecipeField(_categoryName, _recipeIndex, "IconResourcePath", basePath);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        LoadIcon();
    }

    private void OnTagsPressed()
    {
        if (_model == null || _entry == null) return;
        var allTags = new HashSet<string>(_model.GetAllRecipeTags());
        allTags.UnionWith(RecipeEditorModel.GetAllResourceTags());

        var popup = RecipeTagsPopup.Create(_model, _categoryName, _recipeIndex, _entry, allTags);
        AddChild(popup);

        var rect = _tagsButton.GetGlobalRect();
        popup.Position = new Vector2I((int)rect.Position.X, (int)rect.End.Y);
        popup.TagsChanged += OnTagsChanged;
        popup.Popup();
    }

    private void OnTagsChanged()
    {
        if (_model == null || _entry == null) return;
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        _tagsButton.Text = $"Tags ({_entry.Tags.Count})";
    }

    private void OnInputSlotChanged(int slotIndex, int kind, string key, float amount)
    {
        if (_model == null) return;
        _model.UpdateInputSlot(_categoryName, _recipeIndex, slotIndex,
            (RecipeEditorModel.SlotKind)kind, key, amount);
    }

    private void OnInputSlotDeleted(int slotIndex)
    {
        if (_model == null) return;
        _model.DeleteInputSlot(_categoryName, _recipeIndex, slotIndex);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        RebuildInputRows();
    }

    private void OnAddInputPressed()
    {
        if (_model == null) return;
        var slot = new RecipeEditorModel.InputSlot
        {
            Kind = RecipeEditorModel.SlotKind.Resource,
            Key = "",
            Amount = 1f
        };
        _model.AddInputSlot(_categoryName, _recipeIndex, slot);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        RebuildInputRows();
    }

    private void OnOutputSlotChanged(int slotIndex, int kind, string key, float amount)
    {
        if (_model == null) return;
        _model.UpdateOutputSlot(_categoryName, _recipeIndex, slotIndex,
            (RecipeEditorModel.SlotKind)kind, key, amount);
    }

    private void OnOutputSlotDeleted(int slotIndex)
    {
        if (_model == null) return;
        _model.DeleteOutputSlot(_categoryName, _recipeIndex, slotIndex);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        RebuildOutputRows();
    }

    private void OnAddOutputPressed()
    {
        if (_model == null) return;
        var slot = new RecipeEditorModel.OutputSlot
        {
            Kind = RecipeEditorModel.SlotKind.Resource,
            Key = "",
            Amount = 1f
        };
        _model.AddOutputSlot(_categoryName, _recipeIndex, slot);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        RebuildOutputRows();
    }

    private void OnConditionalSlotChanged(int slotIndex)
    {
        if (_model == null) return;
        // Read mutated state directly from the row — Godot signals can't carry List<T>.
        var rows = _conditionalsContainer.GetChildren();
        if (slotIndex < 0 || slotIndex >= rows.Count) return;
        if (rows[slotIndex] is not ConditionalOutputRow row) return;

        // Defensive copy so the editor model never aliases the row's mutable list.
        var rules = new System.Collections.Generic.List<Structures.Resources.ConditionRule>();
        foreach (var rule in row.Rules)
        {
            rules.Add(new Structures.Resources.ConditionRule
            {
                Join = rule.Join,
                Variable = rule.Variable,
                Operator = rule.Operator,
                Value = rule.Value,
            });
        }
        _model.UpdateConditionalOutputSlot(_categoryName, _recipeIndex, slotIndex,
            rules, row.Resource, row.Amount);
    }

    private void OnConditionalSlotDeleted(int slotIndex)
    {
        if (_model == null) return;
        _model.DeleteConditionalOutputSlot(_categoryName, _recipeIndex, slotIndex);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        RebuildConditionalOutputRows();
    }

    private void OnAddConditionalPressed()
    {
        if (_model == null) return;
        var slot = new RecipeEditorModel.ConditionalOutputSlot
        {
            Resource = "",
            Amount = 1f,
        };
        _model.AddConditionalOutputSlot(_categoryName, _recipeIndex, slot);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        RebuildConditionalOutputRows();
    }

    private void OnMoveUpPressed()
    {
        if (_model == null || _recipeIndex <= 0) return;
        _model.MoveRecipe(_categoryName, _recipeIndex, _recipeIndex - 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnMoveDownPressed()
    {
        if (_model == null) return;
        var recipes = _model.Categories[_categoryName].Recipes;
        if (_recipeIndex >= recipes.Count - 1) return;
        _model.MoveRecipe(_categoryName, _recipeIndex, _recipeIndex + 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnDeletePressed()
    {
        if (_entry == null) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Recipe",
            DialogText = $"Delete recipe '{_entry.RecipeId}'? Buildings referencing this id will break until updated."
        };
        dialog.Confirmed += () =>
        {
            if (_model == null) return;
            _model.DeleteRecipe(_categoryName, _recipeIndex);
            EmitSignal(SignalName.CardsNeedRebuild);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(450, 150));
    }
}
#endif
