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

    private TextureRect _iconRect = null!;
    private LineEdit _basePathLabel = null!;
    private Button _clearButton = null!;
    private SpinBox _scaleSpin = null!;
    private ColorPickerButton _tintButton = null!;

    private PackedScene? _iconPickerScene;

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
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _iconPickerScene = GD.Load<PackedScene>(
            "res://DeveloperTools/ResourceEditor/IconPickerPopup.tscn");
        BuildLayout();
        RefreshControls();
    }

    private void BuildLayout()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(grid);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Icon" });

        var iconRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _iconRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(64, 64),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TooltipText = "Click to choose icon"
        };
        _iconRect.GuiInput += OnIconInput;
        iconRow.AddChild(_iconRect);

        _basePathLabel = new LineEdit
        {
            Editable = false,
            PlaceholderText = "(no icon)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        iconRow.AddChild(_basePathLabel);

        _clearButton = new Button { Text = "✕", TooltipText = "Clear icon" };
        _clearButton.Pressed += OnClearPressed;
        iconRow.AddChild(_clearButton);
        grid.AddChild(iconRow);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Scale" });
        _scaleSpin = new SpinBox
        {
            MinValue = 0.1,
            MaxValue = 10,
            Step = 0.05,
            AllowGreater = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scaleSpin.ValueChanged += v => OnIconEdited("Scale", (float)v);
        grid.AddChild(_scaleSpin);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Tint" });
        _tintButton = new ColorPickerButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120, 24)
        };
        _tintButton.ColorChanged += c => OnIconEdited("Tint", c);
        grid.AddChild(_tintButton);
    }

    private void RefreshControls()
    {
        if (_entry == null) return;
        _basePathLabel.Text = _entry.Icon.BasePath ?? "";
        _scaleSpin.SetValueNoSignal(_entry.Icon.Scale);
        _tintButton.Color = _entry.Icon.Tint;
        LoadIcon();
    }

    private void LoadIcon()
    {
        if (_entry == null) return;
        Texture2D? texture = null;
        if (!string.IsNullOrEmpty(_entry.Icon.BasePath))
        {
            texture = IconDataLoader.LoadIconTexture(
                _entry.Icon.BasePath, _entry.IdName);
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
        OnIconEdited("BasePath", basePath);
        _basePathLabel.Text = basePath;
        LoadIcon();
    }

    private void OnClearPressed()
    {
        OnIconEdited("BasePath", "");
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
