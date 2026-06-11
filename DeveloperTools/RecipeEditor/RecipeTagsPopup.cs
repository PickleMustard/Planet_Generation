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

    [Export] private HFlowContainer _currentTagsFlow = null!;
    [Export] private VBoxContainer _allTagsVBox = null!;
    [Export] private LineEdit _newTagEdit = null!;

    private static PackedScene? _scene;

    public static RecipeTagsPopup Create(
        RecipeEditorModel model,
        string categoryName,
        int recipeIndex,
        RecipeEditorModel.RecipeEditEntry entry,
        HashSet<string> allTags)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/RecipeEditor/RecipeTagsPopup.tscn");
        var popup = _scene.Instantiate<RecipeTagsPopup>();
        popup.Initialize(model, categoryName, recipeIndex, entry, allTags);
        return popup;
    }

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
        RefreshDisplay();
    }

    private void OnNewTagSubmitted(string _) => AddNewTag();
    private void OnAddPressed() => AddNewTag();

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
