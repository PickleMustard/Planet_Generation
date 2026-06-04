#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DeveloperTools.RecipeEditor;

/// <summary>
/// Popup for tag management on a recipe entry. Mirrors ResourceEditor's TagsPopup
/// but binds to RecipeEditorModel. Built programmatically — no scene file.
/// </summary>
public partial class RecipeTagsPopup : PopupPanel
{
    [Signal]
    public delegate void TagsChangedEventHandler();

    private RecipeEditorModel? _model;
    private string _categoryName = "";
    private int _recipeIndex;
    private RecipeEditorModel.RecipeEditEntry? _entry;
    private HashSet<string> _allTags = new();

    private HFlowContainer _currentTagsFlow = null!;
    private VBoxContainer _allTagsVBox = null!;
    private LineEdit _newTagEdit = null!;
    private Button _addTagButton = null!;
    private Button _closeButton = null!;

    public void Initialize(
        RecipeEditorModel model,
        string categoryName,
        int recipeIndex,
        RecipeEditorModel.RecipeEditEntry entry,
        HashSet<string> allTags)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);

        _model = model;
        _categoryName = categoryName;
        _recipeIndex = recipeIndex;
        _entry = entry;
        _allTags = new HashSet<string>(allTags);
    }

    public override void _Ready()
    {
        base._Ready();
        MinSize = new Vector2I(420, 520);
        Size = new Vector2I(420, 520);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        AddChild(root);

        var currentHeader = new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Current tags" };
        currentHeader.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(currentHeader);

        _currentTagsFlow = new HFlowContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_currentTagsFlow);

        root.AddChild(new HSeparator { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var allHeader = new Label { ThemeTypeVariation = "LabelHighContrast", Text = "All tags" };
        allHeader.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(allHeader);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 240)
        };
        _allTagsVBox = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        scroll.AddChild(_allTagsVBox);
        root.AddChild(scroll);

        var addRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _newTagEdit = new LineEdit
        {
            PlaceholderText = "new_tag",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _newTagEdit.TextSubmitted += _ => AddNewTag();
        addRow.AddChild(_newTagEdit);

        _addTagButton = new Button { Text = "Add" };
        _addTagButton.Pressed += AddNewTag;
        addRow.AddChild(_addTagButton);
        root.AddChild(addRow);

        _closeButton = new Button
        {
            Text = "Close",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _closeButton.Pressed += OnClosePressed;
        root.AddChild(_closeButton);

        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        RefreshCurrentTags();
        RefreshAllTags();
    }

    private void RefreshCurrentTags()
    {
        if (_entry == null) return;
        foreach (var child in _currentTagsFlow.GetChildren())
            child.QueueFree();

        foreach (var tag in _entry.Tags)
        {
            var tagButton = new Button { Text = $"{tag} ✕" };
            tagButton.AddThemeFontSizeOverride("font_size", 11);
            tagButton.Pressed += () => RemoveTag(tag);
            _currentTagsFlow.AddChild(tagButton);
        }
    }

    private void RefreshAllTags()
    {
        if (_entry == null) return;
        foreach (var child in _allTagsVBox.GetChildren())
            child.QueueFree();

        foreach (var tag in _allTags.OrderBy(t => t))
        {
            var cb = new CheckBox
            {
                Text = tag,
                ButtonPressed = _entry.Tags.Contains(tag)
            };
            cb.Toggled += pressed => OnTagToggled(tag, pressed);
            _allTagsVBox.AddChild(cb);
        }
    }

    private void RemoveTag(string tag)
    {
        if (_model == null || _entry == null) return;
        var newTags = new HashSet<string>(_entry.Tags);
        newTags.Remove(tag);
        _model.UpdateRecipeTags(_categoryName, _recipeIndex, newTags);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        RefreshDisplay();
        EmitSignal(SignalName.TagsChanged);
    }

    private void OnTagToggled(string tag, bool pressed)
    {
        if (_model == null || _entry == null) return;
        var newTags = new HashSet<string>(_entry.Tags);
        if (pressed) newTags.Add(tag); else newTags.Remove(tag);
        _model.UpdateRecipeTags(_categoryName, _recipeIndex, newTags);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        RefreshCurrentTags();
        EmitSignal(SignalName.TagsChanged);
    }

    private void AddNewTag()
    {
        if (_model == null || _entry == null) return;

        string newTag = _newTagEdit.Text.Trim();
        if (string.IsNullOrEmpty(newTag))
        {
            ShowAcceptDialog("Invalid Tag", "Tag name cannot be empty.");
            return;
        }
        if (newTag.Contains(' '))
        {
            ShowAcceptDialog("Invalid Tag", "Tag name cannot contain spaces.");
            return;
        }

        var newTags = new HashSet<string>(_entry.Tags) { newTag };
        _model.UpdateRecipeTags(_categoryName, _recipeIndex, newTags);
        _entry = _model.Categories[_categoryName].Recipes[_recipeIndex];
        _allTags.Add(newTag);
        _newTagEdit.Text = "";
        RefreshDisplay();
        EmitSignal(SignalName.TagsChanged);
    }

    private void OnClosePressed()
    {
        Hide();
        QueueFree();
    }

    private void ShowAcceptDialog(string title, string message)
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = message
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(300, 120));
    }
}
#endif
