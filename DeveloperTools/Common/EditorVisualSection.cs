#if DEBUG
using System;
using System.Collections.Generic;
using Godot;
using Structures.Resources;

namespace DeveloperTools.Common;

/// <summary>
/// Full-parity visual editor section bound to an <see cref="EditorVisual"/> POCO
/// (mutated in place, with <c>onChanged</c> fired on every edit). Mirrors the
/// building editor's VisualSection: scalar controls (model path, material, scale,
/// rotation offset, animation, shape) paired with a live 3D <see cref="ModelPreviewPane"/>.
/// The animation dropdown's selection is stored in <see cref="EditorVisual.AnimationPath"/>
/// (reused as the clip name) so the existing <c>animation_path</c> YAML field round-trips.
/// </summary>
public partial class EditorVisualSection : VBoxContainer
{
    private EditorVisual _visual = null!;
    private Action _onChanged = null!;

    [Export] private Button _modelPathButton = null!;
    [Export] private Button _modelPathClearButton = null!;
    [Export] private LineEdit _materialEdit = null!;
    [Export] private SpinBox _scaleSpin = null!;
    [Export] private SpinBox _rotX = null!;
    [Export] private SpinBox _rotY = null!;
    [Export] private SpinBox _rotZ = null!;
    [Export] private OptionButton _animationButton = null!;
    [Export] private OptionButton _shapeButton = null!;
    [Export] private SpinBox _shapeSizeSpin = null!;
    [Export] private Label _shapeSummaryLabel = null!;
    [Export] private ModelPreviewPane _preview = null!;

    private readonly List<string> _animationChoices = new();
    private readonly List<string> _shapeChoices = new();

    private static PackedScene? _scene;

    public static EditorVisualSection Create(EditorVisual visual, Action onChanged)
    {
        _scene ??= GD.Load<PackedScene>("res://DeveloperTools/Common/EditorVisualSection.tscn");
        var section = _scene.Instantiate<EditorVisualSection>();
        section.Initialize(visual, onChanged);
        return section;
    }

    public void Initialize(EditorVisual visual, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(visual);
        ArgumentNullException.ThrowIfNull(onChanged);
        _visual = visual;
        _onChanged = onChanged;
    }

    public override void _Ready()
    {
        base._Ready();
        _modelPathButton.Pressed += OnModelPathPressed;
        _modelPathClearButton.Pressed += OnModelPathCleared;
        _materialEdit.TextChanged += t =>
        {
            _visual.ModelMaterial = string.IsNullOrWhiteSpace(t) ? null : t.Trim();
            _onChanged();
        };
        _scaleSpin.ValueChanged += v => { _visual.Scale = (float)v; _onChanged(); ApplyToPreview(); };
        _rotX.ValueChanged += _ => OnRotationChanged();
        _rotY.ValueChanged += _ => OnRotationChanged();
        _rotZ.ValueChanged += _ => OnRotationChanged();
        _animationButton.ItemSelected += OnAnimationSelected;
        _shapeButton.ItemSelected += OnShapeSelected;
        _shapeSizeSpin.ValueChanged += v => { _visual.ShapeSize = (float)v; _onChanged(); };
        _preview.AnimationListChanged += OnAnimationListChanged;

        RebuildShapeOptions();
        RefreshControls();
        ApplyToPreview();
    }

    private void RefreshControls()
    {
        string mp = _visual.ModelResourcePath ?? "";
        _modelPathButton.Text = string.IsNullOrEmpty(mp) ? "(none)" : mp;
        _modelPathButton.TooltipText = mp;
        _materialEdit.Text = _visual.ModelMaterial ?? "";
        _scaleSpin.SetValueNoSignal(_visual.Scale);
        _rotX.SetValueNoSignal(_visual.RotationOffset.X);
        _rotY.SetValueNoSignal(_visual.RotationOffset.Y);
        _rotZ.SetValueNoSignal(_visual.RotationOffset.Z);
        RebuildAnimationOptionsFromEntry();
        SelectCurrentShape();
        _shapeSizeSpin.SetValueNoSignal(_visual.ShapeSize);
        UpdateShapeSummary();
    }

    // ── Shapes ───────────────────────────────────────────────────────────

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

    private void SelectCurrentShape()
    {
        string current = _visual.ShapeId;
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
        var shape = BuildingShape2DDatabase.Instance.Get(_visual.ShapeId);
        if (shape == null) { _shapeSummaryLabel.Text = "(shape not found)"; return; }
        var parts = new List<string>(shape.Sides.Count);
        for (int i = 0; i < shape.Sides.Count; i++)
        {
            var slots = shape.Sides[i].Slots;
            parts.Add(slots.Count == 0 ? $"side {i}: —" : $"side {i}: {string.Join(",", slots)}");
        }
        _shapeSummaryLabel.Text = string.Join("\n", parts);
    }

    private void OnShapeSelected(long index)
    {
        if (index < 0 || index >= _shapeChoices.Count) return;
        _visual.ShapeId = _shapeChoices[(int)index];
        _onChanged();
        UpdateShapeSummary();
    }

    // ── Animation ────────────────────────────────────────────────────────

    private void RebuildAnimationOptionsFromEntry()
    {
        _animationButton.Clear();
        _animationChoices.Clear();
        _animationChoices.Add("");
        _animationButton.AddItem("(none)", 0);
        string current = _visual.AnimationPath ?? "";
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
        string current = _visual.AnimationPath ?? "";
        int idx = _animationChoices.IndexOf(current);
        if (idx < 0 && !string.IsNullOrEmpty(current))
        {
            _animationChoices.Add(current);
            _animationButton.AddItem($"{current} (missing)", _animationChoices.Count - 1);
            idx = _animationChoices.Count - 1;
        }
        _animationButton.Select(idx < 0 ? 0 : idx);
    }

    private void OnAnimationSelected(long index)
    {
        if (index < 0 || index >= _animationChoices.Count) return;
        string chosen = _animationChoices[(int)index];
        _visual.AnimationPath = string.IsNullOrEmpty(chosen) ? null : chosen;
        _onChanged();
        ApplyToPreview();
    }

    // ── Model path ───────────────────────────────────────────────────────

    private void OnModelPathPressed()
    {
        var popup = ModelPathPopup.Create();
        AddChild(popup);
        popup.ModelSelected += path =>
        {
            _visual.ModelResourcePath = string.IsNullOrWhiteSpace(path) ? null : path;
            _onChanged();
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
        _visual.ModelResourcePath = null;
        _onChanged();
        _modelPathButton.Text = "(none)";
        _modelPathButton.TooltipText = "";
        ApplyToPreview();
    }

    private void OnRotationChanged()
    {
        _visual.RotationOffset = new Vector3((float)_rotX.Value, (float)_rotY.Value, (float)_rotZ.Value);
        _onChanged();
        ApplyToPreview();
    }

    private void ApplyToPreview() =>
        _preview.ApplyVisual(_visual.ModelResourcePath, _visual.Scale, _visual.RotationOffset, _visual.AnimationPath);
}
#endif
