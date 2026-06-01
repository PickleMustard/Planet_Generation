#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using Debug;
using DeveloperTools.Common;

namespace DeveloperTools.StationEditor;

/// <summary>
/// Debug module for station YAML configuration editing. Two-panel layout:
/// categories (one per file) on the left, station cards on the right. Save writes
/// YAML, clears StationConfigLoader's cache, then reloads StationDatabase so the
/// running game reflects edits without a restart. GUI defined in .tscn;
/// behavior only in this script.
/// </summary>
public partial class StationEditorModule : BaseDebugModule
{
    public override string ModuleName => "Stations";

    private const string StationsDirectory = "res://Configuration/stations";

    private StationEditorModel? _model;

    private ItemList _categoryList = null!;
    private Label _categoryHeaderLabel = null!;
    private Button _deleteCategoryButton = null!;
    private VBoxContainer _stationListContainer = null!;
    private HSplitContainer _split = null!;
    private Button _saveButton = null!;
    private Button _revertButton = null!;
    private Label _feedbackLabel = null!;

    private string? _selectedCategory;
    private double _feedbackTimer;

    public override void _Ready()
    {
        base._Ready();
        AcquireNodeReferences();
        AdjustSplitOffset();
        LoadModel();
    }

    private void AcquireNodeReferences()
    {
        _categoryList = GetNode<ItemList>("%CategoryList");
        _categoryHeaderLabel = GetNode<Label>("%CategoryHeaderLabel");
        _deleteCategoryButton = GetNode<Button>("%DeleteCategoryButton");
        _stationListContainer = GetNode<VBoxContainer>("%StationListContainer");
        _split = GetNode<HSplitContainer>("RootVBox/Split");
        _saveButton = GetNode<Button>("%SaveButton");
        _revertButton = GetNode<Button>("%RevertButton");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");
    }

    private void AdjustSplitOffset()
    {
        _split.SplitOffsets = new[] { (int)GetViewport().GetVisibleRect().Size.X / 5 };
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateButtonStates();
        UpdateDirtyIndicator();
        UpdateFeedbackLabel(delta);
    }

    // ── Model loading ────────────────────────────────────────────────────

    private void LoadModel()
    {
        try
        {
            _model = new StationEditorModel(StationsDirectory);
            _model.LoadFromDisk();
            PopulateCategoryList();
            if (_categoryList.ItemCount > 0)
            {
                _categoryList.Select(0);
                OnCategorySelected(0);
            }
            else
            {
                RefreshStationList();
            }
        }
        catch (InvalidOperationException ex)
        {
            GameLogger.Error($"Failed to load station editor model: {ex.Message}");
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
            RefreshStationList();
            return;
        }
        _selectedCategory = _categoryList.GetItemText((int)index);
        _deleteCategoryButton.Disabled = false;
        RefreshStationList();
    }

    private void RefreshStationList()
    {
        foreach (var child in _stationListContainer.GetChildren())
            child.QueueFree();

        if (_model == null || _selectedCategory == null || !_model.Categories.ContainsKey(_selectedCategory))
        {
            _categoryHeaderLabel.Text = "Select a file";
            var placeholder = new Label { Text = "Select a file", HorizontalAlignment = HorizontalAlignment.Center };
            placeholder.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _stationListContainer.AddChild(placeholder);
            return;
        }

        var category = _model.Categories[_selectedCategory];
        _categoryHeaderLabel.Text = category.CategoryName;
        for (int i = 0; i < category.Stations.Count; i++)
        {
            var card = StationCard.Create(_model, _selectedCategory, i, category.Stations[i]);
            card.CardsNeedRebuild += RefreshStationList;
            _stationListContainer.AddChild(card);
        }
    }

    // ── Category create / delete ─────────────────────────────────────────

    private void OnNewCategoryPressed()
    {
        var lineEdit = new LineEdit { PlaceholderText = "File name (no extension)", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var dialog = new ConfirmationDialog { Title = "New File", DialogText = "Enter file name (becomes {name}.yaml):" };
        dialog.AddChild(lineEdit);
        dialog.Confirmed += () =>
        {
            string name = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(name)) { ShowAcceptDialog("Invalid Name", "File name cannot be empty."); return; }
            if (name.Contains(' ') || name.Contains('.') || name.Contains('/'))
            { ShowAcceptDialog("Invalid Name", "File name cannot contain spaces, dots, or slashes."); return; }
            if (_model != null && _model.Categories.ContainsKey(name))
            { ShowAcceptDialog("Duplicate Name", $"File '{name}' already exists."); return; }
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
            Title = "Delete File",
            DialogText = $"Delete file '{_selectedCategory}' and all its stations?\nDoes not take effect until Save."
        };
        dialog.Confirmed += () =>
        {
            try { _model?.DeleteCategory(_selectedCategory!); }
            catch (KeyNotFoundException ex) { GameLogger.Warning(ex.Message); }
            _selectedCategory = null;
            _deleteCategoryButton.Disabled = true;
            PopulateCategoryList();
            RefreshStationList();
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    private void OnNewStationPressed()
    {
        if (_model == null || string.IsNullOrEmpty(_selectedCategory)) return;
        var entry = new StationEditorModel.StationEditEntry
        {
            Name = "New_Station",
            StationType = _selectedCategory,
            ConstructionTime = 30f
        };
        _model.AddStation(_selectedCategory, entry);
        RefreshStationList();
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
            StationEditorYamlIO.WriteAllCategories(
                StationsDirectory,
                new Dictionary<string, StationEditorModel.StationCategoryData>(_model.Categories));
            // Loader caches with an early return; clear it so the DB reload sees new YAML.
            StationConfigLoader.ClearCache();
            _model.LoadFromDisk();
            int reloaded = EditorDatabaseReloader.ReloadAll("StationDatabase");
            PopulateCategoryList();
            RefreshStationList();
            ShowFeedback($"Saved + reloaded {reloaded} DBs.");
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Station save failed: {ex.Message}");
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
                RefreshStationList();
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