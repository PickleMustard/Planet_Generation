#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UtilityLibrary;
using UtilityLibrary.DataLoading;
using Debug;
using DeveloperTools.Common;

namespace DeveloperTools.ShipEditor;

/// <summary>
/// Debug module for ship YAML configuration editing. Two-panel layout: categories
/// (one per file) on the left, ship cards on the right. Save writes YAML, clears
/// ShipConfigLoader's cache, then reloads ShipDatabase so the running game reflects
/// edits without a restart. GUI defined in ShipEditorModule.tscn; behavior in this script.
/// </summary>
public partial class ShipEditorModule : BaseDebugModule
{
    public override string ModuleName => "Ships";

    private const string ShipsDirectory = "res://Configuration/ships";

    private ShipEditorModel? _model;

    // Scene node references — wired in the Godot editor via the inspector.
    [Export] public ItemList CategoryList = null!;
    [Export] public Label CategoryHeaderLabel = null!;
    [Export] public Button DeleteCategoryButton = null!;
    [Export] public VBoxContainer ShipListContainer = null!;
    [Export] public Button SaveButton = null!;
    [Export] public Button RevertButton = null!;
    [Export] public Label FeedbackLabel = null!;
    [Export] public HSplitContainer Split = null!;

    private string? _selectedCategory;
    private double _feedbackTimer;

    public override void _Ready()
    {
        base._Ready();
        ApplyDynamicLayout();
        LoadModel();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateButtonStates();
        UpdateDirtyIndicator();
        UpdateFeedbackLabel(delta);
    }

    private void ApplyDynamicLayout()
    {
        Split.SplitOffsets = new[] { (int)GetViewport().GetVisibleRect().Size.X / 5 };
    }

    // ── Model loading ────────────────────────────────────────────────────

    private void LoadModel()
    {
        try
        {
            _model = new ShipEditorModel(ShipsDirectory);
            _model.LoadFromDisk();
            PopulateCategoryList();
            if (CategoryList.ItemCount > 0)
            {
                CategoryList.Select(0);
                OnCategorySelected(0);
            }
            else
            {
                RefreshShipList();
            }
        }
        catch (InvalidOperationException ex)
        {
            GameLogger.Error($"Failed to load ship editor model: {ex.Message}");
            ShowAcceptDialog("Load Error", ex.Message);
        }
    }

    private void PopulateCategoryList()
    {
        CategoryList.Clear();
        if (_model == null) return;
        foreach (var key in _model.Categories.Keys.OrderBy(k => k))
            CategoryList.AddItem(key);
    }

    private void OnCategorySelected(long index)
    {
        if (CategoryList.ItemCount == 0 || index < 0)
        {
            _selectedCategory = null;
            RefreshShipList();
            return;
        }
        _selectedCategory = CategoryList.GetItemText((int)index);
        DeleteCategoryButton.Disabled = false;
        RefreshShipList();
    }

    private void RefreshShipList()
    {
        foreach (var child in ShipListContainer.GetChildren())
            child.QueueFree();

        if (_model == null || _selectedCategory == null || !_model.Categories.ContainsKey(_selectedCategory))
        {
            CategoryHeaderLabel.Text = "Select a file";
            var placeholder = new Label { Text = "Select a file", HorizontalAlignment = HorizontalAlignment.Center };
            placeholder.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            ShipListContainer.AddChild(placeholder);
            return;
        }

        var category = _model.Categories[_selectedCategory];
        CategoryHeaderLabel.Text = category.CategoryName;
        for (int i = 0; i < category.Ships.Count; i++)
        {
            var card = ShipCard.Create(_model, _selectedCategory, i, category.Ships[i]);
            card.CardsNeedRebuild += RefreshShipList;
            ShipListContainer.AddChild(card);
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
                for (int i = 0; i < CategoryList.ItemCount; i++)
                    if (CategoryList.GetItemText(i) == name) { CategoryList.Select(i); OnCategorySelected(i); break; }
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
            DialogText = $"Delete file '{_selectedCategory}' and all its ships?\nDoes not take effect until Save."
        };
        dialog.Confirmed += () =>
        {
            try { _model?.DeleteCategory(_selectedCategory!); }
            catch (KeyNotFoundException ex) { GameLogger.Warning(ex.Message); }
            _selectedCategory = null;
            DeleteCategoryButton.Disabled = true;
            PopulateCategoryList();
            RefreshShipList();
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    private void OnNewShipPressed()
    {
        if (_model == null || string.IsNullOrEmpty(_selectedCategory)) return;
        var entry = new ShipEditorModel.ShipEditEntry { Name = "New_Ship", WorkRequired = 10f };
        _model.AddShip(_selectedCategory, entry);
        RefreshShipList();
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
            ShipEditorYamlIO.WriteAllCategories(
                ShipsDirectory,
                new Dictionary<string, ShipEditorModel.ShipCategoryData>(_model.Categories));
            // Loader caches with an early return; clear it so the DB reload sees new YAML.
            ShipConfigLoader.ClearCache();
            _model.LoadFromDisk();
            int reloaded = EditorDatabaseReloader.ReloadAll("ShipDatabase");
            PopulateCategoryList();
            RefreshShipList();
            ShowFeedback($"Saved + reloaded {reloaded} DBs.");
        }
        catch (Exception ex)
        {
            GameLogger.Error($"Ship save failed: {ex.Message}");
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
                RefreshShipList();
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
        SaveButton.Disabled = !dirty;
        RevertButton.Disabled = !dirty;
    }

    private void UpdateDirtyIndicator()
    {
        if (_model == null) return;
        Name = _model.HasUnsavedChanges ? ModuleName + " *" : ModuleName;
    }

    private void ShowFeedback(string message)
    {
        _feedbackTimer = 2.0;
        FeedbackLabel.Text = message;
        FeedbackLabel.Visible = true;
        FeedbackLabel.Modulate = Colors.White;
    }

    private void UpdateFeedbackLabel(double delta)
    {
        if (_feedbackTimer <= 0) return;
        _feedbackTimer -= delta;
        if (_feedbackTimer <= 0) { FeedbackLabel.Visible = false; _feedbackTimer = 0; }
        else
        {
            float alpha = _feedbackTimer < 0.5 ? (float)_feedbackTimer / 0.5f : 1.0f;
            FeedbackLabel.Modulate = new Color(1f, 1f, 1f, alpha);
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
