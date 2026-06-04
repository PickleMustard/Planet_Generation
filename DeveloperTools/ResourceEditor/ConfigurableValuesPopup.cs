#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DeveloperTools.ResourceEditor;

/// <summary>
/// Popup for editing a resource's configurable values: a map of free-form string
/// keys to integer values. Each existing entry shows a key label, an integer SpinBox,
/// and a remove button; an add row appends a new key:value pair.
/// GUI layout is defined in ConfigurableValuesPopup.tscn.
/// </summary>
public partial class ConfigurableValuesPopup : PopupPanel
{
    // --- Signals ---

    /// <summary>
    /// Emitted when values are modified and the parent should update display.
    /// </summary>
    [Signal]
    public delegate void ValuesChangedEventHandler();

    // --- Data Fields ---

    private ResourceEditorModel? _model;
    private string _categoryName = "";
    private int _resourceIndex;
    private ResourceEditorModel.ResourceEditEntry? _entry;

    // --- UI References ---

    private VBoxContainer _valuesVBox = null!;
    private LineEdit _newKeyEdit = null!;
    private SpinBox _newValueSpinBox = null!;
    private Button _addButton = null!;
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
        ResourceEditorModel.ResourceEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);

        _model = model;
        _categoryName = categoryName;
        _resourceIndex = resourceIndex;
        _entry = entry;
    }

    public override void _Ready()
    {
        base._Ready();

        _valuesVBox = GetNode<VBoxContainer>("%ValuesVBox");
        _newKeyEdit = GetNode<LineEdit>("%NewKeyEdit");
        _newValueSpinBox = GetNode<SpinBox>("%NewValueSpinBox");
        _addButton = GetNode<Button>("%AddButton");
        _closeButton = GetNode<Button>("%CloseButton");

        _newKeyEdit.TextSubmitted += OnNewKeyTextSubmitted;
        _addButton.Pressed += OnAddPressed;
        _closeButton.Pressed += OnClosePressed;

        RefreshDisplay();
    }

    // ========================================================================
    // REFRESH DISPLAY
    // ========================================================================

    private void RefreshDisplay()
    {
        if (_valuesVBox == null || _entry == null)
            return;

        foreach (var child in _valuesVBox.GetChildren())
            child.QueueFree();

        foreach (var kvp in _entry.ConfigurableValues.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string key = kvp.Key;

            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

            var keyLabel = new Label
            { ThemeTypeVariation = "LabelHighContrast",
                Text = key,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            row.AddChild(keyLabel);

            var valueSpin = new SpinBox
            {
                MinValue = -1000000,
                MaxValue = 1000000,
                Value = kvp.Value,
            };
            valueSpin.ValueChanged += (v) => OnValueChanged(key, (int)v);
            row.AddChild(valueSpin);

            var removeButton = new Button { Text = "✕" };
            removeButton.Pressed += () => RemoveValue(key);
            row.AddChild(removeButton);

            _valuesVBox.AddChild(row);
        }
    }

    // ========================================================================
    // VALUE OPERATIONS
    // ========================================================================

    private void OnValueChanged(string key, int value)
    {
        if (_model == null || _entry == null)
            return;

        var newValues = new Dictionary<string, int>(_entry.ConfigurableValues)
        {
            [key] = value,
        };

        _model.UpdateConfigurableValues(_categoryName, _resourceIndex, newValues);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];

        EmitSignal(SignalName.ValuesChanged);
    }

    private void RemoveValue(string key)
    {
        if (_model == null || _entry == null)
            return;

        var newValues = new Dictionary<string, int>(_entry.ConfigurableValues);
        newValues.Remove(key);

        _model.UpdateConfigurableValues(_categoryName, _resourceIndex, newValues);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];

        RefreshDisplay();
        EmitSignal(SignalName.ValuesChanged);
    }

    private void OnAddPressed()
    {
        AddNewValue();
    }

    private void OnNewKeyTextSubmitted(string text)
    {
        AddNewValue();
    }

    private void AddNewValue()
    {
        if (_newKeyEdit == null || _model == null || _entry == null)
            return;

        string newKey = _newKeyEdit.Text.Trim();

        if (string.IsNullOrEmpty(newKey))
        {
            ShowAcceptDialog("Invalid Key", "Key name cannot be empty.");
            return;
        }

        if (newKey.Contains(' '))
        {
            ShowAcceptDialog("Invalid Key", "Key name cannot contain spaces.");
            return;
        }

        if (_entry.ConfigurableValues.ContainsKey(newKey))
        {
            ShowAcceptDialog("Duplicate Key", $"Key '{newKey}' already exists.");
            return;
        }

        var newValues = new Dictionary<string, int>(_entry.ConfigurableValues)
        {
            [newKey] = (int)_newValueSpinBox.Value,
        };

        _model.UpdateConfigurableValues(_categoryName, _resourceIndex, newValues);
        _entry = _model.Categories[_categoryName].Resources[_resourceIndex];

        _newKeyEdit.Text = "";
        _newValueSpinBox.Value = 0;

        RefreshDisplay();
        EmitSignal(SignalName.ValuesChanged);
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
            DialogText = message,
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(300, 120));
    }
}
#endif
