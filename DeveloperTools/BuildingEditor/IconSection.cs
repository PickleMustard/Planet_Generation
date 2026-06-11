#if DEBUG
using System;
using DeveloperTools.ResourceEditor;
using Godot;
using Structures.Enums;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Icon block editor: clickable thumb that opens the shared IconPickerPopup,
/// plus scale and tint controls. Mirrors ResourceCard's icon UX.
/// </summary>
public partial class IconSection : VBoxContainer
{
    private BuildingEditorModel? _model;
    private string _categoryName = "";
    private int _buildingIndex;
    private BuildingEditorModel.BuildingEditEntry? _entry;

    [Export] private TextureRect _iconRect = null!;
    [Export] private LineEdit _basePathLabel = null!;
    [Export] private SpinBox _scaleSpin = null!;
    [Export] private ColorPickerButton _tintButton = null!;

    private PackedScene? _iconPickerScene;

    private static PackedScene? _scene;

    public static IconSection Create(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        BuildingEditorModel.BuildingEditEntry entry)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BuildingEditor/IconSection.tscn");
        var section = _scene.Instantiate<IconSection>();
        section.Initialize(model, categoryName, buildingIndex, entry);
        return section;
    }

    public void Initialize(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        BuildingEditorModel.BuildingEditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(entry);
        _model = model;
        _categoryName = categoryName;
        _buildingIndex = buildingIndex;
        _entry = entry;
    }

    public override void _Ready()
    {
        base._Ready();
        _iconPickerScene = GD.Load<PackedScene>(
            "res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn");
        _scaleSpin.ValueChanged += v => OnIconEdited("Scale", (float)v);
        _tintButton.ColorChanged += c => OnIconEdited("Tint", c);
        RefreshControls();
    }

    private void RefreshControls()
    {
        if (_entry == null) return;
        _basePathLabel.Text = _entry.Icon.ResourcePath ?? "";
        _scaleSpin.SetValueNoSignal(_entry.Icon.Scale);
        _tintButton.Color = _entry.Icon.Tint;
        LoadIcon();
    }

    private void LoadIcon()
    {
        if (_entry == null) return;
        Texture2D? texture = null;
        if (!string.IsNullOrEmpty(_entry.Icon.ResourcePath))
        {
            texture = IconDataLoader.LoadIconTexture(
                _entry.Icon.ResourcePath, _entry.IdName);
        }
        _iconRect.Texture = texture ?? IconDataLoader.GetFallbackIcon();
    }

    private void OnIconInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb
            && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            OpenPicker();
        }
    }

    private void OpenPicker()
    {
        if (_iconPickerScene == null) return;
        var popup = _iconPickerScene.Instantiate<IconPickerPopup>();
        AddChild(popup);
        popup.IconSelected += OnIconSelected;
        var rect = _iconRect.GetGlobalRect();
        popup.Position = new Vector2I((int)rect.End.X + 4, (int)rect.Position.Y);
        popup.OpenPicker();
    }

    private void OnIconSelected(string basePath)
    {
        OnIconEdited("ResourcePath", basePath);
        _basePathLabel.Text = basePath;
        LoadIcon();
    }

    private void OnClearPressed()
    {
        OnIconEdited("ResourcePath", "");
        _basePathLabel.Text = "";
        LoadIcon();
    }

    private void OnIconEdited(string field, object value)
    {
        if (_model == null || _entry == null) return;
        _model.UpdateIcon(_categoryName, _buildingIndex, field, value);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
    }
}
#endif
