#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using UtilityLibrary;
using Debug;
using DeveloperTools.Common;

namespace DeveloperTools.ResourceEditor;

/// <summary>
/// Debug module for resource YAML configuration editing.
/// Two-panel layout: category list (left), resource list (right).
/// Save/Revert toolbar with buffered write model.
/// Enhanced UX: RichTextLabel validation output, success/revert feedback
/// labels with auto-fade, and confirmation dialogs for destructive actions.
/// GUI layout is defined in ResourceEditorModule.tscn.
/// </summary>
public partial class ResourceEditorModule : BaseDebugModule
{
	public override string ModuleName => "Resources";

	private const string CategoriesDirectory = "res://Configuration/ResourceDefinition/categories";

	// --- Model ---
	private ResourceEditorModel? _model;

	// --- UI References ---
	private HSplitContainer? _splitContainer;
	private ItemList? _categoryList;
	private Label? _categoryHeaderLabel;
	private Button? _newCategoryButton;
	private Button? _deleteCategoryButton;
	private Button? _newResourceButton;
	private VBoxContainer? _resourceListContainer;
	private Button? _saveButton;
	private Button? _revertButton;
	private Label? _feedbackLabel;

	// --- Scene References ---
	private PackedScene? _cardScene;

	// --- State ---
	private string? _selectedCategory;

	// --- Feedback State ---
	private string _feedbackText = "";
	private double _feedbackTimer;

	public override void _Ready()
	{
		base._Ready();

		// Get node references from scene tree
		_splitContainer = GetNode<HSplitContainer>("%HSplitContainer");
		_categoryList = GetNode<ItemList>("%CategoryList");
		_categoryHeaderLabel = GetNode<Label>("%CategoryHeaderLabel");
		_newCategoryButton = GetNode<Button>("%NewCategoryButton");
		_deleteCategoryButton = GetNode<Button>("%DeleteCategoryButton");
		_newResourceButton = GetNode<Button>("%NewResourceButton");
		_resourceListContainer = GetNode<VBoxContainer>("%ResourceListContainer");
		_saveButton = GetNode<Button>("%SaveButton");
		_revertButton = GetNode<Button>("%RevertButton");
		_feedbackLabel = GetNode<Label>("%FeedbackLabel");

		// Load sub-scenes
		_cardScene = GD.Load<PackedScene>("res://DeveloperTools/ResourceEditor/ResourceCard.tscn");

		// Set dynamic split offset
		var viewportWidth = (int)GetViewport().GetVisibleRect().Size.X;
		_splitContainer.SplitOffsets = new[] { viewportWidth / 5 };

		// Connect signals
		_categoryList.ItemSelected += OnCategorySelected;
		_newCategoryButton.Pressed += OnNewCategoryPressed;
		_deleteCategoryButton.Pressed += OnDeleteCategoryPressed;
		_newResourceButton.Pressed += OnNewResourcePressed;
		_saveButton.Pressed += OnSavePressed;
		_revertButton.Pressed += OnRevertPressed;

		// Init feedback label
		_feedbackLabel.Visible = false;
		_feedbackLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1.0f, 0.4f));

		// Load model
		LoadModel();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		UpdateButtonStates();
		UpdateDirtyIndicator();
		UpdateFeedbackLabel(delta);
	}

	// ========================================================================
	// MODEL LOADING
	// ========================================================================

	private void LoadModel()
	{
		try
		{
			_model = new ResourceEditorModel(CategoriesDirectory);
			_model.LoadFromDisk();
			PopulateCategoryList();
			if (_categoryList!.ItemCount > 0)
				_categoryList.Select(0);
			OnCategorySelected(0);
		}
		catch (InvalidOperationException ex)
		{
			GameLogger.Error($"Failed to load resource editor model: {ex.Message}");
			ShowAcceptDialog("Load Error", ex.Message);
		}
	}

	private void ReloadModel()
	{
		LoadModel();
	}

	// ========================================================================
	// CATEGORY LIST
	// ========================================================================

	private void PopulateCategoryList()
	{
		_categoryList?.Clear();
		if (_model == null)
			return;

		foreach (var categoryKey in _model.Categories.Keys)
		{
			_categoryList?.AddItem(categoryKey);
		}
	}

	private void OnCategorySelected(long index)
	{
		if (_categoryList == null || _categoryList.ItemCount == 0 || index < 0)
		{
			_selectedCategory = null;
			RefreshResourceList();
			return;
		}

		_selectedCategory = _categoryList.GetItemText((int)index);
		_deleteCategoryButton!.Disabled = false;
		RefreshResourceList();
	}

	// ========================================================================
	// RESOURCE LIST
	// ========================================================================

	private void RefreshResourceList()
	{
		if (_resourceListContainer == null || _cardScene == null)
			return;

		// Clear existing children
		foreach (var child in _resourceListContainer.GetChildren())
			child.QueueFree();

		if (_model == null || _selectedCategory == null || !_model.Categories.ContainsKey(_selectedCategory))
		{
			if (_categoryHeaderLabel != null)
				_categoryHeaderLabel.Text = "Select a category";
			var placeholder = new Label
			{
				Text = "Select a category",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			placeholder.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
			_resourceListContainer.AddChild(placeholder);
			return;
		}

		var category = _model.Categories[_selectedCategory];
		_categoryHeaderLabel!.Text = category.CategoryName;

		var allTags = _model.GetAllTags();

		for (int i = 0; i < category.Resources.Count; i++)
		{
			var entry = category.Resources[i];
			var card = _cardScene.Instantiate<ResourceCard>();
			card.Initialize(_model, _selectedCategory, i, entry, allTags);
			card.CardsNeedRebuild += RefreshResourceList;
			_resourceListContainer.AddChild(card);
		}
	}

	// ========================================================================
	// NEW CATEGORY
	// ========================================================================

	private void OnNewCategoryPressed()
	{
		var lineEdit = new LineEdit
		{
			PlaceholderText = "Category name (lowercase, no spaces)",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};

		var dialog = new ConfirmationDialog
		{
			Title = "New Category",
			DialogText = "Enter category name:"
		};

		dialog.AddChild(lineEdit);

		dialog.Confirmed += () =>
		{
			string name = lineEdit.Text.Trim().ToLowerInvariant();

			if (string.IsNullOrEmpty(name))
			{
				ShowAcceptDialog("Invalid Name", "Category name cannot be empty.");
				return;
			}

			if (name.Contains(' ') || name.Contains('.'))
			{
				ShowAcceptDialog("Invalid Name", "Category name must be lowercase with no spaces or dots.");
				return;
			}

			if (name.EndsWith(".yaml"))
			{
				ShowAcceptDialog("Invalid Name", "Category name must not include '.yaml' suffix.");
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
				// Select the new category
				for (int i = 0; i < _categoryList!.ItemCount; i++)
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

	// ========================================================================
	// DELETE CATEGORY
	// ========================================================================

	private void OnDeleteCategoryPressed()
	{
		if (string.IsNullOrEmpty(_selectedCategory))
			return;

		var dialog = new ConfirmationDialog
		{
			Title = "Delete Category",
			DialogText = $"Delete category '{_selectedCategory}' and all its resources?\nWill not take effect until Save."
		};

		dialog.Confirmed += () =>
		{
			try
			{
				_model?.DeleteCategory(_selectedCategory!);
			}
			catch (KeyNotFoundException ex)
			{
				GameLogger.Warning(ex.Message);
			}

			_selectedCategory = null;
			_deleteCategoryButton!.Disabled = true;
			PopulateCategoryList();
			RefreshResourceList();
		};

		AddChild(dialog);
		dialog.PopupCentered(new Vector2I(400, 150));
	}

	// ========================================================================
	// NEW RESOURCE
	// ========================================================================

	private void OnNewResourcePressed()
	{
		if (_model == null || string.IsNullOrEmpty(_selectedCategory))
			return;

		var entry = new ResourceEditorModel.ResourceEditEntry
		{
			IdName = "new_resource",
			ResourceTier = 0,
			StateOfMatter = Structures.Enums.StateOfMatter.Solid,
			MaxStackSize = 100f,
			TransportWeight = 1.0f,
			Tags = new HashSet<string>(),
			IconBasePath = null,
			IconScale = 1.0f,
			IconTint = Colors.White
		};

		_model.AddResource(_selectedCategory, entry);
		RefreshResourceList();
	}

	// ========================================================================
	// SAVE (Enhanced UX)
	// ========================================================================

	private void OnSavePressed()
	{
		if (_model == null)
			return;

		var validationMessages = _model.Validate();

		// Separate errors from warnings (icon path issues are warnings)
		var errors = validationMessages
			.Where(m => !m.Contains("not starting with res://"))
			.ToList();
		var warnings = validationMessages
			.Where(m => m.Contains("not starting with res://"))
			.ToList();

		// Block save on errors
		if (errors.Count > 0)
		{
			ShowValidationDialog(errors, warnings);
			return;
		}

		// Show warnings but allow save
		if (warnings.Count > 0)
		{
			ShowValidationDialog(null, warnings, allowSave: true);
			return;
		}

		ExecuteSave();
	}

	private void ExecuteSave()
	{
		try
		{
			ResourceEditorYamlIO.WriteAllCategories(
				CategoriesDirectory,
				new Dictionary<string, ResourceEditorModel.ResourceCategoryData>(_model.Categories)
			);
			_model.LoadFromDisk();
			int reloaded = EditorDatabaseReloader.ReloadAll("ResourceDatabase");
			PopulateCategoryList();
			RefreshResourceList();
			ShowFeedback($"Saved + reloaded {reloaded} DBs.");
		}
		catch (Exception ex)
		{
			GameLogger.Error($"Save failed: {ex.Message}");
			ShowAcceptDialog("Save Error", ex.Message);
		}
	}

	/// <summary>
	/// Shows a validation dialog with RichTextLabel for colored errors/warnings.
	/// If allowSave is true, the dialog confirms before proceeding with save.
	/// </summary>
	private void ShowValidationDialog(
		List<string>? errors,
		List<string>? warnings,
		bool allowSave = false)
	{
		var richLabel = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollFollowing = true,
			CustomMinimumSize = new Vector2(350, 150),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};

		var text = "";

		if (errors != null && errors.Count > 0)
		{
			foreach (var error in errors)
				text += $"[color=red]{error}[/color]\n";
		}

		if (warnings != null && warnings.Count > 0)
		{
			foreach (var warning in warnings)
				text += $"[color=yellow]{warning}[/color]\n";
		}

		richLabel.Text = text;

		var dialogTitle = errors != null && errors.Count > 0
			? "Validation Errors"
			: "Validation Warnings";

		if (allowSave)
		{
			var dialog = new ConfirmationDialog
			{
				Title = dialogTitle,
				DialogText = "Warnings found. Continue saving?"
			};
			dialog.AddChild(richLabel);

			dialog.Confirmed += ExecuteSave;

			AddChild(dialog);
			dialog.PopupCentered(new Vector2I(500, 300));
		}
		else
		{
			var dialog = new AcceptDialog
			{
				Title = dialogTitle
			};
			dialog.AddChild(richLabel);

			AddChild(dialog);
			dialog.PopupCentered(new Vector2I(500, 300));
		}
	}

	// ========================================================================
	// REVERT (Enhanced UX)
	// ========================================================================

	private void OnRevertPressed()
	{
		if (_model == null || !_model.HasUnsavedChanges)
			return;

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
				RefreshResourceList();
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

	// ========================================================================
	// UI STATE
	// ========================================================================

	private void UpdateButtonStates()
	{
		if (_model == null)
			return;

		var dirty = _model.HasUnsavedChanges;

		if (_saveButton != null)
			_saveButton.Disabled = !dirty;

		if (_revertButton != null)
			_revertButton.Disabled = !dirty;
	}

	private void UpdateDirtyIndicator()
	{
		if (_model == null)
			return;

		Name = _model.HasUnsavedChanges ? ModuleName + " *" : ModuleName;
	}

	// ========================================================================
	// FEEDBACK LABEL
	// ========================================================================

	private void ShowFeedback(string message)
	{
		_feedbackText = message;
		_feedbackTimer = 2.0;

		if (_feedbackLabel != null)
		{
			_feedbackLabel.Text = message;
			_feedbackLabel.Visible = true;
			_feedbackLabel.Modulate = Colors.White;
		}
	}

	private void UpdateFeedbackLabel(double delta)
	{
		if (_feedbackTimer <= 0 || _feedbackLabel == null)
			return;

		_feedbackTimer -= delta;

		if (_feedbackTimer <= 0)
		{
			_feedbackLabel.Visible = false;
			_feedbackTimer = 0;
			_feedbackText = "";
		}
		else
		{
			// Fade out over last 0.5 seconds
			float alpha = _feedbackTimer < 0.5 ? (float)_feedbackTimer / 0.5f : 1.0f;
			_feedbackLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
		}
	}

	// ========================================================================
	// MODULE LIFECYCLE
	// ========================================================================

	public override void OnModuleEnabled()
	{
		if (_model == null)
			ReloadModel();
		base.OnModuleEnabled();
	}

	public override void OnModuleDisabled()
	{
		base.OnModuleDisabled();
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
		dialog.PopupCentered(new Vector2I(400, 150));
	}
}
#endif
