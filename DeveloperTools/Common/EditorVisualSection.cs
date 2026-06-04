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

    private Button _modelPathButton = null!;
    private Button _modelPathClearButton = null!;
    private LineEdit _materialEdit = null!;
    private SpinBox _scaleSpin = null!;
    private SpinBox _rotX = null!;
    private SpinBox _rotY = null!;
    private SpinBox _rotZ = null!;
    private OptionButton _animationButton = null!;
    private OptionButton _shapeButton = null!;
    private SpinBox _shapeSizeSpin = null!;
    private Label _shapeSummaryLabel = null!;
    private ModelPreviewPane _preview = null!;

    private readonly List<string> _animationChoices = new();
    private readonly List<string> _shapeChoices = new();

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
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildLayout();
        RefreshControls();
        ApplyToPreview();
    }

    private void BuildLayout()
    {
        var header = new Label { Text = "Visual" };
        header.AddThemeColorOverride("font_color", new Color(0.7f, 1.0f, 0.7f));
        AddChild(header);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(grid);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Model Path" });
        var modelRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _modelPathButton = new Button { Text = "(none)", SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
        _modelPathButton.Pressed += OnModelPathPressed;
        modelRow.AddChild(_modelPathButton);
        _modelPathClearButton = new Button { Text = "✕", TooltipText = "Clear model path" };
        _modelPathClearButton.Pressed += OnModelPathCleared;
        modelRow.AddChild(_modelPathClearButton);
        grid.AddChild(modelRow);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Model Material" });
        _materialEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "res:// path to a material override (optional)"
        };
        _materialEdit.TextChanged += t =>
        {
            _visual.ModelMaterial = string.IsNullOrWhiteSpace(t) ? null : t.Trim();
            _onChanged();
        };
        grid.AddChild(_materialEdit);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Scale" });
        _scaleSpin = new SpinBox
        {
            MinValue = 0.01, MaxValue = 1000, Step = 0.05, AllowGreater = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scaleSpin.ValueChanged += v => { _visual.Scale = (float)v; _onChanged(); ApplyToPreview(); };
        grid.AddChild(_scaleSpin);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Rotation Offset (deg)" });
        var rotRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rotX = MakeRot();
        _rotY = MakeRot();
        _rotZ = MakeRot();
        _rotX.ValueChanged += _ => OnRotationChanged();
        _rotY.ValueChanged += _ => OnRotationChanged();
        _rotZ.ValueChanged += _ => OnRotationChanged();
        rotRow.AddChild(_rotX);
        rotRow.AddChild(_rotY);
        rotRow.AddChild(_rotZ);
        grid.AddChild(rotRow);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Animation" });
        _animationButton = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _animationButton.ItemSelected += OnAnimationSelected;
        grid.AddChild(_animationButton);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Shape (2D Board)" });
        _shapeButton = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _shapeButton.ItemSelected += OnShapeSelected;
        grid.AddChild(_shapeButton);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Shape Size (px)" });
        _shapeSizeSpin = new SpinBox
        {
            MinValue = 8, MaxValue = 512, Step = 1, AllowGreater = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _shapeSizeSpin.ValueChanged += v => { _visual.ShapeSize = (float)v; _onChanged(); };
        grid.AddChild(_shapeSizeSpin);

        grid.AddChild(new Label { ThemeTypeVariation = "LabelHighContrast", Text = "Shape Slots" });
        _shapeSummaryLabel = new Label
        { ThemeTypeVariation = "LabelHighContrast",
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddChild(_shapeSummaryLabel);

        RebuildShapeOptions();

        _preview = new ModelPreviewPane();
        _preview.AnimationListChanged += OnAnimationListChanged;
        AddChild(_preview);
    }

    private static SpinBox MakeRot() => new()
    {
        MinValue = -360, MaxValue = 360, Step = 1, AllowGreater = true,
        CustomMinimumSize = new Vector2(72, 0)
    };

    private void RefreshControls()
    {
        string mp = _visual.ModelPath ?? "";
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
        var popup = new ModelPathPopup();
        AddChild(popup);
        popup.ModelSelected += path =>
        {
            _visual.ModelPath = string.IsNullOrWhiteSpace(path) ? null : path;
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
        _visual.ModelPath = null;
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
        _preview.ApplyVisual(_visual.ModelPath, _visual.Scale, _visual.RotationOffset, _visual.AnimationPath);
}
#endif
