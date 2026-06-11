#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using Debug;

namespace DeveloperTools.EngineEditor;

/// <summary>
/// Debug module for engine YAML configuration editing. Two-panel layout:
/// categories (one per file, e.g. Chemical / Nuclear) on the left, engine cards on
/// the right. Engines have no runtime database — Save writes YAML and clears
/// EngineConfigLoader's cache so ships pick up new categories on next access.
/// UI built programmatically; matching .tscn is a minimal root Control.
/// </summary>
public partial class EngineEditorModule : BaseDebugModule
{
    public override string ModuleName => "Engines";

    private const string EnginesDirectory = "res://Configuration/engines";

    private EngineEditorModel? _model;

    private ItemList _categoryList = null!;
    private Label _categoryHeaderLabel = null!;
    private Button _deleteCategoryButton = null!;
    private VBoxContainer _engineListContainer = null!;
    private Button _saveButton = null!;
    private Button _revertButton = null!;
    private Label _feedbackLabel = null!;

    private string? _selectedCategory;
    private double _feedbackTimer;

    public override void _Ready()
    {
        base._Ready();
        BuildLayout();
        LoadModel();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateButtonStates();
        UpdateDirtyIndicator();
        UpdateFeedbackLabel(delta);
    }

    private static StyleBoxFlat MakeStyleBox(Color color) => new() { BgColor = color };

    private void BuildLayout()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var rootVBox = new VBoxContainer();
        rootVBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        rootVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rootVBox.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(rootVBox);

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rootVBox.AddChild(split);
        split.SplitOffsets = new[] { (int)GetViewport().GetVisibleRect().Size.X / 5 };

        var leftPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddThemeStyleboxOverride("panel", MakeStyleBox(new Color(0.12f, 0.12f, 0.14f)));
        split.AddChild(leftPanel);
        var leftVBox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddChild(leftVBox);

        var categoriesHeader = new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Categories", HorizontalAlignment = HorizontalAlignment.Center };
        categoriesHeader.AddThemeFontSizeOverride("font_size", 14);
        leftVBox.AddChild(categoriesHeader);

        _categoryList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AllowReselect = true
        };
        _categoryList.ItemSelected += OnCategorySelected;
        leftVBox.AddChild(_categoryList);

        var newCategoryButton = new Button { Text = "+ New Category", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        newCategoryButton.Pressed += OnNewCategoryPressed;
        leftVBox.AddChild(newCategoryButton);

        _deleteCategoryButton = new Button { Text = "✕ Delete Category", SizeFlagsHorizontal = SizeFlags.ExpandFill, Disabled = true };
        _deleteCategoryButton.Pressed += OnDeleteCategoryPressed;
        leftVBox.AddChild(_deleteCategoryButton);

        var rightPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        rightPanel.AddThemeStyleboxOverride("panel", MakeStyleBox(new Color(0.12f, 0.12f, 0.14f)));
        split.AddChild(rightPanel);
        var rightVBox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rightPanel.AddChild(rightVBox);

        var rightHeader = new HBoxContainer();
        rightVBox.AddChild(rightHeader);
        _categoryHeaderLabel = new Label { Text = "Select a category", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _categoryHeaderLabel.AddThemeFontSizeOverride("font_size", 14);
        _categoryHeaderLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.4f));
        rightHeader.AddChild(_categoryHeaderLabel);
        var newEngineButton = new Button { Text = "+ New Engine" };
        newEngineButton.Pressed += OnNewEnginePressed;
        rightHeader.AddChild(newEngineButton);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        rightVBox.AddChild(scroll);
        _engineListContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _engineListContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_engineListContainer);

        var toolbar = new PanelContainer();
        toolbar.AddThemeStyleboxOverride("panel", MakeStyleBox(new Color(0.10f, 0.10f, 0.12f)));
        rootVBox.AddChild(toolbar);
        var toolbarHBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        toolbar.AddChild(toolbarHBox);
        toolbarHBox.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _revertButton = new Button { Text = "Revert", Disabled = true };
        _revertButton.Pressed += OnRevertPressed;
        toolbarHBox.AddChild(_revertButton);
        _saveButton = new Button { Text = "Save", Disabled = true };
        _saveButton.Pressed += OnSavePressed;
        toolbarHBox.AddChild(_saveButton);
        _feedbackLabel = new Label { Visible = false };
        _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1.0f, 0.4f));
        _feedbackLabel.AddThemeFontSizeOverride("font_size", 12);
        toolbarHBox.AddChild(_feedbackLabel);
    }

    // ── Model loading ────────────────────────────────────────────────────

    private void LoadModel()
    {
        try
        {
            _model = new EngineEditorModel(EnginesDirectory);
            _model.LoadFromDisk();
            PopulateCategoryList();
            if (_categoryList.ItemCount > 0)
            {
                _categoryList.Select(0);
                OnCategorySelected(0);
            }
            else
            {
                RefreshEngineList();
            }
        }
        catch (InvalidOperationException ex)
        {
            GameLogger.Error($"Failed to load engine editor model: {ex.Message}");
            ShowAcceptDialog("Load Error", ex.Message);
        }
    }

    private void PopulateCategoryList()
    {
        _categoryList.Clear();
        if (_model == null) return;
        foreach (var key in _model.Categories.Keys.OrderBy(k => k))
            _categoryList.AddItem(key);
    }

    private void OnCategorySelected(long index)
    {
        if (_categoryList.ItemCount == 0 || index < 0)
        {
            _selectedCategory = null;
            RefreshEngineList();
            return;
        }
        _selectedCategory = _categoryList.GetItemText((int)index);
        _deleteCategoryButton.Disabled = false;
        RefreshEngineList();
    }

    private void RefreshEngineList()
    {
        foreach (var child in _engineListContainer.GetChildren())
            child.QueueFree();

        if (_model == null || _selectedCategory == null || !_model.Categories.ContainsKey(_selectedCategory))
        {
            _categoryHeaderLabel.Text = "Select a category";
            var placeholder = new Label { Text = "Select a category", HorizontalAlignment = HorizontalAlignment.Center };
            placeholder.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _engineListContainer.AddChild(placeholder);
            return;
        }

        var category = _model.Categories[_selectedCategory];
        _categoryHeaderLabel.Text = category.CategoryName;
        for (int i = 0; i < category.Engines.Count; i++)
        {
            var card = EngineCard.Create(_model, _selectedCategory, i, category.Engines[i]);
            card.CardsNeedRebuild += RefreshEngineList;
            _engineListContainer.AddChild(card);
        }
    }

    // ── Category create / delete ─────────────────────────────────────────

    private void OnNewCategoryPressed()
    {
        var lineEdit = new LineEdit { PlaceholderText = "Category name (e.g. Chemical)", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var dialog = new ConfirmationDialog { Title = "New Category", DialogText = "Enter category name (becomes {name}.yaml):" };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string name = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(name)) { ShowAcceptDialog("Invalid Name", "Category name cannot be empty."); return; }
            if (name.Contains(' ') || name.Contains('.') || name.Contains('/'))
            { ShowAcceptDialog("Invalid Name", "Category name cannot contain spaces, dots, or slashes."); return; }
            if (_model != null && _model.Categories.ContainsKey(name))
            { ShowAcceptDialog("Duplicate Name", $"Category '{name}' already exists."); return; }
            try
            {
                _model?.AddCategory(name);
                PopulateCategoryList();
                for (int i = 0; i < _categoryList.ItemCount; i++)
                    if (_categoryList.GetItemText(i) == name) { _categoryList.Select(i); OnCategorySelected(i); break; }
            }
            catch (ArgumentException ex) { ShowAcceptDialog("Error", ex.Message); }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    private void OnDeleteCategoryPressed()
    {
        if (string.IsNullOrEmpty(_selectedCategory)) return;
        var dialog = new ConfirmationDialog
        {
            Title = "Delete Category",
            DialogText = $"Delete category '{_selectedCategory}' and all its engines?\nDoes not take effect until Save."
        };
        dialog.Confirmed += () =>
        {
            try { _model?.DeleteCategory(_selectedCategory!); }
            catch (KeyNotFoundException ex) { GameLogger.Warning(ex.Message); }
            _selectedCategory = null;
            _deleteCategoryButton.Disabled = true;
            PopulateCategoryList();
            RefreshEngineList();
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    private void OnNewEnginePressed()
    {
        if (_model == null || string.IsNullOrEmpty(_selectedCategory)) return;
        var entry = new EngineEditorModel.EngineEditEntry { Name = "New_Engine", SpecificImpulse = 300f, Thrust = 1000f };
        _model.AddEngine(_selectedCategory, entry);
        RefreshEngineList();
    }

    // ── Save / Revert ────────────────────────────────────────────────────

    private void OnSavePressed()
    {
        if (_model == null) return;
        var messages = _model.Validate();
        var errors = messages.Where(m => !m.StartsWith("Warning:")).ToList();
        var warnings = messages.Where(m => m.StartsWith("Warning:")).ToList();
        if (errors.Count > 0) { ShowValidationDialog(errors, warnings); return; }
        if (warnings.Count > 0) { ShowValidationDialog(null, warnings, allowSave: true); return; }
        ExecuteSave();
    }

    private void ExecuteSave()
    {
        if (_model == null) return;
        try
        {
            EngineEditorYamlIO.WriteAllCategories(
                EnginesDirectory,
                new Dictionary<string, EngineEditorModel.EngineCategoryData>(_model.Categories));
            // No EngineDatabase exists; clearing the loader cache makes ships pick up
            // new engines/categories on next access.
            EngineConfigLoader.ClearCache();
            _model.LoadFromDisk();
            PopulateCategoryList();
            RefreshEngineList();
            ShowFeedback("Saved + cleared engine cache.");
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Engine save failed: {ex.Message}");
            ShowAcceptDialog("Save Error", ex.Message);
        }
    }

    private void ShowValidationDialog(List<string>? errors, List<string>? warnings, bool allowSave = false)
    {
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true, ScrollFollowing = true,
            CustomMinimumSize = new Vector2(380, 180), SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var text = "";
        if (errors != null) foreach (var e in errors) text += $"[color=red]{e}[/color]\n";
        if (warnings != null) foreach (var w in warnings) text += $"[color=yellow]{w}[/color]\n";
        rich.Text = text;
        string title = errors != null && errors.Count > 0 ? "Validation Errors" : "Validation Warnings";
        if (allowSave)
        {
            var dialog = new ConfirmationDialog { Title = title, DialogText = "Warnings found. Continue saving?" };
            dialog.AddChild(rich);
            dialog.Confirmed += ExecuteSave;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(520, 320));
        }
        else
        {
            var dialog = new AcceptDialog { Title = title };
            dialog.AddChild(rich);
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(520, 320));
        }
    }

    private void OnRevertPressed()
    {
        if (_model == null || !_model.HasUnsavedChanges) return;
        var dialog = new ConfirmationDialog { Title = "Revert", DialogText = "Discard all unsaved changes?" };
        dialog.Confirmed += () =>
        {
            try
            {
                _model.LoadFromDisk();
                PopulateCategoryList();
                RefreshEngineList();
                ShowFeedback("Reverted");
            }
            catch (InvalidOperationException ex)
            {
                GameLogger.Error($"Revert failed: {ex.Message}");
                ShowAcceptDialog("Revert Error", ex.Message);
            }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(350, 100));
    }

    // ── UI state ─────────────────────────────────────────────────────────

    private void UpdateButtonStates()
    {
        if (_model == null) return;
        var dirty = _model.HasUnsavedChanges;
        _saveButton.Disabled = !dirty;
        _revertButton.Disabled = !dirty;
    }

    private void UpdateDirtyIndicator()
    {
        if (_model == null) return;
        Name = _model.HasUnsavedChanges ? ModuleName + " *" : ModuleName;
    }

    private void ShowFeedback(string message)
    {
        _feedbackTimer = 2.0;
        _feedbackLabel.Text = message;
        _feedbackLabel.Visible = true;
        _feedbackLabel.Modulate = Colors.White;
    }

    private void UpdateFeedbackLabel(double delta)
    {
        if (_feedbackTimer <= 0) return;
        _feedbackTimer -= delta;
        if (_feedbackTimer <= 0) { _feedbackLabel.Visible = false; _feedbackTimer = 0; }
        else
        {
            float alpha = _feedbackTimer < 0.5 ? (float)_feedbackTimer / 0.5f : 1.0f;
            _feedbackLabel.Modulate = new Color(1f, 1f, 1f, alpha);
        }
    }

    public override void OnModuleEnabled()
    {
        if (_model == null) LoadModel();
        base.OnModuleEnabled();
    }

    private void ShowAcceptDialog(string title, string message)
    {
        var dialog = new AcceptDialog { Title = title, DialogText = message };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }
}
#endif
