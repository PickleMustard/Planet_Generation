#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UtilityLibrary;

namespace DeveloperTools.ResourceEditor;

/// <summary>
/// Popup for tag management on a resource entry.
/// Shows current tags (removable), all tags (assignable via CheckBox),
/// and provides an input for adding new tags.
/// GUI layout is defined in TagsPopup.tscn.
/// </summary>
public partial class TagsPopup : PopupPanel
{
	// --- Signals ---

	/// <summary>
	/// Emitted when tags are modified and the parent should update display.
	/// </summary>
	[Signal]
	public delegate void TagsChangedEventHandler();

	// --- Data Fields ---

	private ResourceEditorModel? _model;
	private string _categoryName = "";
	private int _resourceIndex;
	private ResourceEditorModel.ResourceEditEntry? _entry;
	private HashSet<string> _allTags = new();

	// --- UI References ---

	private HFlowContainer _currentTagsFlow = null!;
	private VBoxContainer _allTagsVBox = null!;
	private LineEdit _newTagEdit = null!;
	private Button _addTagButton = null!;
	private Button _closeButton = null!;

	// ========================================================================
	// INITIALIZATION
	// ========================================================================

	/// <summary>
	/// Sets the model data for this popup. Must be called before adding
	/// to the scene tree (which triggers _Ready).
	/// </summary>
	public void Initialize(
		ResourceEditorModel model,
		string categoryName,
		int resourceIndex,
		ResourceEditorModel.ResourceEditEntry entry,
		HashSet<string> allTags)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(entry);

		_model = model;
		_categoryName = categoryName;
		_resourceIndex = resourceIndex;
		_entry = entry;
		_allTags = new HashSet<string>(allTags);
	}

	public override void _Ready()
	{
		base._Ready();

		// Get node references from scene tree
		_currentTagsFlow = GetNode<HFlowContainer>("%CurrentTagsFlow");
		_allTagsVBox = GetNode<VBoxContainer>("%AllTagsVBox");
		_newTagEdit = GetNode<LineEdit>("%NewTagEdit");
		_addTagButton = GetNode<Button>("%AddTagButton");
		_closeButton = GetNode<Button>("%CloseButton");

		// Connect signals
		_newTagEdit.TextSubmitted += OnNewTagTextSubmitted;
		_addTagButton.Pressed += OnAddTagPressed;
		_closeButton.Pressed += OnClosePressed;

		// Initial display
		RefreshDisplay();
	}

	// ========================================================================
	// REFRESH DISPLAY
	// ========================================================================

	private void RefreshDisplay()
	{
		RefreshCurrentTags();
		RefreshAllTags();
	}

	private void RefreshCurrentTags()
	{
		if (_currentTagsFlow == null || _entry == null)
			return;

		// Clear existing tag buttons
		foreach (var child in _currentTagsFlow.GetChildren())
			child.QueueFree();

		// Add current tags as removable buttons
		foreach (var tag in _entry.Tags)
		{
			var tagButton = new Button
			{
				Text = tag,
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
			};
			tagButton.AddThemeFontSizeOverride("font_size", 11);
			tagButton.Pressed += () => RemoveTag(tag);
			_currentTagsFlow.AddChild(tagButton);
		}
	}

	private void RefreshAllTags()
	{
		if (_allTagsVBox == null || _entry == null)
			return;

		// Clear existing checkboxes
		foreach (var child in _allTagsVBox.GetChildren())
			child.QueueFree();

		// Sort tags alphabetically for consistent display
		var sortedTags = _allTags.OrderBy(t => t).ToList();

		foreach (var tag in sortedTags)
		{
			var checkBox = new CheckBox
			{
				Text = tag,
				ButtonPressed = _entry.Tags.Contains(tag),
				Name = $"TagCheckBox_{tag}"
			};
			checkBox.Toggled += (pressed) => OnTagToggled(tag, pressed);
			_allTagsVBox.AddChild(checkBox);
		}
	}

	// ========================================================================
	// TAG OPERATIONS
	// ========================================================================

	private void RemoveTag(string tag)
	{
		if (_model == null || _entry == null)
			return;

		var newTags = new HashSet<string>(_entry.Tags);
		newTags.Remove(tag);

		_model.UpdateResourceTags(_categoryName, _resourceIndex, newTags);
		_entry = _model.Categories[_categoryName].Resources[_resourceIndex];

		RefreshDisplay();
		EmitSignal(SignalName.TagsChanged);
	}

	private void OnTagToggled(string tag, bool pressed)
	{
		if (_model == null || _entry == null)
			return;

		var newTags = new HashSet<string>(_entry.Tags);

		if (pressed)
			newTags.Add(tag);
		else
			newTags.Remove(tag);

		_model.UpdateResourceTags(_categoryName, _resourceIndex, newTags);
		_entry = _model.Categories[_categoryName].Resources[_resourceIndex];

		RefreshCurrentTags();
		EmitSignal(SignalName.TagsChanged);
	}

	private void OnAddTagPressed()
	{
		AddNewTag();
	}

	private void OnNewTagTextSubmitted(string text)
	{
		AddNewTag();
	}

	private void AddNewTag()
	{
		if (_newTagEdit == null || _model == null || _entry == null)
			return;

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

		// Add tag to resource
		var newTags = new HashSet<string>(_entry.Tags);
		newTags.Add(newTag);

		_model.UpdateResourceTags(_categoryName, _resourceIndex, newTags);
		_entry = _model.Categories[_categoryName].Resources[_resourceIndex];

		// Add tag to all-tags set if new
		_allTags.Add(newTag);

		// Clear input
		_newTagEdit.Text = "";

		RefreshDisplay();
		EmitSignal(SignalName.TagsChanged);
	}

	private void OnClosePressed()
	{
		Hide();
		QueueFree();
	}

	// ========================================================================
	// DIALOG HELPER
	// ========================================================================

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
