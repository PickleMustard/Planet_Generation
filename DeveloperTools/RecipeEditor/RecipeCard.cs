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
/// Built programmatically — no scene file. Instantiated via `new RecipeCard()`
/// then Initialize() then AddChild().
/// </summary>
public partial class RecipeCard : PanelContainer
{
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    private RecipeEditorModel? _model;
    private string _categoryName = "";
    private int _recipeIndex;
    private RecipeEditorModel.RecipeEditEntry? _entry;

    private TextureRect _iconRect = null!;
    private LineEdit _recipeIdEdit = null!;
    private LineEdit _displayNameEdit = null!;
    private TextEdit _descriptionEdit = null!;
    private LineEdit _categoryEdit = null!;
    private SpinBox _workRequiredSpin = null!;
    private Button _tagsButton = null!;
    private VBoxContainer _inputsContainer = null!;
    private VBoxContainer _outputsContainer = null!;
    private Button _addInputButton = null!;
    private Button _addOutputButton = null!;
    private Button _moveUpButton = null!;
    private Button _moveDownButton = null!;
    private Button _deleteButton = null!;

    private PackedScene? _iconPickerScene;

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
        BuildLayout();
        _iconPickerScene = GD.Load<PackedScene>(
            "res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn");
        RefreshControls();
    }

    private void BuildLayout()
    {
        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.16f, 0.19f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.3f, 0.3f, 0.35f),
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusBottomLeft = 3
        };
        AddThemeStyleboxOverride("panel", styleBox);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(root);

        // ── Header row: icon + recipe_id + actions ─────────────────────────
        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);

        _iconRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(48, 48),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TooltipText = "Click to choose icon"
        };
        _iconRect.GuiInput += OnIconInput;
        header.AddChild(_iconRect);

        var headerFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(headerFields);

        _recipeIdEdit = new LineEdit { PlaceholderText = "recipe_id", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _recipeIdEdit.TextChanged += text => OnFieldEdited("RecipeId", text);
        headerFields.AddChild(_recipeIdEdit);

        _displayNameEdit = new LineEdit { PlaceholderText = "Display Name", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _displayNameEdit.TextChanged += text => OnFieldEdited("DisplayName", text);
        headerFields.AddChild(_displayNameEdit);

        var actions = new VBoxContainer();
        _moveUpButton = new Button { Text = "▲", TooltipText = "Move up" };
        _moveUpButton.Pressed += OnMoveUpPressed;
        actions.AddChild(_moveUpButton);

        _moveDownButton = new Button { Text = "▼", TooltipText = "Move down" };
        _moveDownButton.Pressed += OnMoveDownPressed;
        actions.AddChild(_moveDownButton);

        _deleteButton = new Button { Text = "✕", TooltipText = "Delete recipe" };
        _deleteButton.Pressed += OnDeletePressed;
        actions.AddChild(_deleteButton);
        header.AddChild(actions);

        // ── Fields grid ────────────────────────────────────────────────────
        var fields = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(fields);

        fields.AddChild(new Label { Text = "Category" });
        _categoryEdit = new LineEdit
        {
            PlaceholderText = "extraction / agriculture / power / ...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _categoryEdit.TextChanged += text => OnFieldEdited("Category", text);
        fields.AddChild(_categoryEdit);

        fields.AddChild(new Label { Text = "Work" });
        _workRequiredSpin = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100000,
            Step = 0.1f,
            AllowGreater = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _workRequiredSpin.ValueChanged += value => OnFieldEdited("WorkRequired", (float)value);
        fields.AddChild(_workRequiredSpin);

        fields.AddChild(new Label { Text = "Description" });
        _descriptionEdit = new TextEdit
        {
            PlaceholderText = "Recipe description",
            CustomMinimumSize = new Vector2(0, 60),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        _descriptionEdit.TextChanged += () => OnFieldEdited("Description", _descriptionEdit.Text);
        fields.AddChild(_descriptionEdit);

        fields.AddChild(new Label { Text = "Tags" });
        _tagsButton = new Button { Text = "Tags (0)" };
        _tagsButton.Pressed += OnTagsPressed;
        fields.AddChild(_tagsButton);

        // ── Inputs ─────────────────────────────────────────────────────────
        var inputsHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var inputsLabel = new Label { Text = "Inputs", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inputsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 1.0f));
        inputsHeader.AddChild(inputsLabel);

        _addInputButton = new Button { Text = "+ Input" };
        _addInputButton.Pressed += OnAddInputPressed;
        inputsHeader.AddChild(_addInputButton);
        root.AddChild(inputsHeader);

        _inputsContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_inputsContainer);

        // ── Outputs ────────────────────────────────────────────────────────
        var outputsHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var outputsLabel = new Label { Text = "Outputs", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        outputsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 1.0f, 0.7f));
        outputsHeader.AddChild(outputsLabel);

        _addOutputButton = new Button { Text = "+ Output" };
        _addOutputButton.Pressed += OnAddOutputPressed;
        outputsHeader.AddChild(_addOutputButton);
        root.AddChild(outputsHeader);

        _outputsContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(_outputsContainer);
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
            var row = new InputSlotRow();
            row.Configure(i, _entry.Inputs[i]);
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
            var row = new OutputSlotRow();
            row.Configure(i, _entry.Outputs[i]);
            row.SlotChanged += OnOutputSlotChanged;
            row.SlotDeleted += OnOutputSlotDeleted;
            _outputsContainer.AddChild(row);
        }
    }

    private void LoadIcon()
    {
        if (_entry == null) return;
        Texture2D? texture = null;
        if (!string.IsNullOrEmpty(_entry.IconBasePath))
        {
            texture = IconDataLoader.LoadIconTexture(
                _entry.IconBasePath, IconSize.Medium, _entry.RecipeId);
        }
        _iconRect.Texture = texture ?? IconDataLoader.GetFallbackIcon(IconSize.Medium);
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
        _model.UpdateRecipeField(_categoryName, _recipeIndex, "IconBasePath", basePath);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        LoadIcon();
    }

    private void OnTagsPressed()
    {
        if (_model == null || _entry == null) return;
        var allTags = new HashSet<string>(_model.GetAllRecipeTags());
        allTags.UnionWith(RecipeEditorModel.GetAllResourceTags());

        var popup = new RecipeTagsPopup();
        popup.Initialize(_model, _categoryName, _recipeIndex, _entry, allTags);
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
