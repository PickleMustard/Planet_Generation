#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using DeveloperTools.Common;
using Structures.Resources;

namespace DeveloperTools.BuildingEditor;

/// <summary>
/// Composite UI section for the visual: block of a building.
/// Pairs scalar controls (model path, scale, rotation, animation) with an
/// interactive 3D preview pane that reflects edits live.
/// </summary>
public partial class VisualSection : VBoxContainer
{
    private BuildingEditorModel? _model;
    private string _categoryName = "";
    private int _buildingIndex;
    private BuildingEditorModel.BuildingEditEntry? _entry;

    [Export] private Button _modelPathButton = null!;
    [Export] private Button _modelPathClearButton = null!;
    [Export] private SpinBox _scaleSpin = null!;
    [Export] private SpinBox _rotX = null!;
    [Export] private SpinBox _rotY = null!;
    [Export] private SpinBox _rotZ = null!;
    [Export] private OptionButton _animationButton = null!;
    [Export] private OptionButton _shapeButton = null!;
    [Export] private SpinBox _shapeSizeSpin = null!;
    [Export] private Label _shapeSummaryLabel = null!;
    [Export] private ModelPreviewPane _preview = null!;

    private List<string> _animationChoices = new();
    private List<string> _shapeChoices = new();

    private static PackedScene? _scene;

    public static VisualSection Create(
        BuildingEditorModel model,
        string categoryName,
        int buildingIndex,
        BuildingEditorModel.BuildingEditEntry entry)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/BuildingEditor/VisualSection.tscn");
        var section = _scene.Instantiate<VisualSection>();
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
        _scaleSpin.ValueChanged += v => { OnVisualEdited("Scale", (float)v); ApplyToPreview(); };
        _rotX.ValueChanged += _ => OnRotationChanged();
        _rotY.ValueChanged += _ => OnRotationChanged();
        _rotZ.ValueChanged += _ => OnRotationChanged();
        _shapeSizeSpin.ValueChanged += v => OnVisualEdited("ShapeSize", (float)v);
        _preview.AnimationListChanged += OnAnimationListChanged;

        RebuildShapeOptions();
        RefreshControls();
        ApplyToPreview();
    }

    private void RebuildShapeOptions()
    {
        _shapeButton.Clear();
        _shapeChoices.Clear();
        foreach (var kvp in BuildingShape2DDatabase.Instance.All)
        {
            _shapeChoices.Add(kvp.Key);
            string label = string.IsNullOrEmpty(kvp.Value.DisplayName)
                ? kvp.Key
                : $"{kvp.Value.DisplayName} ({kvp.Key})";
            _shapeButton.AddItem(label, _shapeChoices.Count - 1);
        }
        if (_shapeChoices.Count == 0)
        {
            _shapeChoices.Add("hexagon");
            _shapeButton.AddItem("hexagon", 0);
        }
    }

    private void RefreshControls()
    {
        if (_entry == null) return;
        string mp = _entry.Visual.ModelResourcePath ?? "";
        _modelPathButton.Text = string.IsNullOrEmpty(mp) ? "(none)" : mp;
        _modelPathButton.TooltipText = mp;
        _scaleSpin.SetValueNoSignal(_entry.Visual.Scale);
        _rotX.SetValueNoSignal(_entry.Visual.RotationOffset.X);
        _rotY.SetValueNoSignal(_entry.Visual.RotationOffset.Y);
        _rotZ.SetValueNoSignal(_entry.Visual.RotationOffset.Z);
        RebuildAnimationOptionsFromEntry();
        SelectCurrentShape();
        _shapeSizeSpin.SetValueNoSignal(_entry.Visual.ShapeSize);
        UpdateShapeSummary();
    }

    private void SelectCurrentShape()
    {
        if (_entry == null) return;
        string current = _entry.Visual.ShapeId;
        int idx = _shapeChoices.IndexOf(current);
        if (idx < 0 && !string.IsNullOrEmpty(current))
        {
            _shapeChoices.Add(current);
            _shapeButton.AddItem($"{current} (missing)", _shapeChoices.Count - 1);
            idx = _shapeChoices.Count - 1;
        }
        _shapeButton.Select(idx < 0 ? 0 : idx);
    }

    private void UpdateShapeSummary()
    {
        if (_entry == null) { _shapeSummaryLabel.Text = ""; return; }
        var shape = BuildingShape2DDatabase.Instance.Get(_entry.Visual.ShapeId);
        if (shape == null) { _shapeSummaryLabel.Text = "(shape not found)"; return; }
        var parts = new List<string>(shape.Sides.Count);
        for (int i = 0; i < shape.Sides.Count; i++)
        {
            var slots = shape.Sides[i].Slots;
            if (slots.Count == 0)
                parts.Add($"side {i}: —");
            else
                parts.Add($"side {i}: {string.Join(",", slots)}");
        }
        _shapeSummaryLabel.Text = string.Join("\n", parts);
    }

    private void RebuildAnimationOptionsFromEntry()
    {
        if (_entry == null) return;
        _animationButton.Clear();
        _animationChoices.Clear();
        _animationChoices.Add("");
        _animationButton.AddItem("(none)", 0);
        string current = _entry.Visual.AnimationName ?? "";
        if (!string.IsNullOrEmpty(current))
        {
            _animationChoices.Add(current);
            _animationButton.AddItem(current, 1);
            _animationButton.Select(1);
        }
        else
        {
            _animationButton.Select(0);
        }
    }

    private void OnAnimationListChanged(string[] animations)
    {
        _animationButton.Clear();
        _animationChoices.Clear();
        _animationChoices.Add("");
        _animationButton.AddItem("(none)", 0);
        for (int i = 0; i < animations.Length; i++)
        {
            _animationChoices.Add(animations[i]);
            _animationButton.AddItem(animations[i], i + 1);
        }
        string current = _entry?.Visual.AnimationName ?? "";
        int idx = _animationChoices.IndexOf(current);
        if (idx < 0 && !string.IsNullOrEmpty(current))
        {
            _animationChoices.Add(current);
            _animationButton.AddItem($"{current} (missing)", _animationChoices.Count - 1);
            idx = _animationChoices.Count - 1;
        }
        _animationButton.Select(idx < 0 ? 0 : idx);
    }

    private void OnModelPathPressed()
    {
        var popup = ModelPathPopup.Create();
        AddChild(popup);
        popup.ModelSelected += path =>
        {
            OnVisualEdited("ModelResourcePath", path);
            _modelPathButton.Text = path;
            _modelPathButton.TooltipText = path;
            ApplyToPreview();
        };
        var rect = _modelPathButton.GetGlobalRect();
        popup.Position = new Vector2I((int)rect.Position.X, (int)rect.End.Y);
        popup.Popup();
    }

    private void OnModelPathCleared()
    {
        OnVisualEdited("ModelResourcePath", "");
        _modelPathButton.Text = "(none)";
        _modelPathButton.TooltipText = "";
        ApplyToPreview();
    }

    private void OnRotationChanged()
    {
        var v = new Vector3((float)_rotX.Value, (float)_rotY.Value, (float)_rotZ.Value);
        OnVisualEdited("RotationOffset", v);
        ApplyToPreview();
    }

    private void OnAnimationSelected(long index)
    {
        if (index < 0 || index >= _animationChoices.Count) return;
        string chosen = _animationChoices[(int)index];
        OnVisualEdited("AnimationName", chosen);
        ApplyToPreview();
    }

    private void OnShapeSelected(long index)
    {
        if (index < 0 || index >= _shapeChoices.Count) return;
        OnVisualEdited("ShapeId", _shapeChoices[(int)index]);
        UpdateShapeSummary();
    }

    private void OnVisualEdited(string field, object value)
    {
        if (_model == null || _entry == null) return;
        _model.UpdateVisual(_categoryName, _buildingIndex, field, value);
        _entry = _model.Categories[_categoryName].Buildings[_buildingIndex];
    }

    private void ApplyToPreview()
    {
        if (_entry == null) return;
        _preview.ApplyVisual(
            _entry.Visual.ModelResourcePath,
            _entry.Visual.Scale,
            _entry.Visual.RotationOffset,
            _entry.Visual.AnimationName);
    }
}
#endif
