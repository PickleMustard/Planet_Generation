#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Enums;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.ResourceEditor;

/// <summary>
/// Per-resource card control with inline editing for all resource fields.
/// Displays icon, editable name, tier/stack/weight/state controls,
/// tag management popup, and reorder/delete action buttons.
/// GUI layout is defined in ResourceCard.tscn.
/// </summary>
public partial class ResourceCard : PanelContainer
{
    // --- Signals ---

    /// <summary>
    /// Emitted when a mutation requires the parent to rebuild all cards
    /// (e.g., reorder, delete, new resource added).
    /// </summary>
    [Signal]
    public delegate void CardsNeedRebuildEventHandler();

    // --- Data Fields ---

    private ResourceEditorModel? _model;
    private string _categoryName = "";
    private int _resourceIndex;
    private ResourceEditorModel.ResourceEditEntry? _entry;
    private HashSet<string>? _allTags;

    // --- UI References ---

    private TextureRect _iconRect = null!;
    private Label _nameLabel = null!;
    private LineEdit _nameEdit = null!;
    private SpinBox _tierSpinBox = null!;
    private HSlider _stackSlider = null!;
    private Label _stackValueLabel = null!;
    private HSlider _weightSlider = null!;
    private Label _weightValueLabel = null!;
    private OptionButton _stateOption = null!;
    private Button _tagsButton = null!;
    private Button _moveUpButton = null!;
    private Button _moveDownButton = null!;
    private Button _deleteButton = null!;

    // --- Scene References ---

    private PackedScene? _tagsPopupScene;
    private PackedScene? _iconPickerScene;

    // --- State ---

    private bool _isEditingName;

    // ========================================================================
    // INITIALIZATION
    // ========================================================================

    /// <summary>
    /// Sets the model data for this card. Must be called before adding
    /// to the scene tree (which triggers _Ready).
    /// </summary>
    public void Initialize(
        ResourceEditorModel model,
        string categoryName,
        int resourceIndex,
        ResourceEditorModel.ResourceEditEntry entry,
        HashSet<string> allTags
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);

        _model = model;
        _categoryName = categoryName;
        _resourceIndex = resourceIndex;
        _entry = entry;
        _allTags = allTags;

        Name = $"ResourceCard_{_entry.IdName}";
    }

    public override void _Ready()
    {
        base._Ready();

        // Get node references from scene tree
        _iconRect = GetNode<TextureRect>("%IconRect");
        _nameLabel = GetNode<Label>("%NameLabel");
        _nameEdit = GetNode<LineEdit>("%NameEdit");
        _tierSpinBox = GetNode<SpinBox>("%TierSpinBox");
        _stackSlider = GetNode<HSlider>("%StackSlider");
        _stackValueLabel = GetNode<Label>("%StackValueLabel");
        _weightSlider = GetNode<HSlider>("%WeightSlider");
        _weightValueLabel = GetNode<Label>("%WeightValueLabel");
        _stateOption = GetNode<OptionButton>("%StateOption");
        _tagsButton = GetNode<Button>("%TagsButton");
        _moveUpButton = GetNode<Button>("%MoveUpButton");
        _moveDownButton = GetNode<Button>("%MoveDownButton");
        _deleteButton = GetNode<Button>("%DeleteButton");

        // Populate OptionButton items (data, not layout)
        _stateOption.AddItem("Solid", (int)StateOfMatter.Solid);
        _stateOption.AddItem("Fluid", (int)StateOfMatter.Fluid);

        // Load sub-scenes
        _tagsPopupScene = GD.Load<PackedScene>(
            "res://DeveloperTools/ResourceEditor/TagsPopup.tscn"
        );
        _iconPickerScene = GD.Load<PackedScene>(
            "res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn"
        );

        // Connect signals
        _iconRect.GuiInput += OnIconInput;
        _nameLabel.GuiInput += OnNameLabelInput;
        _nameEdit.TextSubmitted += OnNameTextSubmitted;
        _nameEdit.FocusExited += OnNameFocusExited;
        _tierSpinBox.ValueChanged += OnTierChanged;
        _stackSlider.ValueChanged += OnStackSizeChanged;
        _weightSlider.ValueChanged += OnTransportWeightChanged;
        _stateOption.ItemSelected += OnStateSelected;
        _tagsButton.Pressed += OnTagsPressed;
        _moveUpButton.Pressed += OnMoveUpPressed;
        _moveDownButton.Pressed += OnMoveDownPressed;
        _deleteButton.Pressed += OnDeletePressed;

        // Initial display refresh
        RefreshControls();
    }

    // ========================================================================
    // REFRESH
    // ========================================================================

    /// <summary>
    /// Updates all controls from the given entry state.
    /// Called after initial build and after parent rebuilds cards.
    /// </summary>
    public void Refresh(ResourceEditorModel.ResourceEditEntry entry, int newIndex)
    {
        _entry = entry;
        _resourceIndex = newIndex;
        Name = $"ResourceCard_{_entry.IdName}";
        RefreshControls();
    }

    private void RefreshControls()
    {
        if (_entry == null || _model == null)
            return;

        // Icon
        LoadIcon();

        // Name
        if (!_isEditingName)
        {
            _nameLabel.Text = _entry.IdName;
            _nameLabel.Visible = true;
            _nameEdit.Visible = false;
        }

        // Tier
        _tierSpinBox.SetValueNoSignal(_entry.ResourceTier);

        // Stack size
        _stackSlider.SetValueNoSignal(_entry.MaxStackSize);
        _stackValueLabel.Text = ((int)_entry.MaxStackSize).ToString();

        // Transport weight
        _weightSlider.SetValueNoSignal(_entry.TransportWeight);
        _weightValueLabel.Text = _entry.TransportWeight.ToString("0.0");

        // State of matter
        for (int i = 0; i < _stateOption.ItemCount; i++)
        {
            if (_stateOption.GetItemId(i) == (int)_entry.StateOfMatter)
            {
                _stateOption.Select(i);
                break;
            }
        }

        // Tags button
        _tagsButton.Text = $"Tags ({_entry.Tags.Count})";

        // Move buttons
        if (!_model.Categories.ContainsKey(_categoryName))
            return;

        var resources = _model.Categories[_categoryName].Resources;
        _moveUpButton.Disabled = _resourceIndex <= 0;
        _moveDownButton.Disabled = _resourceIndex >= resources.Count - 1;
    }

    private void LoadIcon()
    {
        if (_iconRect == null || _entry == null)
            return;

        Texture2D? texture = null;

        if (!string.IsNullOrEmpty(_entry.IconBasePath))
        {
            texture = IconDataLoader.LoadIconTexture(
                _entry.IconBasePath,
                _entry.IdName
            );
        }

        if (texture == null)
        {
            texture = IconDataLoader.GetFallbackIcon();
        }

        _iconRect.Texture = texture;
    }

    // ========================================================================
    // ICON CLICK
    // ========================================================================

    private void OnIconInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            OpenIconPicker();
        }
    }

    private void OpenIconPicker()
    {
        if (_model == null || _entry == null || _iconPickerScene == null)
            return;

        var popup = _iconPickerScene.Instantiate<IconPickerPopup>();
        AddChild(popup);
        popup.IconSelected += OnIconSelected;

        // Position near the icon
        var iconRect = _iconRect.GetGlobalRect();
        popup.Position = new Vector2I((int)iconRect.End.X + 4, (int)iconRect.Position.Y);

        popup.OpenPicker();
    }

    private void OnIconSelected(string basePath)
    {
        if (_model == null || _entry == null)
            return;

        _model.UpdateResourceField(_categoryName, _resourceIndex, "IconBasePath", basePath);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];
        LoadIcon();
    }

    // ========================================================================
    // INLINE NAME EDITING
    // ========================================================================

    private void OnNameLabelInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            StartNameEdit();
        }
    }

    private void StartNameEdit()
    {
        _isEditingName = true;
        _nameEdit.Text = _entry?.IdName ?? "";
        _nameLabel.Visible = false;
        _nameEdit.Visible = true;
        _nameEdit.GrabFocus();
        _nameEdit.SelectAll();
    }

    private void FinishNameEdit()
    {
        if (!_isEditingName || _entry == null || _model == null)
            return;

        _isEditingName = false;
        string newText = _nameEdit.Text.Trim();

        if (string.IsNullOrEmpty(newText))
        {
            // Revert on empty
            _nameLabel.Text = _entry.IdName;
            _nameLabel.Visible = true;
            _nameEdit.Visible = false;
            return;
        }

        if (newText == _entry.IdName)
        {
            // No change
            _nameLabel.Visible = true;
            _nameEdit.Visible = false;
            return;
        }

        // Check for duplicate names across all categories
        if (IsDuplicateName(newText))
        {
            ShowAcceptDialog(
                "Duplicate Name",
                $"Resource name '{newText}' already exists. Reverting."
            );
            _nameLabel.Text = _entry.IdName;
            _nameLabel.Visible = true;
            _nameEdit.Visible = false;
            return;
        }

        // Valid rename
        _model.UpdateResourceField(_categoryName, _resourceIndex, "IdName", newText);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];
        _nameLabel.Text = _entry.IdName;
        _nameLabel.Visible = true;
        _nameEdit.Visible = false;
        Name = $"ResourceCard_{_entry.IdName}";
    }

    private bool IsDuplicateName(string name)
    {
        if (_model == null)
            return false;

        foreach (var category in _model.Categories.Values)
        {
            for (int i = 0; i < category.Resources.Count; i++)
            {
                var resource = category.Resources[i];
                if (
                    resource.IdName == name
                    && !(category.CategoryName == _categoryName && i == _resourceIndex)
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void OnNameTextSubmitted(string _)
    {
        FinishNameEdit();
    }

    private void OnNameFocusExited()
    {
        CallDeferred(MethodName.FinishNameEditDeferred);
    }

    private void FinishNameEditDeferred()
    {
        FinishNameEdit();
    }

    // ========================================================================
    // FIELD CHANGES
    // ========================================================================

    private void OnTierChanged(double value)
    {
        if (_model == null || _entry == null)
            return;
        _model.UpdateResourceField(_categoryName, _resourceIndex, "ResourceTier", (int)value);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];
    }

    private void OnStackSizeChanged(double value)
    {
        if (_model == null || _entry == null)
            return;
        _model.UpdateResourceField(_categoryName, _resourceIndex, "MaxStackSize", (float)value);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];
        _stackValueLabel.Text = ((int)value).ToString();
    }

    private void OnTransportWeightChanged(double value)
    {
        if (_model == null || _entry == null)
            return;
        _model.UpdateResourceField(_categoryName, _resourceIndex, "TransportWeight", (float)value);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];
        _weightValueLabel.Text = ((float)value).ToString("0.0");
    }

    private void OnStateSelected(long index)
    {
        if (_model == null || _entry == null)
            return;
        int id = _stateOption.GetItemId((int)index);
        var state = (StateOfMatter)id;
        _model.UpdateResourceField(_categoryName, _resourceIndex, "StateOfMatter", state);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];
    }

    // ========================================================================
    // TAGS
    // ========================================================================

    private void OnTagsPressed()
    {
        if (_model == null || _entry == null || _tagsPopupScene == null)
            return;

        var currentAllTags = _model.GetAllTags();
        var popup = _tagsPopupScene.Instantiate<TagsPopup>();
        popup.Initialize(_model, _categoryName, _resourceIndex, _entry, currentAllTags);
        AddChild(popup);

        // Position near the button
        var buttonRect = _tagsButton.GetGlobalRect();
        popup.Position = new Vector2I((int)buttonRect.Position.X, (int)buttonRect.End.Y);

        popup.TagsChanged += OnTagsChanged;
        popup.Popup();
    }

    private void OnTagsChanged()
    {
        if (_model == null || _entry == null)
            return;
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];
        _tagsButton.Text = $"Tags ({_entry.Tags.Count})";
    }

    // ========================================================================
    // ACTIONS: MOVE / DELETE
    // ========================================================================

    private void OnMoveUpPressed()
    {
        if (_model == null || _resourceIndex <= 0)
            return;

        _model.MoveResource(_categoryName, _resourceIndex, _resourceIndex - 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnMoveDownPressed()
    {
        if (_model == null || !_model.Categories.ContainsKey(_categoryName))
            return;

        var resources = _model.Categories[_categoryName].Resources;
        if (_resourceIndex >= resources.Count - 1)
            return;

        _model.MoveResource(_categoryName, _resourceIndex, _resourceIndex + 1);
        EmitSignal(SignalName.CardsNeedRebuild);
    }

    private void OnDeletePressed()
    {
        if (_entry == null)
            return;

        var dialog = new ConfirmationDialog
        {
            Title = "Delete Resource",
            DialogText = $"Delete resource '{_entry.IdName}'? This cannot be undone.",
        };

        dialog.Confirmed += () =>
        {
            if (_model == null)
                return;
            _model.DeleteResource(_categoryName, _resourceIndex);
            EmitSignal(SignalName.CardsNeedRebuild);
        };

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }

    // ========================================================================
    // DIALOG HELPER
    // ========================================================================

    private void ShowAcceptDialog(string title, string message)
    {
        var dialog = new AcceptDialog { Title = title, DialogText = message };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(400, 150));
    }
}
#endif
