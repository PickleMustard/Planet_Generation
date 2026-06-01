#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UtilityLibrary;
using Debug;
using DeveloperTools.Common;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Debug module for building YAML configuration editing.
/// Two-panel layout: categories (subdirectories) on the left, building cards on the right.
/// Save/Revert toolbar with buffered write model. UI built programmatically; the
/// matching .tscn is a minimal root Control with this script attached.
/// </summary>
public partial class BuildingEditorModule : BaseDebugModule
{
    public override string ModuleName => "Buildings";

    private const string BuildingsDirectory = "res://Configuration/Buildings";

    private BuildingEditorModel? _model;

    private HSplitContainer _splitContainer = null!;
    private ItemList _categoryList = null!;
    private Label _categoryHeaderLabel = null!;
    private Button _newCategoryButton = null!;
    private Button _deleteCategoryButton = null!;
    private Button _newBuildingButton = null!;
    private VBoxContainer _buildingListContainer = null!;
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

        _splitContainer = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rootVBox.AddChild(_splitContainer);
        var viewportWidth = (int)GetViewport().GetVisibleRect().Size.X;
        _splitContainer.SplitOffsets = new[] { viewportWidth / 5 };

        // ── Left panel ─────────────────────────────────────────────────────
        var leftPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddThemeStyleboxOverride("panel", MakeStyleBox(new Color(0.12f, 0.12f, 0.14f)));
        _splitContainer.AddChild(leftPanel);

        var leftVBox = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        leftPanel.AddChild(leftVBox);

        var categoriesHeader = new Label
        {
            Text = "Categories",
            HorizontalAlignment = HorizontalAlignment.Center
        };
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

        _newCategoryButton = new Button
        {
            Text = "+ New Category",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _newCategoryButton.Pressed += OnNewCategoryPressed;
        leftVBox.AddChild(_newCategoryButton);

        _deleteCategoryButton = new Button
        {
            Text = "✕ Delete Category",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Disabled = true
        };
        _deleteCategoryButton.Pressed += OnDeleteCategoryPressed;
        leftVBox.AddChild(_deleteCategoryButton);

        // ── Right panel ────────────────────────────────────────────────────
        var rightPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rightPanel.AddThemeStyleboxOverride("panel", MakeStyleBox(new Color(0.12f, 0.12f, 0.14f)));
        _splitContainer.AddChild(rightPanel);

        var rightVBox = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        rightPanel.AddChild(rightVBox);

        var rightHeader = new HBoxContainer();
        rightVBox.AddChild(rightHeader);

        _categoryHeaderLabel = new Label
        {
            Text = "Select a category",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _categoryHeaderLabel.AddThemeFontSizeOverride("font_size", 14);
        _categoryHeaderLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.4f));
        rightHeader.AddChild(_categoryHeaderLabel);

        _newBuildingButton = new Button { Text = "+ New Building" };
        _newBuildingButton.Pressed += OnNewBuildingPressed;
        rightHeader.AddChild(_newBuildingButton);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        rightVBox.AddChild(scroll);

        _buildingListContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _buildingListContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_buildingListContainer);

        // ── Toolbar ────────────────────────────────────────────────────────
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

    private static StyleBoxFlat MakeStyleBox(Color color) => new() { BgColor = color };

    // ─── Model Loading ────────────────────────────────────────────────────

    private void LoadModel()
    {
        try
        {
            _model = new BuildingEditorModel(BuildingsDirectory);
            _model.LoadFromDisk();
            PopulateCategoryList();
            if (_categoryList.ItemCount > 0)
            {
                _categoryList.Select(0);
                OnCategorySelected(0);
            }
            else
            {
                RefreshBuildingList();
            }
        }
        catch (InvalidOperationException ex)
        {
            GameLogger.Error($"Failed to load building editor model: {ex.Message}");
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
            RefreshBuildingList();
            return;
        }
        _selectedCategory = _categoryList.GetItemText((int)index);
        _deleteCategoryButton.Disabled = false;
        RefreshBuildingList();
    }

    // ─── Building List ────────────────────────────────────────────────────

    private void RefreshBuildingList()
    {
        foreach (var child in _buildingListContainer.GetChildren())
            child.QueueFree();

        if (_model == null || _selectedCategory == null
            || !_model.Categories.ContainsKey(_selectedCategory))
        {
            _categoryHeaderLabel.Text = "Select a category";
            var placeholder = new Label
            {
                Text = "Select a category",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            placeholder.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _buildingListContainer.AddChild(placeholder);
            return;
        }

        var category = _model.Categories[_selectedCategory];
        _categoryHeaderLabel.Text = category.CategoryName;

        for (int i = 0; i < category.Buildings.Count; i++)
        {
            var card = new BuildingCard();
            card.Initialize(_model, _selectedCategory, i, category.Buildings[i]);
            card.CardsNeedRebuild += RefreshBuildingList;
            _buildingListContainer.AddChild(card);
        }
    }

    // ─── New / Delete Category ────────────────────────────────────────────

    private void OnNewCategoryPressed()
    {
        var lineEdit = new LineEdit
        {
            PlaceholderText = "Category name (PascalCase)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };

        var dialog = new ConfirmationDialog
        {
            Title = "New Category",
            DialogText = "Enter category name (becomes a subdirectory):"
        };
        dialog.AddChild(lineEdit);

        dialog.Confirmed += () =>
        {
            string name = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowAcceptDialog("Invalid Name", "Category name cannot be empty.");
                return;
            }
            if (name.Contains(' ') || name.Contains('.') || name.Contains('/'))
            {
                ShowAcceptDialog("Invalid Name", "Category name cannot contain spaces, dots, or slashes.");
                return;
            }
            if (_model != null && _model.Categories.ContainsKey(name))
            {
                ShowAcceptDialog("Duplicate Name", $"Category '{name}' already exists.");
                return;
            }
            try
            {
                _model?.AddCategory(name);
                PopulateCategoryList();
                for (int i = 0; i < _categoryList.ItemCount; i++)
                {
                    if (_categoryList.GetItemText(i) == name)
                    {
                        _categoryList.Select(i);
                        OnCategorySelected(i);
                        break;
                    }
                }
            }
            catch (ArgumentException ex)
            {
                ShowAcceptDialog("Error", ex.Message);
            }
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
            DialogText = $"Delete category '{_selectedCategory}' and all its buildings?\nDoes not take effect until Save."
        };
        dialog.Confirmed += () =>
        {
            try { _model?.DeleteCategory(_selectedCategory!); }
            catch (KeyNotFoundException ex) { GameLogger.Warning(ex.Message); }

            _selectedCategory = null;
            _deleteCategoryButton.Disabled = true;
            PopulateCategoryList();
            RefreshBuildingList();
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    // ─── New Building ─────────────────────────────────────────────────────

    private void OnNewBuildingPressed()
    {
        if (_model == null || string.IsNullOrEmpty(_selectedCategory)) return;

        var entry = new BuildingEditorModel.BuildingEditEntry
        {
            IdName = "new_building",
            DisplayName = "New Building",
            Description = "",
            Category = _selectedCategory.ToLowerInvariant(),
            MaxResourceTier = 0,
            WorkRequired = 100f,
            BuildingLimit = -1,
            Demolishable = true,
        };
        entry.SourceFilePath = _model.DefaultSourceFilePath(_selectedCategory, entry.IdName);

        _model.AddBuilding(_selectedCategory, entry);
        RefreshBuildingList();
    }

    // ─── Save ─────────────────────────────────────────────────────────────

    private void OnSavePressed()
    {
        if (_model == null) return;

        var messages = _model.Validate();
        var errors = messages.Where(m => !m.StartsWith("Warning:")).ToList();
        var warnings = messages.Where(m => m.StartsWith("Warning:")).ToList();

        if (errors.Count > 0)
        {
            ShowValidationDialog(errors, warnings);
            return;
        }
        if (warnings.Count > 0)
        {
            ShowValidationDialog(null, warnings, allowSave: true);
            return;
        }
        ExecuteSave();
    }

    private void ExecuteSave()
    {
        if (_model == null) return;
        try
        {
            BuildingEditorYamlIO.WriteAll(
                BuildingsDirectory,
                new Dictionary<string, BuildingEditorModel.BuildingCategoryData>(_model.Categories),
                _model.LoadedSourceFiles
            );
            _model.LoadFromDisk();
            int reloaded = EditorDatabaseReloader.ReloadAll("BuildingDatabase");
            PopulateCategoryList();
            RefreshBuildingList();
            ShowFeedback($"Saved + reloaded {reloaded} DBs.");
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Building save failed: {ex.Message}");
            ShowAcceptDialog("Save Error", ex.Message);
        }
    }

    private void ShowValidationDialog(List<string>? errors, List<string>? warnings, bool allowSave = false)
    {
        var rich = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollFollowing = true,
            CustomMinimumSize = new Vector2(380, 180),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var text = "";
        if (errors != null) foreach (var e in errors) text += $"[color=red]{e}[/color]\n";
        if (warnings != null) foreach (var w in warnings) text += $"[color=yellow]{w}[/color]\n";
        rich.Text = text;

        string title = errors != null && errors.Count > 0 ? "Validation Errors" : "Validation Warnings";
        if (allowSave)
        {
            var dialog = new ConfirmationDialog
            {
                Title = title,
                DialogText = "Warnings found. Continue saving?"
            };
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
        var dialog = new ConfirmationDialog
        {
            Title = "Revert",
            DialogText = "Discard all unsaved changes?"
        };
        dialog.Confirmed += () =>
        {
            try
            {
                _model.LoadFromDisk();
                PopulateCategoryList();
                RefreshBuildingList();
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
        if (_feedbackTimer <= 0)
        {
            _feedbackLabel.Visible = false;
            _feedbackTimer = 0;
        }
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
